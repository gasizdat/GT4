using GT4.UI.Abstraction;
using GT4.UI.Items;

namespace GT4.UI.Components;

public class EnhanceCommandParameter : BindableObject
{
  public static readonly BindableProperty EnhancementProperty =
          BindableProperty.Create(nameof(Enhancement), typeof(IPhotoEnhancement), typeof(EnhanceCommandParameter));
  public static readonly BindableProperty PhotoProperty =
          BindableProperty.Create(nameof(Photo), typeof(PersonDataItem), typeof(EnhanceCommandParameter));

  public IPhotoEnhancement Enhancement
  {
    get => (IPhotoEnhancement)GetValue(EnhancementProperty);
    set => SetValue(EnhancementProperty, value);
  }

  public PersonDataItem Photo
  {
    get => (PersonDataItem)GetValue(PhotoProperty);
    set => SetValue(PhotoProperty, value);
  }
}
