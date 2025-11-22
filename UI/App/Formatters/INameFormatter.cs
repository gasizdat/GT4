using GT4.Core.Project.Dto;

namespace GT4.UI.Formatters;

public interface INameFormatter
{
  string ToString(PersonInfo personInfo, NameFormat format);
}