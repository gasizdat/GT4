using GT4.Core.Utils;
using GT4.UI.Resources;
using Microsoft.Extensions.Configuration;

namespace GT4.UI.Utils.Formatters;

public class DateFormatter : IDateFormatter
{
  class FullDateFormatSetting : ISettingEditor
  {
    private const string FullDateFormatSection = "DateFormatter.FullDateFormat";
    private const string DefaultFullDateFormat = "DD MM YYYY";
    private readonly DateFormatter _DateFormatter;
    private readonly IConfiguration _Configuration;
    private readonly IInteractiveConfiguration? _InteractiveConfiguration;

    public FullDateFormatSetting(
      DateFormatter dateFormatter,
      IConfiguration configuration, 
      IInteractiveConfiguration? interactiveConfiguration)
    {
      _DateFormatter = dateFormatter;
      _Configuration = configuration;
      _InteractiveConfiguration = interactiveConfiguration;
    }

    public string Group => nameof(DateFormatter);

    public string DisplayName => UIStrings.FieldDateDisplayFormat;

    public string Description => UIStrings.FieldDateDisplayFormatHint;

    public string Example => _DateFormatter.ToString(Date.Now);

    public string Value
    {
      get => _Configuration[FullDateFormatSection] ?? DefaultFullDateFormat;
      set => _InteractiveConfiguration?.SetKey(FullDateFormatSection, value);
    }
  }

  class ShortDateFormatSetting : ISettingEditor
  {
    private const string ShortDateFormatSection = "DateFormatter.ShortDateFormat";
    private const string DefaultShortDateFormat = "MM YYYY";
    private readonly DateFormatter _DateFormatter;
    private readonly IConfiguration _Configuration;
    private readonly IInteractiveConfiguration? _InteractiveConfiguration;

    public ShortDateFormatSetting(
      DateFormatter dateFormatter,
      IConfiguration configuration,
      IInteractiveConfiguration? interactiveConfiguration)
    {
      _DateFormatter = dateFormatter;
      _Configuration = configuration;
      _InteractiveConfiguration = interactiveConfiguration;
    }

    public string Group => nameof(DateFormatter);

    public string DisplayName => UIStrings.FieldShortDateDisplayFormat;

    public string Description => UIStrings.FieldShortDateDisplayFormatHint;

    public string Example => _DateFormatter.ToString(Date.Now with { Status = DateStatus.DayUnknown });

    public string Value
    {
      get => _Configuration[ShortDateFormatSection] ?? DefaultShortDateFormat;
      set => _InteractiveConfiguration?.SetKey(ShortDateFormatSection, value);
    }
  }
  
  private const string D4 = "D4";
  private const string D2 = "D2";
  private readonly FullDateFormatSetting _FullDateFormatSetting;
  private readonly ShortDateFormatSetting _ShortDateFormatSetting;

  public DateFormatter(
    IConfiguration configuration, 
    ISettingEditorsHolder? settingEditorsHolder = null,
    [FromKeyedServices(WellKnownActiveConfigurations.AppConfig)] 
    IInteractiveConfiguration? interactiveConfiguration = null)
  {
    _FullDateFormatSetting = new FullDateFormatSetting(this, configuration, interactiveConfiguration);
    _ShortDateFormatSetting = new ShortDateFormatSetting(this, configuration, interactiveConfiguration);
    settingEditorsHolder?.AddSetting(_FullDateFormatSetting);
    settingEditorsHolder?.AddSetting(_ShortDateFormatSetting);
  }

  public string ToString(Date? date)
  {
    // TODO Apply Date.Sign

    if (date.HasValue)
    {
      var year = YearToString(date.Value);
      var month = MonthToString(date.Value);
      var monthNumber = MonthToNumber(date.Value);
      var day = DayToString(date.Value);
      return date.Value.Status switch
      {
        DateStatus.WellKnown => ToString(_FullDateFormatSetting.Value, year, month, monthNumber, day),
        DateStatus.DayUnknown => ToString(_ShortDateFormatSetting.Value, year, month, monthNumber, day),
        DateStatus.MonthUnknown => year,
        DateStatus.YearApproximate => string.Format(UIStrings.DateStatusYearApproximate_1, year),
        DateStatus.Unknown => UIStrings.DateStatusUnknown,
        _ => $"⚠ Unexpected DateStatus={date.Value.Status}"
      };
    }
    else
    {
      return UIStrings.DateStatusNotDefined;
    }
  }

  protected static string YearToString(Date date)
  {
    var ret = date.Year.ToString(D4);

    return ret;
  }
  protected string MonthToNumber(Date date)
  {
    string ret;
    var month = date.Month;
    ret = month.ToString(D2);

    return ret;
  }

  protected string MonthToString(Date date)
  {
    string ret;
    var month = date.Month;
    if (Language.Current == Language.RU)
    {
      ret = month switch
      {
        1 => UIStrings.Month_01,
        2 => UIStrings.Month_02,
        3 => UIStrings.Month_03,
        4 => UIStrings.Month_04,
        5 => UIStrings.Month_05,
        6 => UIStrings.Month_06,
        7 => UIStrings.Month_07,
        8 => UIStrings.Month_08,
        9 => UIStrings.Month_09,
        10 => UIStrings.Month_10,
        11 => UIStrings.Month_11,
        12 => UIStrings.Month_12,
        _ => month.ToString(D2)
      };

      ret = ret.ToLower();

      if (date.Status == DateStatus.WellKnown)
      {
        ret = MonthGenitiveRU(ret);
      }
    }
    else
    {
      ret = month switch
      {
        1 => UIStrings.Month_01,
        2 => UIStrings.Month_02,
        3 => UIStrings.Month_03,
        4 => UIStrings.Month_04,
        5 => UIStrings.Month_05,
        6 => UIStrings.Month_06,
        7 => UIStrings.Month_07,
        8 => UIStrings.Month_08,
        9 => UIStrings.Month_09,
        10 => UIStrings.Month_10,
        11 => UIStrings.Month_11,
        12 => UIStrings.Month_12,
        _ => month.ToString(D2)
      };
    }
    return ret;
  }

  protected static string MonthGenitiveRU(string month)
  {
    var ret = month.Last() switch
    {
      'ь' or 'й' => month.Substring(0, month.Length - 1) + "я",
      _ => month + "a"
    };

    return ret;
  }

  protected static string DayToString(Date date)
  {
    var ret = date.Day.ToString(D2);

    return ret;
  }

  protected static string ToString(string format, string year, string month, string monthNumber, string day)
  {
    var ret = TemplateInterpolator.Format(format, new Dictionary<string, string>()
    {
      { "YYYY", year},
      { "MM", monthNumber},
      { "MMM", month},
      { "DD", day},
    });

    return ret;
  }
}