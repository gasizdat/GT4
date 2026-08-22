using GT4.UI.Abstraction;
using GT4.UI.Components;
using GT4.UI.Items;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class PageLayoutTests
{
  private static async Task<PageLayout> CreateLayoutAsync()
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new PageLayout(services.Provider));
  }

  private static Task ArrangeAsync(PageLayout layout, double width, double height) =>
    MainThread.InvokeOnMainThreadAsync(() => ((IView)layout).Arrange(new Rect(0, 0, width, height)));

  private static Task AddMenuItemAsync(PageLayout layout) =>
    MainThread.InvokeOnMainThreadAsync(() => layout.MenuItems.Add(new PageMenuItem { Text = "🔄️ Refresh" }));

  [Fact]
  public async Task Without_menu_items_neither_menu_shows_in_either_orientation()
  {
    var layout = await CreateLayoutAsync();

    await ArrangeAsync(layout, 400, 800);
    Assert.False(layout.IsTopMenuVisible);
    Assert.False(layout.IsSideMenuVisible);

    await ArrangeAsync(layout, 800, 400);
    Assert.False(layout.IsTopMenuVisible);
    Assert.False(layout.IsSideMenuVisible);
  }

  [Fact]
  public async Task Before_the_first_layout_pass_neither_menu_shows_even_with_items()
  {
    var layout = await CreateLayoutAsync();

    await AddMenuItemAsync(layout);

    Assert.False(layout.IsTopMenuVisible);
    Assert.False(layout.IsSideMenuVisible);
  }

  [Fact]
  public async Task A_portrait_layout_puts_the_menu_on_top()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);

    await ArrangeAsync(layout, 400, 800);

    Assert.True(layout.IsTopMenuVisible);
    Assert.False(layout.IsSideMenuVisible);
  }

  [Fact]
  public async Task A_landscape_layout_puts_the_menu_on_the_side()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);

    await ArrangeAsync(layout, 800, 400);

    Assert.False(layout.IsTopMenuVisible);
    Assert.True(layout.IsSideMenuVisible);
  }

  [Fact]
  public async Task A_square_layout_still_shows_exactly_one_menu()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);

    await ArrangeAsync(layout, 500, 500);

    Assert.NotEqual(layout.IsTopMenuVisible, layout.IsSideMenuVisible);
  }

  [Fact]
  public async Task Rotating_reports_both_menu_flags_as_changed()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);
    await ArrangeAsync(layout, 400, 800);
    var changed = new List<string?>();
    layout.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    await ArrangeAsync(layout, 800, 400);

    Assert.Contains(nameof(PageLayout.IsTopMenuVisible), changed);
    Assert.Contains(nameof(PageLayout.IsSideMenuVisible), changed);
  }

  [Fact]
  public async Task Adding_the_first_menu_item_reports_only_the_flag_that_actually_turned_on()
  {
    var layout = await CreateLayoutAsync();
    await ArrangeAsync(layout, 400, 800);
    var changed = new List<string?>();
    layout.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

    await AddMenuItemAsync(layout);

    Assert.Contains(nameof(PageLayout.IsTopMenuVisible), changed);
    Assert.DoesNotContain(nameof(PageLayout.IsSideMenuVisible), changed);
  }

  [Fact]
  public async Task Rotating_does_not_rebuild_the_menu_buttons()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);
    await ArrangeAsync(layout, 400, 800);
    var topMenu = layout.FindByName<FlexLayout>("TopMenu");
    var sideMenu = layout.FindByName<FlexLayout>("SideMenu");
    var topButton = topMenu.Children.Single();
    var sideButton = sideMenu.Children.Single();

    await ArrangeAsync(layout, 800, 400);

    Assert.Same(topButton, topMenu.Children.Single());
    Assert.Same(sideButton, sideMenu.Children.Single());
    Assert.Same(layout.MenuItems, BindableLayout.GetItemsSource(topMenu));
    Assert.Same(layout.MenuItems, BindableLayout.GetItemsSource(sideMenu));
  }

  [Fact]
  public async Task The_hint_shows_only_once_it_holds_text()
  {
    var layout = await CreateLayoutAsync();

    Assert.False(layout.IsHintVisible);

    await MainThread.InvokeOnMainThreadAsync(() => layout.Hint = "   ");
    Assert.False(layout.IsHintVisible);

    await MainThread.InvokeOnMainThreadAsync(() => layout.Hint = "Open or create family");
    Assert.True(layout.IsHintVisible);
  }

  [Fact]
  public async Task An_assigned_loading_flag_drives_the_activity_indicator()
  {
    var layout = await CreateLayoutAsync();
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object);
    var indicator = layout.FindByName<ActivityIndicator>("LoadingIndicator");
    await MainThread.InvokeOnMainThreadAsync(() => layout.Loading = loading);

    Assert.False(indicator.IsRunning);
    Assert.False(indicator.IsVisible);

    await MainThread.InvokeOnMainThreadAsync(() => loading.IsLoading = true);
    Assert.True(indicator.IsRunning);
    Assert.True(indicator.IsVisible);

    await MainThread.InvokeOnMainThreadAsync(() => loading.IsLoading = false);
    Assert.False(indicator.IsRunning);
    Assert.False(indicator.IsVisible);
  }

  [Fact]
  public async Task A_flag_already_set_shows_the_indicator_the_moment_it_is_assigned()
  {
    var layout = await CreateLayoutAsync();
    var alertService = new Mock<IAlertService>();
    var loading = new PageLoading(alertService.Object) { IsLoading = true };
    var indicator = layout.FindByName<ActivityIndicator>("LoadingIndicator");

    await MainThread.InvokeOnMainThreadAsync(() => layout.Loading = loading);

    Assert.True(indicator.IsRunning);
    Assert.True(indicator.IsVisible);
  }

  [Fact]
  public async Task A_layout_left_without_a_loading_flag_keeps_the_indicator_hidden()
  {
    var layout = await CreateLayoutAsync();
    var indicator = layout.FindByName<ActivityIndicator>("LoadingIndicator");

    Assert.Null(layout.Loading);
    Assert.False(indicator.IsRunning);
    Assert.False(indicator.IsVisible);
  }
}
