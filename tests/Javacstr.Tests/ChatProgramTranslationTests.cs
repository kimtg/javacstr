using Xunit;
using Javacstr.Core.Models;
using Javacstr.Core.Translation;
using Javacstr.Core.Java;

namespace Javacstr.Tests;

public class ChatProgramTranslationTests
{
    private readonly JavaToCSharpTranslator _translator = new(new TranslationOptions
    {
        UseFileScopedNamespaces = true,
        ConvertMethodCasing = true,
        MapGui = true
    });

    [Fact]
    public void TestCharArithmeticTranslation()
    {
        string java = """
        public class ChatProtocol {
            public static char[] encodeString(String input, int key) {
                char[] chars = input.toCharArray();
                for (int i = 0; i < chars.length; i++) {
                    chars[i] += key;
                }
                return chars;
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("chars[i] = (char)(chars[i] + key);", result.OutputCode);
    }

    [Fact]
    public void TestChatGuiLayoutTranslation()
    {
        string java = """
        import javax.swing.*;
        import java.awt.*;
        import javax.swing.border.EmptyBorder;

        public class Chat extends JFrame {
            private JPanel contentPane;
            private TextArea textAreaLog;
            private TextArea textAreaMsg;
            private JButton buttonSend;
            private JPanel panel_1;
            private JPanel panel_2;
            private JPanel panel;
            private JTextField textFieldListeningPort;

            public Chat() {
                contentPane = new JPanel();
                contentPane.setBorder(new EmptyBorder(5, 5, 5, 5));
                setContentPane(contentPane);
                contentPane.setLayout(new BorderLayout(0, 0));

                textAreaLog = new TextArea();
                contentPane.add(textAreaLog, BorderLayout.CENTER);

                panel_1 = new JPanel();
                contentPane.add(panel_1, BorderLayout.SOUTH);
                panel_1.setLayout(new BorderLayout(0, 0));

                textAreaMsg = new TextArea();
                panel_1.add(textAreaMsg);

                buttonSend = new JButton("Send");
                panel_1.add(buttonSend, BorderLayout.EAST);

                panel_2 = new JPanel();
                panel_1.add(panel_2, BorderLayout.NORTH);
                panel_2.setLayout(new FlowLayout(FlowLayout.CENTER, 5, 5));

                textFieldListeningPort = new JTextField();
                panel_2.add(textFieldListeningPort);
                textFieldListeningPort.setColumns(4);

                panel = new JPanel();
                panel_1.add(panel, BorderLayout.SOUTH);
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        string csharp = result.OutputCode;
        Assert.Contains("contentPane.Dock = DockStyle.Fill;", csharp);
        Assert.Contains("contentPane.Padding = new Padding(5, 5, 5, 5);", csharp);
        Assert.Contains("panel_2 = new FlowLayoutPanel();", csharp);
        Assert.Contains("panel = new FlowLayoutPanel();", csharp);
        Assert.Contains("panel_1.Height = 150;", csharp);
        Assert.Contains("panel_2.Height = 35;", csharp);
        Assert.Contains("panel.Height = 35;", csharp);
        Assert.Contains("textAreaLog.SendToBack();", csharp);
        Assert.Contains("textAreaMsg.SendToBack();", csharp);
        Assert.Contains("textFieldListeningPort.Width = Math.Max(50, 40);", csharp);
    }

    [Fact]
    public void TestChatDefaultCloseOperationTranslation()
    {
        string java = """
        import javax.swing.JFrame;

        public class Chat extends JFrame {
            public Chat() {
                setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains("FormClosed += (sender, e) => Environment.Exit(0);", result.OutputCode);
    }

    [Fact]
    public void TestWinFormsTextNewlineTranslation()
    {
        string java = """
        import java.awt.TextArea;

        public class Chat {
            private TextArea textAreaLog;

            public void log() {
                textAreaLog.append("Welcome to Chat\n");
                textAreaLog.append("IP: " + myAddr + "\n");
                textAreaLog.setText("Line1\nLine2");
            }
        }
        """;

        var result = _translator.Translate(java);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Contains(@"textAreaLog.AppendText(""Welcome to Chat\r\n"");", result.OutputCode);
        Assert.Contains(@"textAreaLog.AppendText(""IP: "" + myAddr + ""\r\n"");", result.OutputCode);
        Assert.Contains(@"textAreaLog.Text = ""Line1\r\nLine2"";", result.OutputCode);

        // Roundtrip to Java
        var csToJava = new CSharpToJavaTranslator(new TranslationOptions { MapGui = true });
        var back = csToJava.Translate(result.OutputCode);
        Assert.True(back.Success, string.Join("\n", back.Errors));
        Assert.Contains(@"textAreaLog.append(""Welcome to Chat\n"");", back.OutputCode);
        Assert.Contains(@"textAreaLog.append(""IP: "" + myAddr + ""\n"");", back.OutputCode);
        Assert.Contains(@"textAreaLog.setText(""Line1\nLine2"");", back.OutputCode);
    }
}
