using GT4.UI.Components;
using GT4.UI.Items;
using GT4.UI.Pages;
using GT4.UI.Resources;
using Moq;
using Xunit;

namespace GT4.UI.DeviceTests;

public class PageLayoutTests
{
#if WINDOWS
  private const bool HasBackButton = false;
#else
  private const bool HasBackButton = true;
#endif

  private static Task<PageLayout> CreateLayoutAsync() => CreateLayoutAsync(new TestServices());

  private static async Task<PageLayout> CreateLayoutAsync(TestServices services)
  {
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => new PageLayout(services.Provider));
  }

  private static async Task<T> CreatePageAsync<T>() where T : notnull
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    return await MainThread.InvokeOnMainThreadAsync(() => services.Provider.GetRequiredService<T>());
  }

  private static Button BackButton(PageLayout layout)
  {
    var grid = (Grid)layout.Content;
    return grid.Children.OfType<Button>().Single();
  }

  // Whichever menu the current orientation put on screen holds the arranged button; the other is collapsed.
  private static Rect MenuButtonFrame(PageLayout layout)
  {
    var topMenu = layout.FindByName<FlexLayout>("TopMenu");
    var sideMenu = layout.FindByName<FlexLayout>("SideMenu");
    var topButton = (Button)topMenu.Children.Single();
    var sideButton = (Button)sideMenu.Children.Single();
    return topButton.Frame.Width > sideButton.Frame.Width ? topButton.Frame : sideButton.Frame;
  }

  private static Label TitleLabel(PageLayout layout)
  {
    var grid = (Grid)layout.Content;
    return grid.Children.OfType<Label>().First();
  }

  private static async Task<double> TitleOffsetAsync(bool hasBackButton)
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var layout = await MainThread.InvokeOnMainThreadAsync(() => new PageLayout(services.Provider)
    {
      Title = "Genealogy tree",
      HasBackButton = hasBackButton,
    });

    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = layout });
    await using var window = await WindowHost.AttachAsync(page);

    var frame = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => TitleLabel(layout).Frame),
      frame => frame.Width > 0,
      timeoutMessage: "The title was never arranged.");

    // Beyond the title's own horizontal padding, so the result is what the back button's column adds.
    return frame.X - TitleLabel(layout).Margin.Left;
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
  public async Task Back_navigates_one_level_up()
  {
    var services = new TestServices();
    var layout = await CreateLayoutAsync(services);

    await MainThread.InvokeOnMainThreadAsync(() => layout.BackItem.Command.Execute(null));

    await Poll.UntilAsync(
      () => Task.FromResult(services.NavigationService.Invocations.Count),
      count => count > 0,
      timeoutMessage: "The back button did not navigate.");
    services.NavigationService.Verify(n => n.GoToAsync("..", true), Times.Once());
  }

  [Fact]
  public async Task The_back_button_stays_hidden_until_a_page_asks_for_it()
  {
    var layout = await CreateLayoutAsync();

    Assert.False(layout.HasBackButton);
    Assert.False(BackButton(layout).IsVisible);
  }

  [Fact]
  public async Task Asking_for_a_back_button_shows_it_with_its_glyph_and_tooltip()
  {
    var layout = await CreateLayoutAsync();

    await MainThread.InvokeOnMainThreadAsync(() => layout.HasBackButton = true);

    var button = BackButton(layout);
    Assert.Equal(HasBackButton, button.IsVisible);
    Assert.Equal(UIStrings.MenuItemNameBack, layout.BackItem.Text);
    Assert.Equal(layout.BackItem.ButtonText, button.Text);
    Assert.Equal(layout.BackItem.ToolTipText, ToolTipProperties.GetText(button));
  }

  // The Shell nav bar is switched off app-wide (Styles.xaml, TargetType="Page"), which is what makes
  // the layout's own button the only in-app way back on Windows and Android alike.
  [Fact]
  public async Task A_page_hides_the_Shell_nav_bar()
  {
    var page = await CreatePageAsync<MainPage>();

    Assert.False(Shell.GetNavBarIsVisible(page));
  }

  [Fact]
  public async Task MainPage_offers_no_way_back_because_it_is_the_root()
  {
    var page = await CreatePageAsync<MainPage>();

    var layout = (PageLayout)page.Content;

    Assert.False(layout.HasBackButton);
  }

  // The button lives in a column of its own, so a layout that doesn't ask for one -- every dialog,
  // and the pages that carry no title -- must keep its header exactly where it was. Only a page
  // attached to a real window measures a Button: detached, its handler never sizes the text.
  [Fact]
  public async Task A_hidden_back_button_takes_no_room_from_the_title()
  {
    var withoutBack = await TitleOffsetAsync(hasBackButton: false);
    var withBack = await TitleOffsetAsync(hasBackButton: true);

    Assert.Equal(0, withoutBack);
    Assert.True(HasBackButton == withBack > 0, $"The back button claimed no width: title starts at {withBack}.");
  }

  // Both sides come from one token rather than one measuring the other: binding WidthRequest to Height
  // feeds the width back into what measured it, and EmojiAdorner's WordWrap closes that loop. Nothing
  // then keeps the token above the glyph it boxes, so the probe -- the same style and font size without
  // the size requests -- measures what the glyph actually needs.
  [Fact]
  public async Task A_menu_button_is_square_and_clears_its_glyph()
  {
    var layout = await CreateLayoutAsync();
    await AddMenuItemAsync(layout);
    var probe = await MainThread.InvokeOnMainThreadAsync(() =>
    {
      var button = new Button
      {
        Style = (Style)layout.Resources["MenuButton"],
        BindingContext = layout.MenuItems.Single(),
      };
      button.SetDynamicResource(Button.FontSizeProperty, "ActionButtonTextSize");
      layout.Body = button;
      return button;
    });

    var page = await MainThread.InvokeOnMainThreadAsync(() => new ContentPage { Content = layout });
    await using var window = await WindowHost.AttachAsync(page);

    var glyph = await Poll.UntilAsync(
      () => MainThread.InvokeOnMainThreadAsync(() => probe.Frame),
      frame => frame.Width > 0,
      timeoutMessage: "The probe button was never arranged.");
    var box = await MainThread.InvokeOnMainThreadAsync(() => MenuButtonFrame(layout));

    Assert.Equal(box.Height, box.Width);
    Assert.True(
      box.Width >= glyph.Width && box.Height >= glyph.Height,
      $"The glyph needs {glyph.Width}x{glyph.Height} but its button is {box.Width}x{box.Height}.");
  }

  public static TheoryData<Type> PushedPages =>
  [
    typeof(TestableDateCalendarPage),
    typeof(TestableFamilyPage),
    typeof(TestableFamilyTreePage),
    typeof(TestableGalleryPage),
    typeof(TestableKinshipFinderPage),
    typeof(TestableNamesPage),
    typeof(TestablePersonPage),
    typeof(TestableProjectListPage),
    typeof(TestableProjectPage),
    typeof(TestableStatisticsPage),
    typeof(ProjectRevisionsPage),
    typeof(SettingsPage),
  ];

  [Theory]
  [MemberData(nameof(PushedPages))]
  public async Task Every_pushed_page_offers_a_way_back(Type pageType)
  {
    var services = new TestServices();
    await MainThread.InvokeOnMainThreadAsync(TestStyles.EnsureLoaded);
    var page = await MainThread.InvokeOnMainThreadAsync(() => (ContentPage)services.Provider.GetRequiredService(pageType));

    var layout = (PageLayout)page.Content;

    Assert.Equal(HasBackButton, layout.HasBackButton);
    Assert.Equal(HasBackButton, BackButton(layout).IsVisible);
  }
}
