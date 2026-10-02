import java.awt.BorderLayout;
import java.awt.EventQueue;

import javax.swing.JFrame;
import javax.swing.JPanel;
import javax.swing.border.EmptyBorder;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.SocketException;
import java.net.UnknownHostException;
import java.awt.event.ActionListener;
import java.awt.event.ActionEvent;
import java.util.*;
import javax.swing.JLabel;
import javax.swing.JTextField;
import javax.swing.UIManager;
import javax.swing.UnsupportedLookAndFeelException;

import java.awt.FlowLayout;
import java.awt.TextArea;

import javax.swing.JButton;

@SuppressWarnings("serial")
public class Chat extends JFrame {

	private JPanel contentPane;
	private static TextArea textAreaLog;
	private TextArea textAreaMsg;
	private JButton buttonSend;

	private static Thread serverThread = null;
	private static Chat thisFrame;
	private JPanel panel;
	private JLabel lblDestination;
	private JLabel lblPort;
	private JTextField textFieldDestPort;
	private JPanel panel_1;
	private JTextField textFieldIPAddr;
	private JPanel panel_2;
	private JLabel lblListeningPort;
	private JLabel lblNickname;
	private JTextField textFieldListeningPort;
	private JTextField textFieldNickname;
	private JButton btnChangePort;

	/**
	 * Launch the application.
	 */
	public static void main(String[] args) {
		EventQueue.invokeLater(new Runnable() {
			public void run() {
				try {
					Chat frame = new Chat();
					frame.setVisible(true);
				} catch (Exception e) {
					e.printStackTrace();
				}
			}
		});
	}

	private static void showMyAddr() {
		String myAddr = null;

		try {
			boolean isLoopBack = true;
			Enumeration<NetworkInterface> en = NetworkInterface.getNetworkInterfaces();
			while (en.hasMoreElements()) {
				NetworkInterface ni = en.nextElement();
				if (ni.isLoopback())
					continue;

				Enumeration<InetAddress> inetAddresses = ni.getInetAddresses();
				while (inetAddresses.hasMoreElements()) {
					InetAddress ia = inetAddresses.nextElement();
					if (ia.getHostAddress() != null && ia.getHostAddress().indexOf(".") != -1) {
						myAddr = ia.getHostAddress();
						isLoopBack = false;
						break;
					}
				}
				if (!isLoopBack)
					break;
			}
		} catch (SocketException e1) {
			e1.printStackTrace();
		}
		textAreaLog.append("My IP address: " + myAddr + "\n");
	}

	/**
	 * Create the frame.
	 */
	public Chat() {
		try {
			UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
		} catch (ClassNotFoundException e2) {
			// TODO Auto-generated catch block
			e2.printStackTrace();
		} catch (InstantiationException e2) {
			// TODO Auto-generated catch block
			e2.printStackTrace();
		} catch (IllegalAccessException e2) {
			// TODO Auto-generated catch block
			e2.printStackTrace();
		} catch (UnsupportedLookAndFeelException e2) {
			// TODO Auto-generated catch block
			e2.printStackTrace();
		}
		thisFrame = this;
		setTitle("Chat");
		setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
		setBounds(100, 100, 470, 536);
		contentPane = new JPanel();
		contentPane.setBorder(new EmptyBorder(5, 5, 5, 5));
		setContentPane(contentPane);
		contentPane.setLayout(new BorderLayout(0, 0));

		textAreaLog = new TextArea();
		textAreaLog.setEditable(false);

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

		lblListeningPort = new JLabel("Listening Port:");
		panel_2.add(lblListeningPort);

		textFieldListeningPort = new JTextField();
		textFieldListeningPort.setText("3333");
		panel_2.add(textFieldListeningPort);
		textFieldListeningPort.setColumns(4);
		
		btnChangePort = new JButton("Change Port");
		btnChangePort.addActionListener(new ActionListener() {
			public void actionPerformed(ActionEvent arg0) {
				changeListeningPort();
			}
		});
		panel_2.add(btnChangePort);
		lblNickname = new JLabel("Nickname:");
		panel_2.add(lblNickname);
		textFieldNickname = new JTextField();
		try {
			textFieldNickname.setText(InetAddress.getLocalHost().getHostName());
		} catch (UnknownHostException e2) {
			// TODO Auto-generated catch block
			e2.printStackTrace();
		}
		panel_2.add(textFieldNickname);
		textFieldNickname.setColumns(8);

		panel = new JPanel();
		panel_1.add(panel, BorderLayout.SOUTH);

		lblDestination = new JLabel("Destination:");
		panel.add(lblDestination);

		textFieldIPAddr = new JTextField();
		textFieldIPAddr.setText("127.0.0.1");
		panel.add(textFieldIPAddr);
		textFieldIPAddr.setColumns(10);

		lblPort = new JLabel("Port:");
		panel.add(lblPort);

		textFieldDestPort = new JTextField();
		textFieldDestPort.setText("3333");
		panel.add(textFieldDestPort);
		textFieldDestPort.setColumns(4);
		this.pack();

		buttonSend.addActionListener(new ActionListener() {
			public void actionPerformed(ActionEvent e) {
				Socket echoSocket = null;
				PrintWriter out = null;
				String dest = textFieldIPAddr.getText();

				try {
					echoSocket = new Socket(dest, Integer.parseInt(textFieldDestPort.getText()));
					out = new PrintWriter(echoSocket.getOutputStream(), true);
				} catch (UnknownHostException ex) {
					textAreaLog.append(dest + ": Don't know about host.\n");
				} catch (IOException ex) {
					textAreaLog.append(dest + ": Couldn't get I/O for " + "the connection.\n");
				}

				int key = (int) (Math.random() * 65535 + 1); // random key
				out.println(key);

				String message = textFieldNickname.getText() + ": " + textAreaMsg.getText();
				char[] output = ChatProtocol.encodeString(message, key);
				out.println(output.length);
				// out.print(output);
				for (int i = 0; i < output.length; i++) {
					out.println((int) output[i]);
				}
				textAreaLog.append("                    " + dest + "<- " + message + "\n");
				textAreaMsg.setText("");
				// System.out.println(key);

				out.close();
				try {
					echoSocket.close();
				} catch (IOException e1) {
					// TODO Auto-generated catch block
					e1.printStackTrace();
				}
			}
		});
		textAreaLog.append("Welcome to Chat\n");
		showMyAddr();
		textAreaMsg.requestFocusInWindow();
		changeListeningPort();
	}

	private void changeListeningPort() {
		if (serverThread != null) {
			serverThread.interrupt();
		}
		int newPort = Integer.parseInt(textFieldListeningPort.getText());
		serverThread = new Thread(new Server(newPort));
		serverThread.start();
	}

	public static class Server implements Runnable {
		int serverPort;
		
		public Server(int serverPort) {
			this.serverPort = serverPort;
		}
		
		@Override
		public void run() {
			ServerSocket serverSocket = null;					

			try {
				serverSocket = new ServerSocket(serverPort);
				textAreaLog.append("Listening on port " + serverPort + ".\n");
			} catch (IOException e) {
				textAreaLog.append("Could not listen on port " + serverPort + ".\n" + e.toString());
				e.printStackTrace();
				return;
			}

			Socket clientSocket = null;
			while (!Thread.currentThread().isInterrupted()) {
				try {
					clientSocket = serverSocket.accept();
				} catch (IOException e) {
					System.err.println("Accept failed.");
					System.exit(1);
				}

				PrintWriter out;
				BufferedReader in;
				try {
					out = new PrintWriter(clientSocket.getOutputStream(), true);
					in = new BufferedReader(new InputStreamReader(clientSocket.getInputStream()));

					int key;

					key = Integer.parseInt(in.readLine());
					int length = Integer.parseInt(in.readLine());

					char[] buffer = new char[length];
					// in.read(buffer, 0, length);
					for (int i = 0; i < length; i++) {
						buffer[i] = (char) Integer.parseInt(in.readLine());
					}
					String clientAddr = clientSocket.getInetAddress().getHostAddress();
					textAreaLog.append(clientAddr + "-> " + new String(ChatProtocol.decodeString(buffer, key)) + "\n");

					// flash the window
					if (!thisFrame.isFocused()) {
						try {
							final int sleepTime = 50;
							for (int i = 0; i < 2; i++) {
								thisFrame.setVisible(false);
								Thread.sleep(sleepTime);
								thisFrame.setVisible(true);
								Thread.sleep(sleepTime);
							}
						} catch (Exception e) {
							e.printStackTrace();
						}
					}
					thisFrame.toFront();

					out.close();
					in.close();
					clientSocket.close();
				} catch (IOException e) {
					// TODO Auto-generated catch block
					e.printStackTrace();
				}
			}
			try {
				serverSocket.close();
			} catch (IOException e) {
				// TODO Auto-generated catch block
				e.printStackTrace();
			}
		}
	}

	static class ChatProtocol {
		public static char[] encodeString(String input, int key) {
			System.out.println("encode");
			System.out.println(input);
			System.out.println("key: " + key);
			char[] chars = input.toCharArray();

			for (int i = 0; i < chars.length; i++) {
				System.out.print(chars[i] + " ");
				System.out.print((int) chars[i] + " ");
				chars[i] += key;
				System.out.print((int) chars[i] + ",");
			}
			System.out.println();
			return chars;
		}

		public static char[] decodeString(char[] input, int key) {
			System.out.println("decode");
			System.out.println("key: " + key);
			char[] chars = new char[input.length];
			for (int i = 0; i < chars.length; i++) {
				chars[i] = input[i];
				System.out.print((int) chars[i] + " ");
				chars[i] -= key;
				System.out.print((int) chars[i] + ",");
			}
			System.out.println();
			return chars;
		}

	}
}
