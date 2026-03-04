using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DocFlow.AI.Providers;
using DocFlow.CodeAnalysis.CSharp;
using DocFlow.CodeGen.CSharp;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using System.Text.RegularExpressions;
using DocFlow.Integration.CodeGen;
using DocFlow.Integration.Mapping;
using DocFlow.Integration.Models;
using DocFlow.Integration.Schemas;
using DocFlow.Integration.Schemas.OpenApi;
using DocFlow.Integration.Validation;
using DocFlow.Vision;
using Spectre.Console;

namespace DocFlow.CLI;

public class Program
{
    private const string Version = "1.0.0-preview";

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = BuildRootCommand();

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler((ex, context) =>
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                if (context.ParseResult.GetValueForOption(VerboseOption))
                {
                    AnsiConsole.WriteException(ex);
                }
                context.ExitCode = 1;
            })
            .Build();

        return await parser.InvokeAsync(args);
    }

    // Global options
    private static readonly Option<bool> VerboseOption = new(
        aliases: ["-v", "--verbose"],
        description: "Show detailed output");

    private static readonly Option<bool> QuietOption = new(
        aliases: ["-q", "--quiet"],
        description: "Minimal output, only errors");

    private static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("DocFlow - Intelligent Documentation and Modeling Toolkit");

        rootCommand.AddCommand(BuildDiagramCommand());
        rootCommand.AddCommand(BuildCodegenCommand());
        rootCommand.AddCommand(BuildRoundtripCommand());
        rootCommand.AddCommand(BuildScanCommand());
        rootCommand.AddCommand(BuildIntegrateCommand());

        // Custom version option with ASCII banner
        rootCommand.SetHandler(() =>
        {
            ShowBanner();
            AnsiConsole.MarkupLine("\nUse [green]docflow --help[/] to see available commands.");
        });

        return rootCommand;
    }

    #region Diagram Command

    private static Command BuildDiagramCommand()
    {
        var inputArg = new Argument<FileInfo>(
            name: "input",
            description: "C# source file or directory to parse");

        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Output file path (default: same name with .mmd extension)");

        var recursiveOption = new Option<bool>(
            aliases: ["-r", "--recursive"],
            description: "Process all .cs files in directory");

        var noRelationshipsOption = new Option<bool>(
            name: "--no-relationships",
            description: "Exclude relationship lines from diagram");

        var command = new Command("diagram", "Generate Mermaid class diagram from C# source")
        {
            inputArg,
            outputOption,
            recursiveOption,
            noRelationshipsOption,
            VerboseOption,
            QuietOption
        };

        command.AddAlias("d");

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var recursive = context.ParseResult.GetValueForOption(recursiveOption);
            var noRelationships = context.ParseResult.GetValueForOption(noRelationshipsOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteDiagramCommand(input, output, recursive, noRelationships, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteDiagramCommand(
        FileInfo input, FileInfo? output, bool recursive, bool noRelationships, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        // Validate input
        if (!input.Exists && !Directory.Exists(input.FullName))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File or directory not found: [yellow]{input.FullName}[/]");
            AnsiConsole.MarkupLine("[dim]Tip: Make sure the path exists and you have read permissions.[/]");
            return 1;
        }

        var files = new List<string>();
        if (Directory.Exists(input.FullName))
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            files.AddRange(Directory.GetFiles(input.FullName, "*.cs", searchOption));
            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] No .cs files found in [cyan]{input.FullName}[/]");
                return 1;
            }
        }
        else
        {
            files.Add(input.FullName);
        }

        if (!quiet)
        {
            AnsiConsole.MarkupLine($"\n[blue]Parsing[/] {files.Count} C# file(s)...\n");
        }

        var parser = new CSharpModelParser();
        var combinedModel = new SemanticModel { Name = Path.GetFileNameWithoutExtension(input.Name) };
        var stopwatch = Stopwatch.StartNew();

        await AnsiConsole.Status()
            .StartAsync("Parsing C# source files...", async ctx =>
            {
                foreach (var file in files)
                {
                    ctx.Status($"Parsing [cyan]{Path.GetFileName(file)}[/]...");

                    var parseResult = await parser.ParseAsync(ParserInput.FromFile(file));
                    if (!parseResult.Success)
                    {
                        AnsiConsole.MarkupLine($"[red]Error parsing {Path.GetFileName(file)}:[/]");
                        foreach (var error in parseResult.Errors)
                        {
                            AnsiConsole.MarkupLine($"  [red]•[/] {error.Code}: {error.Message}");
                        }
                        continue;
                    }

                    // Merge into combined model
                    foreach (var entity in parseResult.Model.Entities.Values)
                    {
                        if (!combinedModel.Entities.ContainsKey(entity.Id))
                            combinedModel.AddEntity(entity);
                    }
                    foreach (var rel in parseResult.Model.Relationships)
                    {
                        combinedModel.Relationships.Add(rel);
                    }

                    if (verbose)
                    {
                        AnsiConsole.MarkupLine($"  [green]✓[/] {Path.GetFileName(file)}: {parseResult.Model.Entities.Count} entities");
                    }
                }
            });

        if (combinedModel.Entities.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No entities found in the source file(s).");
            return 1;
        }

        // Remove relationships if requested
        if (noRelationships)
        {
            combinedModel.Relationships.Clear();
        }

        // Generate Mermaid
        var generator = new MermaidClassDiagramGenerator();
        var generateResult = await generator.GenerateAsync(combinedModel);

        if (!generateResult.Success)
        {
            AnsiConsole.MarkupLine("[red]Error generating diagram:[/]");
            foreach (var error in generateResult.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {error.Code}: {error.Message}");
            }
            return 1;
        }

        stopwatch.Stop();

        // Determine output path
        var outputPath = output?.FullName ?? Path.ChangeExtension(input.FullName, ".mmd");
        await File.WriteAllTextAsync(outputPath, generateResult.Content!);

        // Display results
        if (!quiet)
        {
            PrintModelSummary(combinedModel, verbose);

            AnsiConsole.WriteLine();
            var panel = new Panel(generateResult.Content!)
            {
                Header = new PanelHeader("[green]Generated Mermaid Diagram[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            };
            AnsiConsole.Write(panel);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Success![/] Diagram saved to [cyan]{outputPath}[/]");
            AnsiConsole.MarkupLine($"[dim]Completed in {stopwatch.ElapsedMilliseconds}ms[/]");
        }
        else
        {
            Console.WriteLine(outputPath);
        }

        return 0;
    }

    #endregion

    #region Codegen Command

    private static Command BuildCodegenCommand()
    {
        var inputArg = new Argument<FileInfo>(
            name: "input",
            description: "Mermaid diagram file (.mmd) to parse");

        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Output file path (default: same name with .cs extension)");

        var namespaceOption = new Option<string?>(
            aliases: ["-n", "--namespace"],
            description: "Namespace for generated code (default: derived from filename)");

        var styleOption = new Option<string>(
            aliases: ["--style"],
            getDefaultValue: () => "ddd",
            description: "Code style: 'ddd' (Domain-Driven Design) or 'poco' (Plain Old CLR Objects)");

        var command = new Command("codegen", "Generate C# code from Mermaid diagram")
        {
            inputArg,
            outputOption,
            namespaceOption,
            styleOption,
            VerboseOption,
            QuietOption
        };

        command.AddAlias("c");

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var ns = context.ParseResult.GetValueForOption(namespaceOption);
            var style = context.ParseResult.GetValueForOption(styleOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteCodegenCommand(input, output, ns, style!, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteCodegenCommand(
        FileInfo input, FileInfo? output, string? ns, string style, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        if (!input.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: [yellow]{input.FullName}[/]");
            AnsiConsole.MarkupLine("[dim]Tip: Make sure the Mermaid diagram file exists.[/]");
            return 1;
        }

        if (!quiet)
        {
            AnsiConsole.MarkupLine($"\n[blue]Parsing[/] Mermaid diagram: [cyan]{input.Name}[/]\n");
        }

        var stopwatch = Stopwatch.StartNew();
        SemanticModel? model = null;

        await AnsiConsole.Status()
            .StartAsync("Parsing Mermaid diagram...", async ctx =>
            {
                var parser = new MermaidClassDiagramParser();
                var parseResult = await parser.ParseAsync(ParserInput.FromFile(input.FullName));

                if (!parseResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing Mermaid diagram:[/]");
                    foreach (var error in parseResult.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]•[/] Line {error.Line}: {error.Message}");
                    }
                    return;
                }

                model = parseResult.Model;

                // Set namespace
                if (!string.IsNullOrEmpty(ns))
                {
                    model.Name = ns;
                }

                ctx.Status("Generating C# code...");

                // Generate C#
                var generator = new CSharpModelGenerator();
                var generateResult = await generator.GenerateAsync(model);

                if (!generateResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error generating code:[/]");
                    foreach (var error in generateResult.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]•[/] {error.Code}: {error.Message}");
                    }
                    model = null;
                    return;
                }

                // Save output
                var outputPath = output?.FullName ?? Path.ChangeExtension(input.FullName, ".cs");
                await File.WriteAllTextAsync(outputPath, generateResult.Content!);

                stopwatch.Stop();

                if (!quiet)
                {
                    PrintModelSummary(model, verbose);

                    AnsiConsole.WriteLine();
                    var panel = new Panel(Markup.Escape(generateResult.Content!))
                    {
                        Header = new PanelHeader("[green]Generated C# Code[/]"),
                        Border = BoxBorder.Rounded,
                        Padding = new Padding(1, 0)
                    };
                    AnsiConsole.Write(panel);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[green]Success![/] Code saved to [cyan]{outputPath}[/]");
                    AnsiConsole.MarkupLine($"[dim]Completed in {stopwatch.ElapsedMilliseconds}ms[/]");
                }
                else
                {
                    Console.WriteLine(outputPath);
                }
            });

        return model != null ? 0 : 1;
    }

    #endregion

    #region Roundtrip Command

    private static Command BuildRoundtripCommand()
    {
        var inputArg = new Argument<FileInfo>(
            name: "input",
            description: "C# source file to round-trip");

        var outputOption = new Option<DirectoryInfo?>(
            aliases: ["-o", "--output"],
            description: "Output directory for generated files");

        var compareOption = new Option<bool>(
            name: "--compare",
            description: "Show semantic diff between original and generated");

        var command = new Command("roundtrip", "Full round-trip test: C# -> Mermaid -> C#")
        {
            inputArg,
            outputOption,
            compareOption,
            VerboseOption,
            QuietOption
        };

        command.AddAlias("r");

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var compare = context.ParseResult.GetValueForOption(compareOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteRoundtripCommand(input, output, compare, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteRoundtripCommand(
        FileInfo input, DirectoryInfo? outputDir, bool compare, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        if (!input.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: [yellow]{input.FullName}[/]");
            return 1;
        }

        var baseDir = outputDir?.FullName ?? Path.GetDirectoryName(input.FullName)!;
        var baseName = Path.GetFileNameWithoutExtension(input.Name);

        if (!quiet)
        {
            AnsiConsole.MarkupLine($"\n[blue]Round-trip test:[/] [cyan]{input.Name}[/]\n");
        }

        var stopwatch = Stopwatch.StartNew();
        SemanticModel? originalModel = null;
        SemanticModel? roundTripModel = null;
        string? mermaidContent = null;
        string? csharpContent = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Running round-trip...", async ctx =>
            {
                // Step 1: Parse original C#
                ctx.Status("[bold]Step 1/4:[/] Parsing original C#...");
                var csharpParser = new CSharpModelParser();
                var originalResult = await csharpParser.ParseAsync(ParserInput.FromFile(input.FullName));

                if (!originalResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing C# source:[/]");
                    foreach (var error in originalResult.Errors)
                        AnsiConsole.MarkupLine($"  [red]•[/] {error.Message}");
                    return;
                }

                originalModel = originalResult.Model;

                // Step 2: Generate Mermaid
                ctx.Status("[bold]Step 2/4:[/] Generating Mermaid diagram...");
                var mermaidGenerator = new MermaidClassDiagramGenerator();
                var mermaidResult = await mermaidGenerator.GenerateAsync(originalModel);
                mermaidContent = mermaidResult.Content;

                var mermaidPath = Path.Combine(baseDir, $"{baseName}.mmd");
                await File.WriteAllTextAsync(mermaidPath, mermaidContent!);

                // Step 3: Parse Mermaid back
                ctx.Status("[bold]Step 3/4:[/] Parsing generated Mermaid...");
                var mermaidParser = new MermaidClassDiagramParser();
                var mermaidParseResult = await mermaidParser.ParseAsync(ParserInput.FromContent(mermaidContent!));

                if (!mermaidParseResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing generated Mermaid:[/]");
                    foreach (var error in mermaidParseResult.Errors)
                        AnsiConsole.MarkupLine($"  [red]•[/] {error.Message}");
                    return;
                }

                roundTripModel = mermaidParseResult.Model;

                // Step 4: Generate C#
                ctx.Status("[bold]Step 4/4:[/] Generating C# from round-tripped model...");
                var csharpGenerator = new CSharpModelGenerator();
                var csharpResult = await csharpGenerator.GenerateAsync(roundTripModel);
                csharpContent = csharpResult.Content;

                var csharpPath = Path.Combine(baseDir, $"{baseName}.generated.cs");
                await File.WriteAllTextAsync(csharpPath, csharpContent!);
            });

        stopwatch.Stop();

        if (originalModel == null || roundTripModel == null)
        {
            return 1;
        }

        if (!quiet)
        {
            // Show comparison table
            AnsiConsole.WriteLine();
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Metric[/]").LeftAligned())
                .AddColumn(new TableColumn("[bold]Original[/]").Centered())
                .AddColumn(new TableColumn("[bold]Round-Trip[/]").Centered())
                .AddColumn(new TableColumn("[bold]Status[/]").Centered());

            var entitiesMatch = originalModel.Entities.Count == roundTripModel.Entities.Count;
            var relsMatch = originalModel.Relationships.Count == roundTripModel.Relationships.Count;

            table.AddRow(
                "Entities",
                originalModel.Entities.Count.ToString(),
                roundTripModel.Entities.Count.ToString(),
                entitiesMatch ? "[green]✓ Match[/]" : "[yellow]≠ Differ[/]");

            table.AddRow(
                "Relationships",
                originalModel.Relationships.Count.ToString(),
                roundTripModel.Relationships.Count.ToString(),
                relsMatch ? "[green]✓ Match[/]" : "[yellow]≠ Differ[/]");

            AnsiConsole.Write(table);

            if (compare || verbose)
            {
                // Classification comparison
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Entity Classifications:[/]");

                var classTable = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn("Classification")
                    .AddColumn("Original")
                    .AddColumn("Round-Trip")
                    .AddColumn("Status");

                var classifications = originalModel.Entities.Values
                    .Select(e => e.Classification)
                    .Union(roundTripModel.Entities.Values.Select(e => e.Classification))
                    .Distinct()
                    .OrderBy(c => c.ToString());

                foreach (var classification in classifications)
                {
                    var origCount = originalModel.Entities.Values.Count(e => e.Classification == classification);
                    var rtCount = roundTripModel.Entities.Values.Count(e => e.Classification == classification);
                    var match = origCount == rtCount;

                    classTable.AddRow(
                        classification.ToString(),
                        origCount.ToString(),
                        rtCount.ToString(),
                        match ? "[green]✓[/]" : "[yellow]≠[/]");
                }

                AnsiConsole.Write(classTable);
            }

            // Output files
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold]Generated Files:[/]");
            AnsiConsole.MarkupLine($"  [cyan]1.[/] {Path.Combine(baseDir, $"{baseName}.mmd")}");
            AnsiConsole.MarkupLine($"  [cyan]2.[/] {Path.Combine(baseDir, $"{baseName}.generated.cs")}");

            if (verbose && csharpContent != null)
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(csharpContent)
                {
                    Header = new PanelHeader("[green]Generated C# Code[/]"),
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 0)
                };
                AnsiConsole.Write(panel);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Round-trip complete![/] [dim]({stopwatch.ElapsedMilliseconds}ms)[/]");
        }

        return 0;
    }

    #endregion

    #region Scan Command

    private static Command BuildScanCommand()
    {
        var inputArg = new Argument<FileInfo>(
            name: "image",
            description: "Whiteboard or diagram image to scan (PNG, JPG, WEBP)");

        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Output file path for extracted Mermaid diagram");

        var contextOption = new Option<string?>(
            aliases: ["-c", "--context"],
            description: "Context hint for the diagram (e.g., 'e-commerce domain model')");

        var command = new Command("scan", "Scan whiteboard or diagram image using AI vision")
        {
            inputArg,
            outputOption,
            contextOption,
            VerboseOption,
            QuietOption
        };

        command.AddAlias("s");

        command.SetHandler(async (context) =>
        {
            var input = context.ParseResult.GetValueForArgument(inputArg);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var contextHint = context.ParseResult.GetValueForOption(contextOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteScanCommand(input, output, contextHint, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteScanCommand(
        FileInfo input, FileInfo? output, string? contextHint, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        // Check for API key using the multi-source resolution
        var apiKey = ClaudeProvider.ResolveApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            var userConfigPath = ClaudeProvider.GetUserConfigPath();
            var projectConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "docflow.json");

            AnsiConsole.WriteLine();
            var panel = new Panel(
                "[red]Claude API key not configured![/]\n\n" +
                "Configure your API key using one of these methods [dim](in priority order)[/]:\n\n" +
                "[bold]1. Environment variable:[/]\n" +
                "   [dim]Linux/macOS:[/] export ANTHROPIC_API_KEY='sk-ant-...'\n" +
                "   [dim]Windows:[/]     set ANTHROPIC_API_KEY=sk-ant-...\n" +
                "   [dim]PowerShell:[/]  $env:ANTHROPIC_API_KEY='sk-ant-...'\n\n" +
                $"[bold]2. User config:[/] [cyan]{userConfigPath}[/]\n" +
                "   [dim]{ \"anthropicApiKey\": \"sk-ant-...\" }[/]\n\n" +
                $"[bold]3. Project config:[/] [cyan]{projectConfigPath}[/]\n" +
                "   [dim]{ \"anthropicApiKey\": \"sk-ant-...\" }[/]\n\n" +
                "[dim]Get your API key at https://console.anthropic.com/[/]")
            {
                Header = new PanelHeader("[bold yellow]Configuration Required[/]"),
                Border = BoxBorder.Double,
                Padding = new Padding(2, 1)
            };
            AnsiConsole.Write(panel);
            return 1;
        }

        // Validate input file
        if (!input.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Image file not found: [yellow]{input.FullName}[/]");
            return 1;
        }

        var extension = input.Extension.ToLowerInvariant();
        var supportedFormats = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!supportedFormats.Contains(extension))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Unsupported image format: [yellow]{extension}[/]");
            AnsiConsole.MarkupLine($"[dim]Supported formats: {string.Join(", ", supportedFormats)}[/]");
            return 1;
        }

        if (!quiet)
        {
            AnsiConsole.MarkupLine($"\n[blue]Scanning[/] whiteboard image: [cyan]{input.Name}[/]\n");
        }

        var stopwatch = Stopwatch.StartNew();
        WhiteboardScanResult? result = null;

        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing image with AI...", async ctx =>
                {
                    // Create the scanner with MermaidParser for full model parsing
                    var mermaidParser = new MermaidClassDiagramParser();
                    var scanner = new WhiteboardScanner(new ClaudeProvider(), mermaidParser);

                    ctx.Status("Detecting diagram type...");

                    var scanInput = new WhiteboardInput
                    {
                        FilePath = input.FullName,
                        ContextHint = contextHint
                    };

                    ctx.Status("Extracting diagram elements...");

                    result = await scanner.ScanAsync(scanInput);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }

        stopwatch.Stop();

        if (result == null || !result.Success)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to scan whiteboard image");
            if (result?.Errors.Count > 0)
            {
                foreach (var error in result.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {error.Code}: {error.Message}");
                }
            }
            return 1;
        }

        // Display results
        if (!quiet)
        {
            // Show detection info
            AnsiConsole.WriteLine();
            var detectionTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Property")
                .AddColumn("Value");

            detectionTable.AddRow("Diagram Type", $"[cyan]{result.DetectedDiagramType}[/]");
            detectionTable.AddRow("Confidence", $"[green]{result.OverallConfidence:P0}[/]");
            detectionTable.AddRow("Entities Found", result.Model.Entities.Count.ToString());
            detectionTable.AddRow("Relationships", result.Model.Relationships.Count.ToString());

            if (result.Statistics != null)
            {
                detectionTable.AddRow("Analysis Time", $"{result.Statistics.AnalysisDuration.TotalSeconds:F1}s");
            }

            AnsiConsole.Write(detectionTable);

            // Show entities preview
            if (result.Model.Entities.Count > 0 && (verbose || result.Model.Entities.Count <= 10))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Detected Entities:[/]");

                var entityTable = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn("Entity")
                    .AddColumn("Properties")
                    .AddColumn("Operations");

                foreach (var entity in result.Model.Entities.Values.OrderBy(e => e.Name))
                {
                    entityTable.AddRow(
                        $"[cyan]{entity.Name}[/]",
                        entity.Properties.Count.ToString(),
                        entity.Operations.Count.ToString());
                }

                AnsiConsole.Write(entityTable);
            }

            // Show the generated Mermaid
            if (result.GeneratedOutputs.TryGetValue("Mermaid", out var mermaidResult) &&
                !string.IsNullOrEmpty(mermaidResult.Content))
            {
                AnsiConsole.WriteLine();
                var panel = new Panel(mermaidResult.Content)
                {
                    Header = new PanelHeader("[green]Generated Mermaid Diagram[/]"),
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 0)
                };
                AnsiConsole.Write(panel);

                // Save the output
                var outputPath = output?.FullName ?? Path.ChangeExtension(input.FullName, ".mmd");
                await File.WriteAllTextAsync(outputPath, mermaidResult.Content);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]Success![/] Diagram saved to [cyan]{outputPath}[/]");
            }

            // Show any warnings
            if (result.Warnings.Count > 0)
            {
                AnsiConsole.WriteLine();
                foreach (var warning in result.Warnings)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning.Message}");
                }
            }

            AnsiConsole.MarkupLine($"[dim]Completed in {stopwatch.ElapsedMilliseconds}ms[/]");
        }
        else
        {
            // Quiet mode: just output the path
            if (result.GeneratedOutputs.TryGetValue("Mermaid", out var mermaidResult) &&
                !string.IsNullOrEmpty(mermaidResult.Content))
            {
                var outputPath = output?.FullName ?? Path.ChangeExtension(input.FullName, ".mmd");
                await File.WriteAllTextAsync(outputPath, mermaidResult.Content);
                Console.WriteLine(outputPath);
            }
        }

        return 0;
    }

    #endregion

    #region Integrate Command

    private static Command BuildIntegrateCommand()
    {
        var command = new Command("integrate", "API integration and CDM mapping tools");
        command.AddAlias("i");

        command.AddCommand(BuildParseCommand());
        command.AddCommand(BuildAnalyzeCommand());
        command.AddCommand(BuildSlaCommand());
        command.AddCommand(BuildGenerateCommand());

        return command;
    }

    private static Command BuildParseCommand()
    {
        var specArg = new Argument<FileInfo>(
            name: "spec",
            description: "OpenAPI specification file (JSON or YAML)");

        var verboseOption = new Option<bool>(
            aliases: ["-v", "--verbose"],
            description: "Show detailed entity and endpoint information");

        var command = new Command("parse", "Parse OpenAPI spec and display entities/endpoints")
        {
            specArg,
            verboseOption
        };

        command.SetHandler(async (FileInfo spec, bool verbose) =>
        {
            ShowBanner();

            if (!spec.Exists)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {spec.FullName}");
                return;
            }

            AnsiConsole.MarkupLine("Parsing OpenAPI specification...");

            var parser = new OpenApiParser();
            var schemaResult = await parser.ParseSchemaAsync(ParserInput.FromFile(spec.FullName));

            if (!schemaResult.Success)
            {
                AnsiConsole.MarkupLine("[red]Error parsing OpenAPI spec:[/]");
                foreach (var error in schemaResult.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]*[/] {Markup.Escape(error.Code)}: {Markup.Escape(error.Message)}");
                }
                return;
            }

            var model = schemaResult.Model;
            var externalInfo = schemaResult.ExternalSystem!;
            var endpoints = schemaResult.Endpoints;

            // Header panel
            var headerPanel = new Panel(
                $"[bold]API:[/] [cyan]{Markup.Escape(externalInfo.Name)}[/] [dim]{Markup.Escape(externalInfo.Version)}[/]\n" +
                $"[bold]Entities:[/] {model.Entities.Count}\n" +
                $"[bold]Endpoints:[/] {endpoints.Count}")
            {
                Header = new PanelHeader("[bold blue]OpenAPI Specification[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(headerPanel);
            AnsiConsole.WriteLine();

            // Entities table
            var entityTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Entity")
                .AddColumn(new TableColumn("Properties").Centered())
                .AddColumn("Type");

            foreach (var entity in model.Entities.Values.OrderBy(e => e.Name))
            {
                entityTable.AddRow(
                    $"[cyan]{Markup.Escape(entity.Name)}[/]",
                    entity.Properties.Count.ToString(),
                    entity.Classification.ToString());
            }

            var entityPanel = new Panel(entityTable)
            {
                Header = new PanelHeader("[bold]Entities[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(entityPanel);
            AnsiConsole.WriteLine();

            // Endpoints table
            var endpointTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Method")
                .AddColumn("Path")
                .AddColumn("Operation");

            foreach (var endpoint in endpoints.OrderBy(e => e.Path).ThenBy(e => e.Method))
            {
                var methodColor = endpoint.Method switch
                {
                    Integration.Models.HttpMethod.Get => "green",
                    Integration.Models.HttpMethod.Post => "blue",
                    Integration.Models.HttpMethod.Put => "yellow",
                    Integration.Models.HttpMethod.Patch => "yellow",
                    Integration.Models.HttpMethod.Delete => "red",
                    _ => "white"
                };

                endpointTable.AddRow(
                    $"[{methodColor}]{endpoint.Method.ToString().ToUpperInvariant()}[/]",
                    Markup.Escape(endpoint.Path),
                    Markup.Escape(endpoint.Id ?? "-"));
            }

            var endpointPanel = new Panel(endpointTable)
            {
                Header = new PanelHeader("[bold]Endpoints[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(endpointPanel);

            // Verbose: show entity details
            if (verbose)
            {
                AnsiConsole.WriteLine();

                foreach (var entity in model.Entities.Values.OrderBy(e => e.Name))
                {
                    var propTable = new Table()
                        .Border(TableBorder.Simple)
                        .AddColumn("Property")
                        .AddColumn("Type")
                        .AddColumn("Required");

                    foreach (var prop in entity.Properties.OrderBy(p => p.Name))
                    {
                        propTable.AddRow(
                            Markup.Escape(prop.Name),
                            $"[dim]{Markup.Escape(prop.Type.Name)}[/]",
                            prop.IsRequired ? "[green]Yes[/]" : "[dim]No[/]");
                    }

                    var propPanel = new Panel(propTable)
                    {
                        Header = new PanelHeader($"[bold]{Markup.Escape(entity.Name)}[/]"),
                        Border = BoxBorder.Rounded
                    };
                    AnsiConsole.Write(propPanel);
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Parse completed[/]");
        }, specArg, verboseOption);

        return command;
    }

    private static Command BuildAnalyzeCommand()
    {
        var specArg = new Argument<FileInfo>(
            name: "spec",
            description: "OpenAPI specification file (JSON or YAML)");

        var cdmOption = new Option<string>(
            aliases: ["--cdm"],
            description: "Path to CDM C# files (file or directory)")
        {
            IsRequired = true
        };

        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Save IntegrationSpec as JSON");

        var thresholdOption = new Option<int?>(
            aliases: ["--threshold"],
            description: "Only show mappings below this confidence percentage (default: show all)");

        var command = new Command("analyze", "Analyze API schema and suggest CDM mappings")
        {
            specArg,
            cdmOption,
            outputOption,
            thresholdOption,
            VerboseOption,
            QuietOption
        };

        command.SetHandler(async (context) =>
        {
            var spec = context.ParseResult.GetValueForArgument(specArg);
            var cdmPath = context.ParseResult.GetValueForOption(cdmOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var threshold = context.ParseResult.GetValueForOption(thresholdOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteAnalyzeCommand(spec, cdmPath!, output, threshold, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteAnalyzeCommand(
        FileInfo spec, string cdmPath, FileInfo? output, int? threshold, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        // Validate inputs
        if (!spec.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] OpenAPI spec not found: [yellow]{spec.FullName}[/]");
            return 1;
        }

        if (!File.Exists(cdmPath) && !Directory.Exists(cdmPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] CDM path not found: [yellow]{cdmPath}[/]");
            return 1;
        }

        var stopwatch = Stopwatch.StartNew();
        SemanticModel? externalModel = null;
        SemanticModel? cdmModel = null;
        MappingResult? mappingResult = null;
        ExternalSystemInfo? externalSystem = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing API specification...", async ctx =>
            {
                // Parse OpenAPI spec
                ctx.Status("Parsing OpenAPI specification...");
                var openApiParser = new OpenApiParser();
                var schemaResult = await openApiParser.ParseSchemaAsync(ParserInput.FromFile(spec.FullName));

                if (!schemaResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing OpenAPI spec:[/]");
                    foreach (var error in schemaResult.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]*[/] {error.Code}: {error.Message}");
                    }
                    return;
                }

                externalModel = schemaResult.Model;
                externalSystem = schemaResult.ExternalSystem;

                // Parse CDM C# files
                ctx.Status("Parsing CDM C# files...");
                var csharpParser = new CSharpModelParser();
                cdmModel = new SemanticModel { Name = "CDM" };

                var cdmFiles = new List<string>();
                if (Directory.Exists(cdmPath))
                {
                    cdmFiles.AddRange(Directory.GetFiles(cdmPath, "*.cs", SearchOption.AllDirectories));
                }
                else
                {
                    cdmFiles.Add(cdmPath);
                }

                if (cdmFiles.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] No .cs files found in [cyan]{cdmPath}[/]");
                    return;
                }

                // Derive CDM name from directory or file name
                var cdmName = Directory.Exists(cdmPath)
                    ? new DirectoryInfo(cdmPath).Name
                    : Path.GetFileNameWithoutExtension(cdmPath);
                cdmModel.Name = cdmName;

                foreach (var file in cdmFiles)
                {
                    var parseResult = await csharpParser.ParseAsync(ParserInput.FromFile(file));
                    if (parseResult.Success)
                    {
                        foreach (var entity in parseResult.Model.Entities.Values)
                        {
                            if (!cdmModel.Entities.ContainsKey(entity.Id))
                            {
                                cdmModel.AddEntity(entity);
                            }
                        }
                    }
                }

                // Run CDM mapping
                ctx.Status("Generating mapping suggestions...");
                var mapper = new CdmMapper();
                mappingResult = await mapper.MapToCdmAsync(externalModel, cdmModel);
            });

        stopwatch.Stop();

        if (externalModel == null || cdmModel == null || mappingResult == null)
        {
            return 1;
        }

        if (!quiet)
        {
            DisplayMappingResults(
                externalSystem,
                cdmModel,
                mappingResult,
                threshold,
                verbose);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Analysis completed in {stopwatch.ElapsedMilliseconds}ms[/]");
        }

        // Save IntegrationSpec if output specified
        if (output != null)
        {
            var integrationSpec = new IntegrationSpec
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{externalSystem?.Name ?? "API"} Integration",
                Version = externalSystem?.Version ?? "1.0.0",
                ExternalSystem = externalSystem ?? new ExternalSystemInfo
                {
                    Name = "Unknown API",
                    BaseUrl = "https://api.example.com"
                },
                CanonicalModel = new CdmReference
                {
                    Name = cdmModel.Name ?? "CDM",
                    Version = "1.0.0",
                    SourcePath = Path.GetFullPath(cdmPath)
                },
                EntityMappings = mappingResult.EntityMappings,
                GeneratedAt = DateTime.UtcNow,
                ConfidenceReport = mappingResult.ConfidenceReport
            };

            var json = JsonSerializer.Serialize(integrationSpec, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(output.FullName, json);
            AnsiConsole.MarkupLine($"[green]Saved:[/] IntegrationSpec to [cyan]{output.FullName}[/]");
        }

        return 0;
    }

    private static void DisplayMappingResults(
        ExternalSystemInfo? externalSystem,
        SemanticModel cdmModel,
        MappingResult result,
        int? threshold,
        bool verbose)
    {
        // Header panel
        var headerContent = new Markup(
            $"[bold]External System:[/] [cyan]{externalSystem?.Name ?? "Unknown"}[/]" +
            (externalSystem?.Version != null ? $" [dim]v{externalSystem.Version}[/]" : "") +
            $"\n[bold]CDM:[/] [cyan]{cdmModel.Name}[/]" +
            $"\n[bold]Overall Confidence:[/] {FormatConfidence(result.ConfidenceReport.OverallConfidence)}");

        var headerPanel = new Panel(headerContent)
        {
            Header = new PanelHeader("[bold blue]CDM Mapping Analysis[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
        AnsiConsole.Write(headerPanel);
        AnsiConsole.WriteLine();

        // Confidence report panel
        var report = result.ConfidenceReport;
        var reportTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn(new TableColumn("Count").RightAligned());

        reportTable.AddRow("Total Mappings", report.TotalMappings.ToString());
        reportTable.AddRow("[green]High Confidence (>90%)[/]", $"[green]{report.HighConfidence}[/]");
        reportTable.AddRow("[yellow]Medium Confidence (70-90%)[/]", $"[yellow]{report.MediumConfidence}[/]");
        reportTable.AddRow("[red]Low Confidence (<70%)[/]", $"[red]{report.LowConfidence}[/]");
        reportTable.AddRow("[dim]Unmapped[/]", $"[dim]{report.Unmapped}[/]");

        var reportPanel = new Panel(reportTable)
        {
            Header = new PanelHeader("[bold]Mapping Confidence Report[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(reportPanel);
        AnsiConsole.WriteLine();

        // Entity mappings table
        var entityTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("External")
            .AddColumn("CDM Entity")
            .AddColumn(new TableColumn("Confidence").Centered())
            .AddColumn(new TableColumn("Status").Centered());

        foreach (var mapping in result.EntityMappings.OrderByDescending(m => m.Confidence))
        {
            // Filter by threshold if specified
            if (threshold.HasValue && mapping.Confidence * 100 >= threshold.Value)
            {
                continue;
            }

            var confidenceDisplay = FormatConfidence(mapping.Confidence);
            var statusDisplay = mapping.Status switch
            {
                IntegrationStatus.Verified => "[green]* Auto[/]",
                IntegrationStatus.NeedsReview => "[yellow]! Review[/]",
                _ => "[red]? Unmapped[/]"
            };

            var cdmEntity = mapping.CdmEntityName == mapping.ExternalEntityName && mapping.Confidence < 0.5
                ? "[dim]???[/]"
                : $"[cyan]{Markup.Escape(mapping.CdmEntityName)}[/]";

            entityTable.AddRow(
                Markup.Escape(mapping.ExternalEntityName),
                cdmEntity,
                confidenceDisplay,
                statusDisplay);
        }

        // Add unmapped entities
        foreach (var unmapped in result.UnmappedEntities)
        {
            entityTable.AddRow(
                Markup.Escape(unmapped),
                "[dim]???[/]",
                "[red]0%[/]",
                "[red]? Unmapped[/]");
        }

        var entityPanel = new Panel(entityTable)
        {
            Header = new PanelHeader("[bold]Entity Mappings[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(entityPanel);

        // Field mappings (verbose mode or high confidence entities)
        if (verbose)
        {
            AnsiConsole.WriteLine();

            foreach (var mapping in result.EntityMappings.Where(m => m.FieldMappings.Count > 0))
            {
                // Filter by threshold if specified
                if (threshold.HasValue && mapping.Confidence * 100 >= threshold.Value)
                {
                    continue;
                }

                var fieldTable = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn("External Field")
                    .AddColumn("CDM Field")
                    .AddColumn(new TableColumn("Confidence").Centered())
                    .AddColumn("Reasoning");

                foreach (var field in mapping.FieldMappings.OrderByDescending(f => f.Confidence))
                {
                    var targetField = field.TargetField == "???"
                        ? "[dim]???[/]"
                        : $"[cyan]{Markup.Escape(field.TargetField)}[/]";

                    fieldTable.AddRow(
                        Markup.Escape(field.SourceField),
                        targetField,
                        FormatConfidence(field.Confidence),
                        Markup.Escape(field.Reasoning ?? ""));
                }

                var fieldPanel = new Panel(fieldTable)
                {
                    Header = new PanelHeader($"[bold]{Markup.Escape(mapping.ExternalEntityName)} -> {Markup.Escape(mapping.CdmEntityName)}[/]"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(fieldPanel);
                AnsiConsole.WriteLine();
            }
        }
    }

    private static string FormatConfidence(double confidence)
    {
        var percent = (int)(confidence * 100);
        return percent switch
        {
            > 90 => $"[green]{percent}%[/]",
            > 70 => $"[yellow]{percent}%[/]",
            > 0 => $"[red]{percent}%[/]",
            _ => "[dim]0%[/]"
        };
    }

    #endregion

    #region SLA Command

    private static Command BuildSlaCommand()
    {
        var urlArg = new Argument<string>(
            name: "url",
            description: "The endpoint URL to validate");

        var expectedOption = new Option<string>(
            aliases: ["--expected"],
            description: "Expected max data age (e.g., 30s, 5m, 1h)")
        {
            IsRequired = true
        };

        var samplesOption = new Option<int>(
            aliases: ["--samples"],
            getDefaultValue: () => 10,
            description: "Number of samples to collect");

        var intervalOption = new Option<string>(
            aliases: ["--interval"],
            getDefaultValue: () => "5s",
            description: "Time between samples (e.g., 5s, 1m)");

        var timestampPathOption = new Option<string?>(
            aliases: ["--timestamp-path"],
            description: "JSON path to timestamp field (e.g., $.data.updated_at)");

        var headerOption = new Option<string[]>(
            aliases: ["--header"],
            description: "Add header (repeatable, e.g., --header \"Authorization=Bearer xxx\")")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Save detailed report as JSON");

        var command = new Command("sla", "Validate data freshness against SLA requirements")
        {
            urlArg,
            expectedOption,
            samplesOption,
            intervalOption,
            timestampPathOption,
            headerOption,
            outputOption,
            VerboseOption,
            QuietOption
        };

        command.SetHandler(async (context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArg);
            var expected = context.ParseResult.GetValueForOption(expectedOption);
            var samples = context.ParseResult.GetValueForOption(samplesOption);
            var interval = context.ParseResult.GetValueForOption(intervalOption);
            var timestampPath = context.ParseResult.GetValueForOption(timestampPathOption);
            var headers = context.ParseResult.GetValueForOption(headerOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteSlaCommand(
                url, expected!, samples, interval!, timestampPath,
                headers ?? [], output, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteSlaCommand(
        string url, string expectedStr, int sampleCount, string intervalStr,
        string? timestampPath, string[] headers, FileInfo? output,
        bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        // Parse duration strings
        if (!TryParseDuration(expectedStr, out var expectedMaxAge))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid duration format: [yellow]{expectedStr}[/]");
            AnsiConsole.MarkupLine("[dim]Examples: 30s, 5m, 1h, 500ms[/]");
            return 1;
        }

        if (!TryParseDuration(intervalStr, out var sampleInterval))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid interval format: [yellow]{intervalStr}[/]");
            return 1;
        }

        // Parse headers
        var authHeaders = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            var parts = header.Split('=', 2);
            if (parts.Length == 2)
            {
                authHeaders[parts[0].Trim()] = parts[1].Trim();
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Invalid header format: [dim]{header}[/] (expected key=value)");
            }
        }

        // Build the request
        var request = new SlaValidationRequest
        {
            EndpointUrl = url,
            ExpectedMaxAge = expectedMaxAge,
            SampleCount = sampleCount,
            SampleInterval = sampleInterval,
            TimestampJsonPath = timestampPath,
            AuthHeaders = authHeaders.Count > 0 ? authHeaders : null
        };

        if (!quiet)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[blue]SLA Validation:[/] [cyan]{url}[/]");
            AnsiConsole.MarkupLine($"[blue]Expected Max Age:[/] [cyan]{expectedMaxAge}[/]");
            if (!string.IsNullOrEmpty(timestampPath))
            {
                AnsiConsole.MarkupLine($"[blue]Timestamp Path:[/] [cyan]{timestampPath}[/]");
            }
            AnsiConsole.WriteLine();
        }

        SlaValidationReport? report = null;
        var samples = new List<DataFreshnessSample>();

        try
        {
            var validator = new SlaValidator();

            // Use live progress display
            await AnsiConsole.Progress()
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"[cyan]Collecting {sampleCount} samples...[/]", maxValue: sampleCount);

                    for (int i = 0; i < sampleCount; i++)
                    {
                        try
                        {
                            var sample = await validator.QuickCheckAsync(request);
                            samples.Add(sample);

                            if (verbose && !quiet)
                            {
                                var ageStr = sample.DataAge.HasValue
                                    ? FormatDuration(sample.DataAge.Value)
                                    : "[dim]N/A[/]";
                                var status = sample.DataAge.HasValue && sample.DataAge.Value <= expectedMaxAge
                                    ? "[green]PASS[/]"
                                    : "[red]FAIL[/]";
                            }
                        }
                        catch (Exception ex)
                        {
                            samples.Add(new DataFreshnessSample
                            {
                                SampledAt = DateTime.UtcNow,
                                Success = false,
                                HttpStatusCode = 0,
                                ResponseTime = TimeSpan.Zero
                            });

                            if (verbose)
                            {
                                AnsiConsole.MarkupLine($"[yellow]Sample {i + 1} failed:[/] {ex.Message}");
                            }
                        }

                        task.Increment(1);

                        // Wait between samples (except for the last one)
                        if (i < sampleCount - 1)
                        {
                            await Task.Delay(sampleInterval);
                        }
                    }
                });

            // Analyze the results
            report = AnalyzeSamples(samples, request);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (verbose)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }

        if (!quiet)
        {
            DisplaySlaResults(report, verbose);
        }

        // Save report if output specified
        if (output != null)
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(output.FullName, json);
            AnsiConsole.MarkupLine($"[green]Saved:[/] Report to [cyan]{output.FullName}[/]");
        }

        // Return exit code based on verdict
        return report.Verdict switch
        {
            SlaVerdict.Compliant => 0,
            SlaVerdict.MarginallyCompliant => 0,
            SlaVerdict.MinorViolation => 1,
            SlaVerdict.SevereViolation => 2,
            _ => 1
        };
    }

    private static SlaValidationReport AnalyzeSamples(
        List<DataFreshnessSample> samples,
        SlaValidationRequest request)
    {
        var validSamples = samples.Where(s => s.Success && s.DataAge.HasValue).ToList();

        if (validSamples.Count == 0)
        {
            return new SlaValidationReport
            {
                EndpointUrl = request.EndpointUrl,
                ExpectedMaxAge = request.ExpectedMaxAge,
                Verdict = SlaVerdict.Unknown,
                TotalSamples = samples.Count,
                ValidSamples = 0,
                Samples = samples,
                Notes = ["No valid samples with extractable timestamps"]
            };
        }

        var dataAges = validSamples.Select(s => s.DataAge!.Value).ToList();
        var samplesOverSla = validSamples.Count(s => s.DataAge > request.ExpectedMaxAge);
        var compliancePercentage = (double)(validSamples.Count - samplesOverSla) / validSamples.Count * 100;

        var avgAge = TimeSpan.FromTicks((long)dataAges.Average(a => a.Ticks));
        var maxAge = dataAges.Max();

        SlaVerdict verdict;
        if (compliancePercentage >= 99)
        {
            verdict = SlaVerdict.Compliant;
        }
        else if (compliancePercentage >= 95)
        {
            verdict = SlaVerdict.MarginallyCompliant;
        }
        else if (avgAge > request.ExpectedMaxAge * 10)
        {
            verdict = SlaVerdict.SevereViolation;
        }
        else
        {
            verdict = SlaVerdict.MinorViolation;
        }

        var notes = new List<string>();

        if (verdict == SlaVerdict.SevereViolation)
        {
            notes.Add($"SEVERE: Average data age ({avgAge}) is significantly higher than SLA ({request.ExpectedMaxAge})");
            notes.Add("This may indicate stale data caching or upstream data delays.");
        }

        if (maxAge > request.ExpectedMaxAge * 2)
        {
            notes.Add($"Maximum observed data age ({maxAge}) is more than 2x the SLA requirement.");
        }

        return new SlaValidationReport
        {
            EndpointUrl = request.EndpointUrl,
            ExpectedMaxAge = request.ExpectedMaxAge,
            ActualAverageAge = avgAge,
            ActualMaxAge = maxAge,
            ActualMinAge = dataAges.Min(),
            SamplesOverSla = samplesOverSla,
            TotalSamples = samples.Count,
            ValidSamples = validSamples.Count,
            CompliancePercentage = compliancePercentage,
            Verdict = verdict,
            Samples = samples,
            Notes = notes,
            AverageResponseTime = TimeSpan.FromMilliseconds(
                validSamples.Average(s => s.ResponseTime.TotalMilliseconds)),
            MaxResponseTime = validSamples.Max(s => s.ResponseTime),
            MinResponseTime = validSamples.Min(s => s.ResponseTime)
        };
    }

    private static void DisplaySlaResults(SlaValidationReport report, bool verbose)
    {
        AnsiConsole.WriteLine();

        // Verdict panel with color
        var (verdictEmoji, verdictColor, verdictText) = report.Verdict switch
        {
            SlaVerdict.Compliant => ("*", "green", "COMPLIANT"),
            SlaVerdict.MarginallyCompliant => ("!", "yellow", "MARGINALLY COMPLIANT"),
            SlaVerdict.MinorViolation => ("X", "red", "MINOR VIOLATION"),
            SlaVerdict.SevereViolation => ("!!!", "red bold", "SEVERE VIOLATION"),
            _ => ("?", "dim", "UNKNOWN")
        };

        var resultContent = new StringBuilder();
        resultContent.AppendLine($"[{verdictColor}]{verdictEmoji} {verdictText}[/]");
        resultContent.AppendLine();
        resultContent.AppendLine($"[bold]Expected Max Age:[/]    {report.ExpectedMaxAge}");

        if (report.ActualAverageAge.HasValue)
        {
            var avgColor = report.ActualAverageAge.Value <= report.ExpectedMaxAge ? "green" : "red";
            resultContent.AppendLine($"[bold]Actual Average Age:[/]  [{avgColor}]{report.ActualAverageAge}[/]");
        }

        if (report.ActualMaxAge.HasValue)
        {
            var maxColor = report.ActualMaxAge.Value <= report.ExpectedMaxAge ? "green" : "red";
            resultContent.AppendLine($"[bold]Actual Max Age:[/]      [{maxColor}]{report.ActualMaxAge}[/]");
        }

        if (report.ActualMinAge.HasValue)
        {
            var minColor = report.ActualMinAge.Value <= report.ExpectedMaxAge ? "green" : "red";
            resultContent.AppendLine($"[bold]Actual Min Age:[/]      [{minColor}]{report.ActualMinAge}[/]");
        }

        resultContent.AppendLine();

        var complianceColor = report.CompliancePercentage >= 95 ? "green"
            : report.CompliancePercentage >= 70 ? "yellow"
            : "red";
        var compliantCount = report.ValidSamples - report.SamplesOverSla;
        resultContent.AppendLine($"[bold]Compliance:[/] [{complianceColor}]{report.CompliancePercentage:F1}%[/] ({compliantCount}/{report.ValidSamples} samples within SLA)");

        var panel = new Panel(new Markup(resultContent.ToString()))
        {
            Header = new PanelHeader("[bold]Results[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
        AnsiConsole.Write(panel);

        // Notes if any
        if (report.Notes.Count > 0)
        {
            AnsiConsole.WriteLine();
            foreach (var note in report.Notes)
            {
                AnsiConsole.MarkupLine($"[yellow]Note:[/] {note}");
            }
        }

        // Verbose: show individual samples
        if (verbose && report.Samples.Count > 0)
        {
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("#").Centered())
                .AddColumn("Sampled At")
                .AddColumn("Data Age")
                .AddColumn("Response")
                .AddColumn(new TableColumn("Status").Centered());

            for (int i = 0; i < report.Samples.Count; i++)
            {
                var sample = report.Samples[i];

                var ageStr = sample.DataAge.HasValue
                    ? FormatDuration(sample.DataAge.Value)
                    : "[dim]N/A[/]";

                var responseStr = $"{sample.ResponseTime.TotalMilliseconds:F0}ms";

                string status;
                if (!sample.Success)
                {
                    status = $"[red]ERR {sample.HttpStatusCode}[/]";
                }
                else if (!sample.DataAge.HasValue)
                {
                    status = "[yellow]NO TS[/]";
                }
                else if (sample.DataAge.Value <= report.ExpectedMaxAge)
                {
                    status = "[green]PASS[/]";
                }
                else
                {
                    status = "[red]FAIL[/]";
                }

                table.AddRow(
                    (i + 1).ToString(),
                    sample.SampledAt.ToString("HH:mm:ss"),
                    ageStr,
                    responseStr,
                    status);
            }

            var samplesPanel = new Panel(table)
            {
                Header = new PanelHeader("[bold]Samples[/]"),
                Border = BoxBorder.Rounded
            };
            AnsiConsole.Write(samplesPanel);
        }

        // Response time stats
        if (report.AverageResponseTime.HasValue)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                $"[dim]Response Time: avg {report.AverageResponseTime.Value.TotalMilliseconds:F0}ms, " +
                $"max {report.MaxResponseTime?.TotalMilliseconds:F0}ms, " +
                $"min {report.MinResponseTime?.TotalMilliseconds:F0}ms[/]");
        }
    }

    private static bool TryParseDuration(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Match patterns like "30s", "5m", "1h", "500ms"
        var match = Regex.Match(input.Trim(), @"^(\d+(?:\.\d+)?)(ms|s|m|h)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        var value = double.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToLowerInvariant();

        result = unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(value),
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => TimeSpan.Zero
        };

        return true;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{duration:hh\\:mm\\:ss}";
        if (duration.TotalMinutes >= 1)
            return $"{duration:mm\\:ss}";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F1}s";
        return $"{duration.TotalMilliseconds:F0}ms";
    }

    #endregion

    #region Generate Command

    private static Command BuildGenerateCommand()
    {
        var specArg = new Argument<FileInfo>(
            name: "spec",
            description: "OpenAPI specification file (JSON or YAML)");

        var cdmOption = new Option<string>(
            aliases: ["--cdm"],
            description: "Path to CDM C# files (file or directory)")
        {
            IsRequired = true
        };

        var outputOption = new Option<DirectoryInfo>(
            aliases: ["-o", "--output"],
            description: "Output directory for generated code")
        {
            IsRequired = true
        };

        var namespaceOption = new Option<string>(
            aliases: ["-n", "--namespace"],
            getDefaultValue: () => "Integration.Generated",
            description: "Namespace for generated code");

        var generateOption = new Option<string?>(
            aliases: ["--generate"],
            description: "Comma-separated types to generate: dtos,mappers,client,validators (default: all)");

        var command = new Command("generate", "Generate integration code from OpenAPI spec")
        {
            specArg,
            cdmOption,
            outputOption,
            namespaceOption,
            generateOption,
            VerboseOption,
            QuietOption
        };

        command.SetHandler(async (context) =>
        {
            var spec = context.ParseResult.GetValueForArgument(specArg);
            var cdmPath = context.ParseResult.GetValueForOption(cdmOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var ns = context.ParseResult.GetValueForOption(namespaceOption);
            var generate = context.ParseResult.GetValueForOption(generateOption);
            var verbose = context.ParseResult.GetValueForOption(VerboseOption);
            var quiet = context.ParseResult.GetValueForOption(QuietOption);

            context.ExitCode = await ExecuteGenerateCommand(
                spec, cdmPath!, output!, ns!, generate, verbose, quiet);
        });

        return command;
    }

    private static async Task<int> ExecuteGenerateCommand(
        FileInfo spec, string cdmPath, DirectoryInfo output, string ns,
        string? generateTypes, bool verbose, bool quiet)
    {
        if (!quiet) ShowBanner();

        // Validate inputs
        if (!spec.Exists)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] OpenAPI spec not found: [yellow]{spec.FullName}[/]");
            return 1;
        }

        if (!File.Exists(cdmPath) && !Directory.Exists(cdmPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] CDM path not found: [yellow]{cdmPath}[/]");
            return 1;
        }

        // Parse generate types
        var genDtos = true;
        var genMappers = true;
        var genClient = true;
        var genValidators = true;

        if (!string.IsNullOrEmpty(generateTypes))
        {
            var types = generateTypes.ToLowerInvariant().Split(',').Select(t => t.Trim()).ToHashSet();
            genDtos = types.Contains("dtos");
            genMappers = types.Contains("mappers");
            genClient = types.Contains("client");
            genValidators = types.Contains("validators");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        SchemaParseResult? schemaResult = null;
        SemanticModel? cdmModel = null;
        MappingResult? mappingResult = null;
        IntegrationCodeGenerator.GeneratorResult? generatorResult = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating integration code...", async ctx =>
            {
                // Parse OpenAPI spec
                ctx.Status("Parsing OpenAPI specification...");
                var openApiParser = new OpenApiParser();
                schemaResult = await openApiParser.ParseSchemaAsync(ParserInput.FromFile(spec.FullName));

                if (!schemaResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Error parsing OpenAPI spec:[/]");
                    foreach (var error in schemaResult.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]*[/] {error.Code}: {error.Message}");
                    }
                    return;
                }

                // Parse CDM C# files
                ctx.Status("Parsing CDM C# files...");
                var csharpParser = new CSharpModelParser();
                cdmModel = new SemanticModel { Name = "CDM" };

                var cdmFiles = new List<string>();
                if (Directory.Exists(cdmPath))
                {
                    cdmFiles.AddRange(Directory.GetFiles(cdmPath, "*.cs", SearchOption.AllDirectories));
                    cdmModel.Name = new DirectoryInfo(cdmPath).Name;
                }
                else
                {
                    cdmFiles.Add(cdmPath);
                    cdmModel.Name = Path.GetFileNameWithoutExtension(cdmPath);
                }

                foreach (var file in cdmFiles)
                {
                    var parseResult = await csharpParser.ParseAsync(ParserInput.FromFile(file));
                    if (parseResult.Success)
                    {
                        foreach (var entity in parseResult.Model.Entities.Values)
                        {
                            if (!cdmModel.Entities.ContainsKey(entity.Id))
                            {
                                cdmModel.AddEntity(entity);
                            }
                        }
                    }
                }

                // Run CDM mapping
                ctx.Status("Generating mappings...");
                var mapper = new CdmMapper();
                mappingResult = await mapper.MapToCdmAsync(schemaResult.Model, cdmModel);

                // Generate code
                ctx.Status("Generating code files...");
                var generator = new IntegrationCodeGenerator(new IntegrationCodeGenerator.GeneratorOptions
                {
                    Namespace = ns,
                    GenerateDtos = genDtos,
                    GenerateMappers = genMappers,
                    GenerateClient = genClient,
                    GenerateValidators = genValidators
                });

                generatorResult = generator.Generate(schemaResult, cdmModel, mappingResult);
            });

        stopwatch.Stop();

        if (schemaResult == null || cdmModel == null || mappingResult == null || generatorResult == null)
        {
            return 1;
        }

        if (!generatorResult.Success)
        {
            AnsiConsole.MarkupLine("[red]Error generating code:[/]");
            foreach (var error in generatorResult.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]*[/] {error}");
            }
            return 1;
        }

        // Create output directory
        if (!output.Exists)
        {
            output.Create();
        }

        // Write files
        var writtenFiles = new List<(string path, int lines, bool isNew)>();

        foreach (var file in generatorResult.Files)
        {
            var fullPath = Path.Combine(output.FullName, file.RelativePath);
            var dir = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var isNew = !File.Exists(fullPath);
            await File.WriteAllTextAsync(fullPath, file.Content);
            writtenFiles.Add((file.RelativePath, file.LineCount, isNew));

            if (verbose)
            {
                var status = isNew ? "[green]New[/]" : "[yellow]Updated[/]";
                AnsiConsole.MarkupLine($"  {status} {file.RelativePath} ({file.LineCount} lines)");
            }
        }

        if (!quiet)
        {
            DisplayGeneratorResults(
                spec.Name,
                schemaResult.ExternalSystem,
                cdmPath,
                output.FullName,
                writtenFiles,
                generatorResult,
                stopwatch.ElapsedMilliseconds);
        }

        return 0;
    }

    private static void DisplayGeneratorResults(
        string specName,
        ExternalSystemInfo? externalSystem,
        string cdmPath,
        string outputPath,
        List<(string path, int lines, bool isNew)> files,
        IntegrationCodeGenerator.GeneratorResult result,
        long elapsedMs)
    {
        AnsiConsole.WriteLine();

        // Header
        AnsiConsole.MarkupLine($"[blue]Generating integration code...[/]");
        AnsiConsole.MarkupLine($"[bold]Source:[/] {specName} ({externalSystem?.Name ?? "API"} v{externalSystem?.Version ?? "1.0"})");
        AnsiConsole.MarkupLine($"[bold]CDM:[/] {cdmPath}");
        AnsiConsole.MarkupLine($"[bold]Output:[/] {outputPath}");
        AnsiConsole.WriteLine();

        // Files table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("File")
            .AddColumn(new TableColumn("Lines").RightAligned())
            .AddColumn(new TableColumn("Status").Centered());

        foreach (var (path, lines, isNew) in files.OrderBy(f => f.path))
        {
            var status = isNew ? "[green]* New[/]" : "[yellow]~ Updated[/]";
            table.AddRow(path, lines.ToString(), status);
        }

        AnsiConsole.MarkupLine("[bold]Generated Files:[/]");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLine($"[green]*[/] Generated {files.Count} files ({result.TotalLines} lines)");

        if (result.MappingsNeedingReview > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]![/] {result.MappingsNeedingReview} mappings need manual review (see TODO comments)");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Completed in {elapsedMs}ms[/]");
    }

    #endregion

    #region Helpers

    private static void ShowBanner()
    {
        AnsiConsole.Write(new FigletText("DocFlow")
            .Color(Color.Blue));

        AnsiConsole.MarkupLine($"[dim]v{Version} - Intelligent Documentation and Modeling Toolkit[/]");
        AnsiConsole.WriteLine();
    }

    private static void PrintModelSummary(SemanticModel model, bool verbose)
    {
        AnsiConsole.MarkupLine($"[green]Parsed:[/] {model.Entities.Count} entities, {model.Relationships.Count} relationships");

        if (verbose)
        {
            AnsiConsole.WriteLine();
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Entity")
                .AddColumn("Classification")
                .AddColumn("Properties")
                .AddColumn("Operations");

            foreach (var entity in model.Entities.Values.OrderBy(e => e.Name))
            {
                var classification = entity.Classification switch
                {
                    EntityClassification.AggregateRoot => "[bold magenta]AggregateRoot[/]",
                    EntityClassification.Entity => "[cyan]Entity[/]",
                    EntityClassification.ValueObject => "[green]ValueObject[/]",
                    EntityClassification.DomainService => "[yellow]Service[/]",
                    EntityClassification.Interface => "[blue]Interface[/]",
                    EntityClassification.Enum => "[dim]Enum[/]",
                    _ => entity.Classification.ToString()
                };

                table.AddRow(
                    entity.Name,
                    classification,
                    entity.Properties.Count.ToString(),
                    entity.Operations.Count.ToString());
            }

            AnsiConsole.Write(table);
        }
    }

    #endregion
}
