namespace GT4.UI.Items;

public class DateTimeItem : CollectionItemBase<DateTime>
{
  public DateTimeItem(DateTime dateTime)
    : base(dateTime, string.Empty)
  {
  }

  public string DateTimeText => $"{Info.ToLocalTime().ToLongDateString()}, {Info.ToLocalTime().ToLongTimeString()}";
}
