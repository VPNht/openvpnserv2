﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenVpn
{
    public delegate void HandleConnection();
    public delegate void HandleDisconnection();
    public delegate void HandleMessage(string source, string message);
    public delegate void HandleCommand(string command, string message);
    public delegate void HandleMultiLineCommand(string command, string[] messages);

    class ManagementClient
    {
        public event HandleConnection OnConnected;
        public event HandleDisconnection OnDisconnected;
        public event HandleMessage OnMessageReceived;
        public event HandleCommand OnCommandSucceeded;
        public event HandleCommand OnCommandFailed;
        public event HandleMultiLineCommand OnCommandMessageReceived;

        private static int WRITE_BUFFER_SIZE = 10240;
        private static int READ_BUFFER_SIZE = 10240;

        private TcpClient _client = null;
        private NetworkStream _stream = null;
        private byte[] _readBuffer = new byte[READ_BUFFER_SIZE];
        private byte[] _writeBuffer = new byte[WRITE_BUFFER_SIZE];
        private bool _isConnected = false;
        private string _lastSentCommand = "";

        public ManagementClient()
        {
        }

        public bool IsConnected()
        {
            return _isConnected;
        }

        public bool Connect(int port)
        {
            try
            {
                _client = new TcpClient();
                _client.Connect("127.0.0.1", port);
                _stream = _client.GetStream();
                _isConnected = true;

                OnConnected?.Invoke();

                Thread t = new Thread(new ThreadStart(ListenForData));
                t.Start();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            OnDisconnected?.Invoke();
        }

        public void SendCommand(string command)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
            _stream.Write(data, 0, data.Length);
            _lastSentCommand = command;
        }

        private void ListenForData()
        {
            try
            {
                int bytesRead = 0;

                while (_isConnected)
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
                        OnCommandSucceeded(_lastSentCommand, text);
                    }

                    // Command failure
                    // ERROR: [text]
                    else if (response.StartsWith("ERROR: ") && OnCommandFailed != null)
                    {
                        string text = response.Substring(7);
                        OnCommandFailed(_lastSentCommand, text);
                    }

                    // Multi-line command response
                    // .
                    // .
                    // [END]
                    else
                    {
                        string[] messages = response.Split(new string[] { "\r\n", "END" }, StringSplitOptions.RemoveEmptyEntries);
                        OnCommandMessageReceived(_lastSentCommand, messages);
                    }
                }
            }
            catch
            {
            }
        }
    }
}