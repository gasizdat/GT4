using GT4.Core.Project.Abstraction;
using GT4.Core.Project.Dto;
using GT4.Core.Utils;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Converters;
using GT4.UI.Utils.Formatters;

namespace GT4.UI.Components;

public partial class PersonInfoView : ContentView
{
  private readonly ICancellationTokenProvider _CancellationTokenProvider;
  private readonly IAlertService _AlertService;
  private readonly IDateSpanFormatter _DateSpanFormatter;
  private readonly IDateFormatter _DateFormatter;
  private readonly INameFormatter _NameFormatter;
  private readonly OptionalDataConverterResolver _DataConverterResolver;
  private readonly DefaultImageCache _DefaultImageCache;
  private readonly ICurrentProjectProvider _CurrentProjectProvider;
  private ImageSource? _PhotoSource;
  private bool _PhotoReady;
  private int _PhotoGeneration;

  protected PersonInfoView(IServiceProvider serviceProvider)
  {
    _CancellationTokenProvider = serviceProvider.GetRequiredService<ICancellationTokenProvider>();
    _AlertService = serviceProvider.GetRequiredService<IAlertService>();
    _DateSpanFormatter = serviceProvider.GetRequiredService<IDateSpanFormatter>();
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
    _NameFormatter = serviceProvider.GetRequiredService<INameFormatter>();
    _DataConverterResolver = serviceProvider.GetRequiredService<OptionalDataConverterResolver>();
    _DefaultImageCache = serviceProvider.GetRequiredService<DefaultImageCache>();
    _CurrentProjectProvider = serviceProvider.GetRequiredService<ICurrentProjectProvider>();
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

  public string? CommonName
  {
    get
    {
      if (Person is null)
      {
        return null;
      }

      var name = _NameFormatter.ToString(Person, NameFormat);
#if DEBUG
      // Diagnostic-only: makes it possible to cross-reference a person/relative shown on screen
      // against Ids reported by tools like GT4.Tools.RelativesCli. Never shipped in Release.
      name += $" (Id: {Person.Id})";
#endif
      return name;
    }
  }
  public string? LifeDates
  {
    get
    {
      if (Person is null)
      {
        return null;
      }

      var personDates = string.Empty;
      var isDeathDateDisplayed = ShowDeathDate && Person.DeathDate.HasValue;

      if (Person.BirthDate.Status != DateStatus.Unknown || !isDeathDateDisplayed)
      {
        personDates = _DateFormatter.ToString(Person.BirthDate);
      }

      if (isDeathDateDisplayed)
      {
        var deathDate = Person.DeathDate!.Value.Status == DateStatus.Unknown
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
  public ImageSource Photo
  {
    get
    {
      if (Person?.MainPhoto is not { } mainPhoto)
      {
        return GetDefaultImage();
      }

      if (!_PhotoReady)
      {
        _PhotoReady = true;
        var generation = _PhotoGeneration;

        async Task UpdatePhotoAsync()
        {
          using var token = _CancellationTokenProvider.CreateShortOperationCancellationToken();

          // Empty Content means the page that loaded this Person fetched only the photo's Id (e.g.
          // ProjectPage, to avoid decoding every person's BLOB up front) -- fetch the actual bytes now
          // that this instance is realized and about to display them.
          var photoData = mainPhoto.Content.Length == 0
            ? await _CurrentProjectProvider.Project.Data.TryGetDataByIdAsync(mainPhoto.Id, token) ?? mainPhoto
            : mainPhoto;
          var photo = await ImageUtils.ResolvePhotoAsync(_DataConverterResolver, photoData, GetDefaultImage(), token);

          MainThread.BeginInvokeOnMainThread(() =>
          {
            // Handler is null if the view was disconnected (e.g. removed during a CollectionView
            // recycle); generation catches a view that was instead reused for a different Person
            // while this was in flight -- SafeBindableLayout.Rebuild rebinds BindingContext without
            // disconnecting.
            if (Handler is null || generation != _PhotoGeneration)
            {
              return;
            }

            _PhotoSource = photo.Source;
            OnPropertyChanged(nameof(Photo));
          });
        }

        SafeTask.Run(UpdatePhotoAsync, _AlertService);
      }

      return _PhotoSource ?? GetDefaultImage();
    }
  }

  private static void OnPersonChanged(BindableObject obj, object oldValue, object newValue)
  {
    if (obj is PersonInfoView view && oldValue != newValue)
    {
      view._PhotoGeneration++;
      view._PhotoReady = false;
      view._PhotoSource = null;
      view.OnPropertyChanged(nameof(CommonName));
      view.OnPropertyChanged(nameof(LifeDates));
      view.OnPropertyChanged(nameof(Photo));
    }
  }

  private ImageSource GetDefaultImage() =>
    _DefaultImageCache.Get(ImageUtils.DefaultPhotoResourceName(Person?.BiologicalSex ?? BiologicalSex.Unknown));
}
