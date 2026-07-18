using GT4.Core.Gedcom;
using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Gedcom.Extensions;
using GT4.Core.Project;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Project.Extensions;
using GT4.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace GT4.Tools.RelativesCli;

internal static class Cli
{
  internal static async Task<int> RunAsync(string[] args, CancellationToken token)
  {
    if (args.Length == 0)
    {
      PrintUsage();
      return 1;
    }

    var services = new ServiceCollection()
      .AddDefaultProject()
      .AddGedcom()
      .BuildServiceProvider();
    var documentFactory = services.GetRequiredService<IProjectDocumentFactory>();
    var gedcomImporter = services.GetRequiredService<IGedcomImporter>();

    var argIndex = 0;
    IProjectDocument document;

    switch (NextArg(args, ref argIndex))
    {
      case "--db":
        document = await documentFactory.OpenAsync(NextArg(args, ref argIndex), token);
        break;

      case "--gedcom":
        var gedcomPath = NextArg(args, ref argIndex);
        string? outPath = null;
        if (argIndex < args.Length && args[argIndex] == "--out")
        {
          argIndex++;
          outPath = NextArg(args, ref argIndex);
        }
        outPath ??= Path.Combine(Path.GetTempPath(), $"gt4_{Guid.NewGuid():N}.db");

        document = await documentFactory.CreateNewAsync(outPath, Path.GetFileNameWithoutExtension(gedcomPath), token);
        using (var reader = new StreamReader(gedcomPath, Encoding.UTF8))
        {
          await gedcomImporter.ImportAsync(document, reader, token, Path.GetDirectoryName(gedcomPath));
        }
        Console.WriteLine($"Imported GEDCOM into: {outPath}");
        break;

      default:
        PrintUsage();
        return 1;
    }

    await using (document)
    {
      if (argIndex >= args.Length)
      {
        PrintUsage();
        return 1;
      }

      switch (NextArg(args, ref argIndex))
      {
        case "find":
          await RunFindAsync(document, NextArg(args, ref argIndex), token);
          break;

        case "relatives":
          await RunRelativesAsync(document, int.Parse(NextArg(args, ref argIndex)), token);
          break;

        case "tree":
          await RunTreeAsync(document, int.Parse(NextArg(args, ref argIndex)), token);
          break;

        default:
          PrintUsage();
          return 1;
      }
    }

    return 0;
  }

  internal static string FormatDate(Date date) => date.Status switch
  {
    DateStatus.WellKnown => $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}",
    DateStatus.DayUnknown => $"{date.Year:D4}-{date.Month:D2}",
    DateStatus.MonthUnknown or DateStatus.YearApproximate => $"~{date.Year:D4}",
    _ => "?"
  };

  private static async Task RunFindAsync(IProjectDocument document, string query, CancellationToken token)
  {
    var persons = await document.Persons.GetPersonsAsync(token);
    var infos = await document.PersonManager.GetPersonInfosAsync(persons, selectMainPhoto: false, token);

    foreach (var info in infos.Where(i => i.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)))
    {
      Console.WriteLine($"{info.Id,6}  {info.DisplayName,-40}  {FormatDate(info.BirthDate)}");
    }
  }

  private static async Task RunRelativesAsync(IProjectDocument document, int personId, CancellationToken token)
  {
    var person = await document.Persons.TryGetPersonByIdAsync(personId, token);
    if (person is null)
    {
      Console.Error.WriteLine($"No person with Id {personId}.");
      return;
    }

    var relatives = await document.RelativesProvider.GetRelativeInfosAsync(person, selectMainPhoto: false, token);
    foreach (var relative in relatives)
    {
      Console.WriteLine(
        $"{relative.Id,6}  {relative.DisplayName,-40}  {relative.Type,-16}  " +
        $"Gen={relative.Generation.Value,3}  Cons={relative.Consanguinity.Value,3}");
    }
  }

  private static async Task RunTreeAsync(IProjectDocument document, int personId, CancellationToken token)
  {
    var person = await document.Persons.TryGetPersonByIdAsync(personId, token);
    if (person is null)
    {
      Console.Error.WriteLine($"No person with Id {personId}.");
      return;
    }

    var roots = await document.RelativesProvider.GetRelativeInfosAsync(person, selectMainPhoto: true, token);
    var walker = new TreeWalker(document.RelativesProvider);
    var (nodes, issues) = await walker.WalkAsync(roots, token);

    foreach (var node in nodes)
    {
      var marker = node.Issue switch
      {
        TreeIssueType.Loop => "  [LOOP]",
        TreeIssueType.MultipleConnections => "  [MULTIPLE CONNECTIONS]",
        _ => ""
      };
      Console.WriteLine(
        $"{new string(' ', node.Depth * 2)}{node.Info.DisplayName} " +
        $"({node.Info.Type}, Gen={node.Info.Generation.Value}, Cons={node.Info.Consanguinity.Value}){marker}");
    }

    if (issues.Count == 0)
    {
      return;
    }

    Console.WriteLine();
    Console.WriteLine("=== Loop / MultipleConnections detections ===");
    foreach (var issue in issues)
    {
      Console.WriteLine($"[{issue.Type}] {issue.DisplayName} (Id={issue.PersonId})");
      Console.WriteLine($"  1st sighting: Gen={issue.FirstGeneration.Value}, Cons={issue.FirstConsanguinity.Value}");
      Console.WriteLine($"    path: {issue.FirstPath}");
      Console.WriteLine($"  2nd sighting: Gen={issue.SecondGeneration.Value}, Cons={issue.SecondConsanguinity.Value}");
      Console.WriteLine($"    path: {issue.SecondPath}");
      Console.WriteLine();
    }
  }

  private static string NextArg(string[] args, ref int index)
  {
    if (index >= args.Length)
    {
      throw new ArgumentException("Missing expected argument.");
    }
    return args[index++];
  }

  private static void PrintUsage()
  {
    Console.Error.WriteLine("""
      Usage:
        GT4.Tools.RelativesCli --db <path.db> <command> [args]
        GT4.Tools.RelativesCli --gedcom <path.ged> [--out <path.db>] <command> [args]

      Commands:
        find <query>          List persons whose name contains <query>.
        relatives <personId>  List the direct relatives of the given person.
        tree <personId>       Walk the full relative tree from the given person,
                               flagging Loop / MultipleConnections exactly like
                               the app's RelativeTree.ExpandAllAsync does.
      """);
  }
}
