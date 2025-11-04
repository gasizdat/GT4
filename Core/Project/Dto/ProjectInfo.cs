using GT4.Core.Utils;

namespace GT4.Core.Project.Dto;

public record class ProjectInfo(
  string Name,
  string Description,
  FileDescription Origin
);

