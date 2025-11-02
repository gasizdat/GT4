namespace GT4.Core.Utils;

public struct Date
{
  private const int AverageDaysInMonth = 30;
  private const int Digit = 100;
  private const int MonthsInYear = 12;
  private const int UndefinedValue = 0;

  public static Date Create(int code, DateStatus status)
  {
    var absCode = Math.Abs(code);
    return new Date
    {
      Sign = int.Sign(code),
      Day = status >= DateStatus.DayUnknown ? UndefinedValue : (absCode % Digit),
      Month = status >= DateStatus.MonthUnknown ? UndefinedValue : ((absCode / Digit) % Digit),
      Year = absCode / (Digit * Digit),
      Status = status
    };
  }

  public static Date Create(int? year, int? month, int? day, DateStatus status)
  {
    var code = (year ?? 0) * Digit * Digit + (month ?? 0) * Digit + day ?? 0;
    return Create(code, status);
  }

  public static Date Create(DateTime dateTime)
  {
    return new Date
    {
      Sign = 1,
      Day = dateTime.Day,
      Month = dateTime.Month,
      Year = dateTime.Year,
      Status = DateStatus.WellKnown
    };
  }

  public int Sign { get; init; }
  public int Year { get; init; }
  public int Month { get; init; }
  public int Day { get; init; }
  public DateStatus Status { get; init; }
  public int Code => Sign * (
    (Status >= DateStatus.DayUnknown ? UndefinedValue : Day) +
    (Status >= DateStatus.MonthUnknown ? UndefinedValue : (Digit * Month)) +
    (Digit * Digit * Year));

  public static Date Now => Create(DateTime.Now);

  public static bool operator <(Date a, Date b)
  {
    if (a.Sign < b.Sign)
      return true;
    if (a.Sign == b.Sign)
    {
      return a.Code < b.Code;
    }

    return false;
  }

  public static bool operator >(Date a, Date b)
  {
    return a != b && !(a < b);
  }

  public static bool operator ==(Date a, Date b)
  {
    return a.Code == b.Code && a.Status == b.Status;
  }

  public static bool operator !=(Date a, Date b)
  {
    return a.Code != b.Code || a.Status == b.Status;
  }

  public static DateStatus GetWorstStatus(params Date[] dates)
  {
    return dates
      .Select(d => d.Status)
      .Max();
  }

  public static DateSpan operator -(Date to, Date from)
  {
    if (to < from)
      (from, to) = (to, from); // swap to ensure positive

    var worstStatus = GetWorstStatus(from, to);
    switch (worstStatus)
    {
      case DateStatus.Unknown:
        return new DateSpan(Years: UndefinedValue, Months: UndefinedValue, Days: UndefinedValue, Status: worstStatus);
      case DateStatus.YearApproximate:
      case DateStatus.MonthUnknown:
        return new DateSpan(Years: to.Year - from.Year, Months: UndefinedValue, Days: UndefinedValue, Status: worstStatus);
      case DateStatus.DayUnknown:
        {
          // TODO check with different signs
          var years = to.Year - from.Year;
          var months = to.Month - from.Month;
          if (int.IsNegative(months))
          {
            months += MonthsInYear;
            years--;
          }
          return new DateSpan(Years: years, Months: months, Days: UndefinedValue, Status: worstStatus);
        }
      case DateStatus.WellKnown:
        {
          // TODO check with different signs
          var years = to.Year - from.Year;
          var months = to.Month - from.Month;
          var days = to.Day - from.Day;
          if (int.IsNegative(days))
          {
            days += AverageDaysInMonth;
            months--;
          }
          if (int.IsNegative(months))
          {
            months += MonthsInYear;
            years--;
          }

          return new DateSpan(Years: years, Months: months, Days: days, Status: worstStatus);
        }
      default:
        throw new NotImplementedException($"Status: {worstStatus}");
    }
  }

  public override bool Equals(object? obj)
  {
    return obj is Date date ? this == date : false;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(Code, Status);
  }
}
