using System;
using System.Collections.Generic;
using System.Text;

namespace Javacstr.Core.Java;

public class JavaParser
{
    private readonly List<JavaToken> _tokens;
    private int _index;

    private static readonly HashSet<string> Modifiers = new()
    {
        "public", "protected", "private", "static", "final", "abstract",
        "synchronized", "native", "strictfp", "transient", "volatile", "default"
    };

    public JavaParser(List<JavaToken> tokens)
    {
        _tokens = tokens ?? new List<JavaToken>();
    }

    public CompilationUnitNode ParseCompilationUnit()
    {
        var cu = new CompilationUnitNode();

        // 1. Optional package declaration
        if (MatchKeyword("package"))
        {
            var pkgName = ConsumeQualifiedName();
            ConsumeDelimiter(";");
            cu.Package = new PackageDeclarationNode { Name = pkgName };
        }

        // 2. Zero or more import statements
        while (MatchKeyword("import"))
        {
            bool isStatic = MatchKeyword("static");
            var path = ConsumeImportPath();
            ConsumeDelimiter(";");
            cu.Imports.Add(new ImportDeclarationNode { IsStatic = isStatic, Path = path });
        }

        // 3. Types
        while (!IsAtEnd())
        {
            var type = TryParseTypeDeclaration();
            if (type != null)
            {
                cu.Types.Add(type);
            }
            else
            {
                Advance();
            }
        }

        return cu;
    }

    private TypeDeclarationNode? TryParseTypeDeclaration()
    {
        var annotations = ParseAnnotations();
        var modifiers = ParseModifiers();

        if (IsAtEnd()) return null;

        string kind = Current().Text;
        if (kind is not ("class" or "interface" or "enum" or "record"))
        {
            return null;
        }
        Advance(); // consume kind

        string name = ConsumeIdentifier();
        string? typeParams = null;
        if (MatchDelimiter("<"))
        {
            typeParams = "<" + ConsumeUntilBalanced('>', '<') + ">";
        }

        string? extendsType = null;
        if (MatchKeyword("extends"))
        {
            extendsType = ConsumeTypeReference();
        }

        var implementsTypes = new List<string>();
        if (MatchKeyword("implements"))
        {
            do
            {
                implementsTypes.Add(ConsumeTypeReference());
            } while (MatchDelimiter(","));
        }

        var typeNode = new TypeDeclarationNode
        {
            Annotations = annotations,
            Modifiers = modifiers,
            Kind = kind,
            Name = name,
            TypeParameters = typeParams,
            ExtendsType = extendsType,
            ImplementsTypes = implementsTypes
        };

        if (MatchDelimiter("{"))
        {
            while (!IsAtEnd() && !CheckDelimiter("}"))
            {
                var member = TryParseMember(typeNode.Name);
                if (member != null)
                {
                    typeNode.Members.Add(member);
                }
                else
                {
                    Advance();
                }
            }
            ConsumeDelimiter("}");
        }

        return typeNode;
    }

    private MemberNode? TryParseMember(string enclosingTypeName)
    {
        var annotations = ParseAnnotations();
        var modifiers = ParseModifiers();

        if (IsAtEnd() || CheckDelimiter("}")) return null;

        // Check for nested type
        if (Current().Text is "class" or "interface" or "enum" or "record")
        {
            _index -= (annotations.Count + modifiers.Count); // backtrack to let TryParseTypeDeclaration parse it
            return TryParseTypeDeclaration();
        }

        // Check if it's a constructor: Name matches enclosing class and followed by '('
        if (Current().Text == enclosingTypeName && PeekNext().Text == "(")
        {
            string ctorName = ConsumeIdentifier();
            ConsumeDelimiter("(");
            var parameters = ParseParameterList();
            ConsumeDelimiter(")");

            var throwsList = ParseThrowsClause();

            string? initializer = null;
            var body = new BlockStatementNode();

            if (MatchDelimiter("{"))
            {
                // Check if first statement is super(...) or this(...)
                if (CheckKeyword("super") || CheckKeyword("this"))
                {
                    string initTarget = Current().Text;
                    if (PeekNext().Text == "(")
                    {
                        Advance(); // super or this
                        ConsumeDelimiter("(");
                        string args = ConsumeUntilBalanced(')', '(');
                        initializer = $"{initTarget}({args})";
                        ConsumeDelimiter(";");
                    }
                }

                body = ParseBlockBody();
            }

            return new ConstructorDeclarationNode
            {
                Annotations = annotations,
                Modifiers = modifiers,
                Name = ctorName,
                Parameters = parameters,
                Throws = throwsList,
                InitializerCall = initializer,
                Body = body
            };
        }

        // Could be a generic method e.g. <T> void foo()
        string? methodTypeParams = null;
        if (MatchDelimiter("<"))
        {
            methodTypeParams = "<" + ConsumeUntilBalanced('>', '<') + ">";
        }

        // Return type or field type
        string memberType = ConsumeTypeReference();

        // Member name
        string memberName = ConsumeIdentifier();

        // If followed by '(', it's a method!
        if (MatchDelimiter("("))
        {
            var parameters = ParseParameterList();
            ConsumeDelimiter(")");

            var throwsList = ParseThrowsClause();

            BlockStatementNode? body = null;
            if (MatchDelimiter("{"))
            {
                body = ParseBlockBody();
            }
            else
            {
                ConsumeDelimiter(";"); // Abstract or interface method
            }

            return new MethodDeclarationNode
            {
                Annotations = annotations,
                Modifiers = modifiers,
                TypeParameters = methodTypeParams,
                ReturnType = memberType,
                Name = memberName,
                Parameters = parameters,
                Throws = throwsList,
                Body = body
            };
        }
        else
        {
            // Field declaration
            string? initializer = null;
            if (MatchOperator("="))
            {
                initializer = ConsumeUntilStatementSemicolon();
            }
            ConsumeDelimiter(";");

            return new FieldDeclarationNode
            {
                Annotations = annotations,
                Modifiers = modifiers,
                Type = memberType,
                Name = memberName,
                Initializer = initializer
            };
        }
    }

    private List<ParameterNode> ParseParameterList()
    {
        var parameters = new List<ParameterNode>();
        if (CheckDelimiter(")")) return parameters;

        do
        {
            var annos = ParseAnnotations();
            bool isFinal = MatchKeyword("final");
            string paramType = ConsumeTypeReference();
            string paramName = ConsumeIdentifier();

            parameters.Add(new ParameterNode
            {
                Annotations = annos,
                Type = paramType,
                Name = paramName
            });
        } while (MatchDelimiter(","));

        return parameters;
    }

    private List<string> ParseThrowsClause()
    {
        var list = new List<string>();
        if (MatchKeyword("throws"))
        {
            do
            {
                list.Add(ConsumeTypeReference());
            } while (MatchDelimiter(","));
        }
        return list;
    }

    public BlockStatementNode ParseBlockBody()
    {
        var block = new BlockStatementNode();
        while (!IsAtEnd() && !CheckDelimiter("}"))
        {
            var stmt = ParseStatement();
            if (stmt != null)
            {
                block.Statements.Add(stmt);
            }
        }
        ConsumeDelimiter("}");
        return block;
    }

    public StatementNode? ParseStatement()
    {
        if (IsAtEnd() || CheckDelimiter("}")) return null;

        if (MatchDelimiter("{"))
        {
            return ParseBlockBody();
        }

        if (MatchKeyword("if"))
        {
            ConsumeDelimiter("(");
            string cond = ConsumeUntilBalanced(')', '(');
            var thenStmt = ParseStatement() ?? new BlockStatementNode();
            StatementNode? elseStmt = null;
            if (MatchKeyword("else"))
            {
                elseStmt = ParseStatement();
            }
            return new IfStatementNode
            {
                Condition = cond,
                ThenStatement = thenStmt,
                ElseStatement = elseStmt
            };
        }

        if (MatchKeyword("while"))
        {
            ConsumeDelimiter("(");
            string cond = ConsumeUntilBalanced(')', '(');
            var body = ParseStatement() ?? new BlockStatementNode();
            return new WhileStatementNode { Condition = cond, Body = body };
        }

        if (MatchKeyword("do"))
        {
            var body = ParseStatement() ?? new BlockStatementNode();
            ConsumeKeyword("while");
            ConsumeDelimiter("(");
            string cond = ConsumeUntilBalanced(')', '(');
            ConsumeDelimiter(";");
            return new DoWhileStatementNode { Body = body, Condition = cond };
        }

        if (MatchKeyword("for"))
        {
            ConsumeDelimiter("(");
            // Check if foreach e.g. (String s : list)
            int colonIdx = FindTokenInParen(":");
            if (colonIdx > 0)
            {
                string varDecl = ConsumeUntilDelimiter(":");
                ConsumeDelimiter(":");
                string iterable = ConsumeUntilBalanced(')', '(');
                var parts = varDecl.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string vType = parts.Length > 1 ? parts[^2] : "var";
                string vName = parts.Length > 0 ? parts[^1] : "item";
                var body = ParseStatement() ?? new BlockStatementNode();
                return new ForEachStatementNode
                {
                    VariableType = vType,
                    VariableName = vName,
                    Iterable = iterable.Trim(),
                    Body = body
                };
            }
            else
            {
                string init = ConsumeUntilDelimiter(";");
                ConsumeDelimiter(";");
                string cond = ConsumeUntilDelimiter(";");
                ConsumeDelimiter(";");
                string update = ConsumeUntilBalanced(')', '(');
                var body = ParseStatement() ?? new BlockStatementNode();
                return new ForStatementNode
                {
                    Init = init.Trim(),
                    Condition = cond.Trim(),
                    Update = update.Trim(),
                    Body = body
                };
            }
        }

        if (MatchKeyword("try"))
        {
            string? resource = null;
            if (MatchDelimiter("("))
            {
                resource = ConsumeUntilBalanced(')', '(');
            }
            ConsumeDelimiter("{");
            var body = ParseBlockBody();

            var catches = new List<CatchClauseNode>();
            while (MatchKeyword("catch"))
            {
                ConsumeDelimiter("(");
                string exType = ConsumeTypeReference();
                string exVar = ConsumeIdentifier();
                ConsumeDelimiter(")");
                ConsumeDelimiter("{");
                var catchBody = ParseBlockBody();
                catches.Add(new CatchClauseNode
                {
                    ExceptionType = exType,
                    VariableName = exVar,
                    Body = catchBody
                });
            }

            BlockStatementNode? finallyBlock = null;
            if (MatchKeyword("finally"))
            {
                ConsumeDelimiter("{");
                finallyBlock = ParseBlockBody();
            }

            return new TryStatementNode
            {
                ResourceSpecification = resource,
                Body = body,
                CatchClauses = catches,
                FinallyBlock = finallyBlock
            };
        }

        if (MatchKeyword("return"))
        {
            string? expr = null;
            if (!CheckDelimiter(";"))
            {
                expr = ConsumeUntilStatementSemicolon();
            }
            ConsumeDelimiter(";");
            return new ReturnStatementNode { Expression = expr };
        }

        if (MatchKeyword("break"))
        {
            string? label = null;
            if (!CheckDelimiter(";"))
            {
                label = ConsumeIdentifier();
            }
            ConsumeDelimiter(";");
            return new BreakStatementNode { Label = label };
        }

        if (MatchKeyword("continue"))
        {
            string? label = null;
            if (!CheckDelimiter(";"))
            {
                label = ConsumeIdentifier();
            }
            ConsumeDelimiter(";");
            return new ContinueStatementNode { Label = label };
        }

        if (MatchKeyword("synchronized"))
        {
            ConsumeDelimiter("(");
            string expr = ConsumeUntilBalanced(')', '(');
            var body = ParseStatement() ?? new BlockStatementNode();
            return new SynchronizedStatementNode
            {
                Expression = expr.Trim(),
                Body = body
            };
        }

        if (MatchKeyword("switch"))
        {
            ConsumeDelimiter("(");
            string expr = ConsumeUntilBalanced(')', '(');
            ConsumeDelimiter("{");
            var switchNode = new SwitchStatementNode { Expression = expr.Trim() };
            SwitchSectionNode? currentSection = null;

            while (!CheckDelimiter("}") && !IsAtEnd())
            {
                if (MatchKeyword("case"))
                {
                    string label = "case " + ConsumeUntilDelimiter(":") + ":";
                    ConsumeDelimiter(":");
                    if (currentSection == null || currentSection.Statements.Count > 0)
                    {
                        currentSection = new SwitchSectionNode();
                        switchNode.Sections.Add(currentSection);
                    }
                    currentSection.Labels.Add(label);
                }
                else if (MatchKeyword("default"))
                {
                    ConsumeDelimiter(":");
                    if (currentSection == null || currentSection.Statements.Count > 0)
                    {
                        currentSection = new SwitchSectionNode();
                        switchNode.Sections.Add(currentSection);
                    }
                    currentSection.Labels.Add("default:");
                }
                else
                {
                    var stmt = ParseStatement();
                    if (stmt != null)
                    {
                        if (currentSection == null)
                        {
                            currentSection = new SwitchSectionNode();
                            switchNode.Sections.Add(currentSection);
                        }
                        currentSection.Statements.Add(stmt);
                    }
                }
            }
            ConsumeDelimiter("}");
            return switchNode;
        }

        if (MatchKeyword("throw"))
        {
            string expr = ConsumeUntilStatementSemicolon();
            ConsumeDelimiter(";");
            return new ThrowStatementNode { Expression = expr };
        }

        // Generic statement or variable declaration
        string raw = ConsumeUntilStatementSemicolon();
        ConsumeDelimiter(";");

        // Check if raw is a variable declaration: e.g. "int x = 10" or "String s"
        if (TryParseVarDecl(raw, out var vDecl))
        {
            return vDecl;
        }

        return new ExpressionStatementNode { Expression = raw };
    }

    private static readonly HashSet<string> ValidTypeKeywords = new()
    {
        "boolean", "byte", "char", "double", "float", "int", "long", "short", "void", "final", "var"
    };

    private static bool IsValidTypeToken(JavaToken token)
    {
        if (token.Type is JavaTokenType.Identifier) return true;
        if (token.Type is JavaTokenType.Keyword && ValidTypeKeywords.Contains(token.Text)) return true;
        if (token.Type is JavaTokenType.Delimiter && token.Text is "." or "[" or "]" or "?" or ",") return true;
        if (token.Type is JavaTokenType.Operator && token.Text is "<" or ">") return true;
        return false;
    }

    private bool TryParseVarDecl(string raw, out VariableDeclarationStatementNode node)
    {
        node = new VariableDeclarationStatementNode();
        var tokens = new JavaLexer(raw).Tokenize();
        if (tokens.Count > 0 && tokens[^1].Type == JavaTokenType.EndOfFile)
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        if (tokens.Count < 2) return false;
        if (tokens[0].Text is "this" or "super") return false;

        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;
        int eqIdx = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Text == "(") parenDepth++;
            else if (t.Text == ")") { if (parenDepth > 0) parenDepth--; }
            else if (t.Text == "{") braceDepth++;
            else if (t.Text == "}") { if (braceDepth > 0) braceDepth--; }
            else if (t.Text == "[") bracketDepth++;
            else if (t.Text == "]") { if (bracketDepth > 0) bracketDepth--; }
            else if (t.Text == "=" && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
            {
                eqIdx = i;
                break;
            }
        }

        int typeEndIdx = (eqIdx > 1) ? eqIdx - 1 : tokens.Count - 1;
        if (typeEndIdx <= 0) return false;
        for (int i = 0; i < typeEndIdx; i++)
        {
            if (!IsValidTypeToken(tokens[i])) return false;
        }

        if (eqIdx >= 2 && tokens[eqIdx - 2].Text == ".") return false;

        if (eqIdx > 1 && tokens[eqIdx - 1].Type is JavaTokenType.Identifier)
        {
            node.Name = tokens[eqIdx - 1].Text;
            var typeSb = new StringBuilder();
            JavaToken? prev = null;
            for (int i = 0; i < eqIdx - 1; i++)
            {
                AppendTokenWithProperSpacing(typeSb, tokens[i], prev);
                prev = tokens[i];
            }
            node.Type = typeSb.ToString().Trim();
            node.Initializer = raw.Substring(raw.IndexOf('=') + 1).Trim();
            return true;
        }
        else if (eqIdx == -1 && tokens[^1].Type is JavaTokenType.Identifier && tokens.Count >= 2)
        {
            node.Name = tokens[^1].Text;
            var typeSb = new StringBuilder();
            JavaToken? prev = null;
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                AppendTokenWithProperSpacing(typeSb, tokens[i], prev);
                prev = tokens[i];
            }
            node.Type = typeSb.ToString().Trim();
            return true;
        }
        return false;
    }

    private List<AnnotationNode> ParseAnnotations()
    {
        var list = new List<AnnotationNode>();
        while (MatchDelimiter("@"))
        {
            string name = ConsumeIdentifier();
            string? args = null;
            if (MatchDelimiter("("))
            {
                args = ConsumeUntilBalanced(')', '(');
            }
            list.Add(new AnnotationNode { Name = name, Arguments = args });
        }
        return list;
    }

    private List<string> ParseModifiers()
    {
        var list = new List<string>();
        while (!IsAtEnd() && Modifiers.Contains(Current().Text))
        {
            list.Add(Current().Text);
            Advance();
        }
        return list;
    }

    private string ConsumeIdentifier()
    {
        if (Current().Type == JavaTokenType.Identifier || Current().Type == JavaTokenType.Keyword)
        {
            string text = Current().Text;
            Advance();
            return text;
        }
        throw new InvalidOperationException($"Expected identifier at ({Current().Line}:{Current().Column}), got '{Current().Text}'");
    }

    private string ConsumeQualifiedName()
    {
        var sb = new StringBuilder();
        sb.Append(ConsumeIdentifier());
        while (MatchDelimiter("."))
        {
            sb.Append(".");
            sb.Append(ConsumeIdentifier());
        }
        return sb.ToString();
    }

    private string ConsumeImportPath()
    {
        var sb = new StringBuilder();
        sb.Append(ConsumeIdentifier());
        while (MatchDelimiter("."))
        {
            sb.Append(".");
            if (MatchOperator("*"))
            {
                sb.Append("*");
                break;
            }
            sb.Append(ConsumeIdentifier());
        }
        return sb.ToString();
    }

    private string ConsumeTypeReference()
    {
        var sb = new StringBuilder();
        sb.Append(ConsumeIdentifier());

        // Handle generic type arguments <T, List<U>>
        if (MatchDelimiter("<"))
        {
            sb.Append("<");
            sb.Append(ConsumeUntilBalanced('>', '<'));
            sb.Append(">");
        }

        // Handle arrays []
        while (MatchDelimiter("["))
        {
            ConsumeDelimiter("]");
            sb.Append("[]");
        }

        return sb.ToString();
    }

    private static void AppendTokenWithProperSpacing(StringBuilder sb, JavaToken token, JavaToken? prev)
    {
        if (prev != null)
        {
            bool needSpace = true;
            string p = prev.Text;
            string t = token.Text;

            if (p is "." or "::" or "@" || t is "." or "::" or ";" or "," or ")")
                needSpace = false;
            else if (p is "(" or "[" or "<")
                needSpace = false;
            else if (t is ">" or "]" or ")")
                needSpace = false;
            else if (t is "<" && (prev.Type == JavaTokenType.Identifier || p is "new"))
                needSpace = false;
            else if (t is "(" && (prev.Type == JavaTokenType.Identifier || p is ">" or "]" or ")"))
                needSpace = false;
            else if (p is "++" or "--" || t is "++" or "--")
                needSpace = false;

            if (needSpace && sb.Length > 0 && sb[^1] != ' ')
            {
                sb.Append(' ');
            }
        }
        sb.Append(token.Text);
    }

    private string ConsumeUntilBalanced(char closeChar, char openChar)
    {
        var sb = new StringBuilder();
        int depth = 1;
        JavaToken? prev = null;
        while (!IsAtEnd())
        {
            var tok = Current();
            if (tok.Text == openChar.ToString())
            {
                depth++;
            }
            else if (tok.Text == closeChar.ToString())
            {
                depth--;
                if (depth == 0)
                {
                    Advance();
                    return sb.ToString().Trim();
                }
            }
            AppendTokenWithProperSpacing(sb, tok, prev);
            prev = tok;
            Advance();
        }
        return sb.ToString().Trim();
    }

    private string ConsumeUntilDelimiter(string delim)
    {
        var sb = new StringBuilder();
        JavaToken? prev = null;
        while (!IsAtEnd() && Current().Text != delim)
        {
            var tok = Current();
            AppendTokenWithProperSpacing(sb, tok, prev);
            prev = tok;
            Advance();
        }
        return sb.ToString().Trim();
    }

    private string ConsumeUntilStatementSemicolon()
    {
        var sb = new StringBuilder();
        int parenDepth = 0;
        int braceDepth = 0;
        int bracketDepth = 0;
        JavaToken? prev = null;

        while (!IsAtEnd())
        {
            var tok = Current();
            if (tok.Text == ";" && parenDepth == 0 && braceDepth == 0 && bracketDepth == 0)
            {
                break;
            }

            if (tok.Text == "(") parenDepth++;
            else if (tok.Text == ")") { if (parenDepth > 0) parenDepth--; }
            else if (tok.Text == "{") braceDepth++;
            else if (tok.Text == "}") { if (braceDepth > 0) braceDepth--; }
            else if (tok.Text == "[") bracketDepth++;
            else if (tok.Text == "]") { if (bracketDepth > 0) bracketDepth--; }

            AppendTokenWithProperSpacing(sb, tok, prev);
            prev = tok;
            Advance();
        }

        return sb.ToString().Trim();
    }

    private int FindTokenInParen(string target)
    {
        int depth = 1;
        for (int i = _index; i < _tokens.Count; i++)
        {
            var t = _tokens[i];
            if (t.Text == "(") depth++;
            else if (t.Text == ")")
            {
                depth--;
                if (depth == 0) return -1;
            }
            else if (t.Text == target && depth == 1)
            {
                return i;
            }
        }
        return -1;
    }

    private bool CheckDelimiter(string delim) => !IsAtEnd() && Current().Text == delim;
    private bool CheckKeyword(string kw) => !IsAtEnd() && Current().Text == kw;

    private bool MatchKeyword(string kw)
    {
        if (!IsAtEnd() && Current().Text == kw)
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool MatchDelimiter(string delim)
    {
        if (!IsAtEnd() && Current().Text == delim)
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool MatchOperator(string op)
    {
        if (!IsAtEnd() && Current().Text == op)
        {
            Advance();
            return true;
        }
        return false;
    }

    private void ConsumeDelimiter(string delim)
    {
        if (!MatchDelimiter(delim))
        {
            throw new InvalidOperationException($"Expected '{delim}' at ({Current().Line}:{Current().Column}), got '{Current().Text}'");
        }
    }

    private void ConsumeKeyword(string kw)
    {
        if (!MatchKeyword(kw))
        {
            throw new InvalidOperationException($"Expected keyword '{kw}' at ({Current().Line}:{Current().Column}), got '{Current().Text}'");
        }
    }

    private JavaToken Current() => IsAtEnd() ? _tokens[^1] : _tokens[_index];
    private JavaToken PeekNext() => _index + 1 < _tokens.Count ? _tokens[_index + 1] : _tokens[^1];
    private bool IsAtEnd() => _index >= _tokens.Count || _tokens[_index].Type == JavaTokenType.EndOfFile;
    private JavaToken Advance() => _tokens[_index++];
}
