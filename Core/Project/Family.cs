using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public class Family
{
  private readonly ProjectDocument _Document;

  public Family(ProjectDocument document)
  {
    _Document = document;
  }

  public async Task<Name> AddFamilyAsync(string familyName, string maleLastName, string femaleLastName, CancellationToken token)
  {
    using var transaction = await _Document.BeginTransactionAsync(token);

    var name = await _Document.Names.AddNameAsync(familyName, NameType.FamilyName, null, token);
    await Task.WhenAll(
      _Document.Names.AddNameAsync(maleLastName, NameType.LastName | NameType.MaleDeclension, name, token),
      _Document.Names.AddNameAsync(femaleLastName, NameType.LastName | NameType.FemaleDeclension, name, token));

    transaction.Commit();

    return name;
  }

  public async Task RemoveFamilyAsync(Name familyName, CancellationToken token)
  {
    if (familyName.Type != NameType.FamilyName)
      throw new ArgumentException("The provided name is not a family name.", nameof(familyName));

    await _Document.Names.RemoveNameWithSubnamesAsync(familyName, token);

    //TODO : Remove related persons or handle them appropriately
  }

  public async Task<int> AddPersonToFamilyAsync(Name familyName, Person person, CancellationToken token)
  {
    using var transaction = await _Document.BeginTransactionAsync(token);

    var personId = await _Document.Persons.AddPersonAsync(person, token);
    var names = await GetRequiredNames(familyName, person, token);
    if (names.Length > 0)
    {
      await _Document.PersonNames.AddNamesAsync(personId, names, token);
    }

    transaction.Commit();

    return personId;
  }

  private async Task<Name[]> GetRequiredNames(Name familyName, Person person, CancellationToken token)
  {
    var lastNameType = person.BiologicalSex switch
    {
      BiologicalSex.Male => NameType.LastName | NameType.MaleDeclension,
      BiologicalSex.Female => NameType.LastName | NameType.FemaleDeclension,
      _ => NameType.LastName,
    };

    var names = new List<Name>();
    var lastNames = await _Document.Names.GetNameWithSubnamesAsync(familyName.Id, token);
    var lastName = lastNames?.SingleOrDefault(name => name.Type == lastNameType);
    if (lastName is not null && !person.Names.Contains(lastName))
    {
      names.Add(lastName);
    }

    if (!person.Names.Contains(familyName))
    {
      names.Add(familyName);
    }

    return names.ToArray();
  }
}
