using AutoFixture;
using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using Moq;

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
  private IDictionary<int, PersonFullInfo> _Persons = new Dictionary<int, PersonFullInfo>();
  private IDictionary<int, IList<Relative>> _Relatives = new Dictionary<int, IList<Relative>>();

  private int _GetRelativesCallCount = 0;
  private int _GetPersonFullInfoCallCount = 0;
  private int _GetPersonInfosWithPersonsCallCount = 0;

  public int GetRelativesCallCount => _GetRelativesCallCount;
  public int GetPersonFullInfoCallCount => _GetPersonFullInfoCallCount;
  public int GetPersonInfosWithPersonsCallCount => _GetPersonInfosWithPersonsCallCount;

  public void ResetCallCounts()
  {
    _GetRelativesCallCount = 0;
    _GetPersonFullInfoCallCount = 0;
    _GetPersonInfosWithPersonsCallCount = 0;
  }

  private RelativeInfo[] GetRelatives(int personId)
  {
    lock (_fixture)
    {
      if (_Relatives.TryGetValue(personId, out var relatives))
      {

        RelativeInfo GetRelativeInfo(Relative relative)
        {
          var generation = new Generation(relative.Type);
          var ret = new RelativeInfo(
            _Persons[relative.Id],
            relative.Type,
            relative.Date,
            generation,
            Consanguinity.Zero);
          return ret;
        }

        var ret = relatives
            .Select(GetRelativeInfo)
            .ToArray();

        return ret;
      }
    }

    return [];
  }

  public ProjectDocumentMock()
  {
    _TableRelativesMock
      .Setup(s => s.GetRelativesAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person p, CancellationToken _) =>
      {
        Interlocked.Increment(ref _GetRelativesCallCount);
        return _Relatives.TryGetValue(p.Id, out var ret) ? ret.ToArray() : [];
      });

    _TableRelativesMock
      .Setup(s => s.GetRelativesForPersonsAsync(It.IsAny<Person[]>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person[] persons, CancellationToken _) =>
        persons.ToDictionary(p => p.Id, p => _Relatives.TryGetValue(p.Id, out var ret) ? ret.ToArray() : []));


    _PersonManagerMock
      .Setup(s => s.GetPersonInfosAsync(It.IsAny<Person[]>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person[] persons, bool _, CancellationToken _) =>
      {
        Interlocked.Increment(ref _GetPersonInfosWithPersonsCallCount);
        return persons.Select(p => _Persons[p.Id] with { RelativeInfos = GetRelatives(p.Id) }).ToArray();
      });

    _PersonManagerMock
      .Setup(s => s.GetPersonFullInfoAsync(It.IsAny<Person>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((Person p, CancellationToken _) =>
      {
        Interlocked.Increment(ref _GetPersonFullInfoCallCount);
        return _Persons[p.Id] with { RelativeInfos = GetRelatives(p.Id) };
      });
  }

  public int GetNewId() => Interlocked.Add(ref _Id, 100);

  public string ProjectRevision => throw new NotImplementedException();

  public ITableData Data => _TableDataMock.Object;

  public IFamilyManager FamilyManager => _FamilyManagerMock.Object;

  public ITableMetadata Metadata => _TableMetadataMock.Object;

  public ITableNames Names => _TableNamesMock.Object;

  public ITablePersonData PersonData => _TablePersonDataMock.Object;

  public IPersonManager PersonManager => _PersonManagerMock.Object;

  public ITablePersonNames PersonNames => _TablePersonNamesMock.Object;

  public ITablePersons Persons => _TablePersonsMock.Object;

  public ITableRelatives Relatives => _TableRelativesMock.Object;

  // Real providers over the mocked tables: tests exercise the actual graph-walking logic.
  public IRelativesProvider RelativesProvider => new RelativesProvider(this);

  public IFamilyTreeProvider FamilyTreeProvider => new FamilyTreeProvider(this);

  public IKinshipFinder KinshipFinder => new KinshipFinder(this);

  public Task<IProjectTransaction> BeginTransactionAsync(CancellationToken token)
  {
    throw new NotImplementedException();
  }

  public ProjectCommand CreateCommand()
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

  public PersonFullInfo CreatePerson(BiologicalSex sex = BiologicalSex.Unknown)
  {
    lock (_fixture)
    {
      var person = _fixture.Create<PersonFullInfo>() with
      {
        Id = GetNewId(),
        BiologicalSex = sex,
        RelativeInfos = []
      };

      _Persons.Add(person.Id, person);

      return person;
    }
  }

  public void AddRelationship(Person from, Person to, RelationshipType relationshipType)
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
      InnerAdd(from, to, relationshipType);
      var backRef = relationshipType switch
      {
        RelationshipType.Parent => RelationshipType.Child,
        RelationshipType.Child => RelationshipType.Parent,
        RelationshipType.Spouse => RelationshipType.Spouse,
        RelationshipType.AdoptiveParent => RelationshipType.AdoptiveChild,
        RelationshipType.AdoptiveChild => RelationshipType.AdoptiveParent,
        _ => throw new ArgumentException(nameof(relationshipType))
      };
      InnerAdd(to, from, backRef);
    }
  }

  public RelativeFullInfo GetRelativeFullInfo(Person person)
  {
    var ret = new RelativeFullInfo(
      new RelativeInfo(
        _Persons[person.Id],
        default,
        default,
        Generation.Zero,
        Consanguinity.Zero),
      GetRelatives(person.Id)
    );

    return ret;

  }
}
