using GT4.Core.Utils;
using GT4.UI.Formatters;
using GT4.UI.Resources;

namespace GT4.UI.Dialogs;

public partial class SelectDateDialog : ContentPage
{
  private const string D2 = "D2";

  private readonly IDateFormatter _DateFormatter;
  private readonly TaskCompletionSource<Date?> _Info = new(null);
  private readonly string[] _Months = GetMonths();
  private readonly string[] _Days = GetDays();
  private int _Year = 0;
  private int _Month = 1;
  private int _Day = 1;
  private bool _YearSwitch = false;
  private bool _MonthSwitch = false;
  private bool _DaySwitch = false;
  private bool _NotReady = true;

  public SelectDateDialog(Date? date, IDateFormatter dateFormatter)
  {
    _DateFormatter = dateFormatter;
    if (date.HasValue)
    {
      _Year = NormalizeYear(date.Value.Year);
      _Month = NormalizeMonth(date.Value.Month);
      _Day = NormalizeDay(_Year, _Month, date.Value.Day);

      switch (date.Value.Status)
      {
        case DateStatus.MonthUnknown:
          _YearSwitch = true;
          break;
        case DateStatus.DayUnknown:
          _YearSwitch = true;
          _MonthSwitch = true;
          break;
        case DateStatus.WellKnown:
          _YearSwitch = true;
          _MonthSwitch = true;
          _DaySwitch = true;
          break;
      }
    }

    InitializeComponent();
  }

  private static int NormalizeYear(int year) => year;

  private static int NormalizeMonth(int month) => Math.Clamp(month, 1, 12);

  private static bool IsLeapYear(int year) =>
    (year % 4) == 0 && ((year % 100) != 0 || (year % 400) == 0);

  private static int NormalizeDay(int year, int month, int day)
  {
    var daysInMonth = month switch
    {
      1 => 31,
      2 => IsLeapYear(year) ? 29 : 28,
      3 => 31,
      4 => 30,
      5 => 31,
      6 => 30,
      7 => 31,
      8 => 31,
      9 => 30,
      10 => 31,
      11 => 30,
      12 => 31,
      _ => 0
    };


    day = Math.Clamp(day, 1, daysInMonth);
    return day;
  }

  private static string[] GetMonths()
  {
    var ret = new List<string>();
    for (var i = 1; i <= 12; i++)
    {
      var monthNo = i.ToString(D2);
      var monthName = UIStrings.ResourceManager.GetString($"Month_{monthNo}", UIStrings.Culture) ?? "{0}??";
      ret.Add(string.Format(monthName, $"{monthNo} - "));
    }
    return ret.ToArray();
  }

  private static string[] GetDays()
  {
    var ret = new List<string>();
    for (var i = 1; i <= 31; i++)
    {
      ret.Add(i.ToString(D2));
    }
    return ret.ToArray();
  }

  private void Refresh()
  {
    _NotReady = false;
    OnPropertyChanged(nameof(DateString));
    OnPropertyChanged(nameof(DialogButtonName));
  }

  public string[] Months => _Months;

  public string[] Days => _Days;

  public string Year
  {
    get => _Year.ToString();
    set
    {
      if (!int.TryParse(value, out var year))
      {
        year = 0;
      }

      year = NormalizeYear(year);
      if (_Year != year)
      {
        _Year = year;
        _Day = NormalizeDay(_Year, _Month, _Day);
        YearSwitch = true;

        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(Day));
        Refresh();
      }
      else
      {
        OnPropertyChanged(nameof(Year));
      }
    }
  }

  public bool YearSwitch
  {
    get => _YearSwitch;
    set
    {
      if (_YearSwitch != value)
      {
        _YearSwitch = value;
        if (!_YearSwitch)
        {
          MonthSwitch = false;
        }
        OnPropertyChanged(nameof(YearSwitch));
        Refresh();
      }
    }
  }

  public string Month
  {
    get => _Months[_Month - 1];
    set
    {
      var month = NormalizeMonth(Array.FindIndex(_Months, month => month == value) + 1);

      if (_Month != month)
      {
        _Month = month;
        _Day = NormalizeDay(_Year, _Month, _Day);
        MonthSwitch = month > 0;
        OnPropertyChanged(nameof(Month));
        OnPropertyChanged(nameof(Day));
        Refresh();
      }
      else
      {
        OnPropertyChanged(nameof(Month));
      }
    }
  }

  public bool MonthSwitch
  {
    get => _MonthSwitch;
    set
    {
      if (_MonthSwitch != value)
      {
        _MonthSwitch = value;
        if (_MonthSwitch)
        {
          YearSwitch = true;
        }
        else
        {
          DaySwitch = false;
        }

        OnPropertyChanged(nameof(MonthSwitch));
        Refresh();
      }
    }
  }

  public string Day
  {
    get => _Days[_Day - 1];
    set
    {
      var day = NormalizeDay(_Year, _Month, Array.FindIndex(_Days, day => day == value) + 1);

      if (_Day != day)
      {
        _Day = day;
        DaySwitch = true;
        OnPropertyChanged(nameof(Day));
        Refresh();
      }
      else
      {
        OnPropertyChanged(nameof(Day));
      }
    }
  }

  public bool DaySwitch
  {
    get => _DaySwitch;
    set
    {
      if (_DaySwitch != value)
      {
        _DaySwitch = value;
        if (_DaySwitch)
        {
          MonthSwitch = true;
        }
        OnPropertyChanged(nameof(DaySwitch));
        Refresh();
      }
    }
  }

  private Date Date
  {
    get
    {
      DateStatus status;
      if (!YearSwitch)
        status = DateStatus.Unknown;
      else if (!MonthSwitch)
        status = DateStatus.MonthUnknown;
      else if (!DaySwitch)
        status = DateStatus.DayUnknown;
      else
        status = DateStatus.WellKnown;

      return Date.Create(_Year, _Month, _Day, status);
    }
  }

  public string DateString => _DateFormatter.ToString(Date);

  public string DialogButtonName => _NotReady ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Date?> Info => _Info.Task;

  public void OnSelectDateBtn(object sender, EventArgs e)
  {
    _Info.SetResult(_NotReady ? null : Date);
  }
}