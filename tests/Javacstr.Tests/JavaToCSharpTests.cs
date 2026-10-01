using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class JavaToCSharpTests
{
    private readonly JavaToCSharpTranslator _translator = new(new TranslationOptions
    {
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true
    });

    [Fact]
    public void TestInheritanceAndOverride()
    {
        string java = """
        package com.example;

        public class Dog extends Animal implements Pet {
            public Dog(String name) {
                super(name);
            }

            @Override
            public void makeSound() {
                System.out.println("Woof");
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string csharp = result.OutputCode;
        Assert.Contains("namespace Com.Example;", csharp);
        Assert.Contains("class Dog : Animal, Pet", csharp);
        Assert.Contains("Dog(string name) : base(name)", csharp);
        Assert.Contains("public override void MakeSound()", csharp);
        Assert.Contains("Console.WriteLine(\"Woof\");", csharp);
    }

    [Fact]
    public void TestCollectionsAndIndexing()
    {
        string java = """
        public class CollectionHelper {
            public void process() {
                List<String> list = new ArrayList<>();
                list.add("hello");
                String first = list.get(0);
                int count = list.size();

                Map<String, Integer> map = new HashMap<>();
                map.put("key", 42);
                int val = map.get("key");
                boolean has = map.containsKey("key");
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string csharp = result.OutputCode;
        Assert.Contains("List<string> list = new List<string>()", csharp);
        Assert.Contains("string first = list[0]", csharp);
        Assert.Contains("int count = list.Count", csharp);
        Assert.Contains("Dictionary<string, int> map = new Dictionary<string, int>()", csharp);
        Assert.Contains("map[\"key\"] = 42", csharp);
        Assert.Contains("int val = map[\"key\"]", csharp);
        Assert.Contains("bool has = map.ContainsKey(\"key\")", csharp);
    }

    [Fact]
    public void TestSubstringCalculation()
    {
        string java = """
        public class SubstringTest {
            public String slice(String str) {
                return str.substring(2, 7);
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string csharp = result.OutputCode;
        // In Java: substring(2, 7) means start=2, end=7 -> length=5
        // In C#: Substring(2, 5)
        Assert.Contains("str.Substring(2, 5)", csharp);
    }
}
