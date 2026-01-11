using AutoFixture;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using Microsoft.Data.Sqlite;
using Moq;
using System.Data;

namespace GT4.Core.Project.Tests;
internal class ProjectDocumentMock : IProjectDocument
{
  private readonly Fixture _fixture = new();
  private int _Id = 1;

  private Mock<ITableData> _TableDataMock = new(MockBehavior.Strict);
  private Mock<IFamilyManager> _FamilyManagerMock = new(MockBehavior.Strict);
  private Mock<ITableMetadata> _TableMetadataMock = new(MockBehavior.Strict);
  private Mock<ITableNames> _TableNamesMock = new(MockBehavior.Strict);
  private Mock<ITablePersonData> _TablePersonDataMock = new(MockBehavior.Strict);
  private Mock<IPersonManager> _PersonManagerMock = new(MockBehavior.Strict);
  private Mock<ITablePersonNames> _TablePersonNamesMock = new(MockBehavior.Strict);
  private Mock<ITablePersons> _TablePersonsMock = new(MockBehavior.Strict);
  private Mock<ITableRelatives> _TableRelativesMock = new(MockBehavior.Strict);
  private Mock<IRelativesProvider> _RelativesProviderMock = new(MockBehavior.Strict);
  private IDictionary<int, PersonFullInfo> _Persons = new Dictionary<int, PersonFullInfo>();
  private IDictionary<int, IList<Relative>> _Relatives = new Dictionary<int, IList<Relative>>();

  public ProjectDocumentMock()
  {
    _TableRelativesMock
      .Setup(s => s.GetRelativesAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person p, CancellationToken _) =>
        _Relatives.TryGetValue(p.Id, out var ret) ? ret.ToArray() : []);

    _PersonManagerMock
      .Setup(s => s.GetPersonInfosAsync(It.IsAny<Person[]>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person[] persons, bool _, CancellationToken _) =>
        persons.Select(p => _Persons[p.Id]).ToArray());

    _PersonManagerMock
      .Setup(s => s.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person p, CancellationToken _) => _Persons[p.Id]);
  }

  public int GetNewId() => Interlocked.Add(ref _Id, 100);

  public long ProjectRevision => throw new NotImplementedException();

  public ITableData Data => _TableDataMock.Object;

  public IFamilyManager FamilyManager => _FamilyManagerMock.Object;

  public ITableMetadata Metadata => _TableMetadataMock.Object;

  public ITableNames Names => _TableNamesMock.Object;

  public ITablePersonData PersonData => _TablePersonDataMock.Object;

  public IPersonManager PersonManager => _PersonManagerMock.Object;

  public ITablePersonNames PersonNames => _TablePersonNamesMock.Object;

  public ITablePersons Persons => _TablePersonsMock.Object;

  public ITableRelatives Relatives => _TableRelativesMock.Object;

  public IRelativesProvider RelativesProvider => _RelativesProviderMock.Object;

  public Task<IDbTransaction> BeginTransactionAsync(CancellationToken token)
  {
    throw new NotImplementedException();
  }

  public SqliteCommand CreateCommand()
  {
    throw new NotImplementedException();
  }

  public void Dispose()
  {
    throw new NotImplementedException();
  }

  public ValueTask DisposeAsync()
  {
    throw new NotImplementedException();
  }

  public Task<int> GetLastInsertRowIdAsync(CancellationToken token)
  {
    throw new NotImplementedException();
  }

  public void UpdateRevision()
  {
    throw new NotImplementedException();
  }

  public PersonFullInfo CreatePerson(BiologicalSex sex = BiologicalSex.Unknown)
  {
    lock (_fixture)
    {
      var person = _fixture.Create<PersonFullInfo>() with
      {
        Id = GetNewId(),
        BiologicalSex = sex
      };

      _Persons.Add(person.Id, person);

      return person;
    }
  }

  public void AddRelationship(Person personA, Person personB, RelationshipType relationshipType)
  {
    void InnerAdd(Person personA, Person personB, RelationshipType relationshipType)
    {
      var relative = new Relative(personB, relationshipType, null);
      if (_Relatives.TryGetValue(personA.Id, out var relatives))
      {
        relatives.Add(relative);
      }
      else
      {
        _Relatives.Add(personA.Id, [relative]);
      }
    }

    lock (_fixture)
    {
      InnerAdd(personA, personB, relationshipType);
      var backRef = relationshipType switch
      {
        RelationshipType.Parent => RelationshipType.Child,
        RelationshipType.Child => RelationshipType.Parent,
        RelationshipType.Spouse => RelationshipType.Spouse,
        RelationshipType.AdoptiveParent => RelationshipType.AdoptiveChild,
        RelationshipType.AdoptiveChild => RelationshipType.AdoptiveParent,
        _ => throw new ArgumentException(nameof(relationshipType))
      };
      InnerAdd(personB, personA, backRef);
    }
  }

  public RelativeFullInfo GetRelativeFullInfo(Person person)
  {
    lock (_fixture)
    {
      RelativeInfo[] relatives;
      if (_Relatives.TryGetValue(person.Id, out var rels))
      {
        relatives = rels
          .Select(r => new RelativeInfo(_Persons[r.Id], r.Type, r.Date, new Generation(r.Type), Consanguinity.Zero))
          .ToArray();
      }
      else
      {
        relatives = [];
      }

      var ret = new RelativeFullInfo(
        new RelativeInfo(
          _Persons[person.Id],
          default,
          default,
          Generation.Zero,
          Consanguinity.Zero),
        relatives
      );

      return ret;
    }
  }
}
