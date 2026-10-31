using CodingAgent.Enums;

namespace CodingAgent.Services.DevEnvironment;

// Commands lists interchangeable executables; the requirement is met when any of them is on PATH.
public sealed record ToolRequirement(string[] Commands, string Purpose, string WindowsInstall, string LinuxInstall, string MacInstall, bool Optional = false)
{
    public string Name => Commands[0];

    public string InstallHint =>
        OperatingSystem.IsWindows() ? WindowsInstall :
        OperatingSystem.IsMacOS() ? MacInstall :
        LinuxInstall;
}

public static class ToolchainCatalog
{
    public const string SetupScript = "scripts/setup-dev-env.ps1";

    private static readonly ToolRequirement DotNet = new(["dotnet"], ".NET SDK (build and test)",
        "winget install --id Microsoft.DotNet.SDK.10 -e", "sudo apt install dotnet-sdk-10.0", "brew install --cask dotnet-sdk");

    private static readonly ToolRequirement Python = new(["python", "py", "python3"], "Python interpreter",
        "winget install --id Python.Python.3.12 -e", "sudo apt install python3 python3-pip", "brew install python");

    private static readonly ToolRequirement Gcc = new(["gcc"], "C compiler",
        "winget install --id BrechtSanders.WinLibs.POSIX.UCRT -e", "sudo apt install build-essential", "xcode-select --install");

    private static readonly ToolRequirement Gpp = new(["g++"], "C++ compiler",
        "winget install --id BrechtSanders.WinLibs.POSIX.UCRT -e", "sudo apt install build-essential", "xcode-select --install");

    private static readonly ToolRequirement Make = new(["make", "mingw32-make"], "Makefile builds",
        "winget install --id ezwinports.make -e", "sudo apt install make", "xcode-select --install", Optional: true);

    private static readonly ToolRequirement CMake = new(["cmake"], "CMakeLists.txt builds",
        "winget install --id Kitware.CMake -e", "sudo apt install cmake", "brew install cmake", Optional: true);

    private static readonly ToolRequirement Javac = new(["javac"], "Java compiler (JDK)",
        "winget install --id Microsoft.OpenJDK.21 -e", "sudo apt install openjdk-21-jdk", "brew install openjdk@21");

    private static readonly ToolRequirement Maven = new(["mvn"], "pom.xml builds (a Maven wrapper mvnw also works)",
        "download from https://maven.apache.org/download.cgi or 'scoop install maven'", "sudo apt install maven", "brew install maven", Optional: true);

    private static readonly ToolRequirement Gradle = new(["gradle"], "build.gradle builds (a Gradle wrapper gradlew also works)",
        "download from https://gradle.org/install or 'scoop install gradle'", "sudo apt install gradle", "brew install gradle", Optional: true);

    private static readonly ToolRequirement Node = new(["node"], "JavaScript runtime",
        "winget install --id OpenJS.NodeJS.LTS -e", "sudo apt install nodejs npm", "brew install node");

    private static readonly ToolRequirement Npm = new(["npm"], "package scripts (build/test)",
        "installed together with Node.js: winget install --id OpenJS.NodeJS.LTS -e", "sudo apt install npm", "brew install node");

    private static readonly ToolRequirement Npx = new(["npx"], "runs project tools such as tsc and htmlhint",
        "installed together with Node.js: winget install --id OpenJS.NodeJS.LTS -e", "sudo apt install npm", "brew install node");

    private static readonly ToolRequirement HtmlNode = Npx with { Purpose = "htmlhint validation (a built-in tag check is used without it)", Optional = true };

    private static readonly ToolRequirement Nasm = new(["nasm"], "assembler for .asm files",
        "winget install --id NASM.NASM -e", "sudo apt install nasm", "brew install nasm");

    private static readonly ToolRequirement AsmLinker = Gcc with { Purpose = "assembles .s files and links object files" };

    private static readonly ToolRequirement Fpc = new(["fpc"], "Free Pascal compiler",
        "winget install --id FreePascal.FreePascalCompiler -e", "sudo apt install fpc", "brew install fpc");

    private static readonly ToolRequirement Sbcl = new(["sbcl"], "Common Lisp (SBCL)",
        "winget install --id SBCL.SBCL -e", "sudo apt install sbcl", "brew install sbcl");

    private static readonly Dictionary<ProgrammingLanguage, ToolRequirement[]> requirements = new()
    {
        [ProgrammingLanguage.CSharp] = [DotNet],
        [ProgrammingLanguage.Python] = [Python],
        [ProgrammingLanguage.C] = [Gcc, Make, CMake],
        [ProgrammingLanguage.Cpp] = [Gpp, Make, CMake],
        [ProgrammingLanguage.Java] = [Javac, Maven, Gradle],
        [ProgrammingLanguage.JavaScript] = [Node, Npm],
        [ProgrammingLanguage.TypeScript] = [Node, Npm, Npx],
        [ProgrammingLanguage.Html] = [HtmlNode],
        [ProgrammingLanguage.Assembly] = [Nasm, AsmLinker],
        [ProgrammingLanguage.Pascal] = [Fpc],
        [ProgrammingLanguage.Lisp] = [Sbcl]
    };

    public static IReadOnlyList<ToolRequirement> For(ProgrammingLanguage language) =>
        requirements.TryGetValue(language, out var tools) ? tools : [];

    public static ToolRequirement? FindByCommand(string command)
    {
        var name = Path.GetFileNameWithoutExtension(command);
        return requirements.Values
            .SelectMany(tools => tools)
            .FirstOrDefault(tool => tool.Commands.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    public static string MissingToolMessage(string command)
    {
        var tool = FindByCommand(command);
        var hint = tool is null ? String.Empty : $" It provides: {tool.Purpose}. Install: {tool.InstallHint}.";
        return $"ERROR: '{command}' was not found on PATH, so this step could not run.{hint} " +
               "The user has to install it (see " + SetupScript + "); do not retry until it is installed.";
    }
}
