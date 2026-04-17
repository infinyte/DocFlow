using System.Text;
using DocFlow.CLI;
using Spectre.Console;
using Xunit;

namespace DocFlow.CLI.Tests.Integrate;

/// <summary>
/// Exercises <c>docflow integrate docs</c>. Drives the command handler directly
/// (<see cref="Program.ExecuteDocsCommand"/>) so assertions are quick and deterministic;
/// argument parsing is covered separately by <see cref="HelpTests"/>.
/// </summary>
public class DocsCommandTests : IDisposable
{
    private const string PetstoreFixture = "Fixtures/petstore.json";

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"docflow-test-{Guid.NewGuid():N}");
    private readonly TextWriter _originalOut;
    private readonly StringWriter _capturedOut;
    private readonly IAnsiConsole _originalConsole;

    public DocsCommandTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _capturedOut = new StringWriter();
        _originalOut = Console.Out;
        _originalConsole = AnsiConsole.Console;

        Console.SetOut(_capturedOut);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(_capturedOut)
        });
    }

    public void Dispose()
    {
        AnsiConsole.Console = _originalConsole;
        Console.SetOut(_originalOut);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private string CapturedOutput => _capturedOut.ToString();

    [Fact]
    public async Task Cli_Docs_Petstore_WritesExpectedFiles()
    {
        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out"));

        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(PetstoreFixture),
            output: output,
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(0, exitCode);

        string[] expected =
        [
            "index.md",
            "overview.md",
            "domain-model.md",
            "endpoints/pet.md",
            "endpoints/store.md",
            "assets/openapi.json"
        ];

        foreach (var relative in expected)
        {
            var path = Path.Combine(output.FullName, relative);
            Assert.True(File.Exists(path), $"Expected {relative} to be written.");
            Assert.NotEqual(0, new FileInfo(path).Length);
        }
    }

    [Fact]
    public async Task Cli_Docs_CopiesSourceSpecToAssets_ByteIdentical()
    {
        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out"));

        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(PetstoreFixture),
            output: output,
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(0, exitCode);

        var sourceBytes = await File.ReadAllBytesAsync(PetstoreFixture);
        var copiedBytes = await File.ReadAllBytesAsync(Path.Combine(output.FullName, "assets/openapi.json"));
        Assert.Equal(sourceBytes, copiedBytes);
    }

    [Fact]
    public async Task Cli_Docs_YamlSpec_CopiesAsYaml()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: YAML Pet
              version: '1.0'
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: ok
            """;

        var yamlPath = Path.Combine(_tempRoot, "pet.yaml");
        await File.WriteAllTextAsync(yamlPath, yaml);

        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out-yaml"));
        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(yamlPath),
            output: output,
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(0, exitCode);

        var copied = Path.Combine(output.FullName, "assets/openapi.yaml");
        Assert.True(File.Exists(copied), "YAML spec should be preserved as .yaml, not converted.");
        Assert.False(File.Exists(Path.Combine(output.FullName, "assets/openapi.json")));

        Assert.Equal(
            await File.ReadAllTextAsync(yamlPath),
            await File.ReadAllTextAsync(copied));
    }

    [Fact]
    public async Task Cli_Docs_MissingSpec_ReturnsExitCode1()
    {
        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(Path.Combine(_tempRoot, "does-not-exist.json")),
            output: new DirectoryInfo(Path.Combine(_tempRoot, "out")),
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(1, exitCode);
        Assert.Contains("Spec file not found", CapturedOutput);
    }

    [Fact]
    public async Task Cli_Docs_InvalidOutputDir_ReturnsExitCode2()
    {
        // Create a file at the path we'll pass as the output directory. Directory.CreateDirectory
        // on an existing file throws IOException, which the handler maps to exit code 2.
        var collisionPath = Path.Combine(_tempRoot, "collision");
        await File.WriteAllTextAsync(collisionPath, "not a directory");

        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(PetstoreFixture),
            output: new DirectoryInfo(collisionPath),
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(2, exitCode);
        Assert.Contains("I/O error", CapturedOutput);
    }

    [Fact]
    public async Task Cli_Docs_HtmlFlag_WritesHtmlBundle()
    {
        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out-html"));

        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(PetstoreFixture),
            output: output,
            format: "html",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false);

        Assert.Equal(0, exitCode);

        // Every Markdown file has a parallel .html; theme.css ships alongside.
        Assert.True(File.Exists(Path.Combine(output.FullName, "index.html")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "overview.html")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "domain-model.html")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "endpoints/pet.html")));
        Assert.True(File.Exists(Path.Combine(output.FullName, "assets/theme.css")));
    }

    [Fact]
    public async Task Cli_Docs_Verbose_PrintsFileList()
    {
        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out"));

        var exitCode = await Program.ExecuteDocsCommand(
            spec: new FileInfo(PetstoreFixture),
            output: output,
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: true);

        Assert.Equal(0, exitCode);

        var captured = CapturedOutput;
        Assert.Contains("wrote", captured);
        Assert.Contains("index.md", captured);
        Assert.Contains("overview.md", captured);
        Assert.Contains("domain-model.md", captured);
        Assert.Contains("endpoints/pet.md", captured);
    }
}
