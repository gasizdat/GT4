namespace GT4.UI.Utils.Configuration;

internal record Date(
  bool MonthAsNumber = false,
  string FullFormat = "DD MM YYYY",
  string ShortFormat = "MM YYYY"
)
{
  public const string SectionName = "DateConfig";
}