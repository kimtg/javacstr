using System.Collections.Generic;

namespace Javacstr.Core.Mapping;

public static class TypeMapper
{
    private static readonly Dictionary<string, string> JavaToCSharpTypes = new()
    {
        { "boolean", "bool" },
        { "Boolean", "bool" },
        { "byte", "byte" },
        { "Byte", "byte" },
        { "short", "short" },
        { "Short", "short" },
        { "int", "int" },
        { "Integer", "int" },
        { "long", "long" },
        { "Long", "long" },
        { "float", "float" },
        { "Float", "float" },
        { "double", "double" },
        { "Double", "double" },
        { "char", "char" },
        { "Character", "char" },
        { "String", "string" },
        { "Object", "object" },
        { "void", "void" },

        // Collections
        { "List", "List" },
        { "ArrayList", "List" },
        { "LinkedList", "LinkedList" },
        { "Map", "Dictionary" },
        { "HashMap", "Dictionary" },
        { "LinkedHashMap", "Dictionary" },
        { "TreeMap", "SortedDictionary" },
        { "Set", "HashSet" },
        { "HashSet", "HashSet" },
        { "Queue", "Queue" },
        { "Deque", "Queue" },
        { "Stack", "Stack" },
        { "Iterable", "IEnumerable" },
        { "Iterator", "IEnumerator" },
        { "Collection", "ICollection" },

        // Regex
        { "Pattern", "Regex" },
        { "Matcher", "Match" },

        // HTTP
        { "HttpClient", "HttpClient" },
        { "HttpRequest", "HttpRequestMessage" },
        { "HttpResponse", "HttpResponseMessage" },
        { "URI", "Uri" },
        { "URL", "Uri" },

        // Exceptions
        { "Exception", "Exception" },
        { "Throwable", "Exception" },
        { "RuntimeException", "Exception" },
        { "NullPointerException", "NullReferenceException" },
        { "IllegalArgumentException", "ArgumentException" },
        { "IllegalStateException", "InvalidOperationException" },
        { "IndexOutOfBoundsException", "ArgumentOutOfRangeException" },
        { "ArrayIndexOutOfBoundsException", "IndexOutOfRangeException" },
        { "UnsupportedOperationException", "NotSupportedException" },
        { "IOException", "IOException" },
        { "NoSuchElementException", "InvalidOperationException" },
        { "SocketException", "SocketException" },
        { "UnknownHostException", "SocketException" },
        { "ClassNotFoundException", "TypeLoadException" },
        { "InstantiationException", "MemberAccessException" },
        { "IllegalAccessException", "UnauthorizedAccessException" },
        { "UnsupportedLookAndFeelException", "NotSupportedException" }
    };

    private static readonly Dictionary<string, string> CSharpToJavaTypes = new()
    {
        { "bool", "boolean" },
        { "Boolean", "boolean" },
        { "byte", "byte" },
        { "Byte", "byte" },
        { "sbyte", "byte" },
        { "short", "short" },
        { "Int16", "short" },
        { "int", "int" },
        { "Int32", "int" },
        { "long", "long" },
        { "Int64", "long" },
        { "float", "float" },
        { "Single", "float" },
        { "double", "double" },
        { "Double", "double" },
        { "char", "char" },
        { "Char", "char" },
        { "string", "String" },
        { "String", "String" },
        { "object", "Object" },
        { "Object", "Object" },
        { "void", "void" },

        // Collections
        { "List", "List" },
        { "IList", "List" },
        { "Dictionary", "Map" },
        { "IDictionary", "Map" },
        { "SortedDictionary", "TreeMap" },
        { "HashSet", "Set" },
        { "ISet", "Set" },
        { "Queue", "Queue" },
        { "Stack", "Stack" },
        { "IEnumerable", "Iterable" },
        { "IEnumerator", "Iterator" },
        { "ICollection", "Collection" },

        // Regex
        { "Regex", "Pattern" },
        { "Match", "Matcher" },

        // HTTP
        { "HttpClient", "HttpClient" },
        { "HttpRequestMessage", "HttpRequest" },
        { "HttpResponseMessage", "HttpResponse<String>" },
        { "Uri", "URI" },

        // Exceptions
        { "Exception", "Exception" },
        { "NullReferenceException", "NullPointerException" },
        { "ArgumentException", "IllegalArgumentException" },
        { "ArgumentNullException", "IllegalArgumentException" },
        { "InvalidOperationException", "IllegalStateException" },
        { "ArgumentOutOfRangeException", "IndexOutOfBoundsException" },
        { "IndexOutOfRangeException", "ArrayIndexOutOfBoundsException" },
        { "NotSupportedException", "UnsupportedOperationException" },
        { "IOException", "IOException" }
    };

    public static string MapJavaToCSharp(string javaType)
    {
        if (string.IsNullOrWhiteSpace(javaType)) return javaType;
        javaType = javaType.Trim();

        // Handle array types e.g. String[] -> string[]
        if (javaType.EndsWith("[]"))
        {
            string elementType = javaType[..^2];
            return MapJavaToCSharp(elementType) + "[]";
        }

        // Handle generics e.g. Map<String, Integer> -> Dictionary<string, int>
        int genericIdx = javaType.IndexOf('<');
        if (genericIdx > 0 && javaType.EndsWith('>'))
        {
            string baseType = javaType[..genericIdx];
            string typeArgsStr = javaType[(genericIdx + 1)..^1];
            var typeArgs = SplitGenericArgs(typeArgsStr);
            var mappedArgs = typeArgs.ConvertAll(MapJavaToCSharp);
            string mappedBase = JavaToCSharpTypes.TryGetValue(baseType, out var mapped) ? mapped : baseType;
            return $"{mappedBase}<{string.Join(", ", mappedArgs)}>";
        }

        return JavaToCSharpTypes.TryGetValue(javaType, out var result) ? result : javaType;
    }

    public static string MapCSharpToJava(string csharpType)
    {
        if (string.IsNullOrWhiteSpace(csharpType)) return csharpType;
        csharpType = csharpType.Trim();

        // Handle nullable types like int? -> Integer
        if (csharpType.EndsWith('?'))
        {
            string nonNullable = csharpType[..^1];
            return MapCSharpToJavaBoxed(nonNullable);
        }

        // Handle array types e.g. string[] -> String[]
        if (csharpType.EndsWith("[]"))
        {
            string elementType = csharpType[..^2];
            return MapCSharpToJava(elementType) + "[]";
        }

        // Handle generics e.g. Dictionary<string, int> -> Map<String, Integer>
        int genericIdx = csharpType.IndexOf('<');
        if (genericIdx > 0 && csharpType.EndsWith('>'))
        {
            string baseType = csharpType[..genericIdx];
            string typeArgsStr = csharpType[(genericIdx + 1)..^1];
            var typeArgs = SplitGenericArgs(typeArgsStr);
            var mappedArgs = typeArgs.ConvertAll(MapCSharpToJavaBoxed);
            string mappedBase = CSharpToJavaTypes.TryGetValue(baseType, out var mapped) ? mapped : baseType;
            return $"{mappedBase}<{string.Join(", ", mappedArgs)}>";
        }

        return CSharpToJavaTypes.TryGetValue(csharpType, out var result) ? result : csharpType;
    }

    private static string MapCSharpToJavaBoxed(string csharpType)
    {
        return csharpType switch
        {
            "int" or "Int32" => "Integer",
            "bool" or "Boolean" => "Boolean",
            "long" or "Int64" => "Long",
            "double" or "Double" => "Double",
            "float" or "Single" => "Float",
            "byte" or "Byte" => "Byte",
            "short" or "Int16" => "Short",
            "char" or "Char" => "Character",
            _ => MapCSharpToJava(csharpType)
        };
    }

    public static List<string> SplitGenericArgs(string argsStr)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < argsStr.Length; i++)
        {
            char c = argsStr[i];
            if (c == '<') depth++;
            else if (c == '>') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(argsStr[start..i].Trim());
                start = i + 1;
            }
        }
        if (start < argsStr.Length)
        {
            result.Add(argsStr[start..].Trim());
        }
        return result;
    }
}
