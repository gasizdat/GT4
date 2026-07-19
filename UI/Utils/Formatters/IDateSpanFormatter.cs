using GT4.Core.Utils;

namespace GT4.UI.Utils.Formatters;

public interface IDateSpanFormatter
{
  string ToString(DateSpan? dateSpan);
}

public delegate IDateSpanFormatter DateSpanFormatterResolver();