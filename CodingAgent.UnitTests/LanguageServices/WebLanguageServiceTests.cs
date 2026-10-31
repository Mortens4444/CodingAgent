using CodingAgent.Services.LanguageServices;
using CodingAgent.UnitTests.Mocks;

namespace CodingAgent.UnitTests.LanguageServices;

[TestFixture]
public class WebLanguageServiceTests
{
    private const string PackageWithScripts = """{ "scripts": { "build": "webpack", "test": "jest" } }""";

    private TempWorkspace _workspace = null!;
    private RecordingProcessRunner _runner = null!;

    [SetUp]
    public void Setup()
    {
        _workspace = new TempWorkspace();
        _runner = new RecordingProcessRunner();
    }

    [TearDown]
    public void TearDown() => _workspace.Dispose();

    [Test]
    public async Task JavaScript_WithScripts_UsesNpm()
    {
        _workspace.Write("package.json", PackageWithScripts);
        var service = new JavaScriptCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.BuildAsync();
        await service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npm run build", "npm test" }));
    }

    [Test]
    public async Task JavaScript_WithoutScripts_ChecksEachFileAndUsesNodeTestRunner()
    {
        _workspace.Write("package.json", """{ "scripts": { "test": "echo \"Error: no test specified\" && exit 1" } }""");
        _workspace.Write("index.js");
        _workspace.Write("lib/util.mjs");
        _workspace.Write("node_modules/dep/index.js");
        var service = new JavaScriptCodingLanguageService(_workspace.Root, _runner.Mock);

        var build = await service.BuildAsync();
        await service.RunTestsAsync();

        Assert.That(build, Does.Contain("Syntax OK. Checked 2 JavaScript files."));
        Assert.That(_runner.Commands, Is.EqualTo(new[] { "node --check index.js", "node --check lib/util.mjs", "node --test" }));
    }

    [Test]
    public async Task JavaScript_ValidateSyntax_ReportsFailingFiles()
    {
        _workspace.Write("good.js");
        _workspace.Write("bad.js");
        _runner.Result = (_, args) => args[1] == "bad.js" ? RecordingProcessRunner.Failure("SyntaxError") : RecordingProcessRunner.Success("");

        var result = await new JavaScriptCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.StartWith("Syntax errors in 1 of 2"));
        Assert.That(result, Does.Contain("SyntaxError"));
    }

    [Test]
    public async Task JavaScript_NoSources_ReportsError()
    {
        var result = await new JavaScriptCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.StartWith("ERROR: No JavaScript source files"));
    }

    [Test]
    public async Task JavaScript_InvalidPackageJson_IsTreatedAsNoScripts()
    {
        _workspace.Write("package.json", "{ not json");

        await new JavaScriptCodingLanguageService(_workspace.Root, _runner.Mock).RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "node --test" }));
    }

    [Test]
    public async Task TypeScript_WithTsconfigAndNoLocalCompiler_UsesTypescriptPackage()
    {
        _workspace.Write("tsconfig.json", "{}");
        var service = new TypeScriptCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.BuildAsync();
        await service.ValidateSyntaxAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[]
        {
            "npx --yes -p typescript tsc",
            "npx --yes -p typescript tsc --noEmit"
        }));
    }

    [Test]
    public async Task TypeScript_WithLocalCompiler_UsesIt()
    {
        _workspace.Write("tsconfig.json", "{}");
        _workspace.Write("node_modules/.bin/tsc");

        await new TypeScriptCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npx tsc" }));
    }

    [Test]
    public async Task TypeScript_WithScripts_UsesNpm()
    {
        _workspace.Write("package.json", PackageWithScripts);
        var service = new TypeScriptCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.BuildAsync();
        await service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npm run build", "npm test" }));
    }

    [Test]
    public async Task TypeScript_WithoutConfig_ChecksSourceFiles()
    {
        _workspace.Write("app.ts");
        _workspace.Write("view.tsx");
        var service = new TypeScriptCodingLanguageService(_workspace.Root, _runner.Mock);

        await service.BuildAsync();
        await service.RunTestsAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npx --yes -p typescript tsc --noEmit app.ts view.tsx", "node --test" }));
    }

    [Test]
    public async Task TypeScript_NoSources_ReportsError()
    {
        var result = await new TypeScriptCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.StartWith("ERROR: No TypeScript source files"));
    }

    [Test]
    public async Task Html_WithHtmlhintConfig_UsesHtmlhint()
    {
        _workspace.Write(".htmlhintrc", "{}");

        await new HtmlCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npx htmlhint ." }));
    }

    [Test]
    public async Task Html_Build_RunsBuiltInCheck()
    {
        _workspace.Write("index.html", "<html><body><div><p>Hi</div></body></html>");
        _workspace.Write("broken.htm", "<div><span></div>");

        var result = await new HtmlCodingLanguageService(_workspace.Root, _runner.Mock).BuildAsync();

        Assert.That(result, Does.Contain("Exit code: 1"));
        Assert.That(result, Does.Contain("broken.htm: line 1: <span> is not closed before </div>"));
        Assert.That(result, Does.Not.Contain("index.html"));
        Assert.That(_runner.Commands, Is.Empty);
    }

    [Test]
    public async Task Html_ValidFiles_Pass()
    {
        _workspace.Write("index.html", "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body><img src=x></body></html>");

        var result = await new HtmlCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.Contain("Exit code: 0"));
    }

    [Test]
    public async Task Html_NoFiles_ReportsError()
    {
        var result = await new HtmlCodingLanguageService(_workspace.Root, _runner.Mock).ValidateSyntaxAsync();

        Assert.That(result, Does.StartWith("ERROR: No HTML source files"));
    }

    [Test]
    public async Task Html_RunTests_UsesNpmScriptOrExplains()
    {
        var service = new HtmlCodingLanguageService(_workspace.Root, _runner.Mock);
        var withoutScript = await service.RunTestsAsync();
        _workspace.Write("package.json", PackageWithScripts);
        await service.RunTestsAsync();

        Assert.That(withoutScript, Does.StartWith("No automated tests"));
        Assert.That(_runner.Commands, Is.EqualTo(new[] { "npm test" }));
    }

    [TestCase("<div><p>a<p>b</div>")]
    [TestCase("<ul><li>a<li>b</ul>")]
    [TestCase("<br/><input type=\"text\" value=\"a>b\"><!-- <div> -->")]
    [TestCase("<script>if (a < b && c > d) { document.write('<div>'); }</script>")]
    [TestCase("<svg><circle r=\"1\" /></svg>")]
    public void CheckTags_ValidMarkup_HasNoProblems(string html)
    {
        Assert.That(HtmlCodingLanguageService.CheckTags(html), Is.Empty);
    }

    [TestCase("<div>", "line 1: <div> is never closed.")]
    [TestCase("</span>", "line 1: closing tag </span> has no matching opening tag.")]
    [TestCase("<section>\n<div>\n</section>", "line 2: <div> is not closed before </section> on line 3.")]
    public void CheckTags_InvalidMarkup_ReportsProblem(string html, string expected)
    {
        Assert.That(HtmlCodingLanguageService.CheckTags(html), Is.EqualTo(new[] { expected }));
    }
}
