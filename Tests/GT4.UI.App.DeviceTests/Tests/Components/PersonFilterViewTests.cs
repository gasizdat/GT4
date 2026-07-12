using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Utils.Formatters;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public sealed class PersonFilterViewTests
{
  private static async Task<PersonFilterView> CreateViewAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new PersonFilterView());
  }

  private static Task InitializeAsync(PersonFilterView view, TestServices services, Func<Person[]>? snapshotPersons = null) =>
    MainThread.InvokeOnMainThreadAsync(() => view.Initialize(
      services.Provider.GetRequiredService<IBiologicalSexFormatter>(),
      services.Provider.GetRequiredService<ICancellationTokenProvider>(),
      services.CurrentProjectProvider.Object,
      services.AlertService.Object,
      snapshotPersons ?? (() => [])));

  private static PersonInfo SamplePerson(int id = 1, int birthYear = 2000, BiologicalSex sex = BiologicalSex.Male) =>
    new(id, Date.Create(birthYear, 1, 1, DateStatus.WellKnown), null, sex, [], null);

  [Fact]
  public async Task Matches_BeforeInitialize_DoesNotThrowAndUsesAnUnfilteredState()
  {
    var view = await CreateViewAsync();

    var matched = view.Matches(SamplePerson());

    Assert.True(matched);
  }

  [Fact]
  public async Task Initialize_AssigningPickerItemsSource_DoesNotRaiseChanged()
  {
    var view = await CreateViewAsync();
    var services = new TestServices();
    var raised = 0;
    view.Changed += (_, _) => raised++;

    await InitializeAsync(view, services);

    Assert.Equal(0, raised);
  }

  [Fact]
  public async Task ClearFiltersButton_Click_ResetsEveryControlToMatchTheFilter()
  {
    var view = await CreateViewAsync();
    var services = new TestServices();
    await InitializeAsync(view, services);

    var nameEntry = view.FindByName<Entry>("NameFilterEntry");
    var sexPicker = view.FindByName<Picker>("SexFilterPicker");
    var clearButton = view.FindByName<Button>("ClearFiltersButton");

    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      nameEntry.Text = "John";
      sexPicker.SelectedIndex = 1;
    });
    var raised = 0;
    view.Changed += (_, _) => raised++;

    await MainThread.InvokeOnMainThreadAsync(clearButton.SendClicked);

    Assert.Equal(string.Empty, nameEntry.Text);
    Assert.Equal(0, sexPicker.SelectedIndex);
    Assert.False(view.IsAnyFilterActive);
    Assert.Equal(1, raised);
  }

  [Fact]
  public async Task IsFiltersVisible_SetTrue_SnapshotsPersonsSynchronouslyBeforeTheBackgroundFetch()
  {
    var view = await CreateViewAsync();
    var services = new TestServices();
    var snapshotCalls = 0;
    await InitializeAsync(view, services, () => { snapshotCalls++; return []; });

    await MainThread.InvokeOnMainThreadAsync(() => view.IsFiltersVisible = true);

    Assert.Equal(1, snapshotCalls);
  }

  [Fact]
  public async Task IsFiltersVisible_SetTrue_YearBoundsWellAboveInitialRange_ApplyCorrectly()
  {
    // The slider starts at [0, 1]; a real person set pushes bounds well above that (e.g. birth 1850).
    // Slider.Minimum/Maximum have no built-in ordering guard (confirmed directly: setting Minimum
    // alone above the default Maximum of 1 sticks, uncoerced), so applying both in the wrong order
    // is a real way to leave the slider in a Minimum > Maximum state.
    var view = await CreateViewAsync();
    var services = new TestServices();
    var person = new Person(1, Date.Create(1850, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male);
    await InitializeAsync(view, services, () => [person]);

    var loaded = new TaskCompletionSource();
    view.FilterDataLoaded += (_, _) => loaded.SetResult();
    await MainThread.InvokeOnMainThreadAsync(() => view.IsFiltersVisible = true);
    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5));

    var slider = view.FindByName<Slider>("YearSlider");
    Assert.Equal(1850, slider.Minimum);
    Assert.True(slider.Maximum >= DateTime.Now.Year);
    services.AlertService.Verify(a => a.ShowErrorAsync(It.IsAny<Exception>()), Times.Never());
  }
}
