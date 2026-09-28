using GT4.UI.Components;
using GT4.UI.Resources;

namespace GT4.UI.Behaviors;

// A context flyout, so only desktop (right-click) gets the gesture; the copy itself is cross-platform.
public static class CopyText
{
  public static readonly BindableProperty IsEnabledProperty =
      BindableProperty.CreateAttached(
          "IsEnabled",
          typeof(bool),
          typeof(CopyText),
          false,
          propertyChanged: OnIsEnabledChanged);

  public static bool GetIsEnabled(BindableObject view) => (bool)view.GetValue(IsEnabledProperty);
  public static void SetIsEnabled(BindableObject view, bool value) => view.SetValue(IsEnabledProperty, value);

  private static void OnIsEnabledChanged(BindableObject bindable, object oldValue, object newValue)
  {
    if (newValue is not true)
      return;

    var view = (View)bindable;
    var item = new MenuFlyoutItem { Text = UIStrings.MenuItemNameCopy };
    item.Clicked += (_, _) => Copy(view);
    FlyoutBase.SetContextFlyout(view, new MenuFlyout { item });
  }

  // The text is read on use, so it is always what the view shows at that moment.
  private static void Copy(View view)
  {
    var text = view is MarkdownView markdown ? markdown.PlainText : ((Label)view).Text;
    var layout = FindLayout(view);
    layout.CopyCommand.Execute(text);
  }

  // Looked up on use rather than on attach: XAML sets attached properties before the view is parented.
  private static PageLayout FindLayout(Element element) =>
    element as PageLayout ?? FindLayout(element.Parent!);
}
