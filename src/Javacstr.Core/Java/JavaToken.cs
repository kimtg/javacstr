namespace Javacstr.Core.Java;

public enum JavaTokenType
{
    Keyword,
    Identifier,
    StringLiteral,
    CharLiteral,
    NumberLiteral,
    Operator,
    Delimiter,
    Comment,
    Whitespace,
    EndOfFile
}

public class JavaToken
{
    public JavaTokenType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }

    public JavaToken(JavaTokenType type, string text, int line, int column)
    {
        Type = type;
        Text = text;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Type}: '{Text}' at ({Line}:{Column})";
}
