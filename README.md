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
  - `try (...) { ... }` (try-with-resources) $\longleftrightarrow$ `using (...) { ... }`
  - `synchronized (lock) { ... }` $\longleftrightarrow$ `lock (lock) { ... }`
  - `switch (expr) { case ...: default: }`, `break;`, `continue;`
  - Constructor chaining: `super(...)` / `this(...)` inside constructor body $\longleftrightarrow$ `: base(...)` / `: this(...)` on constructor header
  - Auto-properties $\longleftrightarrow$ JavaBean getter and setter methods
  - Naming conventions: `camelCase` $\longleftrightarrow$ `PascalCase` method conversions
- **Standard Library & Idioms**:
  - `System.out.println(...)` $\longleftrightarrow$ `Console.WriteLine(...)`
  - `List<T>`, `ArrayList<T>`, `Map<K, V>`, `HashMap<K, V>`, `HashSet<T>` $\longleftrightarrow$ `List<T>`, `Dictionary<TKey, TValue>`, `HashSet<T>`
  - Indexing: `list.get(i)` / `list.set(i, v)` $\longleftrightarrow$ `list[i]`
  - Map indexing: `map.get(k)` / `map.put(k, v)` $\longleftrightarrow$ `map[k]`
  - Map inspection: `map.keySet()` / `map.values()` $\longleftrightarrow$ `map.Keys` / `map.Values`
  - String: `length()` $\longleftrightarrow$ `Length`, `substring(begin, end)` $\longleftrightarrow$ `Substring(begin, end - begin)`
  - String interpolation: `$"Hello, {name}!"` $\longleftrightarrow$ `String.format(...)` / `"Hello, " + name + "!"`
  - Formatting & Joining: `String.format(...)` $\longleftrightarrow$ `string.Format(...)`, `String.join(...)` $\longleftrightarrow$ `string.Join(...)`
  - I/O & Networking: `BufferedReader`/`FileReader`/`PrintWriter`/`FileWriter` $\longleftrightarrow$ `StreamReader`/`StreamWriter`, `ServerSocket`/`Socket` $\longleftrightarrow$ `TcpListener`/`TcpClient`
  - Min/Max constants: `Integer.MAX_VALUE`, `Long.MAX_VALUE`, `Double.MAX_VALUE` $\longleftrightarrow$ `int.MaxValue`, `long.MaxValue`, `double.MaxValue`
  - Math: `Math.max`, `Math.min`, `Math.abs`, `Math.sqrt`, `Math.ceil`, etc.
- **Regular Expressions**:
  - `Pattern.compile(...)` / `Matcher` / `find()` / `group(...)` $\longleftrightarrow$ `new Regex(...)` / `Match` / `match.Success` / `match.Groups[...]`
  - `Pattern.CASE_INSENSITIVE` $\longleftrightarrow$ `RegexOptions.IgnoreCase`
- **HTTPS & Web Client**:
  - `java.net.http.HttpClient` / `HttpRequest` / `HttpResponse` $\longleftrightarrow$ `System.Net.Http.HttpClient` / `HttpRequestMessage` / `HttpResponseMessage`
- **GUI & Desktop Applications**:
  - Swing/AWT $\longleftrightarrow$ Windows Forms & `System.Drawing`
  - `JFrame`, `JPanel`, `JButton`, `JLabel`, `JTextField`, `JTextArea`, `JCheckBox`, `JComboBox`, `JTable`, `JList` $\longleftrightarrow$ `Form`, `Panel`, `Button`, `Label`, `TextBox`, `CheckBox`, `ComboBox`, `DataGridView`, `ListBox`
  - `JOptionPane.showMessageDialog(...)` $\longleftrightarrow$ `MessageBox.Show(...)`
  - `addActionListener(...)` $\longleftrightarrow$ `.Click += ...`
  - `setText(...)`, `getText()`, `setVisible(...)`, `setEnabled(...)`, `setBounds(...)`, `Controls.Add(...)`
  - `java.awt.Color`, `java.awt.Font` $\longleftrightarrow$ `System.Drawing.Color`, `System.Drawing.Font`
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
│   └── Javacstr.Tests/             # xUnit test suite (30 automated tests)
│       ├── JavaParserTests.cs
│       ├── JavaToCSharpTests.cs
│       ├── CSharpToJavaTests.cs
│       ├── AdvancedLanguageFeatureTests.cs
│       ├── ChatProgramTranslationTests.cs
│       ├── RegexTranslationTests.cs
│       ├── HttpsTranslationTests.cs
│       ├── GuiTranslationTests.cs
│       └── RoundtripTests.cs
└── examples/
    ├── demo/UserAccount.java
    ├── demo/Chat.java
    └── demo/Chat.cs
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
# Display help & version
dotnet run --project src/Javacstr.Cli -- --help
dotnet run --project src/Javacstr.Cli -- --version

# Translate a single Java file to C#
dotnet run --project src/Javacstr.Cli -- input.java -o output.cs

# Translate a single C# file to Java
dotnet run --project src/Javacstr.Cli -- input.cs -o output.java

# View colorized side-by-side diff
dotnet run --project src/Javacstr.Cli -- input.java --diff

# Stream via standard input / Unix pipeline
cat Solution.java | dotnet run --project src/Javacstr.Cli -- - --to csharp

# Batch translate an entire directory (auto-detects Java/C# files)
dotnet run --project src/Javacstr.Cli -- ./src -o ./out

# Interactive REPL mode with multi-snippet support
dotnet run --project src/Javacstr.Cli -- --interactive

# Custom translation flags
dotnet run --project src/Javacstr.Cli -- input.java -o output.cs --no-casing --block-namespaces --no-gui
```

