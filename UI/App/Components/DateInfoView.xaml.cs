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
    BindableProperty.Create(nameof(Caption), typeof(string), typeof(DateInfoView), default(Nullable));

  public static readonly BindableProperty DateProperty =
    BindableProperty.Create(nameof(Date), typeof(Date), typeof(DateInfoView), default(Nullable));

  public static readonly BindableProperty VisibleIfNoDateProperty =
    BindableProperty.Create(nameof(VisibleIfNoDate), typeof(bool), typeof(DateInfoView), default(Boolean));

  public Date? Date
  {
    get
    {
      return (Date?)GetValue(DateProperty);
    }
    set
    {
      SetValue(DateProperty, value);
      OnPropertyChanged(nameof(DateText));
    }
  }

  public string? Caption
  {
    get
    {
      return (string?)GetValue(CaptionProperty);
    }
    set
    {
      SetValue(CaptionProperty, value);
      OnPropertyChanged(nameof(Caption));
    }
  }

  public string DateText =>_Formatter.ToString(Date);

  public bool VisibleIfNoDate
  {
    get => (bool)GetValue(VisibleIfNoDateProperty);
    set
    {
      SetValue(VisibleIfNoDateProperty, value);
      OnPropertyChanged(nameof(VisibleIfNoDate));
    }
  }
}