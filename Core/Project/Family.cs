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
}
