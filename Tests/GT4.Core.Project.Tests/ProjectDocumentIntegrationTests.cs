using FluentAssertions;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using Xunit;

namespace GT4.Core.Project.Tests;

/// <summary>
/// End-to-end coverage against a real on-disk SQLite <see cref="ProjectDocument"/>. Exercises the
/// table layers (Names, Persons, PersonNames, Data, PersonData, Relatives, Metadata) together with the
/// <see cref="PersonManager"/> and <see cref="FamilyManager"/> orchestration that sits on top of them.
/// </summary>
public sealed class ProjectDocumentIntegrationTests : IAsyncLifetime
{
  private readonly string _path = Path.Combine(Path.GetTempPath(), $"gt4_int_{Guid.NewGuid():N}.db");
  private ProjectDocument _doc = null!;
  private CancellationToken Token => TestContext.Current.CancellationToken;

  public async ValueTask InitializeAsync()
  {
    _doc = await ProjectDocument.CreateNewAsync(_path, "integration", TestContext.Current.CancellationToken);
  }

  public async ValueTask DisposeAsync()
  {
    await _doc.DisposeAsync();
    foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
    {
      var file = _path + suffix;
      try
      {
        if (File.Exists(file))
        {
          File.Delete(file);
        }
      }
      catch
      {
        // Best-effort temp cleanup.
      }
    }
  }

  private static readonly Date Birth = Date.Create(19800101, DateStatus.WellKnown);

  private Task<Person> AddBarePersonAsync(BiologicalSex sex = BiologicalSex.Male) =>
    _doc.Persons.AddPersonAsync(new Person(TableBase.NonCommittedId, Birth, null, sex), Token);

  private static Data NewData(DataCategory category, params byte[] content) =>
    new(TableBase.NonCommittedId, content, "application/octet-stream", category);

  // --- Metadata ----------------------------------------------------------------------------------

  [Fact]
  public async Task Metadata_RoundTripsNameAndDescription()
  {
    await _doc.Metadata.SetProjectNameAsync("My Tree", Token);
    await _doc.Metadata.SetProjectDescriptionAsync("Family history", Token);

    (await _doc.Metadata.GetProjectNameAsync(Token)).Should().Be("My Tree");
    (await _doc.Metadata.GetProjectDescriptionAsync(Token)).Should().Be("Family history");
  }

  [Fact]
  public async Task Metadata_Revision_IsAutoStampedOnEveryCommit()
  {
    // Each root commit writes a best-effort "revision" marker, so an explicitly set value is
    // immediately superseded by the commit's own stamp.
    await _doc.Metadata.SetProjectRevisionAsync("ignored", Token);

    (await _doc.Metadata.GetProjectRevisionAsync(Token)).Should().NotBeNullOrEmpty();
  }

  [Fact]
  public async Task Metadata_Get_MissingKey_ReturnsNull()
  {
    (await _doc.Metadata.GetProjectDescriptionAsync(Token)).Should().BeNull();
  }

  [Fact]
  public async Task Metadata_Add_OverwritesExistingKey()
  {
    await _doc.Metadata.SetProjectNameAsync("first", Token);
    await _doc.Metadata.SetProjectNameAsync("second", Token);

    (await _doc.Metadata.GetProjectNameAsync(Token)).Should().Be("second");
  }

  // --- Names / FamilyManager ---------------------------------------------------------------------

  [Fact]
  public async Task AddFamily_CreatesFamilyWithGenderedLastNames()
  {
    var family = await _doc.FamilyManager.AddFamilyAsync("Smith", "Smith", "Smithova", Token);

    family.Type.Should().Be(NameType.FamilyName);
    family.ParentId.Should().BeNull();

    var families = await _doc.FamilyManager.GetFamiliesAsync(Token);
    families.Id().Should().Contain(family.Id);

    var subnames = await _doc.Names.TryGetNameWithSubnamesByIdAsync(family.Id, Token);
    subnames.Should().NotBeNull();
    subnames!.Should().Contain(n => n.Type == (NameType.LastName | NameType.MaleDeclension) && n.Value == "Smith");
    subnames.Should().Contain(n => n.Type == (NameType.LastName | NameType.FemaleDeclension) && n.Value == "Smithova");
  }

  [Fact]
  public async Task TryGetNameById_ReturnsNull_ForNullOrMissing()
  {
    (await _doc.Names.TryGetNameByIdAsync(null, Token)).Should().BeNull();
    (await _doc.Names.TryGetNameByIdAsync(999999, Token)).Should().BeNull();
    (await _doc.Names.TryGetNameWithSubnamesByIdAsync(null, Token)).Should().BeNull();
    (await _doc.Names.TryGetNameWithSubnamesByIdAsync(999999, Token)).Should().BeNull();
  }

  [Fact]
  public async Task AddFirstMaleName_AddsPatronymicSubnames()
  {
    var name = await _doc.Names.AddFirstMaleNameAsync("John", "Johnovich", "Johnovna", Token);

    name.Type.Should().Be(NameType.FirstName | NameType.MaleDeclension);
    var subnames = await _doc.Names.TryGetNameWithSubnamesByIdAsync(name.Id, Token);
    subnames!.Should().Contain(n => n.Type == (NameType.Patronymic | NameType.MaleDeclension) && n.Value == "Johnovich");
    subnames.Should().Contain(n => n.Type == (NameType.Patronymic | NameType.FemaleDeclension) && n.Value == "Johnovna");
  }

  [Fact]
  public async Task AddFirstFemaleName_StoresFemaleFirstName()
  {
    var name = await _doc.Names.AddFirstFemaleNameAsync("Anna", Token);

    name.Type.Should().Be(NameType.FirstName | NameType.FemaleDeclension);
    (await _doc.Names.TryGetNameByIdAsync(name.Id, Token))!.Value.Should().Be("Anna");
  }

  [Fact]
  public async Task UpdateName_ChangesValue()
  {
    var name = await _doc.Names.AddNameAsync("Old", NameType.FirstName, null, Token);

    await _doc.Names.UpdateName(name with { Value = "New" }, Token);

    (await _doc.Names.TryGetNameByIdAsync(name.Id, Token))!.Value.Should().Be("New");
  }

  [Fact]
  public async Task GetNamesByType_FiltersByType_AndAllNamesReturnsEverything()
  {
    await _doc.Names.AddNameAsync("Fam", NameType.FamilyName, null, Token);
    await _doc.Names.AddNameAsync("First", NameType.FirstName, null, Token);

    var families = await _doc.Names.GetNamesByTypeAsync(NameType.FamilyName, Token);
    families.Should().OnlyContain(n => n.Type == NameType.FamilyName);

    var all = await _doc.Names.GetNamesByTypeAsync(NameType.AllNames, Token);
    all.Length.Should().BeGreaterThanOrEqualTo(2);
  }

  [Fact]
  public async Task RemoveFamily_DeletesFamilyAndSubnames()
  {
    var family = await _doc.FamilyManager.AddFamilyAsync("Doomed", "Doomed", "Doomedova", Token);

    await _doc.FamilyManager.RemoveFamilyAsync(family, Token);

    (await _doc.Names.TryGetNameByIdAsync(family.Id, Token)).Should().BeNull();
    (await _doc.Names.TryGetNameWithSubnamesByIdAsync(family.Id, Token)).Should().BeNull();
  }

  [Fact]
  public async Task RemoveFamily_NonFamilyName_Throws()
  {
    var notFamily = await _doc.Names.AddNameAsync("First", NameType.FirstName, null, Token);

    var act = () => _doc.FamilyManager.RemoveFamilyAsync(notFamily, Token);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task UpdateFamily_RenamesFamilyAndLastNames()
  {
    var family = await _doc.FamilyManager.AddFamilyAsync("Before", "BeforeM", "BeforeF", Token);
    var subnames = (await _doc.Names.TryGetNameWithSubnamesByIdAsync(family.Id, Token))!;
    var male = subnames.Single(n => n.Type == (NameType.LastName | NameType.MaleDeclension));
    var female = subnames.Single(n => n.Type == (NameType.LastName | NameType.FemaleDeclension));

    await _doc.FamilyManager.UpdateFamilyAsync(
      family with { Value = "After" },
      male with { Value = "AfterM" },
      female with { Value = "AfterF" },
      Token);

    (await _doc.Names.TryGetNameByIdAsync(family.Id, Token))!.Value.Should().Be("After");
    (await _doc.Names.TryGetNameByIdAsync(male.Id, Token))!.Value.Should().Be("AfterM");
    (await _doc.Names.TryGetNameByIdAsync(female.Id, Token))!.Value.Should().Be("AfterF");
  }

  [Fact]
  public async Task UpdateFamily_FamilyWithParent_Throws()
  {
    var withParent = await _doc.Names.AddNameAsync("Sub", NameType.FamilyName, null, Token) with { ParentId = 1 };

    var act = () => _doc.FamilyManager.UpdateFamilyAsync(withParent, null, null, Token);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task UpdateFamily_LastNameWithWrongParent_Throws()
  {
    var family = await _doc.FamilyManager.AddFamilyAsync("Fam", "FamM", "FamF", Token);
    var foreignLastName = new Name(12345, "X", NameType.LastName | NameType.MaleDeclension, ParentId: family.Id + 1000);

    var act = () => _doc.FamilyManager.UpdateFamilyAsync(family, foreignLastName, null, Token);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task SetUpPersonFamily_AppendsFamilyName_WhenMissing()
  {
    var family = new Name(1, "Smith", NameType.FamilyName, null);
    var person = new PersonInfo(
      new Person(1, Birth, null, BiologicalSex.Male), [], null);

    var updated = _doc.FamilyManager.SetUpPersonFamily(person, family);

    updated.Names.Should().Contain(family);

    // Adding again is a no-op (already present).
    _doc.FamilyManager.SetUpPersonFamily(updated, family).Names.Should().HaveCount(1);
  }

  [Fact]
  public void SetUpPersonFamily_NonFamilyName_Throws()
  {
    var notFamily = new Name(1, "First", NameType.FirstName, null);
    var person = new PersonInfo(new Person(1, Birth, null, BiologicalSex.Male), [], null);

    var act = () => _doc.FamilyManager.SetUpPersonFamily(person, notFamily);

    act.Should().Throw<ArgumentException>();
  }

  // --- Persons -----------------------------------------------------------------------------------

  [Fact]
  public async Task Persons_AddGetUpdateRemove_RoundTrip()
  {
    var person = await AddBarePersonAsync(BiologicalSex.Female);
    person.Id.Should().NotBe(TableBase.NonCommittedId);

    var fetched = await _doc.Persons.TryGetPersonByIdAsync(person.Id, Token);
    fetched.Should().NotBeNull();
    fetched!.BiologicalSex.Should().Be(BiologicalSex.Female);

    var withDeath = person with { DeathDate = Date.Create(20200101, DateStatus.WellKnown) };
    await _doc.Persons.UpdatePersonAsync(withDeath, Token);
    (await _doc.Persons.TryGetPersonByIdAsync(person.Id, Token))!.DeathDate.Should().NotBeNull();

    await _doc.Persons.RemovePersonAsync(person, Token);
    (await _doc.Persons.TryGetPersonByIdAsync(person.Id, Token)).Should().BeNull();
  }

  [Fact]
  public async Task Persons_GetPersons_CachesAndInvalidatesOnWrite()
  {
    var a = await AddBarePersonAsync();
    var firstRead = await _doc.Persons.GetPersonsAsync(Token);
    // Second read hits the WeakReference cache and returns the same set.
    var secondRead = await _doc.Persons.GetPersonsAsync(Token);
    secondRead.Id().Should().BeEquivalentTo(firstRead.Id());

    // TryGetPersonById can also be served from the cache.
    (await _doc.Persons.TryGetPersonByIdAsync(a.Id, Token))!.Id.Should().Be(a.Id);
    (await _doc.Persons.TryGetPersonByIdAsync(int.MaxValue, Token)).Should().BeNull();

    await AddBarePersonAsync();
    (await _doc.Persons.GetPersonsAsync(Token)).Length.Should().Be(firstRead.Length + 1);
  }

  [Fact]
  public async Task PersonManager_AddPerson_WithFamily_AttachesRequiredNames()
  {
    var family = await _doc.FamilyManager.AddFamilyAsync("Smith", "Smith", "Smithova", Token);
    var first = await _doc.Names.AddFirstMaleNameAsync("John", null, null, Token);

    var toAdd = PersonFullInfo.Empty with
    {
      BirthDate = Birth,
      BiologicalSex = BiologicalSex.Male,
      Names = [first, family],
    };

    var added = await _doc.PersonManager.AddPersonAsync(toAdd, Token);
    added.Id.Should().NotBe(TableBase.NonCommittedId);

    var full = await _doc.PersonManager.GetPersonFullInfoAsync(new Person(added.Id, Birth, null, BiologicalSex.Male), Token);
    full.Names.Select(n => n.Value).Should().Contain("John");
    // GetRequiredNames added the male last name in addition to the explicit family name.
    full.Names.Count(n => n.Value == "Smith").Should().BeGreaterThanOrEqualTo(1);
  }

  [Fact]
  public async Task PersonManager_GetPersonFullInfo_ForNonCommitted_Throws()
  {
    var act = () => _doc.PersonManager.GetPersonFullInfoAsync(
      new Person(TableBase.NonCommittedId, Birth, null, BiologicalSex.Male), Token);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task PersonManager_AddPerson_WithPhotosAndBio_SplitsByCategory()
  {
    var toAdd = PersonFullInfo.Empty with
    {
      BirthDate = Birth,
      BiologicalSex = BiologicalSex.Female,
      MainPhoto = NewData(DataCategory.PersonMainPhoto, 1, 2, 3),
      AdditionalPhotos = [NewData(DataCategory.PersonPhoto, 4, 5), NewData(DataCategory.PersonPhoto, 6)],
      Biography = NewData(DataCategory.PersonBio, 7, 8, 9),
    };

    var added = await _doc.PersonManager.AddPersonAsync(toAdd, Token);
    var person = new Person(added.Id, Birth, null, BiologicalSex.Female);
    var full = await _doc.PersonManager.GetPersonFullInfoAsync(person, Token);

    full.MainPhoto.Should().NotBeNull();
    full.MainPhoto!.Content.Should().Equal(1, 2, 3);
    full.AdditionalPhotos.Should().HaveCount(2);
    full.Biography.Should().NotBeNull();
    full.Biography!.Content.Should().Equal(7, 8, 9);
  }

  [Fact]
  public async Task PersonManager_GetPersonInfos_AllAndByArrayAndByName()
  {
    var first = await _doc.Names.AddNameAsync("Findme", NameType.FirstName, null, Token);
    var toAdd = PersonFullInfo.Empty with { BirthDate = Birth, BiologicalSex = BiologicalSex.Male, Names = [first] };
    var added = await _doc.PersonManager.AddPersonAsync(toAdd, Token);
    var person = new Person(added.Id, Birth, null, BiologicalSex.Male);

    var all = await _doc.PersonManager.GetPersonInfosAsync(selectMainPhoto: false, Token);
    all.Id().Should().Contain(added.Id);

    var byArray = await _doc.PersonManager.GetPersonInfosAsync([person], selectMainPhoto: true, Token);
    byArray.Should().ContainSingle().Which.Names.Select(n => n.Value).Should().Contain("Findme");

    var byName = await _doc.PersonManager.GetPersonInfosByNameAsync(first, selectMainPhoto: false, Token);
    byName.Id().Should().Contain(added.Id);
  }

  [Fact]
  public async Task PersonManager_UpdatePerson_ReplacesNamesAndData()
  {
    var first = await _doc.Names.AddNameAsync("Initial", NameType.FirstName, null, Token);
    var added = await _doc.PersonManager.AddPersonAsync(
      PersonFullInfo.Empty with { BirthDate = Birth, BiologicalSex = BiologicalSex.Male, Names = [first] }, Token);

    var replacement = await _doc.Names.AddNameAsync("Renamed", NameType.FirstName, null, Token);
    var updated = ((PersonFullInfo)added) with
    {
      Names = [replacement],
      MainPhoto = NewData(DataCategory.PersonMainPhoto, 42),
    };

    await _doc.PersonManager.UpdatePersonAsync(updated, Token);

    var full = await _doc.PersonManager.GetPersonFullInfoAsync(
      new Person(added.Id, Birth, null, BiologicalSex.Male), Token);
    full.Names.Select(n => n.Value).Should().ContainSingle().Which.Should().Be("Renamed");
    full.MainPhoto.Should().NotBeNull();
  }

  [Fact]
  public async Task PersonManager_UpdatePerson_PromotesMisCategorizedMainPhoto()
  {
    // A photo stored as PersonPhoto but supplied as MainPhoto must be re-categorized.
    var photo = await _doc.Data.AddDataAsync([9, 9], "application/octet-stream", DataCategory.PersonPhoto, Token);
    var added = await _doc.PersonManager.AddPersonAsync(
      PersonFullInfo.Empty with { BirthDate = Birth, BiologicalSex = BiologicalSex.Male }, Token);

    var updated = ((PersonFullInfo)added) with { MainPhoto = photo };
    await _doc.PersonManager.UpdatePersonAsync(updated, Token);

    (await _doc.Data.TryGetDataByIdAsync(photo.Id, Token))!.Category.Should().Be(DataCategory.PersonMainPhoto);
  }

  [Fact]
  public async Task RemovePerson_CascadesDependentRows()
  {
    var first = await _doc.Names.AddNameAsync("Cascade", NameType.FirstName, null, Token);
    var added = await _doc.PersonManager.AddPersonAsync(
      PersonFullInfo.Empty with
      {
        BirthDate = Birth,
        BiologicalSex = BiologicalSex.Male,
        Names = [first],
        MainPhoto = NewData(DataCategory.PersonMainPhoto, 1, 2),
      }, Token);
    var person = new Person(added.Id, Birth, null, BiologicalSex.Male);
    var other = await AddBarePersonAsync();
    await _doc.Relatives.AddRelativesAsync(other, [new Relative(person, RelationshipType.Child, null)], Token);

    // Sanity: the dependent rows exist before removal.
    (await _doc.PersonNames.GetPersonNamesAsync(person, Token)).Should().NotBeEmpty();
    (await _doc.PersonData.GetPersonDataSetAsync(person, null, Token)).Should().NotBeEmpty();
    (await _doc.Relatives.GetRelativesAsync(other, Token)).Should().NotBeEmpty();

    await _doc.Persons.RemovePersonAsync(person, Token);

    // With foreign keys enforced, deleting the person cascades to every dependent row (no orphans),
    // including the relationship row referenced from the *other* side via RelativeId.
    (await _doc.PersonNames.GetPersonNamesAsync(person, Token)).Should().BeEmpty();
    (await _doc.PersonData.GetPersonDataSetAsync(person, null, Token)).Should().BeEmpty();
    (await _doc.Relatives.GetRelativesAsync(other, Token)).Should().BeEmpty();
  }

  // --- Data / PersonData -------------------------------------------------------------------------

  [Fact]
  public async Task Data_AddGetUpdateCategoryRemove()
  {
    var data = await _doc.Data.AddDataAsync([1, 2, 3], "image/png", DataCategory.PersonPhoto, Token);
    (await _doc.Data.TryGetDataByIdAsync(data.Id, Token))!.MimeType.Should().Be("image/png");

    await _doc.Data.UpdateCategoryAsync(data, DataCategory.PersonMainPhoto, Token);
    (await _doc.Data.TryGetDataByIdAsync(data.Id, Token))!.Category.Should().Be(DataCategory.PersonMainPhoto);

    await _doc.Data.RemoveDataAsync(data, Token);
    (await _doc.Data.TryGetDataByIdAsync(data.Id, Token)).Should().BeNull();
  }

  [Fact]
  public async Task Data_TryGetById_Null_ReturnsNull()
  {
    (await _doc.Data.TryGetDataByIdAsync(null, Token)).Should().BeNull();
  }

  [Fact]
  public async Task PersonData_AddGetUpdateRemove()
  {
    var person = await AddBarePersonAsync();

    await _doc.PersonData.AddPersonDataSetAsync(person, [NewData(DataCategory.PersonPhoto, 1)], Token);
    (await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonPhoto, Token)).Should().HaveCount(1);
    (await _doc.PersonData.GetPersonDataSetAsync(person, null, Token)).Should().HaveCount(1);

    await _doc.PersonData.UpdatePersonDataSetAsync(person, [NewData(DataCategory.PersonBio, 2), NewData(DataCategory.PersonPhoto, 3)], Token);
    (await _doc.PersonData.GetPersonDataSetAsync(person, null, Token)).Should().HaveCount(2);

    var bio = (await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonBio, Token)).Single();
    await _doc.PersonData.RemovePersonDataAsync(person, bio, Token);
    (await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonBio, Token)).Should().BeEmpty();
  }

  [Fact]
  public async Task PersonData_UpdateSingleData_ReplacesPrevious()
  {
    var person = await AddBarePersonAsync();
    await _doc.PersonData.UpdatePersonDataAsync(person, NewData(DataCategory.PersonMainPhoto, 1), DataCategory.PersonMainPhoto, Token);
    (await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonMainPhoto, Token)).Should().HaveCount(1);

    // Replace with a different data; old one removed.
    await _doc.PersonData.UpdatePersonDataAsync(person, NewData(DataCategory.PersonMainPhoto, 2), DataCategory.PersonMainPhoto, Token);
    var set = await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonMainPhoto, Token);
    set.Should().ContainSingle().Which.Content.Should().Equal(2);

    // Clearing it removes everything in the category.
    await _doc.PersonData.UpdatePersonDataAsync(person, null, DataCategory.PersonMainPhoto, Token);
    (await _doc.PersonData.GetPersonDataSetAsync(person, DataCategory.PersonMainPhoto, Token)).Should().BeEmpty();
  }

  [Fact]
  public async Task GetPersonDataSetBatch_EmptyInput_ReturnsEmpty()
  {
    var result = await _doc.PersonData.GetPersonDataSetAsync([], null, Token);

    result.Should().BeEmpty();
  }

  [Fact]
  public async Task GetPersonDataSetBatch_NoCategoryFilter_ReturnsAllData()
  {
    var personA = await AddBarePersonAsync();
    var personB = await AddBarePersonAsync();
    await _doc.PersonData.AddPersonDataSetAsync(personA, [NewData(DataCategory.PersonPhoto, 1, 2)], Token);
    await _doc.PersonData.AddPersonDataSetAsync(personB, [NewData(DataCategory.PersonBio, 3, 4)], Token);

    var result = await _doc.PersonData.GetPersonDataSetAsync([personA, personB], null, Token);

    result.Should().ContainKey(personA.Id);
    result.Should().ContainKey(personB.Id);
    result[personA.Id].Should().ContainSingle().Which.Content.Should().Equal(1, 2);
    result[personB.Id].Should().ContainSingle().Which.Content.Should().Equal(3, 4);
  }

  [Fact]
  public async Task GetPersonDataSetBatch_WithCategoryFilter_FiltersPerPerson()
  {
    var person = await AddBarePersonAsync();
    await _doc.PersonData.AddPersonDataSetAsync(person,
      [NewData(DataCategory.PersonPhoto, 10), NewData(DataCategory.PersonBio, 20)], Token);

    var result = await _doc.PersonData.GetPersonDataSetAsync([person], DataCategory.PersonPhoto, Token);

    result.Should().ContainKey(person.Id);
    result[person.Id].Should().ContainSingle().Which.Category.Should().Be(DataCategory.PersonPhoto);
  }

  [Fact]
  public async Task RemovePersonData_KeepsSharedDataReferencedByAnotherPerson()
  {
    // One Data blob linked to two persons. Removing it from one must unlink it there but leave the
    // blob intact (the foreign key blocks deletion while the other person still references it).
    var shared = await _doc.Data.AddDataAsync([7, 7], "application/octet-stream", DataCategory.PersonPhoto, Token);
    var personA = await AddBarePersonAsync();
    var personB = await AddBarePersonAsync();
    await _doc.PersonData.AddPersonDataSetAsync(personA, [shared], Token);
    await _doc.PersonData.AddPersonDataSetAsync(personB, [shared], Token);

    await _doc.PersonData.RemovePersonDataAsync(personA, shared, Token);

    (await _doc.PersonData.GetPersonDataSetAsync(personA, null, Token)).Should().BeEmpty();
    (await _doc.PersonData.GetPersonDataSetAsync(personB, null, Token)).Should().ContainSingle();
    (await _doc.Data.TryGetDataByIdAsync(shared.Id, Token)).Should().NotBeNull();
  }

  // --- Relatives ---------------------------------------------------------------------------------

  [Fact]
  public async Task Relatives_Add_ResolvesBothDirections()
  {
    var parent = await AddBarePersonAsync();
    var child = await AddBarePersonAsync();

    await _doc.Relatives.AddRelativesAsync(parent, [new Relative(child, RelationshipType.Child, null)], Token);

    var parentRelatives = await _doc.Relatives.GetRelativesAsync(parent, Token);
    parentRelatives.Should().ContainSingle();
    parentRelatives[0].Id.Should().Be(child.Id);
    parentRelatives[0].Type.Should().Be(RelationshipType.Child);

    // The child sees the inverse relationship from the same stored row.
    var childRelatives = await _doc.Relatives.GetRelativesAsync(child, Token);
    childRelatives.Should().ContainSingle();
    childRelatives[0].Id.Should().Be(parent.Id);
    childRelatives[0].Type.Should().Be(RelationshipType.Parent);
  }

  [Fact]
  public async Task Relatives_Add_Spouse_IsSymmetric()
  {
    var a = await AddBarePersonAsync(BiologicalSex.Male);
    var b = await AddBarePersonAsync(BiologicalSex.Female);

    await _doc.Relatives.AddRelativesAsync(a, [new Relative(b, RelationshipType.Spouse, null)], Token);

    (await _doc.Relatives.GetRelativesAsync(b, Token)).Single().Type.Should().Be(RelationshipType.Spouse);
  }

  [Fact]
  public async Task Relatives_Add_InvalidType_Throws()
  {
    var a = await AddBarePersonAsync();
    var b = await AddBarePersonAsync();

    var act = () => _doc.Relatives.AddRelativesAsync(a, [new Relative(b, RelationshipType.Sibling, null)], Token);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task Relatives_Update_AddsNewAndKeepsUnchanged()
  {
    var person = await AddBarePersonAsync();
    var keep = await AddBarePersonAsync();
    var drop = await AddBarePersonAsync();
    var add = await AddBarePersonAsync();

    await _doc.Relatives.AddRelativesAsync(person,
      [new Relative(keep, RelationshipType.Child, null), new Relative(drop, RelationshipType.Child, null)], Token);

    await _doc.Relatives.UpdateRelativesAsync(person,
      [new Relative(keep, RelationshipType.Child, null), new Relative(add, RelationshipType.Child, null)], Token);

    var relatives = await _doc.Relatives.GetRelativesAsync(person, Token);
    relatives.Id().Should().BeEquivalentTo([keep.Id, add.Id]);
  }

  [Fact]
  public async Task Relatives_Update_RemovesSpouse_AddedFromTheOtherSide()
  {
    // A spouse edge is stored as a single row whose (PersonId, RelativeId) order depends on who added
    // it: here the marriage is added from b's side, so the row is (b, a, Spouse). Removing it while
    // updating a's relatives must still delete that row — the forward-only DELETE used to miss it and
    // leave a dangling spouse on both people.
    var a = await AddBarePersonAsync(BiologicalSex.Male);
    var b = await AddBarePersonAsync(BiologicalSex.Female);

    await _doc.Relatives.AddRelativesAsync(b, [new Relative(a, RelationshipType.Spouse, null)], Token);

    // Sanity check: the marriage is visible from a's side before the update.
    (await _doc.Relatives.GetRelativesAsync(a, Token)).Should().ContainSingle();

    await _doc.Relatives.UpdateRelativesAsync(a, [], Token);

    (await _doc.Relatives.GetRelativesAsync(a, Token)).Should().BeEmpty();
    (await _doc.Relatives.GetRelativesAsync(b, Token)).Should().BeEmpty();
  }

  [Fact]
  public async Task Relatives_Update_KeepsOneOfTwoDatedSpouseRecords()
  {
    // Regression (currently FAILS): the DELETE in UpdateRelativesAsync filters only on
    // (PersonId, RelativeId, Type) and ignores Date, even though the table's primary key includes
    // Date. When a pair has two spouse records that differ only by Date (a marriage and a later
    // remarriage), updating to keep just one deletes BOTH rows and then never re-adds the kept one
    // (it was classified as "remained"), so the record we asked to keep is lost.
    var a = await AddBarePersonAsync(BiologicalSex.Male);
    var b = await AddBarePersonAsync(BiologicalSex.Female);

    var firstMarriage = Date.Create(20000101, DateStatus.WellKnown);
    var secondMarriage = Date.Create(20100101, DateStatus.WellKnown);

    await _doc.Relatives.AddRelativesAsync(a,
      [
        new Relative(b, RelationshipType.Spouse, firstMarriage),
        new Relative(b, RelationshipType.Spouse, secondMarriage),
      ], Token);

    // Keep only the first marriage record, drop the remarriage.
    await _doc.Relatives.UpdateRelativesAsync(a,
      [new Relative(b, RelationshipType.Spouse, firstMarriage)], Token);

    var relatives = await _doc.Relatives.GetRelativesAsync(a, Token);
    relatives.Should().ContainSingle();
    relatives[0].Id.Should().Be(b.Id);
    relatives[0].Date.Should().Be(firstMarriage);
  }

  [Fact]
  public async Task GetRelativesForPersons_EmptyInput_ReturnsEmpty()
  {
    var result = await _doc.Relatives.GetRelativesForPersonsAsync([], Token);

    result.Should().BeEmpty();
  }

  [Fact]
  public async Task GetRelativesForPersons_ForwardLink_ReturnsRelative()
  {
    var parent = await AddBarePersonAsync();
    var child = await AddBarePersonAsync();
    await _doc.Relatives.AddRelativesAsync(parent, [new Relative(child, RelationshipType.Child, null)], Token);

    var result = await _doc.Relatives.GetRelativesForPersonsAsync([parent], Token);

    result.Should().ContainKey(parent.Id);
    result[parent.Id].Should().ContainSingle().Which.Id.Should().Be(child.Id);
    result[parent.Id].Single().Type.Should().Be(RelationshipType.Child);
  }

  [Fact]
  public async Task GetRelativesForPersons_BackwardLink_ReturnsInvertedRelative()
  {
    var parent = await AddBarePersonAsync();
    var child = await AddBarePersonAsync();
    await _doc.Relatives.AddRelativesAsync(parent, [new Relative(child, RelationshipType.Child, null)], Token);

    // Query from the child side — the row is stored with child as RelativeId, so
    // the backward-link query must flip Child→Parent.
    var result = await _doc.Relatives.GetRelativesForPersonsAsync([child], Token);

    result.Should().ContainKey(child.Id);
    result[child.Id].Should().ContainSingle().Which.Id.Should().Be(parent.Id);
    result[child.Id].Single().Type.Should().Be(RelationshipType.Parent);
  }

  [Fact]
  public async Task GetRelativesForPersons_MultiplePersons_BucketsCorrectly()
  {
    var personA = await AddBarePersonAsync();
    var personB = await AddBarePersonAsync();
    var relA = await AddBarePersonAsync();
    var relB = await AddBarePersonAsync();
    await _doc.Relatives.AddRelativesAsync(personA, [new Relative(relA, RelationshipType.Child, null)], Token);
    await _doc.Relatives.AddRelativesAsync(personB, [new Relative(relB, RelationshipType.Child, null)], Token);

    var result = await _doc.Relatives.GetRelativesForPersonsAsync([personA, personB], Token);

    result.Should().ContainKey(personA.Id);
    result.Should().ContainKey(personB.Id);
    result[personA.Id].Should().ContainSingle().Which.Id.Should().Be(relA.Id);
    result[personB.Id].Should().ContainSingle().Which.Id.Should().Be(relB.Id);
  }

  [Fact]
  public async Task GetRelativesForPersons_AdoptiveRelationship_FlipsCorrectly()
  {
    var adoptiveParent = await AddBarePersonAsync();
    var adoptiveChild = await AddBarePersonAsync();
    await _doc.Relatives.AddRelativesAsync(adoptiveParent,
      [new Relative(adoptiveChild, RelationshipType.AdoptiveChild, null)], Token);

    // From the child's perspective the backward link must yield AdoptiveParent.
    var resultChild = await _doc.Relatives.GetRelativesForPersonsAsync([adoptiveChild], Token);
    resultChild[adoptiveChild.Id].Single().Type.Should().Be(RelationshipType.AdoptiveParent);

    // From the parent's perspective the forward link must yield AdoptiveChild.
    var resultParent = await _doc.Relatives.GetRelativesForPersonsAsync([adoptiveParent], Token);
    resultParent[adoptiveParent.Id].Single().Type.Should().Be(RelationshipType.AdoptiveChild);
  }

  [Fact]
  public async Task Relatives_HasCommonAncestors_DetectsSharedGrandparent()
  {
    var grandparent = await AddBarePersonAsync();
    var parentA = await AddBarePersonAsync();
    var parentB = await AddBarePersonAsync();
    var cousinA = await AddBarePersonAsync();
    var cousinB = await AddBarePersonAsync();
    var stranger = await AddBarePersonAsync();

    await _doc.Relatives.AddRelativesAsync(grandparent,
      [new Relative(parentA, RelationshipType.Child, null), new Relative(parentB, RelationshipType.Child, null)], Token);
    await _doc.Relatives.AddRelativesAsync(parentA, [new Relative(cousinA, RelationshipType.Child, null)], Token);
    await _doc.Relatives.AddRelativesAsync(parentB, [new Relative(cousinB, RelationshipType.Child, null)], Token);

    (await _doc.Relatives.HasCommonAncestorsAsync(cousinA, cousinB, Token)).Should().BeTrue();
    (await _doc.Relatives.HasCommonAncestorsAsync(cousinA, stranger, Token)).Should().BeFalse();
  }

  // --- Document lifecycle ------------------------------------------------------------------------

  [Fact]
  public async Task Open_ExistingDocument_ReadsPersistedData()
  {
    // Self-contained: a fresh file created, closed and reopened, then cleaned up here so the fixture's
    // own document is never touched.
    var path = Path.Combine(Path.GetTempPath(), $"gt4_open_{Guid.NewGuid():N}.db");
    try
    {
      await using (var doc = await ProjectDocument.CreateNewAsync(path, "persisted-test", Token))
      {
        await doc.Metadata.SetProjectNameAsync("persisted", Token);
      }

      await using var reopened = await ProjectDocument.OpenAsync(path, Token);
      (await reopened.Metadata.GetProjectNameAsync(Token)).Should().Be("persisted");
    }
    finally
    {
      foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
      {
        try { File.Delete(path + suffix); } catch { /* best-effort */ }
      }
    }
  }

  [Fact]
  public void Write_BumpsProjectRevision()
  {
    // The revision is a monotonic counter, so every stamp strictly advances it.
    var before = _doc.ProjectRevision;

    _doc.UpdateRevision();

    _doc.ProjectRevision.Should().BeGreaterThan(before);
  }
}
