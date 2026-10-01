using System.IO;
using Javacstr.Core.Models;

namespace Javacstr.Core.Translation;

public class CodeTranslator
{
    private readonly TranslationOptions _options;

    public CodeTranslator(TranslationOptions? options = null)
    {
        _options = options ?? new TranslationOptions();
    }

    public TranslationResult TranslateJavaToCSharp(string javaCode)
    {
        var translator = new JavaToCSharpTranslator(_options);
        return translator.Translate(javaCode);
    }

    public TranslationResult TranslateCSharpToJava(string csharpCode)
    {
        var translator = new CSharpToJavaTranslator(_options);
        return translator.Translate(csharpCode);
    }

    public TranslationResult Translate(string sourceCode, TranslationDirection direction)
    {
        return direction switch
        {
            TranslationDirection.JavaToCSharp => TranslateJavaToCSharp(sourceCode),
            TranslationDirection.CSharpToJava => TranslateCSharpToJava(sourceCode),
            _ => TranslationResult.Fail("Unknown translation direction")
        };
    }

    public static TranslationDirection DetectDirection(string filePath, string content = "")
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".java") return TranslationDirection.JavaToCSharp;
        if (ext == ".cs") return TranslationDirection.CSharpToJava;

        // Content heuristics
        if (content.Contains("package ") || content.Contains("@Override") || content.Contains("System.out."))
        {
            return TranslationDirection.JavaToCSharp;
        }
        if (content.Contains("namespace ") || content.Contains("using System") || content.Contains("Console.WriteLine"))
        {
            return TranslationDirection.CSharpToJava;
        }

        return TranslationDirection.JavaToCSharp;
    }
}
