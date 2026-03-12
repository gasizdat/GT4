namespace GT4.UI.Utils.Configuration;

internal record Date(
  bool MonthAsNumber = true,
  string FullFormat = "YYYY-MM-DD",
  string ShortFormat = "YYYY-MM"
)
{
  public const string SectionName = "DateConfig";
}