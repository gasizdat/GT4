using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Components;
using GT4.UI.Utils.Formatters;
using Xunit;

namespace GT4.UI.DeviceTests;

/// <summary>
/// Covers RelativeRowView's own wiring logic: OnBindingContextChanged hands the bound RelativeRow to
/// its private RelativeConnectorsDrawable and seeds PhotoCenterY from the embedded RelativeInfoView's
/// current PersonInfoFrame, and OnInfoPropertyChanged keeps PhotoCenterY in sync afterward -- but only
/// in reaction to PersonInfoFrame itself, not any other property of that inner view. Reached through
/// ConnectorsDrawable/InfoView, two protected read-only seams added for this test (the drawable and the
/// embedded view are otherwise private XAML-named fields with no other public reflection of their
/// state). OnLayoutSizeChanged is not covered: it only mirrors a real measured Width/Height into the
/// drawable and needs an actual layout pass to observe non-default values, which isn't worth a seam.
/// </summary>
public class RelativeRowViewTests
{
  private static async Task<TestableRelativeRowView> CreateViewAsync()
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new TestableRelativeRowView());
  }

  private static RelativeRow MakeRow(int depth, bool[] ancestorContinues, bool isFilterActive = false)
  {
    var relative = new RelativeInfo(
      new PersonInfo(1, Date.Create(2000, 1, 1, DateStatus.WellKnown), null, BiologicalSex.Male, [], null),
      RelationshipType.Parent,
      null,
      Generation.Parent,
      Consanguinity.Zero);
    return new RelativeRow(
      relative, null, depth, isLast: true, ancestorContinues, shouldShow: true, isFilterActive, new Command(() => { }));
  }

  [Fact]
  public async Task Setting_BindingContext_wires_the_row_into_the_connectors_drawable()
  {
    var view = await CreateViewAsync();
    var row = MakeRow(2, [true, false]);

    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = row);

    Assert.Same(row, view.ConnectorsDrawableForTest.Row);
  }

  [Fact]
  public async Task Setting_BindingContext_indents_by_depth_when_no_filter_is_active()
  {
    var view = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = MakeRow(2, [true, false], isFilterActive: false));

    Assert.True(view.ContentMarginForTest.Left > 0);
  }

  [Fact]
  public async Task Setting_BindingContext_drops_indentation_when_a_filter_is_active()
  {
    var view = await CreateViewAsync();

    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = MakeRow(2, [true, false], isFilterActive: true));

    Assert.Equal(0, view.ContentMarginForTest.Left);
  }

  [Fact]
  public async Task Row_becoming_filter_active_after_binding_drops_its_indentation()
  {
    var view = await CreateViewAsync();
    var row = MakeRow(2, [true, false]);
    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = row);
    Assert.True(view.ContentMarginForTest.Left > 0);

    await MainThread.InvokeOnMainThreadAsync(() => row.IsFilterActive = true);

    Assert.Equal(0, view.ContentMarginForTest.Left);
  }

  [Fact]
  public async Task Setting_BindingContext_seeds_PhotoCenterY_from_the_Info_views_current_frame()
  {
    var view = await CreateViewAsync();
    await MainThread.InvokeOnMainThreadAsync(() => view.InfoViewForTest.PersonInfoFrame = new Rect(0, 0, 40, 60));

    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = MakeRow(1, [false]));

    Assert.Equal(30, view.ConnectorsDrawableForTest.PhotoCenterY);
  }

  [Fact]
  public async Task A_later_change_to_the_Info_views_PersonInfoFrame_updates_PhotoCenterY()
  {
    var view = await CreateViewAsync();
    await MainThread.InvokeOnMainThreadAsync(() => view.BindingContext = MakeRow(1, [false]));

    await MainThread.InvokeOnMainThreadAsync(() => view.InfoViewForTest.PersonInfoFrame = new Rect(0, 0, 40, 100));

    Assert.Equal(50, view.ConnectorsDrawableForTest.PhotoCenterY);
  }

  [Fact]
  public async Task Changing_an_unrelated_Info_view_property_does_not_touch_PhotoCenterY()
  {
    var view = await CreateViewAsync();
    await MainThread.InvokeOnMainThreadAsync(() =>
    {
      view.InfoViewForTest.PersonInfoFrame = new Rect(0, 0, 40, 60);
      view.BindingContext = MakeRow(1, [false]);
    });
    // A sentinel a recompute would overwrite: if the PersonInfoFrame guard were missing, changing
    // NameFormat would still recompute PhotoCenterY from the frame (30), which would pass a plain
    // before/after equality check even with the guard gone. Poke a value the frame could never
    // produce so only "the handler didn't run at all" keeps it.
    await MainThread.InvokeOnMainThreadAsync(() => view.ConnectorsDrawableForTest.PhotoCenterY = 999);

    await MainThread.InvokeOnMainThreadAsync(() => view.InfoViewForTest.NameFormat = NameFormat.ShortPersonName);

    Assert.Equal(999, view.ConnectorsDrawableForTest.PhotoCenterY);
  }
}
