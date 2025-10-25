namespace GT4.Core.Utils;

public enum DateStatus
{
  // The date is fully known
  WellKnown = 1,
  // The day of the date is unknown
  DayUnknown = 2,
  // The month of the date is unknown
  MonthUnknown = 3,
  // The year of the date is approximate
  YearApproximate = 4,
  // The date is completely unknown
  Unknown = 5,
}
