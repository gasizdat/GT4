using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// A hidden label keeps evaluating its Text binding, so the two text properties are asked for their
/// value whether or not anyone can see the result. Each returns null once its own Show flag is off,
/// which is what keeps the name and date formatters off the hot path of a view that shows neither.
/// </summary>
public class PersonInfoViewTests
{
  private static PersonInfo Person =>
    new(1, Date.Now, null, BiologicalSex.Male, [new Name(100, "John", NameType.FirstName, null)], null);

  private static async Task<TestablePersonInfoView> CreateViewAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestablePersonInfoView(services.Provider));
  }

  [Fact]
  public async Task CommonName_is_null_once_the_name_is_hidden()
  {
    var view = await CreateViewAsync(new TestServices());

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.SetValue(PersonInfoView.PersonProperty, Person);
      // The premise: with the label shown, the formatter does run.
      Assert.NotEmpty(view.CommonName!);

      view.SetValue(PersonInfoView.ShowNameProperty, false);

      Assert.Null(view.CommonName);
    });
  }

  [Fact]
  public async Task LifeDates_is_null_once_the_dates_are_hidden()
  {
    var view = await CreateViewAsync(new TestServices());

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.SetValue(PersonInfoView.PersonProperty, Person);
      Assert.NotEmpty(view.LifeDates!);

      view.SetValue(PersonInfoView.ShowDatesProperty, false);

      Assert.Null(view.LifeDates);
    });
  }
}
