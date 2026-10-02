using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;

namespace Javacstr.Tests;

public class GuiTranslationTests
{
    private readonly CodeTranslator _translator = new(new TranslationOptions
    {
        MapGui = true,
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true
    });

    [Fact]
    public void TestJavaGuiToCSharp()
    {
        string java = """
        package com.example.gui;

        import javax.swing.JFrame;
        import javax.swing.JPanel;
        import javax.swing.JButton;
        import javax.swing.JTextField;
        import javax.swing.JLabel;
        import javax.swing.JOptionPane;
        import java.awt.BorderLayout;

        public class MyWindow extends JFrame {
            private JPanel panel;
            private JButton button;
            private JTextField textField;
            private JLabel label;

            public void setupUi() {
                panel = new JPanel();
                button = new JButton();
                textField = new JTextField();
                label = new JLabel();

                panel.add(button);
                button.setText("Click Me");
                String input = textField.getText();
                label.setText(input);
                JOptionPane.showMessageDialog(null, "Hello World");
                setVisible(true);
            }
        }
        """;

        var result = _translator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string cs = result.OutputCode;
        Assert.Contains("using System.Windows.Forms;", cs);
        Assert.Contains("class MyWindow : Form", cs);
        Assert.Contains("private Panel panel;", cs);
        Assert.Contains("private Button button;", cs);
        Assert.Contains("private TextBox textField;", cs);
        Assert.Contains("private Label label;", cs);
        Assert.Contains("panel.Controls.Add(button);", cs);
        Assert.Contains("button.Text = \"Click Me\";", cs);
        Assert.Contains("string input = textField.Text;", cs);
        Assert.Contains("label.Text = input;", cs);
        Assert.Contains("MessageBox.Show(\"Hello World\");", cs);
        Assert.Contains("Visible = true;", cs);
    }

    [Fact]
    public void TestDefaultCloseOperationTranslation()
    {
        string java = """
        import javax.swing.JFrame;

        public class MyWindow extends JFrame {
            public MyWindow() {
                setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            }
        }
        """;

        var result = _translator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("FormClosed += (sender, e) => Environment.Exit(0);", result.OutputCode);

        // Roundtrip to Java
        var backResult = _translator.TranslateCSharpToJava(result.OutputCode);
        Assert.True(backResult.Success, string.Join("\n", backResult.Errors));
        Assert.Contains("setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);", backResult.OutputCode);
    }

    [Fact]
    public void TestCSharpGuiToJava()
    {
        string csharp = """
        namespace Com.Example.Gui;

        using System.Windows.Forms;

        public class MyWindow : Form
        {
            private Panel panel;
            private Button button;
            private TextBox textField;
            private Label label;

            public void SetupUi()
            {
                panel = new Panel();
                button = new Button();
                textField = new TextBox();
                label = new Label();

                panel.Controls.Add(button);
                button.Text = "Click Me";
                string input = textField.Text;
                label.Text = input;
                MessageBox.Show("Hello World");
                Visible = true;
            }
        }
        """;

        var result = _translator.TranslateCSharpToJava(csharp);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string java = result.OutputCode;
        Assert.Contains("import javax.swing.*;", java);
        Assert.Contains("class MyWindow extends JFrame", java);
        Assert.Contains("private JPanel panel;", java);
        Assert.Contains("private JButton button;", java);
        Assert.Contains("private JTextField textField;", java);
        Assert.Contains("private JLabel label;", java);
        Assert.Contains("panel.add(button);", java);
        Assert.Contains("button.setText(\"Click Me\");", java);
        Assert.Contains("String input = textField.getText();", java);
        Assert.Contains("label.setText(input);", java);
        Assert.Contains("JOptionPane.showMessageDialog(null, \"Hello World\");", java);
        Assert.Contains("setVisible(true);", java);
    }

    [Fact]
    public void TestGuiDisabledWhenOptionFalse()
    {
        var options = new TranslationOptions
        {
            MapGui = false,
            UseFileScopedNamespaces = true
        };
        var noGuiTranslator = new CodeTranslator(options);

        string java = """
        package com.example.gui;

        import javax.swing.JButton;

        public class MyWindow {
            public void test(JButton btn) {
                btn.setText("Hello");
            }
        }
        """;

        var result = noGuiTranslator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        // Without GUI mapping, JButton remains JButton or non-winform and setText remains SetText / setText
        Assert.DoesNotContain("using System.Windows.Forms;", result.OutputCode);
        Assert.DoesNotContain("btn.Text = ", result.OutputCode);
    }

    [Fact]
    public void TestChatAppTranslation()
    {
        string java = """
        package com.example.chat;

        import javax.swing.JFrame;
        import javax.swing.JPanel;
        import javax.swing.JButton;
        import javax.swing.JTextField;
        import javax.swing.JLabel;
        import java.awt.TextArea;
        import java.awt.FlowLayout;

        public class Chat extends JFrame {
            private JPanel contentPane;
            private TextArea textAreaLog;
            private TextArea textAreaMsg;
            private JButton buttonSend;
            private JTextField textFieldListeningPort;

            public Chat() {
                setTitle("Chat");
                contentPane = new JPanel();
                setContentPane(contentPane);

                textAreaLog = new TextArea();
                textAreaLog.setEditable(false);
                contentPane.add(textAreaLog);

                buttonSend = new JButton("Send");
                contentPane.add(buttonSend);

                setVisible(true);
            }
        }
        """;

        var result = _translator.TranslateJavaToCSharp(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));

        string cs = result.OutputCode;
        Assert.Contains("class Chat : Form", cs);
        Assert.Contains("TextBox textAreaLog;", cs);
        Assert.Contains("Button buttonSend;", cs);
        Assert.Contains("Text = \"Chat\";", cs);
        Assert.Contains("textAreaLog.ReadOnly = true;", cs);
        Assert.Contains("contentPane.Controls.Add(textAreaLog);", cs);
        Assert.Contains("contentPane.Controls.Add(buttonSend);", cs);
        Assert.Contains("Visible = true;", cs);
    }
}
