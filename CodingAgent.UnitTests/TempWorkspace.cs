namespace CodingAgent.UnitTests;

public sealed class TempWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "CodingAgentTests_" + Path.GetRandomFileName());

    public TempWorkspace() => Directory.CreateDirectory(Root);

    public string Write(string relativePath, string content = "")
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch { }
    }
}
