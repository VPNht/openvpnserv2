using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenVpn
{
    public struct ClientCommand
    {
        public string Name;
        public string Value;
    };

    public enum ClientState
    {
        DISCONNECTED,
        CONNECTING,
        CONNECTED
    };

    public enum OpenVpnState
    {
        DISCONNECTED,
        DISCONNECTING,
        CONNECTING,
        RECONNECTING,
        AUTHENTICATING,
        CONFIGURATING,
        CONNECTED
    };

    public delegate void HandleState(ClientState clientState, OpenVpnState openVpnState);
    public delegate void HandleMessage(string source, string message);
    public delegate void HandleCommand(string command, string message);
    public delegate void HandleMultiLineCommand(string command, string[] messages);

    class ManagementClient
    {
        public event HandleState OnStateChanged;
        public event HandleMessage OnMessageReceived;
        public event HandleCommand OnCommandSucceeded;
        public event HandleCommand OnCommandFailed;
        public event HandleMultiLineCommand OnCommandMessageReceived;

        public static ManagementClient Instance = new ManagementClient();
        
        private TcpClient Client = null;
        private NetworkStream Stream = null;
        private Queue<ClientCommand> Commands = new Queue<ClientCommand>();

        public String OpenVpnPID
        {
            get;
            set;
        }

        public ClientState ClientState
        {
            get;
            private set;
        }

        public OpenVpnState OpenVpnState
        {
            get;
            set;
        }

        public ClientCommand Command
        {
            get { return Commands.First(); }
        }

        public int DownloadedBytes
        {
            get;
            set;
        }

        public int UploadedBytes
        {
            get;
            set;
        }

        public IPAddress LocalIP
        {
            get;
            set;
        }

        public IPAddress RemoteIP
        {
            get;
            set;
        }

        public String Username
        {
            get;
            set;
        }

        public String Password
        {
            get;
            set;
        }

        public DateTime ConnectionStartTime
        {
            get;
            set;
        }

        public ManagementClient()
        {
            this.OpenVpnPID = "";
            this.ClientState = ClientState.DISCONNECTED;
        }

        public bool Connect(int port)
        {
            this.ClientState = ClientState.CONNECTING;

            for (int connectionRetries = 0; connectionRetries < 5 && this.ClientState == ClientState.CONNECTING; connectionRetries++)
            {
                try
                {
                    this.Client = new TcpClient();
                    this.Client.Connect("127.0.0.1", port);
                    this.Stream = Client.GetStream();
                    this.ClientState = ClientState.CONNECTED;
                    this.OpenVpnState = OpenVpnState.CONNECTING;

                    Thread readThread = new Thread(new ThreadStart(ReadData));
                    readThread.Start();

                    return true;
                }
                catch (SocketException e)
                {
                    Thread.Sleep(250);
                }
            }

            this.ClientState = ClientState.DISCONNECTED;
            this.OpenVpnState = OpenVpnState.DISCONNECTED;
            return false;
        }

        public void Disconnect()
        {
            this.Stream.Close();
            this.Client.Close();

            this.ClientState = ClientState.DISCONNECTED;
            this.OpenVpnState = OpenVpnState.DISCONNECTED;
        }

        public void SendCommand( string name, string value = "")
        {
            ClientCommand command = new ClientCommand();
            command.Name = name;
            command.Value = value;
            this.Commands.Enqueue(command);

            if (this.Commands.Count == 1)
            {
                SendNextCommand();
            }
        }

        private void SendNextCommand()
        {
            if (this.Commands.Count != 0)
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(Command.Name + " " + Command.Value + "\r\n");
                Stream.Write(data, 0, data.Length);
            }
        }

        private void ReadData()
        {
            try
            {
                byte[] buffer = new byte[10240];

                while (ClientState == ClientState.CONNECTED)
                {
                    int bytesRead = Stream.Read(buffer, 0, 10240);
                    if (bytesRead == 0) continue;

                    string response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Real-time messages:
                    // >[SOURCE]:[MESSAGE]
                    if (response.StartsWith(">") && OnMessageReceived != null)
                    {
                        string[] messages = response.Split(new char[] { '>' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string message in messages)
                        {
                            string source = message.Substring(0, message.IndexOf(':', 1));
                            string text = message.Substring(source.Length + 1);
                            OnMessageReceived(source, text);
                        }
                    }

                    // Command success
                    // SUCCESS: [text]
                    else if (response.StartsWith("SUCCESS: ") && OnCommandSucceeded != null)
                    {
                        string text = response.Substring(9);
                        OnCommandSucceeded(Command.Name, text);

                        Commands.Dequeue();
                        SendNextCommand();
                    }

                    // Command failure
                    // ERROR: [text]
                    else if (response.StartsWith("ERROR: ") && OnCommandFailed != null)
                    {
                        string text = response.Substring(7);
                        OnCommandFailed(Command.Name, text);

                        Commands.Dequeue();
                        SendNextCommand();
                    }

                    // Multi-line command response
                    // .
                    // .
                    // [END]
                    else
                    {
                        string[] messages = response.Split(new string[] { "\r\n", "END" }, StringSplitOptions.RemoveEmptyEntries);
                        OnCommandMessageReceived(Command.Name, messages);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
