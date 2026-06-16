using System.Diagnostics;

namespace GT4.Core.Project.Dto;

/// <summary>
/// A single person placed on the ancestor-descendant graph. <see cref="Generation"/> is relative to
/// the centred person: positive values are ancestors (drawn upward), negative values are descendants
/// (drawn downward) and zero is the centred person and their spouses.
/// </summary>
[DebuggerDisplay("gen {Generation}: {Person.DisplayName}")]
public record class FamilyTreeNode(PersonInfo Person, int Generation) : ElementId(Person.Id);
