using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Javacstr.Core.Java;
using Javacstr.Core.Mapping;
using Javacstr.Core.Models;

namespace Javacstr.Core.Translation;

public class JavaToCSharpTranslator
{
    private readonly TranslationOptions _options;

    public JavaToCSharpTranslator(TranslationOptions? options = null)
    {
        _options = options ?? new TranslationOptions();
    }

    public TranslationResult Translate(string javaCode)
    {
        try
        {
            var lexer = new JavaLexer(javaCode);
            var tokens = lexer.Tokenize();
            var parser = new JavaParser(tokens);
            var cu = parser.ParseCompilationUnit();

            string csharpCode = EmitCSharp(cu);

            // Format using Roslyn to guarantee standard C# formatting
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);
            var formattedNode = syntaxTree.GetRoot().NormalizeWhitespace();
            string finalCode = formattedNode.ToFullString();

            return TranslationResult.Ok(finalCode);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail($"Java to C# translation failed: {ex.Message}");
        }
    }

    private string EmitCSharp(CompilationUnitNode cu)
    {
        var sb = new StringBuilder();

        // 1. Usings
        var usings = new HashSet<string> { "System" };
        foreach (var imp in cu.Imports)
        {
            string p = imp.Path;
            if (p.StartsWith("java.util.regex"))
            {
                usings.Add("System.Text.RegularExpressions");
            }
            else if (p.StartsWith("java.net.http"))
            {
                usings.Add("System.Net.Http");
            }
            else if (p.StartsWith("java.net"))
            {
                usings.Add("System.Net");
                usings.Add("System.Net.Sockets");
            }
            else if (p.StartsWith("java.util"))
            {
                usings.Add("System.Collections.Generic");
                usings.Add("System.Linq");
            }
            else if (p.StartsWith("java.io"))
            {
                usings.Add("System.IO");
            }
            else if (p.StartsWith("java.lang") || p.StartsWith("java.awt") || p.StartsWith("javax.swing"))
            {
                usings.Add("System.Threading");
            }
        }

        foreach (var u in usings)
        {
            sb.AppendLine($"using {u};");
        }
        sb.AppendLine();

        // 2. Namespace
        bool hasNamespace = cu.Package != null && !string.IsNullOrWhiteSpace(cu.Package.Name);
        string nsName = "";
        if (hasNamespace)
        {
            nsName = ConvertPackageToNamespace(cu.Package!.Name);
            if (_options.UseFileScopedNamespaces)
            {
                sb.AppendLine($"namespace {nsName};");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"namespace {nsName}");
                sb.AppendLine("{");
            }
        }

        // 3. Types
        foreach (var type in cu.Types)
        {
            EmitType(type, sb);
            sb.AppendLine();
        }

        if (hasNamespace && !_options.UseFileScopedNamespaces)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private void EmitType(TypeDeclarationNode type, StringBuilder sb)
    {
        // Modifiers
        var modifiers = new List<string>();
        foreach (var m in type.Modifiers)
        {
            if (m == "final") modifiers.Add("sealed");
            else if (m == "static" && type.Kind == "class")
            {
                // In Java, static nested classes are instantiable non-inner classes;
                // In C#, nested classes are already non-inner, and 'static class' would prevent instantiation or interface implementation.
            }
            else modifiers.Add(m);
        }

        if (modifiers.Count > 0)
        {
            sb.Append(string.Join(" ", modifiers) + " ");
        }

        sb.Append($"{type.Kind} {type.Name}");
        if (!string.IsNullOrEmpty(type.TypeParameters))
        {
            sb.Append(type.TypeParameters);
        }

        // Inheritance
        var baseTypes = new List<string>();
        if (!string.IsNullOrEmpty(type.ExtendsType))
        {
            baseTypes.Add(TypeMapper.MapJavaToCSharp(type.ExtendsType));
        }
        foreach (var imp in type.ImplementsTypes)
        {
            baseTypes.Add(TypeMapper.MapJavaToCSharp(imp));
        }

        if (baseTypes.Count > 0)
        {
            sb.Append($" : {string.Join(", ", baseTypes)}");
        }

        sb.AppendLine();
        sb.AppendLine("{");

        foreach (var member in type.Members)
        {
            EmitMember(member, type.Name, sb);
            sb.AppendLine();
        }

        sb.AppendLine("}");
    }

    private void EmitMember(MemberNode member, string enclosingTypeName, StringBuilder sb)
    {
        switch (member)
        {
            case FieldDeclarationNode field:
                EmitField(field, sb);
                break;
            case ConstructorDeclarationNode ctor:
                EmitConstructor(ctor, enclosingTypeName, sb);
                break;
            case MethodDeclarationNode method:
                EmitMethod(method, sb);
                break;
            case TypeDeclarationNode nestedType:
                EmitType(nestedType, sb);
                break;
        }
    }

    private void EmitField(FieldDeclarationNode field, StringBuilder sb)
    {
        var mods = new List<string>();
        bool isFinal = false;
        bool isStatic = false;
        foreach (var m in field.Modifiers)
        {
            if (m == "final") isFinal = true;
            else if (m == "static") isStatic = true;
            else mods.Add(m);
        }

        if (isStatic && isFinal && !string.IsNullOrEmpty(field.Initializer))
        {
            mods.Add("const");
        }
        else
        {
            if (isStatic) mods.Add("static");
            if (isFinal) mods.Add("readonly");
        }

        if (mods.Count > 0)
        {
            sb.Append(string.Join(" ", mods) + " ");
        }

        string mappedType = TypeMapper.MapJavaToCSharp(field.Type);
        string fieldName = EscapeCSharpKeyword(field.Name);
        sb.Append($"{mappedType} {fieldName}");

        if (!string.IsNullOrEmpty(field.Initializer))
        {
            sb.Append($" = {TranslateExpression(field.Initializer)}");
        }
        sb.AppendLine(";");
    }

    private void EmitConstructor(ConstructorDeclarationNode ctor, string enclosingTypeName, StringBuilder sb)
    {
        if (ctor.Modifiers.Count > 0)
        {
            sb.Append(string.Join(" ", ctor.Modifiers) + " ");
        }

        sb.Append($"{enclosingTypeName}(");
        EmitParameters(ctor.Parameters, sb);
        sb.Append(")");

        // Initializer call: super(...) -> : base(...) and this(...) -> : this(...)
        if (!string.IsNullOrEmpty(ctor.InitializerCall))
        {
            string init = ctor.InitializerCall.Trim();
            if (init.StartsWith("super("))
            {
                string args = init[6..^1];
                sb.Append($" : base({TranslateArguments(args)})");
            }
            else if (init.StartsWith("this("))
            {
                string args = init[5..^1];
                sb.Append($" : this({TranslateArguments(args)})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("{");
        foreach (var stmt in ctor.Body.Statements)
        {
            EmitStatement(stmt, sb);
        }
        sb.AppendLine("}");
    }

    private void EmitMethod(MethodDeclarationNode method, StringBuilder sb)
    {
        var mods = new List<string>();
        bool hasOverride = false;

        foreach (var anno in method.Annotations)
        {
            if (anno.Name == "Override")
            {
                hasOverride = true;
            }
        }

        foreach (var m in method.Modifiers)
        {
            if (m == "final")
            {
                // In C#, non-virtual methods are final by default
            }
            else
            {
                mods.Add(m);
            }
        }

        if (hasOverride && !mods.Contains("override"))
        {
            mods.Add("override");
        }

        if (mods.Count > 0)
        {
            sb.Append(string.Join(" ", mods) + " ");
        }

        if (!string.IsNullOrEmpty(method.TypeParameters))
        {
            sb.Append(method.TypeParameters + " ");
        }

        string returnType = TypeMapper.MapJavaToCSharp(method.ReturnType);
        string methodName = _options.ConvertMethodCasing ? CasingHelper.ToPascalCase(method.Name) : method.Name;
        if (method.Name == "toString") methodName = "ToString";
        if (method.Name == "equals") methodName = "Equals";

        sb.Append($"{returnType} {methodName}(");
        EmitParameters(method.Parameters, sb);
        sb.Append(")");

        if (method.Body == null)
        {
            sb.AppendLine(";");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("{");
            foreach (var stmt in method.Body.Statements)
            {
                EmitStatement(stmt, sb);
            }
            sb.AppendLine("}");
        }
    }

    private void EmitParameters(List<ParameterNode> parameters, StringBuilder sb)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            string mappedType = TypeMapper.MapJavaToCSharp(p.Type);
            string paramName = EscapeCSharpKeyword(p.Name);
            sb.Append($"{mappedType} {paramName}");
            if (i < parameters.Count - 1)
            {
                sb.Append(", ");
            }
        }
    }

    private void EmitStatement(StatementNode stmt, StringBuilder sb)
    {
        switch (stmt)
        {
            case BlockStatementNode block:
                sb.AppendLine("{");
                foreach (var s in block.Statements)
                {
                    EmitStatement(s, sb);
                }
                sb.AppendLine("}");
                break;

            case VariableDeclarationStatementNode vDecl:
                string vType = TypeMapper.MapJavaToCSharp(vDecl.Type);
                bool isConst = false;
                if (vType.StartsWith("final "))
                {
                    vType = vType[6..].Trim();
                    isConst = true;
                }
                string vName = EscapeCSharpKeyword(vDecl.Name);
                if (isConst) sb.Append("const ");
                sb.Append($"{vType} {vName}");
                if (!string.IsNullOrEmpty(vDecl.Initializer))
                {
                    sb.Append($" = {TranslateExpression(vDecl.Initializer, vType)}");
                }
                sb.AppendLine(";");
                break;

            case ExpressionStatementNode exprStmt:
                sb.AppendLine($"{TranslateExpression(exprStmt.Expression)};");
                break;

            case IfStatementNode ifStmt:
                sb.AppendLine($"if ({TranslateExpression(ifStmt.Condition)})");
                EmitSingleOrBlockStatement(ifStmt.ThenStatement, sb);
                if (ifStmt.ElseStatement != null)
                {
                    sb.AppendLine("else");
                    EmitSingleOrBlockStatement(ifStmt.ElseStatement, sb);
                }
                break;

            case WhileStatementNode whileStmt:
                sb.AppendLine($"while ({TranslateExpression(whileStmt.Condition)})");
                EmitSingleOrBlockStatement(whileStmt.Body, sb);
                break;

            case DoWhileStatementNode doStmt:
                sb.AppendLine("do");
                EmitSingleOrBlockStatement(doStmt.Body, sb);
                sb.AppendLine($"while ({TranslateExpression(doStmt.Condition)});");
                break;

            case ForStatementNode forStmt:
                string init = TranslateExpression(forStmt.Init ?? "");
                string cond = TranslateExpression(forStmt.Condition ?? "");
                string update = TranslateExpression(forStmt.Update ?? "");
                sb.AppendLine($"for ({init}; {cond}; {update})");
                EmitSingleOrBlockStatement(forStmt.Body, sb);
                break;

            case ForEachStatementNode forEachStmt:
                string vTypeEach = TypeMapper.MapJavaToCSharp(forEachStmt.VariableType);
                string iter = TranslateExpression(forEachStmt.Iterable);
                sb.AppendLine($"foreach ({vTypeEach} {forEachStmt.VariableName} in {iter})");
                EmitSingleOrBlockStatement(forEachStmt.Body, sb);
                break;

            case ReturnStatementNode retStmt:
                if (string.IsNullOrEmpty(retStmt.Expression))
                    sb.AppendLine("return;");
                else
                    sb.AppendLine($"return {TranslateExpression(retStmt.Expression)};");
                break;

            case ThrowStatementNode thrStmt:
                sb.AppendLine($"throw {TranslateExpression(thrStmt.Expression)};");
                break;

            case TryStatementNode tryStmt:
                sb.AppendLine("try");
                EmitSingleOrBlockStatement(tryStmt.Body, sb);
                foreach (var c in tryStmt.CatchClauses)
                {
                    string exType = TypeMapper.MapJavaToCSharp(c.ExceptionType);
                    sb.AppendLine($"catch ({exType} {c.VariableName})");
                    EmitSingleOrBlockStatement(c.Body, sb);
                }
                if (tryStmt.FinallyBlock != null)
                {
                    sb.AppendLine("finally");
                    EmitSingleOrBlockStatement(tryStmt.FinallyBlock, sb);
                }
                break;

            case RawStatementNode raw:
                sb.AppendLine($"{TranslateExpression(raw.Text)};");
                break;
        }
    }

    private void EmitSingleOrBlockStatement(StatementNode stmt, StringBuilder sb)
    {
        if (stmt is BlockStatementNode block)
        {
            sb.AppendLine("{");
            foreach (var s in block.Statements)
            {
                EmitStatement(s, sb);
            }
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("{");
            EmitStatement(stmt, sb);
            sb.AppendLine("}");
        }
    }

    public string TranslateExpression(string expr, string? targetType = null)
    {
        if (string.IsNullOrWhiteSpace(expr)) return expr;

        // System.out.println -> Console.WriteLine
        expr = Regex.Replace(expr, @"System\.out\.println\s*\((.*?)\)", m => $"Console.WriteLine({TranslateArguments(m.Groups[1].Value)})");
        expr = Regex.Replace(expr, @"System\.out\.print\s*\((.*?)\)", m => $"Console.Write({TranslateArguments(m.Groups[1].Value)})");
        expr = Regex.Replace(expr, @"System\.err\.println\s*\((.*?)\)", m => $"Console.Error.WriteLine({TranslateArguments(m.Groups[1].Value)})");

        // Convert anonymous classes (e.g. Runnable, ActionListener) to lambdas
        expr = ConvertAnonymousClassesToLambdas(expr);

        // Escape C# keyword variables in expressions (prevent @@out)
        expr = Regex.Replace(expr, @"(?<!@)\b(out|in)\s*\.", "@$1.");
        expr = Regex.Replace(expr, @"(?<!@)\b(out|in)\s*=", "@$1 =");
        expr = Regex.Replace(expr, @"(?<!@)\b(out|in)\s*([,;\)])", "@$1$2");
        expr = Regex.Replace(expr, @"\(\s*(?<!@)(out|in)\s*\)", "(@$1)");

        // Array length (output.length -> output.Length)
        expr = Regex.Replace(expr, @"\b(\w+)\.length\b(?!\s*\()", "$1.Length");

        // Common conversions
        expr = Regex.Replace(expr, @"\b(\w+)\.toCharArray\(\)", "$1.ToCharArray()");
        expr = Regex.Replace(expr, @"new\s+String\s*\(", "new string(");
        expr = Regex.Replace(expr, @"\bInteger\.parseInt\s*\((.*?)\)", "int.Parse($1)");
        expr = Regex.Replace(expr, @"\bDouble\.parseDouble\s*\((.*?)\)", "double.Parse($1)");
        expr = Regex.Replace(expr, @"\bLong\.parseLong\s*\((.*?)\)", "long.Parse($1)");
        expr = Regex.Replace(expr, @"\bBoolean\.parseBoolean\s*\((.*?)\)", "bool.Parse($1)");
        expr = Regex.Replace(expr, @"\bMath\.random\(\)", "Random.Shared.NextDouble()");
        expr = Regex.Replace(expr, @"\bSystem\.exit\s*\((.*?)\)", "Environment.Exit($1)");
        expr = Regex.Replace(expr, @"\bThread\.sleep\s*\(", "Thread.Sleep(");
        expr = Regex.Replace(expr, @"\bThread\.currentThread\(\)", "Thread.CurrentThread");
        expr = Regex.Replace(expr, @"\b(\w+)\.interrupt\(\)", "$1.Interrupt()");
        expr = Regex.Replace(expr, @"\b(\w+)\.start\(\)", "$1.Start()");
        expr = Regex.Replace(expr, @"\b(\w+)\.close\(\)", "$1.Close()");
        expr = Regex.Replace(expr, @"\b(\w+)\.readLine\(\)", "$1.ReadLine()");
        expr = Regex.Replace(expr, @"(?<!@)\b(\w+)\.println\s*\((.*?)\)", "$1.WriteLine($2)");
        expr = Regex.Replace(expr, @"@(\w+)\.println\s*\((.*?)\)", "@$1.WriteLine($2)");
        expr = Regex.Replace(expr, @"(?<!@)\b(\w+)\.println\(\)", "$1.WriteLine()");
        expr = Regex.Replace(expr, @"@(\w+)\.println\(\)", "@$1.WriteLine()");
        expr = Regex.Replace(expr, @"(?<!@)\b(\w+)\.print\s*\((.*?)\)", "$1.Write($2)");
        expr = Regex.Replace(expr, @"@(\w+)\.print\s*\((.*?)\)", "@$1.Write($2)");
        expr = Regex.Replace(expr, @"\b(\w+)\.indexOf\s*\(", "$1.IndexOf(");
        expr = Regex.Replace(expr, @"\b(\w+)\.printStackTrace\(\)", "Console.Error.WriteLine($1)");

        // instanceof -> is
        expr = Regex.Replace(expr, @"\binstanceof\s+([A-Za-z0-9_<>]+)", m => $"is {TypeMapper.MapJavaToCSharp(m.Groups[1].Value)}");

        // Substring math: obj.substring(a, b) -> obj.Substring(a, b - a)
        expr = Regex.Replace(expr, @"(\w+)\.substring\s*\(([^,\)]+),\s*([^,\)]+)\)", m =>
        {
            string target = m.Groups[1].Value;
            string start = m.Groups[2].Value.Trim();
            string end = m.Groups[3].Value.Trim();
            if (int.TryParse(start, out int sInt) && int.TryParse(end, out int eInt))
            {
                return $"{target}.Substring({sInt}, {eInt - sInt})";
            }
            return $"{target}.Substring({start}, {end} - ({start}))";
        });
        expr = Regex.Replace(expr, @"(\w+)\.substring\s*\(([^,\)]+)\)", "$1.Substring($2)");

        // Collections & String length / size:
        // .length() -> .Length
        expr = Regex.Replace(expr, @"(\w+)\.length\(\)", "$1.Length");
        // .size() -> .Count
        expr = Regex.Replace(expr, @"(\w+)\.size\(\)", "$1.Count");
        // .get(i) -> [i]
        expr = Regex.Replace(expr, @"(\w+)\.get\s*\(([^,\)]+)\)", "$1[$2]");
        // .set(i, v) -> [i] = v
        expr = Regex.Replace(expr, @"(\w+)\.set\s*\(([^,\)]+),\s*([^,\)]+)\)", "$1[$2] = $3");
        // .put(k, v) -> [k] = v
        expr = Regex.Replace(expr, @"(\w+)\.put\s*\(([^,\)]+),\s*([^,\)]+)\)", "$1[$2] = $3");
        // .containsKey(k) -> .ContainsKey(k)
        expr = Regex.Replace(expr, @"(\w+)\.containsKey\s*\(([^,\)]+)\)", "$1.ContainsKey($2)");

        // String methods
        expr = Regex.Replace(expr, @"(\w+)\.toLowerCase\(\)", "$1.ToLower()");
        expr = Regex.Replace(expr, @"(\w+)\.toUpperCase\(\)", "$1.ToUpper()");
        expr = Regex.Replace(expr, @"(\w+)\.trim\(\)", "$1.Trim()");
        expr = Regex.Replace(expr, @"(\w+)\.equals\s*\(([^,\)]+)\)", "$1.Equals($2)");
        expr = Regex.Replace(expr, @"(\w+)\.equalsIgnoreCase\s*\(([^,\)]+)\)", "string.Equals($1, $2, StringComparison.OrdinalIgnoreCase)");

        // Regex mapping
        if (_options.MapRegex)
        {
            expr = Regex.Replace(expr, @"Pattern\.compile\s*\((.*),\s*Pattern\.CASE_INSENSITIVE\)", "new Regex($1, RegexOptions.IgnoreCase)");
            expr = Regex.Replace(expr, @"Pattern\.compile\s*\((.*)\)", "new Regex($1)");
            expr = Regex.Replace(expr, @"(\w+)\.matcher\s*\(([^,\)]+)\)", "$1.Match($2)");
            expr = Regex.Replace(expr, @"(\w+)\.find\(\)", "$1.Success");
            expr = Regex.Replace(expr, @"(\w+)\.group\s*\(([^,\)]+)\)", "$1.Groups[$2].Value");
            expr = Regex.Replace(expr, @"(\w+)\.group\(\)", "$1.Value");
        }

        // HTTPS mapping
        if (_options.MapHttpClient)
        {
            expr = Regex.Replace(expr, @"HttpClient\.newHttpClient\(\)", "new HttpClient()");
            expr = Regex.Replace(expr, @"HttpClient\.newBuilder\(\)\.build\(\)", "new HttpClient()");
            expr = Regex.Replace(expr, @"HttpRequest\.newBuilder\(\)\.uri\s*\(URI\.create\(([^,\)]+)\)\)\.build\(\)", "new HttpRequestMessage(HttpMethod.Get, $1)");
            expr = Regex.Replace(expr, @"(\w+)\.send\s*\(([^,]+),\s*HttpResponse\.BodyHandlers\.ofString\(\)\)", "$1.Send($2)");
            expr = Regex.Replace(expr, @"(\w+)\.statusCode\(\)", "(int)$1.StatusCode");
            expr = Regex.Replace(expr, @"(\w+)\.body\(\)", "$1.Content.ReadAsStringAsync().Result");
        }

        // Math mapping
        expr = Regex.Replace(expr, @"Math\.(max|min|abs|sqrt|pow|round|floor|ceil)\s*\(", m =>
        {
            string func = m.Groups[1].Value;
            string pascal = CasingHelper.ToPascalCase(func);
            if (pascal == "Ceil") pascal = "Ceiling";
            return $"Math.{pascal}(";
        });

        // Collections methods:
        expr = Regex.Replace(expr, @"\.add\s*\(", ".Add(");
        expr = Regex.Replace(expr, @"\.remove\s*\(", ".Remove(");
        expr = Regex.Replace(expr, @"\.clear\s*\(", ".Clear(");
        expr = Regex.Replace(expr, @"\.contains\s*\(", ".Contains(");

        // Object instantiations:
        if (!string.IsNullOrEmpty(targetType))
        {
            expr = Regex.Replace(expr, @"new\s+ArrayList<>\s*\(", $"new {targetType}(");
            expr = Regex.Replace(expr, @"new\s+HashMap<>\s*\(", $"new {targetType}(");
            expr = Regex.Replace(expr, @"new\s+HashSet<>\s*\(", $"new {targetType}(");
        }

        expr = Regex.Replace(expr, @"new\s+ArrayList<([^>]*)>\s*\(", m =>
        {
            string t = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(t))
                return !string.IsNullOrEmpty(targetType) ? $"new {targetType}(" : "new List<string>(";
            return $"new List<{TypeMapper.MapJavaToCSharp(t)}>(";
        });
        expr = Regex.Replace(expr, @"new\s+HashMap<([^>]*)>\s*\(", m =>
        {
            string t = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(t))
                return !string.IsNullOrEmpty(targetType) ? $"new {targetType}(" : "new Dictionary<string, string>(";
            return $"new Dictionary<{TypeMapper.MapJavaToCSharp(t)}>(";
        });
        expr = Regex.Replace(expr, @"new\s+HashSet<([^>]*)>\s*\(", m =>
        {
            string t = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(t))
                return !string.IsNullOrEmpty(targetType) ? $"new {targetType}(" : "new HashSet<string>(";
            return $"new HashSet<{TypeMapper.MapJavaToCSharp(t)}>(";
        });

        return expr;
    }

    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    };

    private static string EscapeCSharpKeyword(string identifier)
    {
        return CSharpKeywords.Contains(identifier) ? "@" + identifier : identifier;
    }

    private string ConvertAnonymousClassesToLambdas(string expr)
    {
        var pattern = new Regex(@"new\s+([A-Za-z0-9_<>]+)\s*\(\s*\)\s*\{");
        while (true)
        {
            var match = pattern.Match(expr);
            if (!match.Success) break;

            int braceStart = match.Index + match.Length - 1;
            int braceEnd = FindMatchingBrace(expr, braceStart);
            if (braceEnd == -1) break;

            string classBody = expr.Substring(braceStart + 1, braceEnd - braceStart - 1);

            var methodMatch = Regex.Match(classBody, @"(?:@Override\s+)?public\s+(?:void|[\w<>\[\]]+)\s+(\w+)\s*\(([^)]*)\)\s*\{");
            if (methodMatch.Success)
            {
                int mBraceStart = classBody.IndexOf('{', methodMatch.Index);
                int mBraceEnd = FindMatchingBrace(classBody, mBraceStart);
                if (mBraceEnd != -1)
                {
                    string mParams = methodMatch.Groups[2].Value.Trim();
                    string mBody = classBody.Substring(mBraceStart + 1, mBraceEnd - mBraceStart - 1).Trim();

                    var paramNames = new List<string>();
                    if (!string.IsNullOrWhiteSpace(mParams))
                    {
                        var parts = mParams.Split(',');
                        foreach (var part in parts)
                        {
                            var tokens = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length > 0)
                            {
                                paramNames.Add(EscapeCSharpKeyword(tokens[^1]));
                            }
                        }
                    }

                    string lambdaHeader = paramNames.Count == 1 ? $"({paramNames[0]})" : $"({string.Join(", ", paramNames)})";
                    string translatedBody = TranslateExpression(mBody);
                    string replacement = $"{lambdaHeader} => {{ {translatedBody} }}";

                    expr = expr.Substring(0, match.Index) + replacement + expr.Substring(braceEnd + 1);
                    continue;
                }
            }
            break;
        }
        return expr;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        int depth = 0;
        bool inString = false;
        bool inChar = false;
        for (int i = openIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' && !inChar && (i == 0 || text[i - 1] != '\\')) inString = !inString;
            else if (c == '\'' && !inString && (i == 0 || text[i - 1] != '\\')) inChar = !inChar;
            else if (!inString && !inChar)
            {
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }
        return -1;
    }

    private string TranslateArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return args;
        var parts = TypeMapper.SplitGenericArgs(args);
        return string.Join(", ", parts.ConvertAll(x => TranslateExpression(x, null)));
    }

    private string ConvertPackageToNamespace(string pkg)
    {
        var segments = pkg.Split('.');
        var capSegments = new List<string>();
        foreach (var s in segments)
        {
            capSegments.Add(CasingHelper.ToPascalCase(s));
        }
        return string.Join(".", capSegments);
    }
}
