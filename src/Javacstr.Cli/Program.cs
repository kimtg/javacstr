using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Spectre.Console;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 && !Console.IsInputRedirected)
        {
            ShowHelp();
            return 0;
        }

        string inputPath = "";
        string outputPath = "";
        string? targetLang = null;
        bool showDiff = false;
        bool interactive = false;
        var options = new TranslationOptions();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg is "-h" or "--help" or "help")
            {
                ShowHelp();
                return 0;
            }
            if (arg is "-v" or "--version")
            {
                ShowVersion();
                return 0;
            }
            if (arg is "-i" or "--interactive")
            {
                interactive = true;
            }
            else if (arg is "-o" or "--output")
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Option '{arg}' requires an output path.[/]");
                    return 1;
                }
                outputPath = args[++i];
            }
            else if (arg is "--to")
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("-"))
                {
                    AnsiConsole.MarkupLine("[red]Error: Option '--to' requires a target language ('csharp' or 'java').[/]");
                    return 1;
                }
                string lang = args[++i].ToLowerInvariant();
                if (lang is not ("csharp" or "cs" or "java"))
                {
                    AnsiConsole.MarkupLine($"[red]Error: Invalid target language '{Markup.Escape(lang)}'. Supported options: 'csharp' ('cs') or 'java'.[/]");
                    return 1;
                }
                targetLang = lang;
            }
            else if (arg is "--diff")
            {
                showDiff = true;
            }
            else if (arg is "--no-casing")
            {
                options.ConvertMethodCasing = false;
            }
            else if (arg is "--block-namespaces")
            {
                options.UseFileScopedNamespaces = false;
            }
            else if (arg is "--no-properties")
            {
                options.ConvertProperties = false;
            }
            else if (arg is "--no-regex")
            {
                options.MapRegex = false;
            }
            else if (arg is "--no-http")
            {
                options.MapHttpClient = false;
            }
            else if (arg.StartsWith("-") && arg != "-")
            {
                AnsiConsole.MarkupLine($"[red]Error: Unrecognized option '{Markup.Escape(arg)}'.[/]");
                AnsiConsole.MarkupLine("Run [yellow]javacstr --help[/] for a list of available options.");
                return 1;
            }
            else
            {
                inputPath = arg;
            }
        }

        if (interactive)
        {
            RunInteractive(options);
            return 0;
        }

        // Support standard input if '-' is passed as inputPath or input is redirected without a path
        if (inputPath == "-" || (string.IsNullOrEmpty(inputPath) && Console.IsInputRedirected))
        {
            return ProcessStdIn(outputPath, targetLang, showDiff, options);
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            AnsiConsole.MarkupLine("[red]Error: Input file, directory, or '-' (stdin) required.[/]");
            ShowHelp();
            return 1;
        }

        if (Directory.Exists(inputPath))
        {
            return ProcessDirectory(inputPath, outputPath, targetLang, options);
        }

        if (File.Exists(inputPath))
        {
            return ProcessFile(inputPath, outputPath, targetLang, showDiff, options);
        }

        AnsiConsole.MarkupLine($"[red]Error: Input path '{Markup.Escape(inputPath)}' does not exist.[/]");
        return 1;
    }

    private static void ShowVersion()
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        AnsiConsole.MarkupLine($"[bold green]javacstr[/] version [bold cyan]{version}[/] (.NET 10.0)");
        AnsiConsole.MarkupLine("High-fidelity bidirectional Java <-> C# translator");
    }

    private static void ShowHelp()
    {
        var rule = new Rule("[bold green]javacstr - Java <-> C# Bidirectional Translator[/]");
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[bold]Usage:[/] javacstr <input-path> [[options]]");
        AnsiConsole.MarkupLine("       javacstr - [[options]]             (read from stdin)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]General Options:[/]");
        AnsiConsole.MarkupLine("  [yellow]-o, --output <path>[/]      Output file or directory");
        AnsiConsole.MarkupLine("  [yellow]--to <csharp|java>[/]       Target language (auto-detected if omitted)");
        AnsiConsole.MarkupLine("  [yellow]--diff[/]                   Show colorized side-by-side diff");
        AnsiConsole.MarkupLine("  [yellow]-i, --interactive[/]        Launch interactive REPL mode");
        AnsiConsole.MarkupLine("  [yellow]-v, --version[/]            Display version information");
        AnsiConsole.MarkupLine("  [yellow]-h, --help[/]               Show this help message");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Translation Flags:[/]");
        AnsiConsole.MarkupLine("  [yellow]--no-casing[/]              Preserve original method casing (skip PascalCase/camelCase conversion)");
        AnsiConsole.MarkupLine("  [yellow]--block-namespaces[/]       Use C# block namespaces instead of file-scoped namespaces");
        AnsiConsole.MarkupLine("  [yellow]--no-properties[/]          Preserve fields instead of converting C# properties to Java getters/setters");
        AnsiConsole.MarkupLine("  [yellow]--no-regex[/]               Disable automatic regular expression mapping");
        AnsiConsole.MarkupLine("  [yellow]--no-http[/]                Disable HttpClient mapping");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Examples:[/] ");
        AnsiConsole.MarkupLine("  javacstr App.java -o App.cs");
        AnsiConsole.MarkupLine("  javacstr Service.cs -o Service.java");
        AnsiConsole.MarkupLine("  javacstr ./src -o ./converted --to csharp");
        AnsiConsole.MarkupLine("  javacstr Solution.java --diff");
        AnsiConsole.MarkupLine("  cat Solution.java | javacstr - --to csharp");
    }

    private static int ProcessStdIn(string outputFile, string? targetLang, bool showDiff, TranslationOptions options)
    {
        string sourceCode = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            AnsiConsole.MarkupLine("[red]Error: Standard input is empty.[/]");
            return 1;
        }

        var direction = DetermineDirection("stdin", sourceCode, targetLang);
        var translator = new CodeTranslator(options);
        var result = translator.Translate(sourceCode, direction);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Translation failed:[/] {Markup.Escape(string.Join("\n", result.Errors))}");
            return 1;
        }

        if (result.Warnings.Count > 0)
        {
            foreach (var w in result.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(w)}");
            }
        }

        string fromName = direction == TranslationDirection.JavaToCSharp ? "Java" : "C#";
        string toName = direction == TranslationDirection.JavaToCSharp ? "C#" : "Java";

        if (showDiff)
        {
            ShowDiffView(sourceCode, result.OutputCode, fromName, toName);
        }

        if (!string.IsNullOrEmpty(outputFile))
        {
            try
            {
                string? dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputFile, result.OutputCode);
                AnsiConsole.MarkupLine($"[dim]Saved output to: {Markup.Escape(outputFile)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error saving file '{Markup.Escape(outputFile)}': {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }
        else if (!showDiff)
        {
            Console.WriteLine(result.OutputCode);
        }

        return 0;
    }

    private static int ProcessFile(string inputFile, string outputFile, string? targetLang, bool showDiff, TranslationOptions options)
    {
        string sourceCode;
        try
        {
            sourceCode = File.ReadAllText(inputFile);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error reading '{Markup.Escape(inputFile)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var direction = DetermineDirection(inputFile, sourceCode, targetLang);
        var translator = new CodeTranslator(options);
        var result = translator.Translate(sourceCode, direction);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Translation failed:[/] {Markup.Escape(string.Join("\n", result.Errors))}");
            return 1;
        }

        if (result.Warnings.Count > 0)
        {
            foreach (var w in result.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(w)}");
            }
        }

        string fromName = direction == TranslationDirection.JavaToCSharp ? "Java" : "C#";
        string toName = direction == TranslationDirection.JavaToCSharp ? "C#" : "Java";

        AnsiConsole.MarkupLine($"[green]Successfully translated[/] [bold]{Markup.Escape(inputFile)}[/] ([cyan]{fromName}[/]) -> [cyan]{toName}[/]");

        if (showDiff)
        {
            ShowDiffView(sourceCode, result.OutputCode, fromName, toName);
        }

        if (!string.IsNullOrEmpty(outputFile))
        {
            try
            {
                string? dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputFile, result.OutputCode);
                AnsiConsole.MarkupLine($"[dim]Saved output to: {Markup.Escape(outputFile)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error saving file '{Markup.Escape(outputFile)}': {Markup.Escape(ex.Message)}[/]");
                return 1;
            }
        }
        else if (!showDiff)
        {
            Console.WriteLine(result.OutputCode);
        }

        return 0;
    }

    private static int ProcessDirectory(string inputDir, string outputDir, string? targetLang, TranslationOptions options)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            AnsiConsole.MarkupLine("[red]Error: Output directory (-o) must be specified for batch translation.[/]");
            return 1;
        }

        var tasks = new List<(string sourceFile, TranslationDirection direction, string targetExt)>();

        string[] allJavaFiles;
        string[] allCsFiles;
        try
        {
            allJavaFiles = Directory.GetFiles(inputDir, "*.java", SearchOption.AllDirectories);
            allCsFiles = Directory.GetFiles(inputDir, "*.cs", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error scanning directory '{Markup.Escape(inputDir)}': {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        if (targetLang is "csharp" or "cs")
        {
            foreach (var f in allJavaFiles)
                tasks.Add((f, TranslationDirection.JavaToCSharp, ".cs"));
        }
        else if (targetLang is "java")
        {
            foreach (var f in allCsFiles)
                tasks.Add((f, TranslationDirection.CSharpToJava, ".java"));
        }
        else
        {
            // Auto-detect based on directory contents
            if (allJavaFiles.Length > 0 && allCsFiles.Length == 0)
            {
                AnsiConsole.MarkupLine("[dim]Auto-detected Java files -> translating to C#[/]");
                foreach (var f in allJavaFiles)
                    tasks.Add((f, TranslationDirection.JavaToCSharp, ".cs"));
            }
            else if (allCsFiles.Length > 0 && allJavaFiles.Length == 0)
            {
                AnsiConsole.MarkupLine("[dim]Auto-detected C# files -> translating to Java[/]");
                foreach (var f in allCsFiles)
                    tasks.Add((f, TranslationDirection.CSharpToJava, ".java"));
            }
            else if (allJavaFiles.Length > 0 && allCsFiles.Length > 0)
            {
                AnsiConsole.MarkupLine("[dim]Detected mixed Java and C# files -> translating both[/]");
                foreach (var f in allJavaFiles)
                    tasks.Add((f, TranslationDirection.JavaToCSharp, ".cs"));
                foreach (var f in allCsFiles)
                    tasks.Add((f, TranslationDirection.CSharpToJava, ".java"));
            }
        }

        if (tasks.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No translatable (.java or .cs) files found in '{Markup.Escape(inputDir)}'.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold blue]Translating {tasks.Count} file(s) to '{Markup.Escape(outputDir)}'...[/]");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var translator = new CodeTranslator(options);
        int successCount = 0;
        int failCount = 0;

        foreach (var (file, direction, targetExt) in tasks)
        {
            string relative = Path.GetRelativePath(inputDir, file);
            string outRelative = Path.ChangeExtension(relative, targetExt);
            string outFile = Path.Combine(outputDir, outRelative);

            try
            {
                string source = File.ReadAllText(file);
                var res = translator.Translate(source, direction);

                if (res.Success)
                {
                    string? d = Path.GetDirectoryName(outFile);
                    if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                    File.WriteAllText(outFile, res.OutputCode);
                    AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(relative)} [dim]->[/] [green]{Markup.Escape(outRelative)}[/]");
                    successCount++;
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(relative)}: [red]{Markup.Escape(string.Join("; ", res.Errors))}[/]");
                    failCount++;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(relative)}: [red]{Markup.Escape(ex.Message)}[/]");
                failCount++;
            }
        }

        sw.Stop();

        AnsiConsole.WriteLine();
        var summaryTable = new Table().Border(TableBorder.Rounded);
        summaryTable.AddColumn("Metric");
        summaryTable.AddColumn("Value");
        summaryTable.AddRow("Total Files", tasks.Count.ToString());
        summaryTable.AddRow("Succeeded", $"[green]{successCount}[/]");
        summaryTable.AddRow("Failed", failCount > 0 ? $"[red]{failCount}[/]" : "[dim]0[/]");
        summaryTable.AddRow("Time Elapsed", $"{sw.Elapsed.TotalMilliseconds:F0} ms");
        AnsiConsole.Write(summaryTable);

        return failCount == 0 ? 0 : 1;
    }

    private static void RunInteractive(TranslationOptions options)
    {
        var rule = new Rule("[bold green]javacstr Interactive REPL[/]");
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[dim]Commands: [yellow]:switch[/] (toggle direction), [yellow]:clear[/] (clear screen), [yellow]:help[/] (help), [yellow]:quit[/] (exit)[/]");

        var dirChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose translation direction:")
                .AddChoices("Java -> C#", "C# -> Java"));

        var direction = dirChoice.StartsWith("Java")
            ? TranslationDirection.JavaToCSharp
            : TranslationDirection.CSharpToJava;

        while (true)
        {
            string dirLabel = direction == TranslationDirection.JavaToCSharp ? "Java -> C#" : "C# -> Java";
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold cyan]Direction:[/] [yellow]{dirLabel}[/]");
            AnsiConsole.MarkupLine("[dim]Enter code below. Type [bold]###[/] on an empty line to translate, or [bold]:quit[/] to exit:[/]");

            var sb = new StringBuilder();
            bool shouldExit = false;
            bool switchRequested = false;

            while (true)
            {
                string? line = Console.ReadLine();
                if (line == null) { shouldExit = true; break; }
                string trimmed = line.Trim();
                if (trimmed is ":quit" or ":exit" or ":q") { shouldExit = true; break; }
                if (trimmed is ":switch" or ":s") { switchRequested = true; break; }
                if (trimmed is ":clear" or ":cls") { AnsiConsole.Clear(); break; }
                if (trimmed is ":help" or ":h")
                {
                    AnsiConsole.MarkupLine("[yellow]REPL Commands:[/] :switch (toggle direction), :clear (clear screen), :quit (exit), ### (submit code)");
                    break;
                }
                if (trimmed == "###") break;
                sb.AppendLine(line);
            }

            if (shouldExit)
            {
                AnsiConsole.MarkupLine("[green]Exited javacstr REPL.[/]");
                break;
            }

            if (switchRequested)
            {
                direction = direction == TranslationDirection.JavaToCSharp
                    ? TranslationDirection.CSharpToJava
                    : TranslationDirection.JavaToCSharp;
                AnsiConsole.MarkupLine($"[cyan]Switched direction to: {(direction == TranslationDirection.JavaToCSharp ? "Java -> C#" : "C# -> Java")}[/]");
                continue;
            }

            string sourceCode = sb.ToString();
            if (string.IsNullOrWhiteSpace(sourceCode)) continue;

            var translator = new CodeTranslator(options);
            var res = translator.Translate(sourceCode, direction);

            if (res.Success)
            {
                string outLang = direction == TranslationDirection.JavaToCSharp ? "C#" : "Java";
                var panel = new Panel(Markup.Escape(res.OutputCode))
                    .Header($"[bold green]Translated Output ({outLang})[/]")
                    .Border(BoxBorder.Rounded);
                AnsiConsole.Write(panel);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Translation error:[/] {Markup.Escape(string.Join("\n", res.Errors))}");
            }
        }
    }

    private static void ShowDiffView(string source, string translated, string fromLang, string toLang)
    {
        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn(new TableColumn($"[cyan bold]{fromLang} (Source)[/]").LeftAligned());
        table.AddColumn(new TableColumn($"[green bold]{toLang} (Translated)[/]").LeftAligned());

        table.AddRow(
            new Panel(Markup.Escape(source.Trim())).Border(BoxBorder.None),
            new Panel(Markup.Escape(translated.Trim())).Border(BoxBorder.None)
        );

        AnsiConsole.Write(table);
    }

    private static TranslationDirection DetermineDirection(string path, string content, string? targetLang)
    {
        if (targetLang is "csharp" or "cs") return TranslationDirection.JavaToCSharp;
        if (targetLang is "java") return TranslationDirection.CSharpToJava;
        return CodeTranslator.DetectDirection(path, content);
    }
}
