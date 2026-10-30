namespace CodingAgent.Interfaces;

public interface IToDoProvider
{
    Task<string?> GetToDoAsync(string? todoPath = null);
}
