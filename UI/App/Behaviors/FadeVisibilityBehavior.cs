namespace GT4.UI.Behaviors;

// Fades the attached element's Opacity between 0 and 1 as IsVisible changes, instead of the
// instant show/hide a raw IsVisible binding would give.
public sealed class FadeVisibilityBehavior : Behavior<VisualElement>
{
  public static readonly BindableProperty IsVisibleProperty =
      BindableProperty.Create(
          nameof(IsVisible),
          typeof(bool),
          typeof(FadeVisibilityBehavior),
          false,
          propertyChanged: OnIsVisibleChanged);

  public bool IsVisible
  {
    get => (bool)GetValue(IsVisibleProperty);
    set => SetValue(IsVisibleProperty, value);
  }

  public static readonly BindableProperty DurationProperty =
      BindableProperty.Create(nameof(Duration), typeof(uint), typeof(FadeVisibilityBehavior), (uint)200);

  public uint Duration
  {
    get => (uint)GetValue(DurationProperty);
    set => SetValue(DurationProperty, value);
  }

  private VisualElement? _Associated;

  protected override void OnAttachedTo(VisualElement bindable)
  {
    base.OnAttachedTo(bindable);

    _Associated = bindable;

    // Sync the resting state immediately: a propertyChanged callback only fires on a change, so an
    // initial value equal to the bindable property's default (e.g. both false) would otherwise never
    // touch the element at all.
    bindable.IsVisible = IsVisible;
    bindable.Opacity = IsVisible ? 1 : 0;
  }

  protected override void OnDetachingFrom(VisualElement bindable)
  {
    _Associated = null;

    base.OnDetachingFrom(bindable);
  }

  private static async void OnIsVisibleChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var behavior = (FadeVisibilityBehavior)bindable;
    if (behavior._Associated is not { } element)
    {
      return;
    }

    if ((bool)newValue)
    {
      element.Opacity = 0;
      element.IsVisible = true;
      await element.FadeToAsync(1, behavior.Duration);
    }
    else
    {
      await element.FadeToAsync(0, behavior.Duration);
      element.IsVisible = false;
    }
  }
}
