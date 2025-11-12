using GT4.Core.Utils;
using GT4.UI.Resources;
using static GT4.UI.App.Dialogs.CreateNewNameDialog;

namespace GT4.UI.App.Dialogs;

public partial class SelectDateDialog : ContentPage
{
  private const string D4 = "D4";
  private const string D2 = "D2";

  private readonly IDateFormatter _DateFormatter;
  private readonly TaskCompletionSource<Date?> _Info = new(null);
  private int _Year = 0;
  private int _Month = 1;
  private int _Day = 1;
  private bool _YearSwitch = false;
  private bool _MonthSwitch = false;
  private bool _DaySwitch = false;
  private string _SelectDateBtnName = UIStrings.BtnNameCancel;

  public SelectDateDialog(Date? date, IDateFormatter dateFormatter)
  {
    _DateFormatter = dateFormatter;
    if (date.HasValue)
    {
      _Year = date.Value.Year;
      _Month = date.Value.Month;
      _Day = date.Value.Day;
      switch (date.Value.Status)
      {
        case DateStatus.MonthUnknown:
          YearSwitch = true;
          break;
        case DateStatus.DayUnknown:
          YearSwitch = true;
          MonthSwitch = true;
          break;
        case DateStatus.WellKnown:
          YearSwitch = true;
          MonthSwitch = true;
          DaySwitch = true;
          break;
      }
    }

    InitializeComponent();
  }

  private void Refresh()
  {
    SelectDateBtnName = UIStrings.BtnNameOk;
    OnPropertyChanged(nameof(DateString));
  }

  public string Year
  {
    get => _Year.ToString(D4);
    set
    {
      // Derty update
      int.TryParse(value, out _Year);
    }
  }

  public bool YearSwitch
  {
    get => _YearSwitch;
    set
    {
      if (_YearSwitch == value)
      {
        return;
      }

      _YearSwitch = value;
      if (!_YearSwitch)
      {
        MonthSwitch = false;
      }
      OnPropertyChanged(nameof(YearSwitch));
      Refresh();
    }
  }

  public string Month
  {
    get => _Month.ToString(D2);
    set
    {
      //Derty update
      int.TryParse(value, out _Month);
    }
  }

  public bool MonthSwitch
  {
    get => _MonthSwitch;
    set
    {
      if (MonthSwitch == value)
      {
        return;
      }

      _MonthSwitch = value;
      if (_MonthSwitch)
      {
        YearSwitch = true;
        OnMonthChangedEnd(new(), new());
      }
      else
      {
        DaySwitch = false;
      }
      OnPropertyChanged(nameof(MonthSwitch));
      Refresh();
    }
  }

  public string Day
  {
    get => _Day.ToString(D2);
    set
    {
      //Derty update
      int.TryParse(value, out _Day);
    }
  }

  public bool DaySwitch
  {
    get => _DaySwitch;
    set
    {
      if (DaySwitch == value)
      {
        return;
      }

      _DaySwitch = value;
      if (_DaySwitch)
      {
        MonthSwitch = true;
        OnDayChangedEnd(new(), new());
      }
      OnPropertyChanged(nameof(DaySwitch));
      Refresh();
    }
  }

  private Date Date
  {
    get
    {
      int.TryParse(Year, out var year);
      int.TryParse(Month, out var month);
      int.TryParse(Day, out var day);
      DateStatus status;
      if (!YearSwitch)
        status = DateStatus.Unknown;
      else if (!MonthSwitch)
        status = DateStatus.MonthUnknown;
      else if (!DaySwitch)
        status = DateStatus.DayUnknown;
      else
        status = DateStatus.WellKnown;

      return Date.Create(year, month, day, status);
    }
  }

  public string DateString => _DateFormatter.ToString(Date);

  public string SelectDateBtnName
  {
    get => _SelectDateBtnName;
    set
    {
      _SelectDateBtnName = value;
      OnPropertyChanged(nameof(SelectDateBtnName));
    }
  }

  public Task<Date?> Info => _Info.Task;

  public void OnYearChangedEnd(object sender, EventArgs e)
  {
    OnPropertyChanged(nameof(Year));
    YearSwitch = true;
    if (DaySwitch)
    {
      OnDayChangedEnd(sender, e);
    }
    Refresh();
  }

  public void OnMonthChangedEnd(object sender, EventArgs e)
  {
    _Month = Math.Clamp(_Month, 1, 12);
    OnPropertyChanged(nameof(Month));
    MonthSwitch = true;
    if (DaySwitch)
    {
      OnDayChangedEnd(sender, e);
    }
    Refresh();
  }

  public void OnDayChangedEnd(object sender, EventArgs e)
  {
    var daysInMonth = _Month switch
    {
      1 => 31,
      2 => (_Year % 4) == 0 && ((_Year % 100) != 0 || (_Year % 400) == 0) ? 29 : 28,
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


    _Day = Math.Clamp(_Day, 1, daysInMonth);
    OnPropertyChanged(nameof(Day));
    DaySwitch = true;
    Refresh();
  }

  public void OnSelectDateBtn(object sender, EventArgs e)
  {
    _Info.SetResult(Date);
  }
}