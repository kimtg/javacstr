using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class RegexTranslationTests
{
    private readonly CodeTranslator _translator = new(new TranslationOptions
    {
        MapRegex = true,
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true
    });

    [Fact]
    public void TestJavaRegexToCSharp()
    {
        string java = """
        package com.example.regex;

        import java.util.regex.Pattern;
        import java.util.regex.Matcher;

        public class RegexTester {
            public void matchDigits(String text) {
                Pattern pattern = Pattern.compile("(\\d+)");
                Matcher matcher = pattern.matcher(text);
                if (matcher.find()) {
                    String groupVal = matcher.group(1);
                    String wholeVal = matcher.group();
                }
            }

            public void caseInsensitive(String text) {
                Pattern p = Pattern.compile("test", Pattern.CASE_INSENSITIVE);
            }
        }
        """;

        var result = _translator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string cs = result.OutputCode;
        Assert.Contains("using System.Text.RegularExpressions;", cs);
        Assert.Contains("""Regex pattern = new Regex("(\\d+)");""", cs);
        Assert.Contains("Match matcher = pattern.Match(text);", cs);
        Assert.Contains("if (matcher.Success)", cs);
        Assert.Contains("string groupVal = matcher.Groups[1].Value;", cs);
        Assert.Contains("string wholeVal = matcher.Value;", cs);
        Assert.Contains("new Regex(\"test\", RegexOptions.IgnoreCase)", cs);
    }

    [Fact]
    public void TestCSharpRegexToJava()
    {
        string csharp = """
        namespace Com.Example.Regex;

        using System.Text.RegularExpressions;

        public class RegexTester
        {
            public void MatchDigits(string text)
            {
                Regex pattern = new Regex("(\\d+)");
                Match matcher = pattern.Match(text);
                if (matcher.Success)
                {
                    string groupVal = matcher.Groups[1].Value;
                    string wholeVal = matcher.Value;
                }
            }

            public void CaseInsensitive(string text)
            {
                Regex p = new Regex("test", RegexOptions.IgnoreCase);
            }
        }
        """;

        var result = _translator.TranslateCSharpToJava(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        Assert.Contains("import java.util.regex.*;", java);
        Assert.Contains("""Pattern pattern = Pattern.compile("(\\d+)");""", java);
        Assert.Contains("Matcher matcher = pattern.matcher(text);", java);
        Assert.Contains("if (matcher.find())", java);
        Assert.Contains("String groupVal = matcher.group(1);", java);
        Assert.Contains("String wholeVal = matcher.group();", java);
        Assert.Contains("Pattern.compile(\"test\", Pattern.CASE_INSENSITIVE)", java);
    }
}
