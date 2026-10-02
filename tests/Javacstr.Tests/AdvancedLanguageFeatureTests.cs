using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class AdvancedLanguageFeatureTests
{
    private readonly JavaToCSharpTranslator _javaToCs = new(new TranslationOptions
    {
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true,
        MapGui = false
    });

    private readonly CSharpToJavaTranslator _csToJava = new(new TranslationOptions
    {
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true,
        MapGui = false
    });

    [Fact]
    public void TestTryWithResourcesJavaToCSharp()
    {
        string java = """
        public class ResourceDemo {
            public void readFile(String path) {
                try (BufferedReader reader = new BufferedReader(new FileReader(path))) {
                    String line = reader.readLine();
                    System.out.println(line);
                }
            }
        }
        """;

        var result = _javaToCs.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("using (StreamReader reader = new StreamReader(path))", result.OutputCode);
        Assert.Contains("string line = reader.ReadLine();", result.OutputCode);
        Assert.Contains("Console.WriteLine(line);", result.OutputCode);
    }

    [Fact]
    public void TestUsingCSharpToJava()
    {
        string csharp = """
        public class ResourceDemo
        {
            public void ReadFile(string path)
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    string line = reader.ReadLine();
                    Console.WriteLine(line);
                }
            }
        }
        """;

        var result = _csToJava.Translate(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("try (BufferedReader reader = new BufferedReader(new FileReader(path)))", result.OutputCode);
        Assert.Contains("String line = reader.readLine();", result.OutputCode);
        Assert.Contains("System.out.println(line);", result.OutputCode);
    }

    [Fact]
    public void TestSynchronizedAndLockBidirectional()
    {
        string java = """
        public class SyncDemo {
            private final Object lockObj = new Object();
            public void safeIncrement() {
                synchronized (lockObj) {
                    count++;
                }
            }
        }
        """;

        var toCs = _javaToCs.Translate(java);
        Assert.True(toCs.Success, string.Join("\n", toCs.Errors));
        Assert.Contains("lock (lockObj)", toCs.OutputCode);

        string csharp = """
        public class SyncDemo
        {
            private readonly object lockObj = new object();
            public void SafeIncrement()
            {
                lock (lockObj)
                {
                    count++;
                }
            }
        }
        """;

        var toJava = _csToJava.Translate(csharp);
        Assert.True(toJava.Success, string.Join("\n", toJava.Errors));
        Assert.Contains("synchronized (lockObj)", toJava.OutputCode);
    }

    [Fact]
    public void TestSwitchStatementBidirectional()
    {
        string java = """
        public class SwitchDemo {
            public String getDay(int day) {
                switch (day) {
                    case 1:
                        return "Monday";
                    case 2:
                        return "Tuesday";
                    default:
                        return "Other";
                }
            }
        }
        """;

        var toCs = _javaToCs.Translate(java);
        Assert.True(toCs.Success, string.Join("\n", toCs.Errors));
        Assert.Contains("switch (day)", toCs.OutputCode);
        Assert.Contains("case 1:", toCs.OutputCode);
        Assert.Contains("return \"Monday\";", toCs.OutputCode);
        Assert.Contains("default:", toCs.OutputCode);

        string csharp = """
        public class SwitchDemo
        {
            public string GetDay(int day)
            {
                switch (day)
                {
                    case 1:
                        return "Monday";
                    case 2:
                        return "Tuesday";
                    default:
                        return "Other";
                }
            }
        }
        """;

        var toJava = _csToJava.Translate(csharp);
        Assert.True(toJava.Success, string.Join("\n", toJava.Errors));
        Assert.Contains("switch (day)", toJava.OutputCode);
        Assert.Contains("case 1:", toJava.OutputCode);
        Assert.Contains("return \"Monday\";", toJava.OutputCode);
        Assert.Contains("default:", toJava.OutputCode);
    }

    [Fact]
    public void TestInterpolatedStringCSharpToJava()
    {
        string csharp = """
        public class MessageUtil
        {
            public void Greet(string name, int age)
            {
                Console.WriteLine($"Hello, {name}! You are {age} years old.");
            }
        }
        """;

        var result = _csToJava.Translate(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("System.out.println(\"Hello, \" + name + \"! You are \" + age + \" years old.\");", result.OutputCode);
    }

    [Fact]
    public void TestStringFormattingAndUtilities()
    {
        string java = """
        public class TextUtil {
            public void test() {
                String formatted = String.format("Hello %s", name);
                String joined = String.join(", ", list);
                int max = Integer.MAX_VALUE;
                int min = Integer.MIN_VALUE;
            }
        }
        """;

        var toCs = _javaToCs.Translate(java);
        Assert.True(toCs.Success, string.Join("\n", toCs.Errors));
        Assert.Contains("string.Format(\"Hello %s\", name)", toCs.OutputCode);
        Assert.Contains("string.Join(\", \", list)", toCs.OutputCode);
        Assert.Contains("int max = int.MaxValue;", toCs.OutputCode);
        Assert.Contains("int min = int.MinValue;", toCs.OutputCode);

        string csharp = """
        public class TextUtil
        {
            public void Test()
            {
                string formatted = string.Format("Hello {0}", name);
                string joined = string.Join(", ", list);
                int max = int.MaxValue;
                int min = int.MinValue;
            }
        }
        """;

        var toJava = _csToJava.Translate(csharp);
        Assert.True(toJava.Success, string.Join("\n", toJava.Errors));
        Assert.Contains("String.format(\"Hello {0}\", name)", toJava.OutputCode);
        Assert.Contains("String.join(\", \", list)", toJava.OutputCode);
        Assert.Contains("int max = Integer.MAX_VALUE;", toJava.OutputCode);
        Assert.Contains("int min = Integer.MIN_VALUE;", toJava.OutputCode);
    }

    [Fact]
    public void TestBreakAndContinueBidirectional()
    {
        string java = """
        public class LoopControl {
            public void test() {
                for (int i = 0; i < 10; i++) {
                    if (i == 2) continue;
                    if (i == 8) break;
                }
            }
        }
        """;

        var toCs = _javaToCs.Translate(java);
        Assert.True(toCs.Success, string.Join("\n", toCs.Errors));
        Assert.Contains("continue;", toCs.OutputCode);
        Assert.Contains("break;", toCs.OutputCode);

        string csharp = """
        public class LoopControl
        {
            public void Test()
            {
                for (int i = 0; i < 10; i++)
                {
                    if (i == 2) continue;
                    if (i == 8) break;
                }
            }
        }
        """;

        var toJava = _csToJava.Translate(csharp);
        Assert.True(toJava.Success, string.Join("\n", toJava.Errors));
        Assert.Contains("continue;", toJava.OutputCode);
        Assert.Contains("break;", toJava.OutputCode);
    }

    [Fact]
    public void TestDefaultNoCasingAndCasingOption()
    {
        string java = """
        public class MyService {
            public void processData() {
            }
        }
        """;

        // Default options: ConvertMethodCasing should be false (no casing change)
        var defaultToCs = new JavaToCSharpTranslator(new TranslationOptions()).Translate(java);
        Assert.True(defaultToCs.Success);
        Assert.Contains("void processData()", defaultToCs.OutputCode);

        // With ConvertMethodCasing = true (casing option enabled)
        var casingToCs = new JavaToCSharpTranslator(new TranslationOptions { ConvertMethodCasing = true }).Translate(java);
        Assert.True(casingToCs.Success);
        Assert.Contains("void ProcessData()", casingToCs.OutputCode);

        string csharp = """
        public class MyService
        {
            public void ProcessData()
            {
            }
        }
        """;

        // Default options: ConvertMethodCasing should be false (no casing change)
        var defaultToJava = new CSharpToJavaTranslator(new TranslationOptions()).Translate(csharp);
        Assert.True(defaultToJava.Success);
        Assert.Contains("void ProcessData()", defaultToJava.OutputCode);

        // With ConvertMethodCasing = true (casing option enabled)
        var casingToJava = new CSharpToJavaTranslator(new TranslationOptions { ConvertMethodCasing = true }).Translate(csharp);
        Assert.True(casingToJava.Success);
        Assert.Contains("void processData()", casingToJava.OutputCode);
    }
}
