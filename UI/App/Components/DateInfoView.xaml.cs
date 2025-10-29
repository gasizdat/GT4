using GT4.Core.Utils;

namespace GT4.UI.App.Components;

public partial class DateInfoView : ContentView
{
  private readonly IDateFormatter _Formatter = ServiceBuilder.DefaultServices.GetRequiredService<IDateFormatter>();

  public DateInfoView()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty CaptionProperty =
    BindableProperty.Create(nameof(Caption), typeof(string), typeof(DateInfoView), default, BindingMode.OneWay);

  public static readonly BindableProperty DateProperty =
    BindableProperty.Create(nameof(Date), typeof(Date), typeof(DateInfoView), default, BindingMode.OneWay, null, OnDateChanged);

  public Date Date => (Date)GetValue(DateProperty);

  public string Caption => (string)GetValue(CaptionProperty);

  public string DateText => _Formatter.ToString(Date);

  private static void OnDateChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is DateInfoView view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(DateText));
    }
  }
}