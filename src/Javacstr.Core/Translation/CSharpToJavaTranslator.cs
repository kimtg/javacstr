using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Javacstr.Core.Java;
using Javacstr.Core.Mapping;
using Javacstr.Core.Models;

namespace Javacstr.Core.Translation;

public class CSharpToJavaTranslator
{
    private readonly TranslationOptions _options;

    public CSharpToJavaTranslator(TranslationOptions? options = null)
    {
        _options = options ?? new TranslationOptions();
    }

    public TranslationResult Translate(string csharpCode)
    {
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);
            var root = syntaxTree.GetCompilationUnitRoot();

            var cu = new CompilationUnitNode();

            // 1. Namespace -> Package
            var nsNode = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsNode != null)
            {
                string nsText = nsNode.Name.ToString();
                cu.Package = new PackageDeclarationNode
                {
                    Name = ConvertNamespaceToPackage(nsText)
                };
            }

            // 2. Usings -> Imports
            var importsSet = new HashSet<string>();
            foreach (var u in root.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>())
            {
                string name = u.Name?.ToString() ?? "";
                if (name == "System.Collections.Generic" || name == "System.Linq")
                {
                    importsSet.Add("java.util.*");
                }
                else if (name == "System.Text.RegularExpressions")
                {
                    importsSet.Add("java.util.regex.*");
                }
                else if (name == "System.Net.Http")
                {
                    importsSet.Add("java.net.http.*");
                    importsSet.Add("java.net.URI");
                }
                else if (name == "System.IO")
                {
                    importsSet.Add("java.io.*");
                }
            }

            foreach (var imp in importsSet)
            {
                cu.Imports.Add(new ImportDeclarationNode { Path = imp });
            }

            // 3. Types
            var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Where(t => t.Parent is CompilationUnitSyntax || t.Parent is BaseNamespaceDeclarationSyntax);

            foreach (var typeSyntax in typeDeclarations)
            {
                var typeNode = ConvertTypeDeclaration(typeSyntax);
                cu.Types.Add(typeNode);
            }

            var emitter = new JavaEmitter();
            string javaCode = emitter.Emit(cu);
            return TranslationResult.Ok(javaCode);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail($"C# to Java translation failed: {ex.Message}");
        }
    }

    private TypeDeclarationNode ConvertTypeDeclaration(TypeDeclarationSyntax syntax)
    {
        var node = new TypeDeclarationNode
        {
            Name = syntax.Identifier.Text,
            Kind = syntax switch
            {
                ClassDeclarationSyntax => "class",
                InterfaceDeclarationSyntax => "interface",
                RecordDeclarationSyntax => "record",
                StructDeclarationSyntax => "class",
                _ => "class"
            }
        };

        // Modifiers
        foreach (var mod in syntax.Modifiers)
        {
            string m = mod.Text;
            if (m == "sealed") node.Modifiers.Add("final");
            else if (m == "internal") { /* default package-private in java */ }
            else if (m is "public" or "private" or "protected" or "static" or "abstract")
            {
                node.Modifiers.Add(m);
            }
        }

        // Generics
        if (syntax.TypeParameterList != null)
        {
            node.TypeParameters = syntax.TypeParameterList.ToString();
        }

        // Base types (extends vs implements)
        if (syntax.BaseList != null)
        {
            bool first = true;
            foreach (var baseType in syntax.BaseList.Types)
            {
                string rawType = baseType.Type.ToString();
                string mappedType = TypeMapper.MapCSharpToJava(rawType);

                // Guess if interface: starts with 'I' and 2nd char is uppercase, or not first
                bool isInterface = (rawType.Length > 1 && rawType[0] == 'I' && char.IsUpper(rawType[1])) || !first || node.Kind == "interface";

                if (isInterface || node.Kind == "interface")
                {
                    node.ImplementsTypes.Add(mappedType);
                }
                else
                {
                    node.ExtendsType = mappedType;
                }
                first = false;
            }
        }

        // Members
        foreach (var memberSyntax in syntax.Members)
        {
            ConvertMember(memberSyntax, node);
        }

        return node;
    }

    private void ConvertMember(MemberDeclarationSyntax memberSyntax, TypeDeclarationNode parentType)
    {
        switch (memberSyntax)
        {
            case FieldDeclarationSyntax fieldSyntax:
                ConvertField(fieldSyntax, parentType);
                break;

            case PropertyDeclarationSyntax propSyntax:
                ConvertProperty(propSyntax, parentType);
                break;

            case ConstructorDeclarationSyntax ctorSyntax:
                ConvertConstructor(ctorSyntax, parentType);
                break;

            case MethodDeclarationSyntax methodSyntax:
                ConvertMethod(methodSyntax, parentType);
                break;

            case TypeDeclarationSyntax nestedTypeSyntax:
                var nestedNode = ConvertTypeDeclaration(nestedTypeSyntax);
                parentType.Members.Add(nestedNode);
                break;
        }
    }

    private void ConvertField(FieldDeclarationSyntax syntax, TypeDeclarationNode parentType)
    {
        var mods = new List<string>();
        bool isConst = false;
        foreach (var mod in syntax.Modifiers)
        {
            string m = mod.Text;
            if (m == "const") isConst = true;
            else if (m == "readonly") mods.Add("final");
            else if (m is "public" or "private" or "protected" or "static") mods.Add(m);
        }

        if (isConst)
        {
            mods.Add("public");
            mods.Add("static");
            mods.Add("final");
        }

        string fieldType = TypeMapper.MapCSharpToJava(syntax.Declaration.Type.ToString());

        foreach (var variable in syntax.Declaration.Variables)
        {
            string? init = null;
            if (variable.Initializer != null)
            {
                init = TranslateExpression(variable.Initializer.Value.ToString());
            }

            parentType.Members.Add(new FieldDeclarationNode
            {
                Modifiers = new List<string>(mods),
                Type = fieldType,
                Name = variable.Identifier.Text,
                Initializer = init
            });
        }
    }

    private void ConvertProperty(PropertyDeclarationSyntax syntax, TypeDeclarationNode parentType)
    {
        string propName = syntax.Identifier.Text;
        string propType = TypeMapper.MapCSharpToJava(syntax.Type.ToString());
        string fieldName = CasingHelper.ToCamelCase(propName);

        if (_options.ConvertProperties)
        {
            // Add private field: private Type fieldName;
            parentType.Members.Add(new FieldDeclarationNode
            {
                Modifiers = new List<string> { "private" },
                Type = propType,
                Name = fieldName
            });

            // Add Getter: public Type getPropName() { return this.fieldName; }
            string getterName = (propType == "boolean" ? "is" : "get") + CasingHelper.ToPascalCase(propName);
            var getter = new MethodDeclarationNode
            {
                Modifiers = new List<string> { "public" },
                ReturnType = propType,
                Name = getterName,
                Body = new BlockStatementNode
                {
                    Statements = new List<StatementNode>
                    {
                        new ReturnStatementNode { Expression = $"this.{fieldName}" }
                    }
                }
            };
            parentType.Members.Add(getter);

            // Add Setter: public void setPropName(Type fieldName) { this.fieldName = fieldName; }
            var setter = new MethodDeclarationNode
            {
                Modifiers = new List<string> { "public" },
                ReturnType = "void",
                Name = "set" + CasingHelper.ToPascalCase(propName),
                Parameters = new List<ParameterNode>
                {
                    new ParameterNode { Type = propType, Name = fieldName }
                },
                Body = new BlockStatementNode
                {
                    Statements = new List<StatementNode>
                    {
                        new ExpressionStatementNode { Expression = $"this.{fieldName} = {fieldName}" }
                    }
                }
            };
            parentType.Members.Add(setter);
        }
        else
        {
            // Just emit as public field
            parentType.Members.Add(new FieldDeclarationNode
            {
                Modifiers = new List<string> { "public" },
                Type = propType,
                Name = fieldName
            });
        }
    }

    private void ConvertConstructor(ConstructorDeclarationSyntax syntax, TypeDeclarationNode parentType)
    {
        var node = new ConstructorDeclarationNode
        {
            Name = syntax.Identifier.Text
        };

        foreach (var mod in syntax.Modifiers)
        {
            if (mod.Text is "public" or "private" or "protected")
                node.Modifiers.Add(mod.Text);
        }

        foreach (var p in syntax.ParameterList.Parameters)
        {
            node.Parameters.Add(new ParameterNode
            {
                Type = TypeMapper.MapCSharpToJava(p.Type?.ToString() ?? "object"),
                Name = p.Identifier.Text
            });
        }

        // Handle constructor initializer : base(...) or : this(...)
        if (syntax.Initializer != null)
        {
            string initText = syntax.Initializer.ToString().Trim();
            if (initText.StartsWith(": base("))
            {
                string args = initText[7..^1];
                node.InitializerCall = $"super({TranslateArguments(args)})";
            }
            else if (initText.StartsWith(": this("))
            {
                string args = initText[7..^1];
                node.InitializerCall = $"this({TranslateArguments(args)})";
            }
        }

        if (syntax.Body != null)
        {
            node.Body = ConvertBlock(syntax.Body);
        }

        parentType.Members.Add(node);
    }

    private void ConvertMethod(MethodDeclarationSyntax syntax, TypeDeclarationNode parentType)
    {
        var node = new MethodDeclarationNode
        {
            ReturnType = TypeMapper.MapCSharpToJava(syntax.ReturnType.ToString())
        };

        // Modifiers & Override
        foreach (var mod in syntax.Modifiers)
        {
            string m = mod.Text;
            if (m == "override")
            {
                node.Annotations.Add(new AnnotationNode { Name = "Override" });
            }
            else if (m is "public" or "private" or "protected" or "static" or "abstract")
            {
                node.Modifiers.Add(m);
            }
        }

        string rawName = syntax.Identifier.Text;
        string mappedName = _options.ConvertMethodCasing ? CasingHelper.ToCamelCase(rawName) : rawName;
        if (rawName == "ToString") mappedName = "toString";
        if (rawName == "Equals") mappedName = "equals";
        node.Name = mappedName;

        if (syntax.TypeParameterList != null)
        {
            node.TypeParameters = syntax.TypeParameterList.ToString();
        }

        foreach (var p in syntax.ParameterList.Parameters)
        {
            node.Parameters.Add(new ParameterNode
            {
                Type = TypeMapper.MapCSharpToJava(p.Type?.ToString() ?? "object"),
                Name = p.Identifier.Text
            });
        }

        if (syntax.Body != null)
        {
            node.Body = ConvertBlock(syntax.Body);
        }
        else if (syntax.ExpressionBody != null)
        {
            node.Body = new BlockStatementNode
            {
                Statements = new List<StatementNode>
                {
                    new ReturnStatementNode { Expression = TranslateExpression(syntax.ExpressionBody.Expression.ToString()) }
                }
            };
        }

        parentType.Members.Add(node);
    }

    private BlockStatementNode ConvertBlock(BlockSyntax block)
    {
        var node = new BlockStatementNode();
        foreach (var stmt in block.Statements)
        {
            node.Statements.Add(ConvertStatement(stmt));
        }
        return node;
    }

    private StatementNode ConvertStatement(StatementSyntax stmt)
    {
        switch (stmt)
        {
            case BlockSyntax block:
                return ConvertBlock(block);

            case LocalDeclarationStatementSyntax localDecl:
                var v = localDecl.Declaration.Variables.FirstOrDefault();
                string type = TypeMapper.MapCSharpToJava(localDecl.Declaration.Type.ToString());
                string name = v?.Identifier.Text ?? "x";
                string? init = v?.Initializer != null ? TranslateExpression(v.Initializer.Value.ToString()) : null;
                return new VariableDeclarationStatementNode
                {
                    Type = type,
                    Name = name,
                    Initializer = init
                };

            case ExpressionStatementSyntax exprStmt:
                return new ExpressionStatementNode
                {
                    Expression = TranslateExpression(exprStmt.Expression.ToString())
                };

            case IfStatementSyntax ifStmt:
                var thenStmt = ConvertStatement(ifStmt.Statement);
                StatementNode? elseStmt = ifStmt.Else != null ? ConvertStatement(ifStmt.Else.Statement) : null;
                return new IfStatementNode
                {
                    Condition = TranslateExpression(ifStmt.Condition.ToString()),
                    ThenStatement = thenStmt,
                    ElseStatement = elseStmt
                };

            case WhileStatementSyntax whileStmt:
                return new WhileStatementNode
                {
                    Condition = TranslateExpression(whileStmt.Condition.ToString()),
                    Body = ConvertStatement(whileStmt.Statement)
                };

            case DoStatementSyntax doStmt:
                return new DoWhileStatementNode
                {
                    Condition = TranslateExpression(doStmt.Condition.ToString()),
                    Body = ConvertStatement(doStmt.Statement)
                };

            case ForStatementSyntax forStmt:
                string initStr = forStmt.Declaration != null ? forStmt.Declaration.ToString() : string.Join(", ", forStmt.Initializers);
                string condStr = forStmt.Condition?.ToString() ?? "";
                string updateStr = string.Join(", ", forStmt.Incrementors);
                return new ForStatementNode
                {
                    Init = TranslateExpression(initStr),
                    Condition = TranslateExpression(condStr),
                    Update = TranslateExpression(updateStr),
                    Body = ConvertStatement(forStmt.Statement)
                };

            case ForEachStatementSyntax forEachStmt:
                return new ForEachStatementNode
                {
                    VariableType = TypeMapper.MapCSharpToJava(forEachStmt.Type.ToString()),
                    VariableName = forEachStmt.Identifier.Text,
                    Iterable = TranslateExpression(forEachStmt.Expression.ToString()),
                    Body = ConvertStatement(forEachStmt.Statement)
                };

            case ReturnStatementSyntax retStmt:
                return new ReturnStatementNode
                {
                    Expression = retStmt.Expression != null ? TranslateExpression(retStmt.Expression.ToString()) : null
                };

            case ThrowStatementSyntax throwStmt:
                return new ThrowStatementNode
                {
                    Expression = throwStmt.Expression != null ? TranslateExpression(throwStmt.Expression.ToString()) : "new Exception()"
                };

            case TryStatementSyntax tryStmt:
                var tryNode = new TryStatementNode
                {
                    Body = ConvertBlock(tryStmt.Block)
                };
                foreach (var catchClause in tryStmt.Catches)
                {
                    string exType = catchClause.Declaration != null ? TypeMapper.MapCSharpToJava(catchClause.Declaration.Type.ToString()) : "Exception";
                    string exVar = catchClause.Declaration != null ? catchClause.Declaration.Identifier.Text : "e";
                    tryNode.CatchClauses.Add(new CatchClauseNode
                    {
                        ExceptionType = exType,
                        VariableName = exVar,
                        Body = ConvertBlock(catchClause.Block)
                    });
                }
                if (tryStmt.Finally != null)
                {
                    tryNode.FinallyBlock = ConvertBlock(tryStmt.Finally.Block);
                }
                return tryNode;

            default:
                return new RawStatementNode(TranslateExpression(stmt.ToString().TrimEnd(';')));
        }
    }

    public string TranslateExpression(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return expr;

        // is -> instanceof
        expr = Regex.Replace(expr, @"\bis\s+([A-Za-z0-9_<>]+)", m => $"instanceof {TypeMapper.MapCSharpToJava(m.Groups[1].Value)}");

        // Console.WriteLine -> System.out.println
        expr = Regex.Replace(expr, @"Console\.WriteLine\s*\((.*)\)", m => $"System.out.println({TranslateArguments(m.Groups[1].Value)})");
        expr = Regex.Replace(expr, @"Console\.Write\s*\((.*)\)", m => $"System.out.print({TranslateArguments(m.Groups[1].Value)})");
        expr = Regex.Replace(expr, @"Console\.Error\.WriteLine\s*\((.*)\)", m => $"System.err.println({TranslateArguments(m.Groups[1].Value)})");

        // Substring: s.Substring(a, len) -> s.substring(a, a + len)
        expr = Regex.Replace(expr, @"(\w+)\.Substring\s*\(([^,\)]+),\s*([^,\)]+)\)", m =>
        {
            string target = m.Groups[1].Value;
            string start = m.Groups[2].Value.Trim();
            string len = m.Groups[3].Value.Trim();
            if (int.TryParse(start, out int sInt) && int.TryParse(len, out int lInt))
            {
                return $"{target}.substring({sInt}, {sInt + lInt})";
            }
            return $"{target}.substring({start}, {start} + {len})";
        });
        expr = Regex.Replace(expr, @"(\w+)\.Substring\s*\(([^,\)]+)\)", "$1.substring($2)");

        // Regex mapping
        if (_options.MapRegex)
        {
            expr = Regex.Replace(expr, @"new\s+Regex\s*\((.*),\s*RegexOptions\.IgnoreCase\)", "Pattern.compile($1, Pattern.CASE_INSENSITIVE)");
            expr = Regex.Replace(expr, @"new\s+Regex\s*\((.*)\)", "Pattern.compile($1)");
            expr = Regex.Replace(expr, @"(\w+)\.Match\s*\(([^,\)]+)\)", "$1.matcher($2)");
            expr = Regex.Replace(expr, @"(\w+)\.Success\b", "$1.find()");
            expr = Regex.Replace(expr, @"(\w+)\.Groups\[([^\]]+)\]\.Value", "$1.group($2)");
            expr = Regex.Replace(expr, @"(\w+)\.Value\b", "$1.group()");
        }

        // HTTPS mapping
        if (_options.MapHttpClient)
        {
            expr = Regex.Replace(expr, @"new\s+HttpClient\(\)", "HttpClient.newHttpClient()");
            expr = Regex.Replace(expr, @"new\s+HttpRequestMessage\s*\(HttpMethod\.Get,\s*([^\)]+)\)", "HttpRequest.newBuilder().uri(URI.create($1)).build()");
            expr = Regex.Replace(expr, @"(\w+)\.Send\s*\(([^,\)]+)\)", "$1.send($2, HttpResponse.BodyHandlers.ofString())");
            expr = Regex.Replace(expr, @"\(int\)(\w+)\.StatusCode", "$1.statusCode()");
            expr = Regex.Replace(expr, @"(\w+)\.Content\.ReadAsStringAsync\(\)\.Result", "$1.body()");
        }

        // Collections & String length / size:
        // .Length -> .length() or .length
        expr = Regex.Replace(expr, @"(\w+)\.Length\b", "$1.length()");
        // .Count -> .size()
        expr = Regex.Replace(expr, @"(\w+)\.Count\b", "$1.size()");
        // Indexer assignment: map[k] = v -> map.put(k, v) or list[i] = v -> list.set(i, v)
        expr = Regex.Replace(expr, @"(\w+)\[([^\]]+)\]\s*=\s*([^;,\)]+)", "$1.put($2, $3)");
        // Indexer read: list[i] -> list.get(i)
        expr = Regex.Replace(expr, @"(\w+)\[([^\]]+)\]", "$1.get($2)");
        // .ContainsKey(k) -> .containsKey(k)
        expr = Regex.Replace(expr, @"(\w+)\.ContainsKey\s*\(([^,\)]+)\)", "$1.containsKey($2)");

        // Collections methods:
        expr = Regex.Replace(expr, @"\.Add\s*\(", ".add(");
        expr = Regex.Replace(expr, @"\.Remove\s*\(", ".remove(");
        expr = Regex.Replace(expr, @"\.Clear\s*\(", ".clear(");
        expr = Regex.Replace(expr, @"\.Contains\s*\(", ".contains(");

        // String methods
        expr = Regex.Replace(expr, @"(\w+)\.ToLower\(\)", "$1.toLowerCase()");
        expr = Regex.Replace(expr, @"(\w+)\.ToUpper\(\)", "$1.toUpperCase()");
        expr = Regex.Replace(expr, @"(\w+)\.Trim\(\)", "$1.trim()");
        expr = Regex.Replace(expr, @"(\w+)\.Equals\s*\(([^,\)]+)\)", "$1.equals($2)");

        // Math mapping
        expr = Regex.Replace(expr, @"Math\.(Max|Min|Abs|Sqrt|Pow|Round|Floor|Ceiling)\s*\(", m =>
        {
            string func = m.Groups[1].Value;
            string camel = CasingHelper.ToCamelCase(func);
            if (camel == "ceiling") camel = "ceil";
            return $"Math.{camel}(";
        });

        // Object instantiations: new List<T>() -> new ArrayList<>()
        expr = Regex.Replace(expr, @"new\s+List<([^>]*)>\s*\(", m =>
        {
            var args = TypeMapper.SplitGenericArgs(m.Groups[1].Value);
            var mapped = args.ConvertAll(TypeMapper.MapCSharpToJava);
            return $"new ArrayList<{string.Join(", ", mapped)}>(";
        });
        expr = Regex.Replace(expr, @"new\s+Dictionary<([^>]*)>\s*\(", m =>
        {
            var args = TypeMapper.SplitGenericArgs(m.Groups[1].Value);
            var mapped = args.ConvertAll(TypeMapper.MapCSharpToJava);
            return $"new HashMap<{string.Join(", ", mapped)}>(";
        });
        expr = Regex.Replace(expr, @"new\s+HashSet<([^>]*)>\s*\(", m =>
        {
            var args = TypeMapper.SplitGenericArgs(m.Groups[1].Value);
            var mapped = args.ConvertAll(TypeMapper.MapCSharpToJava);
            return $"new HashSet<{string.Join(", ", mapped)}>(";
        });

        return expr;
    }

    private string TranslateArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return args;
        var parts = TypeMapper.SplitGenericArgs(args);
        return string.Join(", ", parts.ConvertAll(TranslateExpression));
    }

    private string ConvertNamespaceToPackage(string ns)
    {
        var segments = ns.Split('.');
        var lowerSegments = new List<string>();
        foreach (var s in segments)
        {
            lowerSegments.Add(s.ToLowerInvariant());
        }
        return string.Join(".", lowerSegments);
    }
}
