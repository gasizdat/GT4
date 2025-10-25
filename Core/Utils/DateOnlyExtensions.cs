namespace GT4.Core.Utils;

public static class DateOnlyExtensions
{
  public static DateOnly Now(this DateOnly dateOnly)
  {
    return Now(DateTime.Now);
  }

  public static DateOnly Now(this DateTime dateTime)
  {
    var (date, _) = dateTime;
    return date;
  }

  public static DateSpan Period(this DateOnly from, DateOnly to)
  {
    if (to < from)
      (from, to) = (to, from); // swap to ensure positive

    int years = to.Year - from.Year;
    int months = to.Month - from.Month;
    int days = to.Day - from.Day;

    if (days < 0)
    {
      months--;
      days += DateTime.DaysInMonth(from.Year, from.Month);
    }

    if (months < 0)
    {
      years--;
      months += 12;
    }

    return new DateSpan(Years: years, Months: months, Days: days);
  }

  public static void x()
  {
    var a = new DateOnly();
    var b = new DateOnly();
    var period = a.Period(b);
  }
}
