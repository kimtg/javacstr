using System;
using System.Collections.Generic;
using System.Text;

namespace Javacstr.Core.Java;

public class JavaLexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _column = 1;

    private static readonly HashSet<string> Keywords = new()
    {
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char",
        "class", "const", "continue", "default", "do", "double", "else", "enum",
        "extends", "final", "finally", "float", "for", "goto", "if", "implements",
        "import", "instanceof", "int", "interface", "long", "native", "new",
        "package", "private", "protected", "public", "return", "short", "static",
        "strictfp", "super", "switch", "synchronized", "this", "throw", "throws",
        "transient", "try", "void", "volatile", "while", "record", "var", "yield",
        "sealed", "non-sealed", "permits", "true", "false", "null"
    };

    public JavaLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public List<JavaToken> Tokenize(bool keepWhitespaceAndComments = false)
    {
        var tokens = new List<JavaToken>();
        _pos = 0;
        _line = 1;
        _column = 1;

        while (!IsAtEnd())
        {
            char c = Peek();

            if (char.IsWhiteSpace(c))
            {
                var ws = ReadWhitespace();
                if (keepWhitespaceAndComments) tokens.Add(ws);
            }
            else if (c == '/' && PeekNext() == '/')
            {
                var comment = ReadLineComment();
                if (keepWhitespaceAndComments) tokens.Add(comment);
            }
            else if (c == '/' && PeekNext() == '*')
            {
                var comment = ReadBlockComment();
                if (keepWhitespaceAndComments) tokens.Add(comment);
            }
            else if (c == '"')
            {
                tokens.Add(ReadStringLiteral());
            }
            else if (c == '\'')
            {
                tokens.Add(ReadCharLiteral());
            }
            else if (char.IsDigit(c))
            {
                tokens.Add(ReadNumberLiteral());
            }
            else if (char.IsLetter(c) || c == '_' || c == '$')
            {
                tokens.Add(ReadIdentifierOrKeyword());
            }
            else
            {
                tokens.Add(ReadOperatorOrDelimiter());
            }
        }

        tokens.Add(new JavaToken(JavaTokenType.EndOfFile, string.Empty, _line, _column));
        return tokens;
    }

    private bool IsAtEnd() => _pos >= _source.Length;

    private char Peek() => IsAtEnd() ? '\0' : _source[_pos];

    private char PeekNext() => _pos + 1 >= _source.Length ? '\0' : _source[_pos + 1];

    private char Advance()
    {
        char c = _source[_pos++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private JavaToken ReadWhitespace()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();
        while (!IsAtEnd() && char.IsWhiteSpace(Peek()))
        {
            sb.Append(Advance());
        }
        return new JavaToken(JavaTokenType.Whitespace, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadLineComment()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();
        sb.Append(Advance()); // /
        sb.Append(Advance()); // /
        while (!IsAtEnd() && Peek() != '\n')
        {
            sb.Append(Advance());
        }
        return new JavaToken(JavaTokenType.Comment, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadBlockComment()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();
        sb.Append(Advance()); // /
        sb.Append(Advance()); // *
        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/')
            {
                sb.Append(Advance());
                sb.Append(Advance());
                break;
            }
            sb.Append(Advance());
        }
        return new JavaToken(JavaTokenType.Comment, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadStringLiteral()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        // Check for text block """
        if (_pos + 2 < _source.Length && _source[_pos + 1] == '"' && _source[_pos + 2] == '"')
        {
            sb.Append(Advance());
            sb.Append(Advance());
            sb.Append(Advance());
            while (!IsAtEnd())
            {
                if (Peek() == '"' && PeekNext() == '"' && _pos + 2 < _source.Length && _source[_pos + 2] == '"')
                {
                    sb.Append(Advance());
                    sb.Append(Advance());
                    sb.Append(Advance());
                    break;
                }
                sb.Append(Advance());
            }
            return new JavaToken(JavaTokenType.StringLiteral, sb.ToString(), startLine, startCol);
        }

        sb.Append(Advance()); // "
        while (!IsAtEnd() && Peek() != '"')
        {
            if (Peek() == '\\')
            {
                sb.Append(Advance());
                if (!IsAtEnd()) sb.Append(Advance());
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (!IsAtEnd() && Peek() == '"')
        {
            sb.Append(Advance());
        }
        return new JavaToken(JavaTokenType.StringLiteral, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadCharLiteral()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();
        sb.Append(Advance()); // '
        while (!IsAtEnd() && Peek() != '\'')
        {
            if (Peek() == '\\')
            {
                sb.Append(Advance());
                if (!IsAtEnd()) sb.Append(Advance());
            }
            else
            {
                sb.Append(Advance());
            }
        }
        if (!IsAtEnd() && Peek() == '\'')
        {
            sb.Append(Advance());
        }
        return new JavaToken(JavaTokenType.CharLiteral, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadNumberLiteral()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();

        if (Peek() == '0' && (PeekNext() == 'x' || PeekNext() == 'X'))
        {
            sb.Append(Advance()); // 0
            sb.Append(Advance()); // x
            while (!IsAtEnd() && (char.IsAsciiHexDigit(Peek()) || Peek() == '_'))
            {
                sb.Append(Advance());
            }
        }
        else
        {
            while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '_'))
            {
                sb.Append(Advance());
            }
            if (Peek() == '.' && char.IsDigit(PeekNext()))
            {
                sb.Append(Advance()); // .
                while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '_'))
                {
                    sb.Append(Advance());
                }
            }
            if (Peek() == 'e' || Peek() == 'E')
            {
                sb.Append(Advance());
                if (Peek() == '+' || Peek() == '-') sb.Append(Advance());
                while (!IsAtEnd() && char.IsDigit(Peek())) sb.Append(Advance());
            }
        }

        if (!IsAtEnd() && (Peek() is 'l' or 'L' or 'f' or 'F' or 'd' or 'D'))
        {
            sb.Append(Advance());
        }

        return new JavaToken(JavaTokenType.NumberLiteral, sb.ToString(), startLine, startCol);
    }

    private JavaToken ReadIdentifierOrKeyword()
    {
        int startLine = _line;
        int startCol = _column;
        var sb = new StringBuilder();
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '$'))
        {
            sb.Append(Advance());
        }
        string text = sb.ToString();
        var type = Keywords.Contains(text) ? JavaTokenType.Keyword : JavaTokenType.Identifier;
        return new JavaToken(type, text, startLine, startCol);
    }

    private JavaToken ReadOperatorOrDelimiter()
    {
        int startLine = _line;
        int startCol = _column;
        char c = Advance();

        // Two-character operators
        string twoChar = c.ToString() + Peek();
        if (twoChar is "==" or "!=" or "<=" or ">=" or "&&" or "||" or "++" or "--" or "+=" or "-=" or "*=" or "/=" or "%=" or "&=" or "|=" or "^=" or "->" or "::")
        {
            Advance();
            return new JavaToken(JavaTokenType.Operator, twoChar, startLine, startCol);
        }

        if (c is '(' or ')' or '{' or '}' or '[' or ']' or ';' or ',' or '.' or '@' or '?' or ':')
        {
            return new JavaToken(JavaTokenType.Delimiter, c.ToString(), startLine, startCol);
        }

        return new JavaToken(JavaTokenType.Operator, c.ToString(), startLine, startCol);
    }
}
