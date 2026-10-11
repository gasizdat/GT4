using GT4.Core.Gedcom.Abstraction;
using GT4.Core.Project.Abstraction;
using GT4.UI.HtmlExport;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Writes the website's HTML export sample (site/sample) from the bundled demo tree. Skipped unless
/// GT4_SAMPLE_SITE_DIR names an empty folder; regenerate with:
/// <code>
/// Remove-Item -Recurse -Force site\sample
/// $env:GT4_SAMPLE_SITE_DIR = (New-Item -ItemType Directory site\sample).FullName
/// dotnet test GT4.CI.Windows.slnf -c Release --framework net10.0-windows10.0.19041.0 --filter "FullyQualifiedName~SampleSiteGenerator"
/// </code>
/// </summary>
public sealed class SampleSiteGenerator
{
  [Fact]
  public async Task Generate()
  {
    var outputFolder = Environment.GetEnvironmentVariable("GT4_SAMPLE_SITE_DIR");
    Assert.SkipWhen(string.IsNullOrEmpty(outputFolder), "Set GT4_SAMPLE_SITE_DIR to regenerate the website's HTML export sample.");

    var culture = CultureInfo.CurrentCulture;
    var uiCulture = CultureInfo.CurrentUICulture;
    var stringsCulture = UIStrings.Culture;
    var english = CultureInfo.GetCultureInfo("en-US");
    CultureInfo.CurrentCulture = english;
    CultureInfo.CurrentUICulture = english;
    UIStrings.Culture = english;
    var projectPath = Path.Combine(Path.GetTempPath(), $"gt4_sample_{Guid.NewGuid():N}.gt4");
    try
    {
      await WriteSampleAsync(projectPath, outputFolder!);
    }
    finally
    {
      CultureInfo.CurrentCulture = culture;
      CultureInfo.CurrentUICulture = uiCulture;
      UIStrings.Culture = stringsCulture;
      File.Delete(projectPath);
    }
  }

  // The developer's saved settings must not reach the sample, so the formats are the app's defaults.
  private static async Task WriteSampleAsync(string projectPath, string outputFolder)
  {
    var token = TestContext.Current.CancellationToken;
    var services = new ServiceCollection();
    GT4Services.Add(services);
    var defaults = new ConfigurationBuilder().Build();
    services.AddSingleton<IConfiguration>(defaults);
    using var provider = services.BuildServiceProvider();

    var projectName = UIStrings.TitleDemoProject;
    var factory = provider.GetRequiredService<IProjectDocumentFactory>();
    await using var document = await factory.CreateNewAsync(projectPath, projectName, token);
    using (var stream = await FileSystem.OpenAppPackageFileAsync("demo.ged"))
    using (var reader = new StreamReader(stream, Encoding.UTF8))
    {
      var importer = provider.GetRequiredService<IGedcomImporter>();
      await importer.ImportAsync(document, reader, token, null);
    }

    var persons = await document.PersonManager.GetPersonInfosAsync(selectMainPhoto: true, token);
    var charlotte = persons.Single(person => person.Names.Any(name => name.Value == "Charlotte"));
    var mainPerson = new MainPersonExport(charlotte, [], new Dictionary<int, double>());

    using var archive = new MemoryStream();
    var exporter = provider.GetRequiredService<HtmlExporter>();
    await exporter.ExportAsync(document, projectName, mainPerson, archive, token);
    archive.Position = 0;
    await ZipFile.ExtractToDirectoryAsync(archive, outputFolder, overwriteFiles: false, token);
  }
}
