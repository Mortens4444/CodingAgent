namespace CodingAgent.Interfaces;

internal interface IToDoProvider
{
    Task<string?> GetToDoAsync(string? todoPath = null);
}
