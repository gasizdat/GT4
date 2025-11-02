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

  public SelectDateDialog(Date? date, ServiceProvider serviceProvider)
  {
    _DateFormatter = serviceProvider.GetRequiredService<IDateFormatter>();
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
      int.TryParse(value, out _Year);
      OnPropertyChanged(nameof(Year));
      Refresh();
    }
  }

  public bool YearSwitch
  {
    get => _YearSwitch;
    set
    {
      _YearSwitch = value;
      if (!_YearSwitch)
      {
        MonthSwitch = false;
        DaySwitch = false;
      }
      Refresh();
    }
  }

  public string Month
  {
    get => _Month.ToString(D2);
    set
    {
      int.TryParse(value, out _Month);
      //_Month = Math.Clamp(_Month, 1, 12);
      OnPropertyChanged(nameof(Month));
      Refresh();
    }
  }

  public bool MonthSwitch
  {
    get => _MonthSwitch;
    set
    {
      _MonthSwitch = YearSwitch && value;
      if (!_MonthSwitch)
      {
        DaySwitch = false;
        OnPropertyChanged(nameof(MonthSwitch));
      }
      Refresh();
    }
  }

  public string Day
  {
    get => _Day.ToString(D2);
    set
    {
      int.TryParse(value, out _Day);
      //_Day = Math.Clamp(_Day, 1, 31);
      OnPropertyChanged(nameof(Day));
      Refresh();
    }
  }

  public bool DaySwitch
  {
    get => _DaySwitch;
    set
    {
      _DaySwitch = MonthSwitch && value;
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

  public void OnSelectDateBtn(object sender, EventArgs e)
  {
    _Info.SetResult(Date);
  }
}