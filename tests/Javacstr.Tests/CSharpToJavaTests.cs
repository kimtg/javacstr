using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class CSharpToJavaTests
{
    private readonly CSharpToJavaTranslator _translator = new(new TranslationOptions
    {
        ConvertMethodCasing = true,
        ConvertProperties = true
    });

    [Fact]
    public void TestInheritanceAndOverride()
    {
        string csharp = """
        namespace Com.Example;

        public class Dog : Animal, IPet
        {
            public Dog(string name) : base(name)
            {
            }

            public override void MakeSound()
            {
                Console.WriteLine("Woof");
            }
        }
        """;

        var result = _translator.Translate(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        Assert.Contains("package com.example;", java);
        Assert.Contains("class Dog extends Animal implements IPet", java);
        Assert.Contains("super(name);", java);
        Assert.Contains("@Override", java);
        Assert.Contains("public void makeSound()", java);
        Assert.Contains("System.out.println(\"Woof\");", java);
    }

    [Fact]
    public void TestPropertiesConversion()
    {
        string csharp = """
        public class User
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }
        """;

        var result = _translator.Translate(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        Assert.Contains("private String name;", java);
        Assert.Contains("public String getName()", java);
        Assert.Contains("public void setName(String name)", java);
        Assert.Contains("private int age;", java);
        Assert.Contains("public int getAge()", java);
        Assert.Contains("public void setAge(int age)", java);
    }

    [Fact]
    public void TestSubstringAndCollections()
    {
        string csharp = """
        public class SliceUtil
        {
            public string Cut(string s)
            {
                return s.Substring(2, 5);
            }

            public void CollectionsTest()
            {
                List<string> list = new List<string>();
                string item = list[0];
                int c = list.Count;
            }
        }
        """;

        var result = _translator.Translate(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        // Substring(2, 5) -> substring(2, 7)
        Assert.Contains("s.substring(2, 7)", java);
        Assert.Contains("list.get(0)", java);
        Assert.Contains("list.size()", java);
    }
}
