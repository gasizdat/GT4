using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Utils.Converters;
using Microsoft.Extensions.DependencyInjection;

namespace GT4.UI.Logic;

// The data the person page needs from the project document to render one person: the person itself and the
// flat, display-ordered list of relatives that seeds the relatives tree.
public record PersonData(PersonFullInfo PersonFullInfo, RelativeInfo[] Roots);

// The person page arranges its image, relatives and biography in a grid whose layout adapts to the
// available space; GridLayout is one cell placement, PersonPageSmartLayout the placement of all three.
public record struct PersonPageSmartLayout(GridLayout Image, GridLayout Relatives, GridLayout Biography);

public record GridLayout(int Column, int ColumnSpan, int Row, int RowSpan);

public class PersonPageLogic
{
  private readonly ICurrentProjectProvider _currentProjectProvider;
  private readonly ICancellationTokenProvider _cancellationTokenProvider;
  private readonly IDataConverter _TextConverter;
  private readonly IDataConverter _GedcomConverter;

  public PersonPageLogic(
    ICurrentProjectProvider currentProjectProvider,
    ICancellationTokenProvider cancellationTokenProvider,
    [FromKeyedServices(DataCategory.PersonBio)]
    IDataConverter dataConverter,
    [FromKeyedServices(DataCategory.PersonGedcomTags)]
    IDataConverter gedcomConverter)
  {
    _currentProjectProvider = currentProjectProvider;
    _cancellationTokenProvider = cancellationTokenProvider;
    _TextConverter = dataConverter;
    _GedcomConverter = gedcomConverter;
  }

  public async Task<PersonData> GetPersonDataAsync(Person person)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    var project = _currentProjectProvider.Project;
    var personFullInfo = await project.PersonManager.GetPersonFullInfoAsync(person, token);

    var relativesProvider = project.RelativesProvider;
    var parentsTask = relativesProvider.GetParentsAsync(personFullInfo.RelativeInfos, token);
    var stepChildrenTask = relativesProvider.GetStepChildrenAsync(personFullInfo.RelativeInfos, token);
    await Task.WhenAll(parentsTask, stepChildrenTask);

    var roots = BuildRoots(personFullInfo, parentsTask.Result, stepChildrenTask.Result, relativesProvider);
    return new PersonData(personFullInfo, roots);
  }

  // Assembles the relatives in the order they are shown under the person: spouses, then ancestors (parents),
  // then siblings, then descendants (children, step-children). Each group is sorted by biological sex.
  private static RelativeInfo[] BuildRoots(
    PersonFullInfo personFullInfo,
    Parents parents,
    RelativeInfo[] stepChildren,
    IRelativesProvider relativesProvider)
  {
    var siblings = relativesProvider.GetSiblings(personFullInfo, parents);
    var spouses = personFullInfo.RelativeInfos.Where(r => r.Type == RelationshipType.Spouse);
    var children = relativesProvider.GetChildren(personFullInfo.RelativeInfos);
    var adoptiveChildren = relativesProvider.GetAdoptiveChildren(personFullInfo.RelativeInfos);

    var roots = new List<RelativeInfo>();
    void Add(IEnumerable<RelativeInfo> relatives)
    {
      var ordered = relatives.OrderBy(r => r.BiologicalSex);
      roots.AddRange(ordered);
    }

    Add(spouses);
    Add(parents.Native);
    Add(parents.Adoptive);
    Add(parents.Step);
    Add(siblings.Native);
    Add(siblings.ByFather);
    Add(siblings.ByMother);
    Add(siblings.Step);
    Add(siblings.Adoptive);
    Add(children);
    Add(adoptiveChildren);
    Add(stepChildren);

    return [.. roots];
  }

  public async Task RemovePersonAsync(Person person)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    await _currentProjectProvider.Project.Persons.RemovePersonAsync(person, token);
  }

  public async Task UpdatePersonAsync(PersonFullInfo personFullInfo)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    await _currentProjectProvider.Project.PersonManager.UpdatePersonAsync(personFullInfo, token);
  }

  public async Task<string> CombineBiographyAsync(PersonFullInfo personFullInfo)
  {
    using var token = _cancellationTokenProvider.CreateDbCancellationToken();
    var bioTask = _TextConverter.ToObjectAsync(personFullInfo.Biography, token);
    var gedcomTask = _GedcomConverter.ToObjectAsync(personFullInfo.GedcomData, token);
    await Task.WhenAll(bioTask, gedcomTask);

    return CombineBiography(bioTask.Result as string, gedcomTask.Result as string);
  }

  // The biography block doubles as the home for the read-only GEDCOM details: the stored bio first, then
  // the rendered residual tags, so a person carrying only imported GEDCOM data still shows the block.
  internal static string CombineBiography(string? bio, string? gedcomDetails)
  {
    if (string.IsNullOrWhiteSpace(gedcomDetails))
      return bio ?? string.Empty;
    if (string.IsNullOrWhiteSpace(bio))
      return gedcomDetails;
    return $"{bio}\n\n{gedcomDetails}";
  }

  // The stored photo contents in display order: main photo first, then the additional photos. A person with
  // no main photo has no stored photos (the page substitutes a default stub) and must carry no additional
  // photos either — that inconsistency is a corrupt record, so it throws.
  public static byte[][]? GetStoredPhotoContents(PersonFullInfo personFullInfo)
  {
    if (personFullInfo.MainPhoto is null)
    {
      if (personFullInfo.AdditionalPhotos.Length != 0)
        throw new ApplicationException("Person photos inconsistency");
      return null;
    }

    var additional = personFullInfo.AdditionalPhotos.Select(photo => photo.Content);
    return [personFullInfo.MainPhoto.Content, .. additional];
  }

  // Portrait (or a narrow landscape window) stacks the three blocks in one column; a wide-enough landscape
  // puts the image and relatives side by side with the biography spanning underneath. Width is measured in
  // device-independent units, so it is scaled by the display density before the 900px threshold check.
  public static PersonPageSmartLayout ComputeLayout(double width, double height, double density)
  {
    var widthInPixels = width * density;
    if (width < height || widthInPixels < 900)
    {
      return new PersonPageSmartLayout(
        Image: new GridLayout(Column: 0, ColumnSpan: 2, Row: 0, RowSpan: 1),
        Relatives: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1),
        Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 2, RowSpan: 1));
    }

    return new PersonPageSmartLayout(
      Image: new GridLayout(Column: 0, ColumnSpan: 1, Row: 0, RowSpan: 1),
      Relatives: new GridLayout(Column: 1, ColumnSpan: 1, Row: 0, RowSpan: 1),
      Biography: new GridLayout(Column: 0, ColumnSpan: 2, Row: 1, RowSpan: 1));
  }
}
