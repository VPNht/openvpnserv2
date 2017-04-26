using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenVpn
{
    public struct ManagementClientCommand
    {
        public string Name;
        public string Value;
    };

    public enum ManagementClientState
    {
        DISCONNECTED = 0,
        CONNECTING,
        AUTHENTICATING,
        RECONNECTING,
        CONNECTED
    };

    public delegate void HandleState(ManagementClientState state);
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

        private static int WRITE_BUFFER_SIZE = 10240;
        private static int READ_BUFFER_SIZE = 10240;

        private TcpClient _client = null;
        private NetworkStream _stream = null;
        private byte[] _readBuffer = new byte[READ_BUFFER_SIZE];
        private byte[] _writeBuffer = new byte[WRITE_BUFFER_SIZE];
        private ManagementClientState _state = ManagementClientState.DISCONNECTED;
        private Queue<ManagementClientCommand> _commands = new Queue<ManagementClientCommand>();

        public ManagementClient()
        {
        }

        public ManagementClientState State
        {
            get { return _state; }
        }

        public ManagementClientCommand Command
        {
            get { return _commands.First(); }
        }

        public bool Connect(int port)
        {
            try
            {
                if ( _client == null || !_client.Connected )
                {
                    _client = new TcpClient();
                    _client.Connect("127.0.0.1", port);
                    _stream = _client.GetStream();
                    _state = ManagementClientState.CONNECTED;

                    Thread readThread = new Thread(new ThreadStart(ReadData));
                    readThread.Start();

                    OnStateChanged?.Invoke(ManagementClientState.CONNECTING);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            if (_client.Connected)
            {
                _client.Close();
            }

            _state = ManagementClientState.DISCONNECTED;

            OnStateChanged?.Invoke(ManagementClientState.DISCONNECTED);
        }

        public void SendCommand( string name, string value)
        {
            ManagementClientCommand command = new ManagementClientCommand();
            command.Name = name;
            command.Value = value;
            _commands.Enqueue(command);

            if (_commands.Count == 1)
            {
                SendNextCommand();
            }
        }

        private void SendNextCommand()
        {
            if (_commands.Count != 0)
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(Command.Name + " " + Command.Value + "\r\n");
                _stream.Write(data, 0, data.Length);
            }
        }

        private void ReadData()
        {
            try
            {
                int bytesRead = 0;

                while (State == ManagementClientState.CONNECTED)
                {
                    bytesRead = _stream.Read(_readBuffer, 0, READ_BUFFER_SIZE);

                    if (bytesRead == 0) continue;

                    string response = System.Text.Encoding.UTF8.GetString(_readBuffer, 0, bytesRead);

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

                        _commands.Dequeue();
                        SendNextCommand();
                    }

                    // Command failure
                    // ERROR: [text]
                    else if (response.StartsWith("ERROR: ") && OnCommandFailed != null)
                    {
                        string text = response.Substring(7);
                        OnCommandFailed(Command.Name, text);

                        _commands.Dequeue();
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
