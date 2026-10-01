using System;
using System.Collections.Generic;
using System.Text;

namespace Javacstr.Core.Java;

public class JavaEmitter
{
    private readonly StringBuilder _sb = new();
    private int _indentLevel;

    private void Indent() => _sb.Append(new string(' ', _indentLevel * 4));
    private void Indent(int level) => _sb.Append(new string(' ', level * 4));

    public string Emit(CompilationUnitNode cu)
    {
        _sb.Clear();
        _indentLevel = 0;

        // Package
        if (cu.Package != null && !string.IsNullOrWhiteSpace(cu.Package.Name))
        {
            _sb.AppendLine($"package {cu.Package.Name};");
            _sb.AppendLine();
        }

        // Imports
        if (cu.Imports.Count > 0)
        {
            foreach (var imp in cu.Imports)
            {
                string staticKw = imp.IsStatic ? "static " : "";
                _sb.AppendLine($"import {staticKw}{imp.Path};");
            }
            _sb.AppendLine();
        }

        // Types
        for (int i = 0; i < cu.Types.Count; i++)
        {
            EmitType(cu.Types[i]);
            if (i < cu.Types.Count - 1)
            {
                _sb.AppendLine();
            }
        }

        return _sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public void EmitType(TypeDeclarationNode type)
    {
        // Annotations
        foreach (var anno in type.Annotations)
        {
            Indent();
            _sb.AppendLine(string.IsNullOrEmpty(anno.Arguments) ? $"@{anno.Name}" : $"@{anno.Name}({anno.Arguments})");
        }

        Indent();
        if (type.Modifiers.Count > 0)
        {
            _sb.Append(string.Join(" ", type.Modifiers) + " ");
        }

        _sb.Append($"{type.Kind} {type.Name}");
        if (!string.IsNullOrEmpty(type.TypeParameters))
        {
            _sb.Append(type.TypeParameters);
        }

        if (!string.IsNullOrEmpty(type.ExtendsType))
        {
            _sb.Append($" extends {type.ExtendsType}");
        }

        if (type.ImplementsTypes.Count > 0)
        {
            _sb.Append($" implements {string.Join(", ", type.ImplementsTypes)}");
        }

        _sb.AppendLine();
        Indent();
        _sb.AppendLine("{");
        _indentLevel++;

        for (int i = 0; i < type.Members.Count; i++)
        {
            var member = type.Members[i];
            EmitMember(member);
            if (i < type.Members.Count - 1)
            {
                _sb.AppendLine();
            }
        }

        _indentLevel--;
        Indent();
        _sb.AppendLine("}");
    }

    private void EmitMember(MemberNode member)
    {
        switch (member)
        {
            case FieldDeclarationNode field:
                EmitField(field);
                break;
            case ConstructorDeclarationNode ctor:
                EmitConstructor(ctor);
                break;
            case MethodDeclarationNode method:
                EmitMethod(method);
                break;
            case TypeDeclarationNode nestedType:
                EmitType(nestedType);
                break;
        }
    }

    private void EmitField(FieldDeclarationNode field)
    {
        foreach (var anno in field.Annotations)
        {
            Indent();
            _sb.AppendLine(string.IsNullOrEmpty(anno.Arguments) ? $"@{anno.Name}" : $"@{anno.Name}({anno.Arguments})");
        }

        Indent();
        if (field.Modifiers.Count > 0)
        {
            _sb.Append(string.Join(" ", field.Modifiers) + " ");
        }
        _sb.Append($"{field.Type} {field.Name}");
        if (!string.IsNullOrEmpty(field.Initializer))
        {
            _sb.Append($" = {field.Initializer}");
        }
        _sb.AppendLine(";");
    }

    private void EmitConstructor(ConstructorDeclarationNode ctor)
    {
        Indent();
        if (ctor.Modifiers.Count > 0)
        {
            _sb.Append(string.Join(" ", ctor.Modifiers) + " ");
        }

        _sb.Append($"{ctor.Name}(");
        EmitParameters(ctor.Parameters);
        _sb.Append(")");

        if (ctor.Throws.Count > 0)
        {
            _sb.Append($" throws {string.Join(", ", ctor.Throws)}");
        }

        _sb.AppendLine();
        Indent();
        _sb.AppendLine("{");
        _indentLevel++;

        if (!string.IsNullOrEmpty(ctor.InitializerCall))
        {
            Indent();
            _sb.AppendLine($"{ctor.InitializerCall};");
        }

        foreach (var stmt in ctor.Body.Statements)
        {
            EmitStatement(stmt);
        }

        _indentLevel--;
        Indent();
        _sb.AppendLine("}");
    }

    private void EmitMethod(MethodDeclarationNode method)
    {
        foreach (var anno in method.Annotations)
        {
            Indent();
            _sb.AppendLine(string.IsNullOrEmpty(anno.Arguments) ? $"@{anno.Name}" : $"@{anno.Name}({anno.Arguments})");
        }

        Indent();
        if (method.Modifiers.Count > 0)
        {
            _sb.Append(string.Join(" ", method.Modifiers) + " ");
        }

        if (!string.IsNullOrEmpty(method.TypeParameters))
        {
            _sb.Append(method.TypeParameters + " ");
        }

        _sb.Append($"{method.ReturnType} {method.Name}(");
        EmitParameters(method.Parameters);
        _sb.Append(")");

        if (method.Throws.Count > 0)
        {
            _sb.Append($" throws {string.Join(", ", method.Throws)}");
        }

        if (method.Body == null)
        {
            _sb.AppendLine(";");
        }
        else
        {
            _sb.AppendLine();
            Indent();
            _sb.AppendLine("{");
            _indentLevel++;

            foreach (var stmt in method.Body.Statements)
            {
                EmitStatement(stmt);
            }

            _indentLevel--;
            Indent();
            _sb.AppendLine("}");
        }
    }

    private void EmitParameters(List<ParameterNode> parameters)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            _sb.Append($"{p.Type} {p.Name}");
            if (i < parameters.Count - 1)
            {
                _sb.Append(", ");
            }
        }
    }

    public void EmitStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case BlockStatementNode block:
                Indent();
                _sb.AppendLine("{");
                _indentLevel++;
                foreach (var s in block.Statements)
                {
                    EmitStatement(s);
                }
                _indentLevel--;
                Indent();
                _sb.AppendLine("}");
                break;

            case VariableDeclarationStatementNode vDecl:
                Indent();
                if (vDecl.Modifiers.Count > 0)
                {
                    _sb.Append(string.Join(" ", vDecl.Modifiers) + " ");
                }
                _sb.Append($"{vDecl.Type} {vDecl.Name}");
                if (!string.IsNullOrEmpty(vDecl.Initializer))
                {
                    _sb.Append($" = {vDecl.Initializer}");
                }
                _sb.AppendLine(";");
                break;

            case ExpressionStatementNode exprStmt:
                Indent();
                _sb.AppendLine($"{exprStmt.Expression};");
                break;

            case IfStatementNode ifStmt:
                Indent();
                _sb.Append($"if ({ifStmt.Condition})");
                EmitSingleOrBlockStatement(ifStmt.ThenStatement);
                if (ifStmt.ElseStatement != null)
                {
                    Indent();
                    _sb.Append("else");
                    EmitSingleOrBlockStatement(ifStmt.ElseStatement);
                }
                break;

            case WhileStatementNode whileStmt:
                Indent();
                _sb.Append($"while ({whileStmt.Condition})");
                EmitSingleOrBlockStatement(whileStmt.Body);
                break;

            case DoWhileStatementNode doStmt:
                Indent();
                _sb.AppendLine("do");
                EmitStatement(doStmt.Body);
                Indent();
                _sb.AppendLine($"while ({doStmt.Condition});");
                break;

            case ForStatementNode forStmt:
                Indent();
                _sb.Append($"for ({forStmt.Init ?? ""}; {forStmt.Condition ?? ""}; {forStmt.Update ?? ""})");
                EmitSingleOrBlockStatement(forStmt.Body);
                break;

            case ForEachStatementNode forEachStmt:
                Indent();
                _sb.Append($"for ({forEachStmt.VariableType} {forEachStmt.VariableName} : {forEachStmt.Iterable})");
                EmitSingleOrBlockStatement(forEachStmt.Body);
                break;

            case ReturnStatementNode retStmt:
                Indent();
                if (string.IsNullOrEmpty(retStmt.Expression))
                    _sb.AppendLine("return;");
                else
                    _sb.AppendLine($"return {retStmt.Expression};");
                break;

            case ThrowStatementNode thrStmt:
                Indent();
                _sb.AppendLine($"throw {thrStmt.Expression};");
                break;

            case TryStatementNode tryStmt:
                Indent();
                if (!string.IsNullOrEmpty(tryStmt.ResourceSpecification))
                    _sb.AppendLine($"try ({tryStmt.ResourceSpecification})");
                else
                    _sb.AppendLine("try");

                EmitStatement(tryStmt.Body);

                foreach (var catchClause in tryStmt.CatchClauses)
                {
                    Indent();
                    _sb.AppendLine($"catch ({catchClause.ExceptionType} {catchClause.VariableName})");
                    EmitStatement(catchClause.Body);
                }

                if (tryStmt.FinallyBlock != null)
                {
                    Indent();
                    _sb.AppendLine("finally");
                    EmitStatement(tryStmt.FinallyBlock);
                }
                break;

            case RawStatementNode rawStmt:
                Indent();
                _sb.AppendLine(rawStmt.Text.EndsWith(";") ? rawStmt.Text : rawStmt.Text + ";");
                break;
        }
    }

    private void EmitSingleOrBlockStatement(StatementNode stmt)
    {
        if (stmt is BlockStatementNode block)
        {
            _sb.AppendLine();
            Indent();
            _sb.AppendLine("{");
            _indentLevel++;
            foreach (var s in block.Statements)
            {
                EmitStatement(s);
            }
            _indentLevel--;
            Indent();
            _sb.AppendLine("}");
        }
        else
        {
            _sb.AppendLine();
            _indentLevel++;
            EmitStatement(stmt);
            _indentLevel--;
        }
    }
}
