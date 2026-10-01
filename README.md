# javacstr - Bidirectional Java <-> C# Translator

A high-fidelity, bidirectional source code translator between **Java** and **C#** written in **.NET 10 (C#)**. It leverages **Microsoft Roslyn (`Microsoft.CodeAnalysis.CSharp`)** for C# syntax parsing, AST analysis, and formatting, paired with a specialized Java AST lexer, parser, and code generator.

---

## Features

- **Bidirectional Translation**:
  - Java $\rightarrow$ C#
  - C# $\rightarrow$ Java
- **Language Syntax & Keyword Mapping**:
  - `package` $\longleftrightarrow$ `namespace` (supports file-scoped and block namespaces)
  - `import` $\longleftrightarrow$ `using`
  - `class A extends B implements C, D` $\longleftrightarrow$ `class A : B, C, D`
  - `@Override` $\longleftrightarrow$ `override`
  - `final` $\longleftrightarrow$ `readonly` / `const` / `sealed`
  - `instanceof` $\longleftrightarrow$ `is`
  - Constructor chaining: `super(...)` / `this(...)` inside constructor body $\longleftrightarrow$ `: base(...)` / `: this(...)` on constructor header
  - Auto-properties $\longleftrightarrow$ JavaBean getter and setter methods
  - Naming conventions: `camelCase` $\longleftrightarrow$ `PascalCase` method conversions
- **Standard Library & Idioms**:
  - `System.out.println(...)` $\longleftrightarrow$ `Console.WriteLine(...)`
  - `List<T>`, `ArrayList<T>`, `Map<K, V>`, `HashMap<K, V>`, `HashSet<T>` $\longleftrightarrow$ `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`
  - Indexing: `list.get(i)` / `list.set(i, v)` $\longleftrightarrow$ `list[i]`
  - Map indexing: `map.get(k)` / `map.put(k, v)` $\longleftrightarrow$ `map[k]`
  - String: `length()` $\longleftrightarrow$ `Length`, `substring(begin, end)` $\longleftrightarrow$ `Substring(begin, end - begin)`
  - Math: `Math.max`, `Math.min`, `Math.abs`, `Math.sqrt`, `Math.ceil`, etc.
- **Regular Expressions**:
  - `Pattern.compile(...)` / `Matcher` / `find()` / `group(...)` $\longleftrightarrow$ `new Regex(...)` / `Match` / `match.Success` / `match.Groups[...]`
  - `Pattern.CASE_INSENSITIVE` $\longleftrightarrow$ `RegexOptions.IgnoreCase`
- **HTTPS & Web Client**:
  - `java.net.http.HttpClient` / `HttpRequest` / `HttpResponse` $\longleftrightarrow$ `System.Net.Http.HttpClient` / `HttpRequestMessage` / `HttpResponseMessage`
- **CLI & User Interface**:
  - Single file translation with auto-detection or explicit `--to`
  - Batch directory translation
  - Interactive REPL mode (`--interactive`)
  - Colorized side-by-side diff preview (`--diff`) powered by `Spectre.Console`

---

## Solution Structure

```
javacstr/
├── src/
│   ├── Javacstr.Core/              # Core translation engine
│   │   ├── Java/                   # Java Lexer, AST nodes, Parser, and Emitter
│   │   ├── CSharp/                 # Roslyn integration and C# to Java AST walker
│   │   ├── Mapping/                # TypeMapper, ApiMapper, CasingHelper
│   │   ├── Models/                 # TranslationOptions, TranslationResult
│   │   └── Translation/            # JavaToCSharpTranslator, CSharpToJavaTranslator, CodeTranslator
│   └── Javacstr.Cli/               # Command-line application (Spectre.Console)
│       └── Program.cs
├── tests/
│   └── Javacstr.Tests/             # xUnit test suite (14 automated tests)
│       ├── JavaParserTests.cs
│       ├── JavaToCSharpTests.cs
│       ├── CSharpToJavaTests.cs
│       ├── RegexTranslationTests.cs
│       ├── HttpsTranslationTests.cs
│       └── RoundtripTests.cs
└── examples/
    └── demo/UserAccount.java
```

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Java JDK (e.g. OpenJDK 25)](https://adoptium.net/) (optional, for compiling generated Java code)

### Running Automated Tests
```powershell
dotnet test
```

### CLI Usage

```powershell
# Display help
dotnet run --project src/Javacstr.Cli -- --help

# Translate a single Java file to C#
dotnet run --project src/Javacstr.Cli -- input.java -o output.cs

# Translate a single C# file to Java
dotnet run --project src/Javacstr.Cli -- input.cs -o output.java

# View colorized side-by-side diff
dotnet run --project src/Javacstr.Cli -- input.java --diff

# Batch translate an entire directory
dotnet run --project src/Javacstr.Cli -- ./src -o ./out --to csharp

# Interactive REPL mode
dotnet run --project src/Javacstr.Cli -- --interactive
```
