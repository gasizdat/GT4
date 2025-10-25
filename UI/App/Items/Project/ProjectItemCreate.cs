using GT4.Core.Project.Dto;
using GT4.UI.Resources;

namespace GT4.UI.App.Items;

public record class ProjectItemCreate() : ProjectInfo(
      Description: string.Empty,
      Name: UIStrings.BtnNameCreateGenealogyTree,
      Path: string.Empty);
