using System.Text;
using DocFlow.CLI;
using Spectre.Console;
using Xunit;

namespace DocFlow.CLI.Tests.Integrate;

public class WatchModeTests : IDisposable
{
    private const string PetstoreFixture = "Fixtures/petstore.json";

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"docflow-watch-{Guid.NewGuid():N}");
    private readonly TextWriter _originalOut;
    private readonly StringWriter _capturedOut;
    private readonly IAnsiConsole _originalConsole;

    public WatchModeTests()
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

    [Fact]
    public async Task Watch_RegeneratesOnFileChange()
    {
        // Set up a writable copy of the fixture so we can modify it.
        var specPath = Path.Combine(_tempRoot, "spec.json");
        File.Copy(PetstoreFixture, specPath);

        var output = new DirectoryInfo(Path.Combine(_tempRoot, "out"));
        var indexPath = Path.Combine(output.FullName, "index.md");

        using var cts = new CancellationTokenSource();
        var watchTask = Program.RunWatchAsync(
            spec: new FileInfo(specPath),
            output: output,
            format: "markdown",
            diagrams: "class",
            withExamples: false,
            groupBy: "tag",
            title: null,
            verbose: false,
            cancellationToken: cts.Token);

        try
        {
            // Wait for the initial build to complete.
            Assert.True(await PollUntilAsync(() => File.Exists(indexPath), TimeSpan.FromSeconds(10)),
                "Initial build did not produce index.md.");

            var initialMtime = File.GetLastWriteTimeUtc(indexPath);

            // FileSystemWatcher triggers on LastWrite; give the filesystem a moment so the mtime
            // of the re-generated file is distinguishable from the initial one.
            await Task.Delay(1_100);

            // Modify the spec.
            var specContent = await File.ReadAllTextAsync(specPath);
            specContent = specContent.Replace("\"Petstore API\"", "\"Petstore API (Modified)\"");
            await File.WriteAllTextAsync(specPath, specContent);

            // Wait for the regeneration to update index.md.
            var updated = await PollUntilAsync(
                () => File.Exists(indexPath) && File.GetLastWriteTimeUtc(indexPath) > initialMtime,
                TimeSpan.FromSeconds(10));

            Assert.True(updated, "Bundle did not regenerate after spec change.");

            // The updated overview should reflect the new title.
            var overview = await File.ReadAllTextAsync(Path.Combine(output.FullName, "overview.md"));
            Assert.Contains("Petstore API (Modified)", overview);
        }
        finally
        {
            cts.Cancel();
            try { await watchTask; } catch { }
        }
    }

    private static async Task<bool> PollUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (predicate()) return true;
            }
            catch (IOException) { /* retry; file may be mid-write */ }
            await Task.Delay(100);
        }
        return false;
    }
}
