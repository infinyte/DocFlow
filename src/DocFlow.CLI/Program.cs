using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using DocFlow.AI.Providers;
using DocFlow.CodeAnalysis.CSharp;
using DocFlow.CodeGen.CSharp;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
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
                    var panel = new Panel(generateResult.Content!)
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
