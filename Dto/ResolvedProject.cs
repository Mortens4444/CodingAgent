using CodingAgent.Enums;

namespace CodingAgent.Dto;

public sealed record ResolvedProject(string Root, string ProjectFile, ProgrammingLanguage ProgrammingLanguage);
