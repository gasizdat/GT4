using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public class FamilyManager : TableBase
{
  public FamilyManager(ProjectDocument document)
    : base(document: document)
  {
  }

  public async Task<Name[]> GetFamiliesAsync(CancellationToken token)
  {
    var ret = await Document.Names.GetNamesByTypeAsync(NameType.FamilyName, token);

    return ret;
  }

  public async Task<Name> AddFamilyAsync(string familyName, string maleLastName, string femaleLastName, CancellationToken token)
  {
    using var transaction = await Document.BeginTransactionAsync(token);

    var name = await Document.Names.AddNameAsync(familyName, NameType.FamilyName, null, token);
    await Task.WhenAll(
      Document.Names.AddNameAsync(maleLastName, NameType.LastName | NameType.MaleDeclension, name, token),
      Document.Names.AddNameAsync(femaleLastName, NameType.LastName | NameType.FemaleDeclension, name, token));

    transaction.Commit();

    return name;
  }

  public async Task RemoveFamilyAsync(Name familyName, CancellationToken token)
  {
    if (familyName.Type != NameType.FamilyName)
      throw new ArgumentException("The provided name is not a family name.", nameof(familyName));

    await Document.Names.RemoveNameWithSubnamesAsync(familyName, token);

    //TODO : Remove related persons or handle them appropriately
  }

  public async Task<Name[]> GetRequiredNames(Name familyName, PersonInfo personInfo, CancellationToken token)
  {
    var lastNameType = personInfo.BiologicalSex switch
    {
      BiologicalSex.Male => NameType.LastName | NameType.MaleDeclension,
      BiologicalSex.Female => NameType.LastName | NameType.FemaleDeclension,
      _ => NameType.LastName,
    };

    var names = new List<Name>();
    var lastNames = await Document.Names.TryGetNameWithSubnamesByIdAsync(familyName.Id, token);
    var lastName = lastNames?.SingleOrDefault(name => name.Type == lastNameType);
    if (lastName is not null && !personInfo.Names.Contains(lastName))
    {
      names.Add(lastName);
    }

    if (!personInfo.Names.Contains(familyName))
    {
      names.Add(familyName);
    }

    return names.ToArray();
  }

  public override Task CreateAsync(CancellationToken token)
  {
    throw new NotSupportedException();
  }
}
