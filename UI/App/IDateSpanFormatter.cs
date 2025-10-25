using GT4.Core.Project.Dto;
using GT4.Core.Utils;

namespace GT4.UI;

public interface IDateSpanFormatter
{
  string ToString(DateSpan? dateSpan, params DateStatus[] dateStatuses);
}