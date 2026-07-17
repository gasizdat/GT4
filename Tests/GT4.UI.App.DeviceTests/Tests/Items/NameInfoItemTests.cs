using GT4.Core.Project.Dto;
using GT4.UI.Items;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>Covers NameInfoItem.CanBeRemoved, which drives the delete adorner's visibility.</summary>
public class NameInfoItemTests
{
  private static Name N(int id, string value, NameType type) => new(id, value, type, null);

  [Fact]
  public void CanBeRemoved_is_false_for_a_FamilyName()
  {
    var item = new NameInfoItem(N(1, "Ivanov", NameType.FamilyName), Mock.Of<INameTypeFormatter>());

    Assert.False(item.CanBeRemoved);
  }

  [Theory]
  [InlineData(NameType.FirstName)]
  [InlineData(NameType.Patronymic)]
  [InlineData(NameType.LastName)]
  public void CanBeRemoved_is_true_for_other_name_types(NameType type)
  {
    var item = new NameInfoItem(N(1, "Ivan", type), Mock.Of<INameTypeFormatter>());

    Assert.True(item.CanBeRemoved);
  }
}
