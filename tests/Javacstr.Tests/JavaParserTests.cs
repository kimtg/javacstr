using Xunit;
using Javacstr.Core.Java;

namespace Javacstr.Tests;

public class JavaParserTests
{
    [Fact]
    public void TestParseClassWithPackageAndImports()
    {
        string code = """
        package com.example.model;

        import java.util.List;
        import java.util.Map;

        public class User {
            private String name;
            private int age;

            public User(String name, int age) {
                this.name = name;
                this.age = age;
            }

            public String getName() {
                return this.name;
            }
        }
        """;

        var lexer = new JavaLexer(code);
        var tokens = lexer.Tokenize();
        var parser = new JavaParser(tokens);
        var cu = parser.ParseCompilationUnit();

        Assert.NotNull(cu.Package);
        Assert.Equal("com.example.model", cu.Package.Name);
        Assert.Equal(2, cu.Imports.Count);
        Assert.Single(cu.Types);

        var type = cu.Types[0];
        Assert.Equal("User", type.Name);
        Assert.Equal("class", type.Kind);
        Assert.Equal(4, type.Members.Count); // 2 fields, 1 constructor, 1 method
    }

    [Fact]
    public void TestConstructorAssignments()
    {
        string code = """
        public class Foo {
            public Foo() {
                this.roles = new ArrayList<>();
            }
        }
        """;
        var lexer = new JavaLexer(code);
        var tokens = lexer.Tokenize();
        var parser = new JavaParser(tokens);
        var cu = parser.ParseCompilationUnit();
        var ctor = Assert.IsType<ConstructorDeclarationNode>(cu.Types[0].Members[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(ctor.Body.Statements[0]);
        Assert.Equal("this.roles = new ArrayList<>()", stmt.Expression);
    }

    [Fact]
    public void TestParseInheritanceAndConstructorChaining()
    {
        string code = """
        public class Dog extends Animal implements Pet, Runnable {
            public Dog(String name) {
                super(name);
            }

            @Override
            public void run() {
                System.out.println("Dog running");
            }
        }
        """;

        var lexer = new JavaLexer(code);
        var tokens = lexer.Tokenize();
        var parser = new JavaParser(tokens);
        var cu = parser.ParseCompilationUnit();

        var dog = cu.Types[0];
        Assert.Equal("Animal", dog.ExtendsType);
        Assert.Contains("Pet", dog.ImplementsTypes);
        Assert.Contains("Runnable", dog.ImplementsTypes);

        var ctor = Assert.IsType<ConstructorDeclarationNode>(dog.Members[0]);
        Assert.Equal("super(name)", ctor.InitializerCall);

        var method = Assert.IsType<MethodDeclarationNode>(dog.Members[1]);
        Assert.Single(method.Annotations);
        Assert.Equal("Override", method.Annotations[0].Name);
    }
}
