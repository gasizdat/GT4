namespace GT4.Core.Utils;

public struct Date
{
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
      Year = status >= DateStatus.Unknown ? UndefinedValue : (absCode / (Digit * Digit)),
      Status = status
    };
  }

  public static Date Create(int? year, int? month, int? day, DateStatus status)
  {
    var yearValue = year ?? 0;
    var magnitude = Math.Abs(yearValue) * Digit * Digit + (month ?? 0) * Digit + (day ?? 0);
    var code = yearValue < 0 ? -magnitude : magnitude;
    return Create(code, status);
  }

  public static Date Create(DateTime dateTime) =>
    new Date
    {
      Sign = 1,
      Day = dateTime.Day,
      Month = dateTime.Month,
      Year = dateTime.Year,
      Status = DateStatus.WellKnown
    };

  public int Sign { get; init; }
  public int Year { get; init; }
  public int Month { get; init; }
  public int Day { get; init; }
  public DateStatus Status { get; init; }
  public int Code => Sign * (
    (Status >= DateStatus.DayUnknown ? UndefinedValue : Day) +
    (Status >= DateStatus.MonthUnknown ? UndefinedValue : (Digit * Month)) +
    (Status >= DateStatus.Unknown ? UndefinedValue : (Digit * Digit * Year)));

  public static Date Now => Create(DateTime.Now);

  public static bool operator <(Date a, Date b)
  {
    if (a.Sign != b.Sign)
    {
      return a.Sign < b.Sign;
    }

    if (a.SequenceCode != b.SequenceCode)
    {
      return a.SequenceCode < b.SequenceCode;
    }

    // Same calendar point but differing certainty: break the tie by status so the order is total and
    // strictly consistent with '>'.
    return a.Status < b.Status;
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
    return a.Code != b.Code || a.Status != b.Status;
  }

  public static DateStatus GetWorstStatus(params Date[] dates) =>
    dates
      .Select(d => d.Status)
      .Max();

  // Proleptic, as stored dates are: the rule also covers the years before 1582 and before leap years began in 45 B.C.
  public static bool IsLeapYear(int year)
  {
    var signedYear = year < 0 ? year + 1 : year;
    return IsLeapSignedYear(signedYear);
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
        return new DateSpan(Years: to.SignedYear - from.SignedYear, Months: UndefinedValue, Days: UndefinedValue, Status: worstStatus);
      case DateStatus.DayUnknown:
        {
          var years = to.SignedYear - from.SignedYear;
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
          var years = to.SignedYear - from.SignedYear;
          var months = to.Month - from.Month;
          var days = to.Day - from.Day;
          if (int.IsNegative(days))
          {
            var (borrowedYear, borrowedMonth) = to.Month == 1
              ? (to.SignedYear - 1, MonthsInYear)
              : (to.SignedYear, to.Month - 1);
            var borrowedDays = DaysInMonth(borrowedYear, borrowedMonth);
            days = to.Day + borrowedDays - Math.Min(from.Day, borrowedDays);
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

  public override bool Equals(object? obj) => obj is Date date ? this == date : false;

  public override int GetHashCode() => HashCode.Combine(Code, Status);

  // There is no year 0: 1 B.C. is followed directly by 1 A.D., so B.C. year N is astronomical year 1-N.
  private int SignedYear => Sign < 0 ? 1 - Year : Year;

  // Monotonic in calendar time, unlike Code whose Sign factor flips month/day ordering for B.C. dates.
  private int SequenceCode =>
    (Status >= DateStatus.DayUnknown ? UndefinedValue : Day) +
    (Status >= DateStatus.MonthUnknown ? UndefinedValue : (Digit * Month)) +
    (Status >= DateStatus.Unknown ? UndefinedValue : (Digit * Digit * SignedYear));

  // DateTime rejects the astronomical years 0 and below.
  private static int DaysInMonth(int signedYear, int month) => month switch
  {
    2 => IsLeapSignedYear(signedYear) ? 29 : 28,
    4 or 6 or 9 or 11 => 30,
    _ => 31,
  };

  private static bool IsLeapSignedYear(int signedYear) =>
    signedYear % 4 == 0 && (signedYear % 100 != 0 || signedYear % 400 == 0);
}
