using GT4.UI.Dialogs;
using GT4.UI.Utils;
using System.Windows.Input;

namespace GT4.UI.Components;

public partial class ImagePresenter : ContentView
{
  private const int _RefreshFPS = 30;
  private const double _MinOpacity = 0.0;
  private const double _MaxOpacity = 1.0;
  private const int _ActiveImages = 2;
  private static IDispatcherTimer _Timer;
  private static readonly TimeSpan _DefaultImageShowTime = TimeSpan.FromSeconds(5);
  private static readonly TimeSpan _DefaultImageFadeTime = TimeSpan.FromSeconds(2.5);
  private static readonly string[] _ImageProperties = [nameof(Image1), nameof(Image2)];
  private static readonly string[] _ImageOpacityProperties = [nameof(ImageOpacity1), nameof(ImageOpacity2)];
  private readonly ImageSource[] _Images;
  private readonly double[] _ImageOpacities;
  private readonly IPageAlertService _PageAlertService;
  private readonly ICommand _Command;
  private readonly ICommand _OpenViewerCommand;
  private uint _CurrentIndex = 0;
  private State _CurrentState = State.Init;
  private int _LastStageTime = 0;

  enum State
  {
    Init,
    ShowImage,
    StartFading,
    StopFading,
    Freeze
  }

  private void Update()
  {
    if (!IsVisible || !IsLoaded)
    {
      return;
    }

    switch (_CurrentState)
    {
      case State.Init:
        Init();
        break;

      case State.ShowImage:
        ShowImage();
        break;

      case State.StartFading:
        StartFading();
        break;

      case State.StopFading:
        StopFading();
        break;

      case State.Freeze:
        break;

      default:
        _CurrentState = default;
        break;
    }
  }

  private void Init()
  {
    for (var i = 0; i < _ActiveImages; i++)
    {
      _ImageOpacities[i] = _MinOpacity;

      if (i < ImageSources.Length)
      {
        _Images[i] = ImageUtils.ImageFromBytes(ImageSources[i]);
        _ImageOpacities[0] = _MaxOpacity;
      }
      else
      {
        _Images[i] = ImageUtils.ImageFromBytes([]);
      }

      OnPropertyChanged(_ImageProperties[i]);
      OnPropertyChanged(_ImageOpacityProperties[i]);
    }

    _CurrentIndex = 0;
    if (ImageSources.Length > 1)
    {
      UpdateStageTime();
      _CurrentState = State.ShowImage;
    }
    else
    {
      _CurrentState = State.Freeze;
    }
  }

  private void ShowImage()
  {
    if (CurrentStateTime > ImageShowTime)
    {
      UpdateStageTime();
      _CurrentState = State.StartFading;
    }
  }

  private void StartFading()
  {
    if (CurrentStateTime >= ImageFadeTime)
    {
      UpdateStageTime();
      _CurrentState = State.StopFading;
      return;
    }

    var activeIndex = _CurrentIndex % _ActiveImages;
    var currentTime = CurrentStateTime;
    var fadeOpacity = Math.Clamp(currentTime / ImageFadeTime, _MinOpacity, _MaxOpacity);

    for (var i = 0; i < _ActiveImages; i++)
    {
      if (i == activeIndex)
      {
        _ImageOpacities[i] = _MaxOpacity - fadeOpacity;
      }
      else
      {
        _ImageOpacities[i] = fadeOpacity;
      }

      OnPropertyChanged(_ImageOpacityProperties[i]);
    }
  }

  private void StopFading()
  {
    _CurrentIndex++;
    Finish();
  }

  private void Finish()
  {
    var activeIndex = _CurrentIndex % _ActiveImages;
    for (var i = 0; i < _ActiveImages; i++)
    {
      if (i == activeIndex)
      {
        _ImageOpacities[i] = _MaxOpacity;
      }
      else
      {
        var sourceIndex = (_CurrentIndex + 1) % ImageSources.Length;
        _ImageOpacities[i] = _MinOpacity;
        _Images[i] = ImageUtils.ImageFromBytes(ImageSources[sourceIndex]);

        OnPropertyChanged(_ImageProperties[i]);
      }

      OnPropertyChanged(_ImageOpacityProperties[i]);
    }

    UpdateStageTime();
    _CurrentState = State.ShowImage;
  }

  private void UpdateStageTime() => _LastStageTime = Environment.TickCount;

  private TimeSpan CurrentStateTime => TimeSpan.FromMilliseconds(Environment.TickCount - _LastStageTime);

  private static void OnBindablePropertyChanged(BindableObject bindableObject, object oldValue, object newValue)
  {
    if (bindableObject is ImagePresenter view && oldValue != newValue)
    {
      view._CurrentIndex = 0;
      view._CurrentState = State.Init;
      view.OnPropertyChanged(nameof(ShowPrevNext));
    }
  }

  static ImagePresenter()
  {
    const double refreshInterval = 1.0 / _RefreshFPS;
    _Timer = Shell.Current.Dispatcher.CreateTimer();
    _Timer.Interval = TimeSpan.FromSeconds(refreshInterval);
    _Timer.IsRepeating = true;
    _Timer.Start();
  }

  protected ImagePresenter(IServiceProvider serviceProvider)
  {
    _PageAlertService = serviceProvider.GetRequiredService<IPageAlertService>();

    List<double> opacities = new();
    List<ImageSource> images = new();
    for (var i = 0; i < _ActiveImages; i++)
    {
      opacities.Add(_MaxOpacity);
      images.Add(ImageUtils.ImageFromBytes([]));
    }

    _ImageOpacities = [.. opacities];
    _Images = [.. images];
    Loaded += (_, _) =>
    {
      Init();
      _Timer.Tick += TimerTick;
    };
    Unloaded += (_, _) => _Timer.Tick -= TimerTick;
    _Command = new SafeCommand(OnNextPicture, _PageAlertService);
    _OpenViewerCommand = new SafeCommand(OnOpenViewerAsync, _PageAlertService);

    InitializeComponent();
  }

  public ImagePresenter()
    : this(GT4Services.Provider)
  {

  }

  private void TimerTick(object? sender, EventArgs e) => Update();

  private async Task OnOpenViewerAsync()
  {
    if (ImageSources.Length == 0)
    {
      return;
    }

    await Shell.Current.Navigation.PushModalAsync(new PhotoViewerDialog(ImageSources, _PageAlertService));
  }

  private void OnNextPicture(object obj)
  {
    switch (obj)
    {
      case string commandName when commandName == "PrevPicture":
        _CurrentIndex--;
        Finish();
        break;
      case string commandName when commandName == "NextPicture":
        _CurrentIndex++;
        Finish();
        break;
    }
  }

  public static readonly BindableProperty ImageShowTimeProperty = BindableProperty.Create(
      nameof(ImageShowTime),
      typeof(TimeSpan),
      typeof(ImagePresenter),
      _DefaultImageShowTime,
      BindingMode.OneWay,
      null,
      OnBindablePropertyChanged);

  public static readonly BindableProperty ImageFadeTimeProperty = BindableProperty.Create(
      nameof(ImageFadeTime),
      typeof(TimeSpan),
      typeof(ImagePresenter),
      _DefaultImageFadeTime,
      BindingMode.OneWay,
      null,
      OnBindablePropertyChanged);

  public static readonly BindableProperty ImageStyleProperty = BindableProperty.Create(
      nameof(ImageStyle),
      typeof(Style),
      typeof(ImagePresenter));

  public static readonly BindableProperty ImageSourcesProperty = BindableProperty.Create(
    nameof(ImageSources),
    typeof(byte[][]),
    typeof(ImagePresenter),
    Array.Empty<byte[]>(),
    BindingMode.OneWay,
    null,
    OnBindablePropertyChanged);

  public Style? ImageStyle
  {
    get => (Style?)GetValue(ImageStyleProperty);
    set => SetValue(ImageStyleProperty, value);
  }

  public byte[][] ImageSources
  {
    get => (byte[][]?)GetValue(ImageSourcesProperty) ?? [];
    set => SetValue(ImageSourcesProperty, value);
  }

  public TimeSpan ImageShowTime
  {
    get => (TimeSpan?)GetValue(ImageShowTimeProperty) ?? _DefaultImageShowTime;
    set => SetValue(ImageShowTimeProperty, value);
  }

  public TimeSpan ImageFadeTime
  {
    get => (TimeSpan?)GetValue(ImageFadeTimeProperty) ?? _DefaultImageFadeTime;
    set => SetValue(ImageFadeTimeProperty, value);
  }

  public bool ShowPrevNext
  {
    get => ImageSources.Length > 1;
  }

  public ImageSource Image1 => _Images[0];

  public ImageSource Image2 => _Images[1];

  public double ImageOpacity1 => _ImageOpacities[0];

  public double ImageOpacity2 => _ImageOpacities[1];

  public ICommand Command => _Command;

  public ICommand OpenViewerCommand => _OpenViewerCommand;
}