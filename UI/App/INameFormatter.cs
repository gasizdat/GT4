using GT4.Core.Project.Dto;

namespace GT4.UI;

public interface INameFormatter
{
  string GetCommonPersonName(PersonInfo personInfo);
  string GetFullPersonName(PersonInfo personInfo);
  string GetPersonInitials(PersonInfo personInfo);
}