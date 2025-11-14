using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Formatters;
using System.Text;

namespace GT4.UI.Components;

public partial class PersonInfoView : ContentView
{
  private readonly IDateSpanFormatter _DateSpanFormatter = ServiceBuilder.DefaultServices.GetRequiredService<IDateSpanFormatter>();
  private readonly IDateFormatter _DateFormatter = ServiceBuilder.DefaultServices.GetRequiredService<IDateFormatter>();
  private readonly INameFormatter _NameFormatter = ServiceBuilder.DefaultServices.GetRequiredService<INameFormatter>();

  public PersonInfoView()
  {
    InitializeComponent();
  }

  public static readonly BindableProperty PersonProperty =
    BindableProperty.Create(nameof(PersonInfo), typeof(PersonInfo), typeof(PersonInfoView), default, BindingMode.OneWay, null, OnPersonChanged);

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

  public PersonInfo? PersonInfo => (PersonInfo?)GetValue(PersonProperty);
  public bool ShowPhoto => (bool)GetValue(ShowPhotoProperty);
  public bool ShowDates => (bool)GetValue(ShowDatesProperty);
  public bool ShowDeathDate => (bool)GetValue(ShowDeathDateProperty);
  public bool ShowAge => (bool)GetValue(ShowAgeProperty);
  public Style? NameLabelStyle => (Style?)GetValue(NameLabelStyleProperty);
  public Style? DatesLabelStyle => (Style?)GetValue(DatesLabelStyleProperty);
  public Style? PhotoStyle => (Style?)GetValue(PhotoStyleProperty);

  public string? CommonName => PersonInfo is null ? null : _NameFormatter.GetCommonPersonName(PersonInfo);
  public string? LifeDates
  {
    get
    {
      if (PersonInfo is null)
      {
        return null;
      }

      var ret = new StringBuilder(_DateFormatter.ToString(PersonInfo.BirthDate));

      if (ShowDeathDate && PersonInfo.DeathDate.HasValue)
      {
        ret.Append(" - ");
        ret.Append(_DateFormatter.ToString(PersonInfo.DeathDate));
      }

      if (ShowAge)
      {
        ret.Append(" (");
        ret.Append(_DateSpanFormatter.ToString((PersonInfo.DeathDate.HasValue ? PersonInfo.DeathDate : Date.Now) - PersonInfo.BirthDate));
        ret.Append(")");
      }

      return ret.ToString();
    }
  }
  public ImageSource Photo => PersonInfo?.MainPhoto is null ? GetDefaultImage() : ImageUtils.ImageFromBytes(PersonInfo.MainPhoto.Content);

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
    return PersonInfo?.BiologicalSex switch
    {
      BiologicalSex.Male => ImageUtils.ImageFromRawResource("male_stub.png"),
      BiologicalSex.Female => ImageUtils.ImageFromRawResource("female_stub.png"),
      _ => ImageUtils.ImageFromRawResource("project_icon.png")
    };
  }
}