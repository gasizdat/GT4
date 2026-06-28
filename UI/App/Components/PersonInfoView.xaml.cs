using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public partial class PersonInfoView : ContentView
{
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly IThumbnailCache _ThumbnailCache;

  protected PersonInfoView(IServiceProvider serviceProvider)
  {
    _DateSpanFormatter = serviceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    _ThumbnailCache = serviceProvider.GetRequiredService<IThumbnailCache>();
    InitializeComponent();
  }

  public PersonInfoView()
    : this(GT4Services.Provider)
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

  public static readonly BindableProperty NameFormatProperty =
    BindableProperty.Create(nameof(NameFormat), typeof(NameFormat), typeof(PersonInfoView), NameFormat.ShortPersonName);

  public PersonInfo? Person => (PersonInfo?)GetValue(PersonProperty);
  public bool ShowPhoto => (bool)GetValue(ShowPhotoProperty);
  public bool ShowDates => (bool)GetValue(ShowDatesProperty);
  public bool ShowDeathDate => (bool)GetValue(ShowDeathDateProperty);
  public bool ShowAge => (bool)GetValue(ShowAgeProperty);
  public Style? NameLabelStyle => (Style?)GetValue(NameLabelStyleProperty);
  public Style? DatesLabelStyle => (Style?)GetValue(DatesLabelStyleProperty);
  public Style? PhotoStyle => (Style?)GetValue(PhotoStyleProperty);
  public NameFormat NameFormat => (NameFormat)GetValue(NameFormatProperty);

  public string? CommonName => Person is null ? null : _NameFormatter.ToString(Person, NameFormat);
  public string? LifeDates
  {
    get
    {
      if (Person is null)
      {
        return null;
      }

      string personDates = string.Empty;
      var isDeathDateDisplayed = ShowDeathDate && Person.DeathDate.HasValue;

      if (Person.BirthDate.Status != DateStatus.Unknown || !isDeathDateDisplayed)
      {
        personDates = _DateFormatter.ToString(Person.BirthDate);
      }

      if (isDeathDateDisplayed)
      {
        string deathDate = Person.DeathDate!.Value.Status == DateStatus.Unknown
                           ? string.Empty
                           : _DateFormatter.ToString(Person.DeathDate);
        deathDate = string.Format(UIStrings.PersonDeathMark_1, deathDate);

        if (personDates == string.Empty)
        {
          personDates = deathDate;
        }
        else
        {
          personDates = string.Format(UIStrings.PersonDates_2, personDates, deathDate);
        }
      }

      if (ShowAge)
      {
        var timeSpan = (Person.DeathDate.HasValue ? Person.DeathDate : Date.Now) - Person.BirthDate;
        if (timeSpan.HasValue && timeSpan.Value.Status != DateStatus.Unknown)
        {
          personDates = string.Format(UIStrings.PersonAge_2, personDates, _DateSpanFormatter.ToString(timeSpan));
        }
      }

      return personDates;
    }
  }
  public ImageSource Photo => _ThumbnailCache.Resolve(Person?.MainPhoto) ?? GetDefaultImage();

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