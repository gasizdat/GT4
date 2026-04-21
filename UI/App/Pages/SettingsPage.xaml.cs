using GT4.Core.Utils;
using GT4.UI.Utils.Formatters;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Pages;

public partial class SettingsPage : ContentPage
{
  private readonly IInteractiveConfiguration? _AppConfig;
  private readonly IConfiguration _Configuration;
  private readonly IDateFormatter _DateFormatter;

  protected SettingsPage(IServiceProvider services)
  {
    _AppConfig = services.GetKeyedService<IInteractiveConfiguration>(WellKnownActiveConfigurations.AppConfig);
    _Configuration = services.GetRequiredService<IConfiguration>();
    _DateFormatter = services.GetRequiredService<IDateFormatter>();

    InitializeComponent();
  }

  public SettingsPage()
    : this(ServiceBuilder.DefaultServices)
  {

  }

  public string DateExample
  {
    get => _DateFormatter.ToString(Date.Now);
  }

  public string DateFormat
  {
    get => DateFormatter.GetFullDateFormat(_Configuration);
    set
    {
      if (_AppConfig == null)
      {
        return;
      }

      DateFormatter.SetFullDateFormat(_AppConfig, value);
      OnPropertyChanged(nameof(DateFormat));
      OnPropertyChanged(nameof(DateExample));
    }
  }

  public string ShortDateExample
  {
    get => _DateFormatter.ToString(Date.Now with { Status = DateStatus.DayUnknown });
  }

  public string ShortDateFormat
  {
    get => DateFormatter.GetShortDateFormat(_Configuration);
    set
    {
      if (_AppConfig == null)
      {
        return;
      }

      DateFormatter.SetShortDateFormat(_AppConfig, value);
      OnPropertyChanged(nameof(ShortDateFormat));
      OnPropertyChanged(nameof(ShortDateExample));
    }
  }
}