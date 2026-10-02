namespace Javacstr.Core.Models;

public enum TranslationDirection
{
    JavaToCSharp,
    CSharpToJava
}

public class TranslationOptions
{
    public TranslationDirection Direction { get; set; } = TranslationDirection.JavaToCSharp;
    public bool ConvertMethodCasing { get; set; } = false;
    public bool ConvertProperties { get; set; } = true;
    public bool UseFileScopedNamespaces { get; set; } = true;
    public bool MapRegex { get; set; } = true;
    public bool MapHttpClient { get; set; } = true;
    public bool MapGui { get; set; } = true;
}

public class TranslationResult
{
    public bool Success { get; set; }
    public string OutputCode { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public static TranslationResult Ok(string code, List<string>? warnings = null)
    {
        return new TranslationResult
        {
            Success = true,
            OutputCode = code,
            Warnings = warnings ?? new List<string>()
        };
    }

    public static TranslationResult Fail(string error)
    {
        return new TranslationResult
        {
            Success = false,
            Errors = new List<string> { error }
        };
    }
}
