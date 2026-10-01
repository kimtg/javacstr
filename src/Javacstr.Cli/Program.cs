using System;
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

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            ShowHelp();
            return 0;
        }

        if (args[0] is "--interactive" or "-i")
        {
            RunInteractive();
            return 0;
        }

        string inputPath = "";
        string outputPath = "";
        string? targetLang = null;
        bool showDiff = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "-o" or "--output" && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if (arg is "--to" && i + 1 < args.Length)
            {
                targetLang = args[++i].ToLowerInvariant();
            }
            else if (arg is "--diff")
            {
                showDiff = true;
            }
            else if (!arg.StartsWith("-"))
            {
                inputPath = arg;
            }
        }

        if (string.IsNullOrEmpty(inputPath))
        {
            AnsiConsole.MarkupLine("[red]Error: Input file or directory required.[/]");
            ShowHelp();
            return 1;
        }

        if (Directory.Exists(inputPath))
        {
            return ProcessDirectory(inputPath, outputPath, targetLang);
        }

        if (File.Exists(inputPath))
        {
            return ProcessFile(inputPath, outputPath, targetLang, showDiff);
        }

        AnsiConsole.MarkupLine($"[red]Error: Input path '{inputPath}' does not exist.[/]");
        return 1;
    }

    private static void ShowHelp()
    {
        var rule = new Rule("[bold green]javacstr - Java <-> C# Bidirectional Translator[/]");
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[bold]Usage:[/] javacstr <input-path> [[options]]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Options:[/]");
        AnsiConsole.MarkupLine("  [yellow]-o, --output <path>[/]      Output file or directory");
        AnsiConsole.MarkupLine("  [yellow]--to <csharp|java>[/]       Target language (auto-detected if omitted)");
        AnsiConsole.MarkupLine("  [yellow]--diff[/]                   Show colorized diff of translation");
        AnsiConsole.MarkupLine("  [yellow]-i, --interactive[/]        Launch interactive REPL mode");
        AnsiConsole.MarkupLine("  [yellow]-h, --help[/]               Show this help message");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Examples:[/] ");
        AnsiConsole.MarkupLine("  javacstr App.java -o App.cs");
        AnsiConsole.MarkupLine("  javacstr Service.cs -o Service.java");
        AnsiConsole.MarkupLine("  javacstr ./src -o ./converted --to csharp");
        AnsiConsole.MarkupLine("  javacstr Solution.java --diff");
    }

    private static int ProcessFile(string inputFile, string outputFile, string? targetLang, bool showDiff)
    {
        string sourceCode = File.ReadAllText(inputFile);
        var direction = DetermineDirection(inputFile, sourceCode, targetLang);

        var translator = new CodeTranslator();
        var result = translator.Translate(sourceCode, direction);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Translation failed:[/] {Markup.Escape(string.Join("\n", result.Errors))}");
            return 1;
        }

        string fromName = direction == TranslationDirection.JavaToCSharp ? "Java" : "C#";
        string toName = direction == TranslationDirection.JavaToCSharp ? "C#" : "Java";

        AnsiConsole.MarkupLine($"[green]Successfully translated[/] [bold]{inputFile}[/] ([cyan]{fromName}[/]) -> [cyan]{toName}[/]");

        if (showDiff)
        {
            ShowDiffView(sourceCode, result.OutputCode, fromName, toName);
        }

        if (!string.IsNullOrEmpty(outputFile))
        {
            string? dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputFile, result.OutputCode);
            AnsiConsole.MarkupLine($"[dim]Saved output to: {outputFile}[/]");
        }
        else if (!showDiff)
        {
            Console.WriteLine(result.OutputCode);
        }

        return 0;
    }

    private static int ProcessDirectory(string inputDir, string outputDir, string? targetLang)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            AnsiConsole.MarkupLine("[red]Error: Output directory (-o) must be specified for batch translation.[/]");
            return 1;
        }

        bool toCSharp = targetLang == "csharp" || targetLang == "cs";
        string searchExt = toCSharp ? "*.java" : "*.cs";
        string targetExt = toCSharp ? ".cs" : ".java";

        var files = Directory.GetFiles(inputDir, searchExt, SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No {searchExt} files found in {inputDir}.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold blue]Translating {files.Length} files...[/]");
        var translator = new CodeTranslator();
        int successCount = 0;

        foreach (var file in files)
        {
            string relative = Path.GetRelativePath(inputDir, file);
            string outRelative = Path.ChangeExtension(relative, targetExt);
            string outFile = Path.Combine(outputDir, outRelative);

            string source = File.ReadAllText(file);
            var direction = toCSharp ? TranslationDirection.JavaToCSharp : TranslationDirection.CSharpToJava;
            var res = translator.Translate(source, direction);

            if (res.Success)
            {
                string? d = Path.GetDirectoryName(outFile);
                if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                File.WriteAllText(outFile, res.OutputCode);
                AnsiConsole.MarkupLine($"  [green]OK[/] {relative} -> {outRelative}");
                successCount++;
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]FAIL[/] {relative}: {string.Join("; ", res.Errors)}");
            }
        }

        AnsiConsole.MarkupLine($"[bold green]Batch translation complete: {successCount}/{files.Length} succeeded.[/]");
        return successCount == files.Length ? 0 : 1;
    }

    private static void RunInteractive()
    {
        var rule = new Rule("[bold green]javacstr Interactive REPL[/]");
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("Select translation direction:");

        var dirChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose direction:")
                .AddChoices(new[] { "Java -> C#", "C# -> Java" }));

        var direction = dirChoice.StartsWith("Java")
            ? TranslationDirection.JavaToCSharp
            : TranslationDirection.CSharpToJava;

        AnsiConsole.MarkupLine("[yellow]Type or paste your code below. Type '###' on an empty line when finished:[/]");
        var sb = new StringBuilder();
        while (true)
        {
            string? line = Console.ReadLine();
            if (line == null || line.Trim() == "###") break;
            sb.AppendLine(line);
        }

        string sourceCode = sb.ToString();
        var translator = new CodeTranslator();
        var res = translator.Translate(sourceCode, direction);

        if (res.Success)
        {
            AnsiConsole.WriteLine();
            var panel = new Panel(res.OutputCode)
                .Header($"[bold green]Translated Output ({ (direction == TranslationDirection.JavaToCSharp ? "C#" : "Java") })[/]")
                .Border(BoxBorder.Rounded);
            AnsiConsole.Write(panel);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Translation error:[/] {string.Join("\n", res.Errors)}");
        }
    }

    private static void ShowDiffView(string source, string translated, string fromLang, string toLang)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn($"[cyan]{fromLang} (Source)[/]").LeftAligned());
        table.AddColumn(new TableColumn($"[green]{toLang} (Translated)[/]").LeftAligned());

        table.AddRow(
            new Panel(Markup.Escape(source)).Border(BoxBorder.None),
            new Panel(Markup.Escape(translated)).Border(BoxBorder.None)
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
