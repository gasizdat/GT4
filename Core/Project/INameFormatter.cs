using GT4.Core.Project.Dto;

namespace GT4.Core.Project;

public interface INameFormatter
{
  string GetCommonPersonName(Person person);
  string GetFullPersonName(Person person);
  string GetPersonInitials(Person person);
}