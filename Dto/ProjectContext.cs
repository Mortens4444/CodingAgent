using CodingAgent.Enums;

namespace CodingAgent.Dto;

public sealed record ProjectContext(string Root, string ProjectFile, ProgrammingLanguage ProgrammingLanguage, string TodoText);
