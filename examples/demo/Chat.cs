using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;

public class Chat : Form
{
    private Panel contentPane;
    private static TextBox textAreaLog;
    private TextBox textAreaMsg;
    private Button buttonSend;
    private static Thread serverThread = null;
    private static Chat thisFrame;
    private Panel panel;
    private Label lblDestination;
    private Label lblPort;
    private TextBox textFieldDestPort;
    private Panel panel_1;
    private TextBox textFieldIPAddr;
    private Panel panel_2;
    private Label lblListeningPort;
    private Label lblNickname;
    private TextBox textFieldListeningPort;
    private TextBox textFieldNickname;
    private Button btnChangePort;
    [STAThread]
    public static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = false;
        Application.Run(new Chat());
    }

    private static void ShowMyAddr()
    {
        string myAddr = null;
        try
        {
            bool isLoopBack = true;
            IEnumerator<NetworkInterface> en = NetworkInterface.GetAllNetworkInterfaces().Cast<NetworkInterface>().GetEnumerator();
            while (en.MoveNext())
            {
                NetworkInterface ni = en.Current;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IEnumerator<IPAddress> inetAddresses = ni.GetIPProperties().UnicastAddresses.Select(u => u.Address).GetEnumerator();
                while (inetAddresses.MoveNext())
                {
                    IPAddress ia = inetAddresses.Current;
                    if (ia.ToString() != null && ia.ToString().IndexOf(".") != -1)
                    {
                        myAddr = ia.ToString();
                        isLoopBack = false;
                        break;
                    }
                }

                if (!isLoopBack)
                {
                    break;
                }
            }
        }
        catch (SocketException e1)
        {
            Console.Error.WriteLine(e1);
        }

        textAreaLog.AppendText("My IP address: " + myAddr + "\r\n");
    }

    public Chat()
    {
        try
        {
            Application.EnableVisualStyles();
        }
        catch (TypeLoadException e2)
        {
            Console.Error.WriteLine(e2);
        }
        catch (MemberAccessException e2)
        {
            Console.Error.WriteLine(e2);
        }
        catch (UnauthorizedAccessException e2)
        {
            Console.Error.WriteLine(e2);
        }
        catch (NotSupportedException e2)
        {
            Console.Error.WriteLine(e2);
        }

        thisFrame = this;
        Text = "Chat";
        FormClosed += (sender, e) => Environment.Exit(0);
        SetBounds(100, 100, 470, 536);
        contentPane = new Panel();
        contentPane.Padding = new Padding(5, 5, 5, 5);
        contentPane.Dock = DockStyle.Fill;
        Controls.Add(contentPane);
        // layout;
        textAreaLog = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both
        };
        textAreaLog.ReadOnly = true;
        textAreaLog.Dock = DockStyle.Fill;
        contentPane.Controls.Add(textAreaLog);
        textAreaLog.SendToBack();
        panel_1 = new Panel();
        panel_1.Dock = DockStyle.Bottom;
        panel_1.Height = 150;
        contentPane.Controls.Add(panel_1);
        // layout;
        textAreaMsg = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both
        };
        textAreaMsg.Dock = DockStyle.Fill;
        panel_1.Controls.Add(textAreaMsg);
        textAreaMsg.SendToBack();
        buttonSend = new Button
        {
            Text = "Send",
            AutoSize = true
        };
        buttonSend.Dock = DockStyle.Right;
        panel_1.Controls.Add(buttonSend);
        panel_2 = new FlowLayoutPanel();
        panel_2.Dock = DockStyle.Top;
        panel_2.Height = 35;
        panel_1.Controls.Add(panel_2);
        // layout;
        lblListeningPort = new Label
        {
            Text = "Listening Port:",
            AutoSize = true
        };
        panel_2.Controls.Add(lblListeningPort);
        textFieldListeningPort = new TextBox();
        textFieldListeningPort.Text = "3333";
        panel_2.Controls.Add(textFieldListeningPort);
        textFieldListeningPort.Width = Math.Max(50, 40);
        btnChangePort = new Button
        {
            Text = "Change Port",
            AutoSize = true
        };
        btnChangePort.Click += (sender, arg0) =>
        {
            ChangeListeningPort();
        };
        panel_2.Controls.Add(btnChangePort);
        lblNickname = new Label
        {
            Text = "Nickname:",
            AutoSize = true
        };
        panel_2.Controls.Add(lblNickname);
        textFieldNickname = new TextBox();
        try
        {
            textFieldNickname.Text = Dns.GetHostName();
        }
        catch (SocketException e2)
        {
            Console.Error.WriteLine(e2);
        }

        panel_2.Controls.Add(textFieldNickname);
        textFieldNickname.Width = Math.Max(50, 80);
        panel = new FlowLayoutPanel();
        panel.Dock = DockStyle.Bottom;
        panel.Height = 35;
        panel_1.Controls.Add(panel);
        lblDestination = new Label
        {
            Text = "Destination:",
            AutoSize = true
        };
        panel.Controls.Add(lblDestination);
        textFieldIPAddr = new TextBox();
        textFieldIPAddr.Text = "127.0.0.1";
        panel.Controls.Add(textFieldIPAddr);
        textFieldIPAddr.Width = Math.Max(50, 100);
        lblPort = new Label
        {
            Text = "Port:",
            AutoSize = true
        };
        panel.Controls.Add(lblPort);
        textFieldDestPort = new TextBox();
        textFieldDestPort.Text = "3333";
        panel.Controls.Add(textFieldDestPort);
        textFieldDestPort.Width = Math.Max(50, 40);
        // pack;
        buttonSend.Click += (sender, e) =>
        {
            TcpClient echoSocket = null;
            StreamWriter @out = null;
            String dest = textFieldIPAddr.Text;
            try
            {
                echoSocket = new TcpClient(dest, int.Parse(textFieldDestPort.Text));
                @out = new StreamWriter(echoSocket.GetStream())
                {
                    AutoFlush = true
                };
            }
            catch (SocketException ex)
            {
                textAreaLog.AppendText(dest + ": Don't know about host.\r\n");
            }
            catch (IOException ex)
            {
                textAreaLog.AppendText(dest + ": Couldn't get I/O for " + "the connection.\r\n");
            }

            int key = (int)(Random.Shared.NextDouble() * 65535 + 1);
            @out.WriteLine(key);
            String message = textFieldNickname.Text + ": " + textAreaMsg.Text;
            char[] output = ChatProtocol.EncodeString(message, key);
            @out.WriteLine(output.Length);
            for (int i = 0; i < output.Length; i++)
            {
                @out.WriteLine((int)output[i]);
            }

            textAreaLog.AppendText("                    " + dest + "<- " + message + "\r\n");
            textAreaMsg.Text = "";
            @out.Close();
            try
            {
                echoSocket.Close();
            }
            catch (IOException e1)
            {
                Console.Error.WriteLine(e1);
            }
        };
        textAreaLog.AppendText("Welcome to Chat\r\n");
        ShowMyAddr();
        textAreaMsg.Focus();
        ChangeListeningPort();
    }

    private void ChangeListeningPort()
    {
        if (serverThread != null)
        {
            serverThread.Interrupt();
        }

        int newPort = int.Parse(textFieldListeningPort.Text);
        serverThread = new Thread(new Server(newPort).Run)
        {
            IsBackground = true
        };
        serverThread.Start();
    }

    public class Server
    {
        int serverPort;
        public Server(int serverPort)
        {
            this.serverPort = serverPort;
        }

        public void Run()
        {
            TcpListener serverSocket = null;
            try
            {
                serverSocket = new TcpListener(IPAddress.Any, serverPort);
                serverSocket.Start();
                textAreaLog.AppendText("Listening on port " + serverPort + ".\r\n");
            }
            catch (IOException e)
            {
                textAreaLog.AppendText("Could not listen on port " + serverPort + ".\r\n" + e.ToString());
                Console.Error.WriteLine(e);
                return;
            }

            TcpClient clientSocket = null;
            while (!false)
            {
                try
                {
                    clientSocket = serverSocket.AcceptTcpClient();
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine("Accept failed.");
                    Environment.Exit(1);
                }

                StreamWriter @out;
                StreamReader @in;
                try
                {
                    @out = new StreamWriter(clientSocket.GetStream())
                    {
                        AutoFlush = true
                    };
                    @in = new StreamReader(clientSocket.GetStream());
                    int key;
                    key = int.Parse(@in.ReadLine());
                    int length = int.Parse(@in.ReadLine());
                    char[] buffer = new char[length];
                    for (int i = 0; i < length; i++)
                    {
                        buffer[i] = (char)int.Parse(@in.ReadLine());
                    }

                    string clientAddr = ((IPEndPoint)clientSocket.Client.RemoteEndPoint).Address.ToString();
                    textAreaLog.AppendText(clientAddr + "-> " + new string (ChatProtocol.DecodeString(buffer, key)) + "\r\n");
                    if (!thisFrame.Focused)
                    {
                        try
                        {
                            const int sleepTime = 50;
                            for (int i = 0; i < 2; i++)
                            {
                                thisFrame.Visible = false;
                                Thread.Sleep(sleepTime);
                                thisFrame.Visible = true;
                                Thread.Sleep(sleepTime);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e);
                        }
                    }

                    thisFrame.BringToFront();
                    @out.Close();
                    @in.Close();
                    clientSocket.Close();
                }
                catch (IOException e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            try
            {
                serverSocket.Stop();
            }
            catch (IOException e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }

    class ChatProtocol
    {
        public static char[] EncodeString(string input, int key)
        {
            Console.WriteLine("encode");
            Console.WriteLine(input);
            Console.WriteLine("key: " + key);
            char[] chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                Console.Write(chars[i] + " ");
                Console.Write((int)chars[i] + " ");
                chars[i] = (char)(chars[i] + key);
                Console.Write((int)chars[i] + ",");
            }

            Console.WriteLine();
            return chars;
        }

        public static char[] DecodeString(char[] input, int key)
        {
            Console.WriteLine("decode");
            Console.WriteLine("key: " + key);
            char[] chars = new char[input.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = input[i];
                Console.Write((int)chars[i] + " ");
                chars[i] = (char)(chars[i] - key);
                Console.Write((int)chars[i] + ",");
            }

            Console.WriteLine();
            return chars;
        }
    }
}