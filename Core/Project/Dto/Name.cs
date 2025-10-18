namespace GT4.Core.Project.Dto;

public record class Name(int Id, string Value, NameType Type, Name? Parent);
