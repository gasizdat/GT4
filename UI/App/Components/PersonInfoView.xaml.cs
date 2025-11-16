using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;
using System.Text;

namespace GT4.UI.Components;

public partial class PersonInfoView : ContentView
{
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;

  protected PersonInfoView(IServiceProvider serviceProvider)
  {
    _DateSpanFormatter = serviceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    InitializeComponent();
  }

  public PersonInfoView()
    : this(ServiceBuilder.DefaultServices)
  {

  }

  public static readonly BindableProperty PersonProperty =
    BindableProperty.Create(nameof(Person), typeof(PersonInfo), typeof(PersonInfoView), default, BindingMode.OneWay, null, OnPersonChanged);

  public static readonly BindableProperty ShowPhotoProperty =
    BindableProperty.Create(nameof(ShowPhoto), typeof(bool), typeof(PersonInfoView), true);

  public static readonly BindableProperty ShowDatesProperty =
    BindableProperty.Create(nameof(ShowDates), typeof(bool), typeof(PersonInfoView), true);

  public static readonly BindableProperty ShowDeathDateProperty =
    BindableProperty.Create(nameof(ShowDeathDate), typeof(bool), typeof(PersonInfoView), true);

  public static readonly BindableProperty ShowAgeProperty =
    BindableProperty.Create(nameof(ShowAge), typeof(bool), typeof(PersonInfoView), true);

  public static readonly BindableProperty NameLabelStyleProperty =
    BindableProperty.Create(nameof(NameLabelStyle), typeof(Style), typeof(PersonInfoView), null);

  public static readonly BindableProperty DatesLabelStyleProperty =
    BindableProperty.Create(nameof(DatesLabelStyle), typeof(Style), typeof(PersonInfoView), null);

  public static readonly BindableProperty PhotoStyleProperty =
    BindableProperty.Create(nameof(PhotoStyle), typeof(Style), typeof(PersonInfoView), null);

  public PersonInfo? Person => (PersonInfo?)GetValue(PersonProperty);
  public bool ShowPhoto => (bool)GetValue(ShowPhotoProperty);
  public bool ShowDates => (bool)GetValue(ShowDatesProperty);
  public bool ShowDeathDate => (bool)GetValue(ShowDeathDateProperty);
  public bool ShowAge => (bool)GetValue(ShowAgeProperty);
  public Style? NameLabelStyle => (Style?)GetValue(NameLabelStyleProperty);
  public Style? DatesLabelStyle => (Style?)GetValue(DatesLabelStyleProperty);
  public Style? PhotoStyle => (Style?)GetValue(PhotoStyleProperty);

  public string? CommonName => Person is null ? null : _NameFormatter.GetCommonPersonName(Person);
  public string? LifeDates
  {
    get
    {
      if (Person is null)
      {
        return null;
      }

      var ret = new StringBuilder(_DateFormatter.ToString(Person.BirthDate));

      if (ShowDeathDate && Person.DeathDate.HasValue)
      {
        ret.Append(" - ");
        ret.Append(_DateFormatter.ToString(Person.DeathDate));
      }

      if (ShowAge)
      {
        ret.Append(" (");
        ret.Append(_DateSpanFormatter.ToString((Person.DeathDate.HasValue ? Person.DeathDate : Date.Now) - Person.BirthDate));
        ret.Append(")");
      }

      return ret.ToString();
    }
  }
  public ImageSource Photo => Person?.MainPhoto is null ? GetDefaultImage() : ImageUtils.ImageFromBytes(Person.MainPhoto.Content);

  private static void OnPersonChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is PersonInfoView view && oldValue != newValue)
    {
      view.OnPropertyChanged(nameof(CommonName));
      view.OnPropertyChanged(nameof(LifeDates));
      view.OnPropertyChanged(nameof(Photo));
    }
  }

  private ImageSource GetDefaultImage()
  {
    return Person?.BiologicalSex switch
    {
      BiologicalSex.Male => ImageUtils.ImageFromRawResource("male_stub.png"),
      BiologicalSex.Female => ImageUtils.ImageFromRawResource("female_stub.png"),
      _ => ImageUtils.ImageFromRawResource("project_icon.png")
    };
  }
}