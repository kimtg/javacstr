using System;
using System.Text.RegularExpressions;

namespace Javacstr.Core.Mapping;

public static class ApiMapper
{
    public static string TranslateJavaMethodCallToCSharp(string target, string methodName, List<string> args)
    {
        // Console / Print
        if (target is "System.out" or "out")
        {
            if (methodName == "println")
                return $"Console.WriteLine({string.Join(", ", args)})";
            if (methodName == "print")
                return $"Console.Write({string.Join(", ", args)})";
        }
        if (target is "System.err" or "err")
        {
            if (methodName == "println")
                return $"Console.Error.WriteLine({string.Join(", ", args)})";
            if (methodName == "print")
                return $"Console.Error.Write({string.Join(", ", args)})";
        }

        // Substring math: Java is (start, end) -> C# is (start, end - start)
        if (methodName == "substring")
        {
            if (args.Count == 1)
            {
                return $"{target}.Substring({args[0]})";
            }
            if (args.Count == 2)
            {
                string start = args[0].Trim();
                string end = args[1].Trim();

                // If both are integer literals e.g. (2, 7) -> (2, 5)
                if (int.TryParse(start, out int sInt) && int.TryParse(end, out int eInt))
                {
                    return $"{target}.Substring({sInt}, {eInt - sInt})";
                }
                return $"{target}.Substring({start}, {end} - ({start}))";
            }
        }

        // String / Collection length / size
        if (methodName == "length" && args.Count == 0)
        {
            return $"{target}.Length";
        }
        if (methodName == "size" && args.Count == 0)
        {
            return $"{target}.Count";
        }

        // List / Map indexing
        if (methodName == "get" && args.Count == 1)
        {
            return $"{target}[{args[0]}]";
        }
        if (methodName == "set" && args.Count == 2)
        {
            return $"{target}[{args[0]}] = {args[1]}";
        }
        if (methodName == "put" && args.Count == 2)
        {
            return $"{target}[{args[0]}] = {args[1]}";
        }
        if (methodName == "containsKey" && args.Count == 1)
        {
            return $"{target}.ContainsKey({args[0]})";
        }
        if (methodName == "containsValue" && args.Count == 1)
        {
            return $"{target}.ContainsValue({args[0]})";
        }
        if (methodName == "keySet" && args.Count == 0)
        {
            return $"{target}.Keys";
        }
        if (methodName == "values" && args.Count == 0)
        {
            return $"{target}.Values";
        }

        // String operations
        if (methodName == "toLowerCase" && args.Count == 0)
            return $"{target}.ToLower()";
        if (methodName == "toUpperCase" && args.Count == 0)
            return $"{target}.ToUpper()";
        if (methodName == "trim" && args.Count == 0)
            return $"{target}.Trim()";
        if (methodName == "isEmpty" && args.Count == 0)
            return $"{target}.Length == 0";
        if (methodName == "equalsIgnoreCase" && args.Count == 1)
            return $"string.Equals({target}, {args[0]}, StringComparison.OrdinalIgnoreCase)";
        if (methodName == "charAt" && args.Count == 1)
            return $"{target}[{args[0]}]";

        // Regex operations
        if (target == "Pattern" && methodName == "compile")
        {
            if (args.Count == 1)
                return $"new Regex({args[0]})";
            if (args.Count == 2)
            {
                string options = args[1].Contains("CASE_INSENSITIVE") ? "RegexOptions.IgnoreCase" : args[1];
                return $"new Regex({args[0]}, {options})";
            }
        }
        if (methodName == "matcher" && args.Count == 1)
        {
            return $"{target}.Match({args[0]})";
        }
        if (methodName == "find" && args.Count == 0)
        {
            return $"{target}.Success";
        }
        if (methodName == "group")
        {
            if (args.Count == 0) return $"{target}.Value";
            if (args.Count == 1) return $"{target}.Groups[{args[0]}].Value";
        }
        if (methodName == "matches" && target == "Pattern")
        {
            if (args.Count == 2) return $"Regex.IsMatch({args[1]}, {args[0]})";
        }

        // HTTP client operations
        if (target == "HttpClient" && (methodName == "newHttpClient" || methodName == "newBuilder"))
        {
            return "new HttpClient()";
        }
        if (methodName == "statusCode" && args.Count == 0)
        {
            return $"(int){target}.StatusCode";
        }
        if (methodName == "body" && args.Count == 0)
        {
            return $"{target}.Content.ReadAsStringAsync().Result";
        }
        if (methodName == "uri" && args.Count == 1)
        {
            return $"{target}.RequestUri = {args[0]}";
        }

        // Math functions
        if (target == "Math")
        {
            string pascal = CasingHelper.ToPascalCase(methodName);
            if (pascal == "Ceil") pascal = "Ceiling";
            return $"Math.{pascal}({string.Join(", ", args)})";
        }

        // Thread.sleep
        if (target == "Thread" && methodName == "sleep")
        {
            return $"Thread.Sleep({string.Join(", ", args)})";
        }

        // Default: method call with PascalCase method name
        string csharpMethod = CasingHelper.ToPascalCase(methodName);
        return string.IsNullOrEmpty(target)
            ? $"{csharpMethod}({string.Join(", ", args)})"
            : $"{target}.{csharpMethod}({string.Join(", ", args)})";
    }

    public static string TranslateCSharpMethodCallToJava(string target, string methodName, List<string> args)
    {
        // Console / Print
        if (target == "Console")
        {
            if (methodName == "WriteLine")
                return $"System.out.println({string.Join(", ", args)})";
            if (methodName == "Write")
                return $"System.out.print({string.Join(", ", args)})";
        }
        if (target == "Console.Error")
        {
            if (methodName == "WriteLine")
                return $"System.err.println({string.Join(", ", args)})";
            if (methodName == "Write")
                return $"System.err.print({string.Join(", ", args)})";
        }

        // Substring: C# is (start, length) -> Java is (start, start + length)
        if (methodName == "Substring")
        {
            if (args.Count == 1)
            {
                return $"{target}.substring({args[0]})";
            }
            if (args.Count == 2)
            {
                string start = args[0].Trim();
                string len = args[1].Trim();

                if (int.TryParse(start, out int sInt) && int.TryParse(len, out int lInt))
                {
                    return $"{target}.substring({sInt}, {sInt + lInt})";
                }
                return $"{target}.substring({start}, {start} + {len})";
            }
        }

        // String operations
        if (methodName == "ToLower" && args.Count == 0)
            return $"{target}.toLowerCase()";
        if (methodName == "ToUpper" && args.Count == 0)
            return $"{target}.toUpperCase()";
        if (methodName == "Trim" && args.Count == 0)
            return $"{target}.trim()";
        if (methodName == "Contains" && args.Count == 1)
            return $"{target}.contains({args[0]})";
        if (methodName == "StartsWith" && args.Count == 1)
            return $"{target}.startsWith({args[0]})";
        if (methodName == "EndsWith" && args.Count == 1)
            return $"{target}.endsWith({args[0]})";
        if (methodName == "IndexOf" && args.Count >= 1)
            return $"{target}.indexOf({string.Join(", ", args)})";

        // Regex operations
        if (target == "Regex" && methodName == "Match")
        {
            if (args.Count == 2)
                return $"Pattern.compile({args[1]}).matcher({args[0]})";
        }
        if (target == "Regex" && methodName == "IsMatch")
        {
            if (args.Count == 2)
                return $"Pattern.matches({args[1]}, {args[0]})";
        }
        if (methodName == "Match" && args.Count == 1)
        {
            return $"{target}.matcher({args[0]})";
        }

        // Math operations
        if (target == "Math")
        {
            string camel = CasingHelper.ToCamelCase(methodName);
            if (camel == "ceiling") camel = "ceil";
            return $"Math.{camel}({string.Join(", ", args)})";
        }

        // Thread.Sleep
        if (target == "Thread" && methodName == "Sleep")
        {
            return $"Thread.sleep({string.Join(", ", args)})";
        }

        // Default: method call with camelCase method name
        string javaMethod = CasingHelper.ToCamelCase(methodName);
        return string.IsNullOrEmpty(target)
            ? $"{javaMethod}({string.Join(", ", args)})"
            : $"{target}.{javaMethod}({string.Join(", ", args)})";
    }
}
