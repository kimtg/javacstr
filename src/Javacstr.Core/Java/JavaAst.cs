using System.Collections.Generic;

namespace Javacstr.Core.Java;

public abstract class JavaNode
{
}

public class CompilationUnitNode : JavaNode
{
    public PackageDeclarationNode? Package { get; set; }
    public List<ImportDeclarationNode> Imports { get; set; } = new();
    public List<TypeDeclarationNode> Types { get; set; } = new();
}

public class PackageDeclarationNode : JavaNode
{
    public string Name { get; set; } = string.Empty;
}

public class ImportDeclarationNode : JavaNode
{
    public bool IsStatic { get; set; }
    public string Path { get; set; } = string.Empty;
}

public class AnnotationNode : JavaNode
{
    public string Name { get; set; } = string.Empty;
    public string? Arguments { get; set; }
}

public abstract class MemberNode : JavaNode
{
    public List<string> Modifiers { get; set; } = new();
    public List<AnnotationNode> Annotations { get; set; } = new();
    public List<string> Comments { get; set; } = new();
}

public class TypeDeclarationNode : MemberNode
{
    public string Kind { get; set; } = "class"; // class, interface, enum, record
    public string Name { get; set; } = string.Empty;
    public string? TypeParameters { get; set; }
    public string? ExtendsType { get; set; }
    public List<string> ImplementsTypes { get; set; } = new();
    public List<MemberNode> Members { get; set; } = new();
}

public class FieldDeclarationNode : MemberNode
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Initializer { get; set; }
}

public class ParameterNode : JavaNode
{
    public List<AnnotationNode> Annotations { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
}

public class ConstructorDeclarationNode : MemberNode
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterNode> Parameters { get; set; } = new();
    public List<string> Throws { get; set; } = new();
    public string? InitializerCall { get; set; } // super(...) or this(...)
    public BlockStatementNode Body { get; set; } = new();
}

public class MethodDeclarationNode : MemberNode
{
    public string? TypeParameters { get; set; }
    public string ReturnType { get; set; } = "void";
    public string Name { get; set; } = string.Empty;
    public List<ParameterNode> Parameters { get; set; } = new();
    public List<string> Throws { get; set; } = new();
    public BlockStatementNode? Body { get; set; } // null if abstract or interface method
}

public abstract class StatementNode : JavaNode
{
}

public class BlockStatementNode : StatementNode
{
    public List<StatementNode> Statements { get; set; } = new();
}

public class ExpressionStatementNode : StatementNode
{
    public string Expression { get; set; } = string.Empty;
}

public class VariableDeclarationStatementNode : StatementNode
{
    public List<string> Modifiers { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Initializer { get; set; }
}

public class IfStatementNode : StatementNode
{
    public string Condition { get; set; } = string.Empty;
    public StatementNode ThenStatement { get; set; } = new BlockStatementNode();
    public StatementNode? ElseStatement { get; set; }
}

public class WhileStatementNode : StatementNode
{
    public string Condition { get; set; } = string.Empty;
    public StatementNode Body { get; set; } = new BlockStatementNode();
}

public class DoWhileStatementNode : StatementNode
{
    public StatementNode Body { get; set; } = new BlockStatementNode();
    public string Condition { get; set; } = string.Empty;
}

public class ForStatementNode : StatementNode
{
    public string? Init { get; set; }
    public string? Condition { get; set; }
    public string? Update { get; set; }
    public StatementNode Body { get; set; } = new BlockStatementNode();
}

public class ForEachStatementNode : StatementNode
{
    public string VariableType { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    public string Iterable { get; set; } = string.Empty;
    public StatementNode Body { get; set; } = new BlockStatementNode();
}

public class ReturnStatementNode : StatementNode
{
    public string? Expression { get; set; }
}

public class ThrowStatementNode : StatementNode
{
    public string Expression { get; set; } = string.Empty;
}

public class CatchClauseNode : JavaNode
{
    public string ExceptionType { get; set; } = "Exception";
    public string VariableName { get; set; } = "e";
    public BlockStatementNode Body { get; set; } = new();
}

public class TryStatementNode : StatementNode
{
    public string? ResourceSpecification { get; set; }
    public BlockStatementNode Body { get; set; } = new();
    public List<CatchClauseNode> CatchClauses { get; set; } = new();
    public BlockStatementNode? FinallyBlock { get; set; }
}

public class RawStatementNode : StatementNode
{
    public string Text { get; set; } = string.Empty;

    public RawStatementNode(string text)
    {
        Text = text;
    }
}
