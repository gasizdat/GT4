using GT4.Core.Utils;
using GT4.Core.Utils.Calendars;
using GT4.UI.Abstraction;
using GT4.UI.Resources;
using GT4.UI.Utils;
using GT4.UI.Utils.Formatters;
using System.Windows.Input;

namespace GT4.UI.Dialogs;

public partial class SelectDateDialog : ContentPage
{
  private const string D2 = "D2";

  private readonly IDateFormatter _DateFormatter;
  private readonly ICommand _DialogCommand;
  private readonly ICommand _CancelCommand;
  private readonly TaskCompletionSource<Date?> _Info = new(null);
  private readonly string[] _Months = GetMonths();
  private readonly string[] _Days = GetDays();
  private int _Year = 0;
  private int _Month = 1;
  private int _Day = 1;
  private bool _ApproximateSwitch = false;
  private bool _YearSwitch = false;
  private bool _MonthSwitch = false;
  private bool _DaySwitch = false;
  private bool _OtherCalendarSwitch = false;
  private DisplayCalendar _OtherCalendar;
  private Date? _OtherCalendarDate;
  private bool _NotReady = true;

  public SelectDateDialog(Date? date, IDateFormatter dateFormatter, IAlertService alertService)
  {
    _DateFormatter = dateFormatter;
    _DialogCommand = new SafeCommand(OnSelectDate, alertService);
    _CancelCommand = new SafeCommand(Cancel, alertService);
    _OtherCalendar = CalendarConversion.OtherThanGregorian(dateFormatter.Calendar);
    if (date.HasValue)
    {
      var signedYear = date.Value.Sign < 0 ? -date.Value.Year : date.Value.Year;
      _Year = NormalizeYear(signedYear);
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

  private static int NormalizeMonth(int month) => DateNormalizer.NormalizeMonth(month);

  private static int NormalizeDay(int year, int month, int day) =>
    DateNormalizer.NormalizeDay(year, month, day);

  private static string[] GetMonths()
  {
    var ret = new List<string>();
    for (var i = 1; i <= 12; i++)
    {
      var monthNo = i.ToString(D2);
      var monthName = UIStrings.ResourceManager.GetString($"Month_{monthNo}", UIStrings.Culture) ?? "??";
      ret.Add($"{monthNo} - {monthName}");
    }
    return [.. ret];
  }

  private static string[] GetDays()
  {
    var ret = new List<string>();
    for (var i = 1; i <= 31; i++)
    {
      ret.Add(i.ToString(D2));
    }
    return [.. ret];
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

  public bool ApproximateSwitch
  {
    get => _ApproximateSwitch;
    set
    {
      if (_ApproximateSwitch != value)
      {
        _ApproximateSwitch = value;
        if (_ApproximateSwitch)
        {
          YearSwitch = true;
          MonthSwitch = false;
          DaySwitch = false;
        }

        OnPropertyChanged(nameof(ApproximateSwitch));
        Refresh();
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
          ApproximateSwitch = false;
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
          ApproximateSwitch = false;
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
          ApproximateSwitch = false;
          MonthSwitch = true;
        }
        OnPropertyChanged(nameof(DaySwitch));
        Refresh();
      }
    }
  }

  public bool OtherCalendarSwitch
  {
    get => _OtherCalendarSwitch;
    set
    {
      if (_OtherCalendarSwitch != value)
      {
        _OtherCalendarSwitch = value;
        if (_OtherCalendarSwitch)
        {
          // The view keeps its numbers for a date it can't show, which would then disagree with them.
          var date = Date;
          var convertible = date.Status == DateStatus.WellKnown && CalendarConversion.IsRepresentable(date, _OtherCalendar);
          OtherCalendarDate = convertible ? date : null;
        }
        else if (_OtherCalendarDate is Date date)
        {
          _Year = date.Year;
          _Month = date.Month;
          _Day = date.Day;
          DaySwitch = true;
          OnPropertyChanged(nameof(Year));
          OnPropertyChanged(nameof(Month));
          OnPropertyChanged(nameof(Day));
        }

        OnPropertyChanged(nameof(OtherCalendarSwitch));
        OnPropertyChanged(nameof(IsGregorianInputVisible));
        Refresh();
      }
    }
  }

  public bool IsGregorianInputVisible => !_OtherCalendarSwitch;

  public DisplayCalendar OtherCalendar
  {
    get => _OtherCalendar;
    set
    {
      if (_OtherCalendar != value)
      {
        _OtherCalendar = value;
        OnPropertyChanged(nameof(OtherCalendar));
        Refresh();
      }
    }
  }

  public Date? OtherCalendarDate
  {
    get => _OtherCalendarDate;
    set
    {
      if (_OtherCalendarDate != value)
      {
        _OtherCalendarDate = value;
        OnPropertyChanged(nameof(OtherCalendarDate));
        Refresh();
      }
    }
  }

  private Date Date
  {
    get
    {
      DateStatus status;

      if (_ApproximateSwitch)
        status = DateStatus.YearApproximate;
      else if (!YearSwitch)
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

  private Date? Result => _NotReady ? null : EnteredDate;

  private Date? EnteredDate => _OtherCalendarSwitch ? _OtherCalendarDate : Date;

  public string DateString
  {
    get
    {
      if (EnteredDate is not null)
        return _DateFormatter.ToString(EnteredDate);

      var name = _DateFormatter.CalendarName(_OtherCalendar);
      var range = _DateFormatter.CalendarRange(_OtherCalendar);
      return string.Format(UIStrings.DateInOtherCalendarPrompt_2, name, range);
    }
  }

  public string DialogButtonName => Result is null ? UIStrings.BtnNameCancel : UIStrings.BtnNameOk;

  public Task<Date?> Info => _Info.Task;

  public ICommand DialogCommand => _DialogCommand;

  public ICommand CancelCommand => _CancelCommand;

  protected override bool OnBackButtonPressed()
  {
    Cancel();
    return true;
  }

  private void OnSelectDate() => _Info.TrySetResult(Result);

  private void Cancel() => _Info.TrySetResult(null);
}
