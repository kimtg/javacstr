using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class RoundtripTests
{
    private readonly CodeTranslator _translator = new(new TranslationOptions
    {
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true,
        ConvertProperties = false // preserve fields for roundtrip stability
    });

    [Fact]
    public void TestJavaToCSharpAndBack()
    {
        string originalJava = """
        package com.example.service;

        import java.util.List;

        public class OrderProcessor {
            public int process(int amount) {
                int tax = 5;
                return amount + tax;
            }
        }
        """;

        // Step 1: Java -> C#
        var csResult = _translator.TranslateJavaToCSharp(originalJava);
        Assert.True(csResult.Success, string.Join("\n", csResult.Errors));
        string csharp = csResult.OutputCode;

        Assert.Contains("namespace Com.Example.Service;", csharp);
        Assert.Contains("class OrderProcessor", csharp);
        Assert.Contains("int Process(int amount)", csharp);

        // Step 2: C# -> Java
        var javaResult = _translator.TranslateCSharpToJava(csharp);
        Assert.True(javaResult.Success, string.Join("\n", javaResult.Errors));
        string roundtripJava = javaResult.OutputCode;

        Assert.Contains("package com.example.service;", roundtripJava);
        Assert.Contains("class OrderProcessor", roundtripJava);
        Assert.Contains("int process(int amount)", roundtripJava);
        Assert.Contains("return amount + tax;", roundtripJava);
    }
}
