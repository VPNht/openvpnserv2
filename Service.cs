﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net;
using Grapevine.Server;
using System.Reflection;
using Newtonsoft.Json;

namespace OpenVpn
{
	class DebuggableService
	{
        protected EventLog EventLog;

		public DebuggableService()
		{
            this.EventLog = new EventLog("VPN.ht");
            this.EventLog.Source = "VPN.ht Service";
            this.EventLog.Log = "VPN.ht Log";
        }

		public void Start(string[] args)
		{
            this.OnStart(args);
        }

		public void Stop()
		{
            this.OnStop();
        }

		protected virtual void OnStart(string[] args)
		{
        }

		protected virtual void OnStop()
		{
        }
	}

	public class OpenVPNConfig
	{
		public Boolean LogAppend { get; set; }
		public String ExePath { get; set; }
		public String ConfigDir { get; set; }
		public String ConfigExtension { get; set; }
		public String LogDir { get; set; }
		public String Priority { get; set; }
	}

	class OpenVpnService : DebuggableService
    {
        public static string DefaultServiceName = "VPN.ht";

        public const string Package = "openvpn";
        private List<OpenVpnChild> Subprocesses;
        private RestServer Server;

        public OpenVpnService()
        {
            this.Subprocesses = new List<OpenVpnChild>();

            // Setup REST server
            this.Server = new RestServer();
            this.Server.AfterStarting += StartOpenVPN;
            this.Server.AfterStopping += StopOpenVPN;

            // Setup OpenVPN client
            ManagementClient.Instance.OnStateChanged += Client_OnStateChanged;
            ManagementClient.Instance.OnCommandSucceeded += Client_OnCommandSucceeded;
            ManagementClient.Instance.OnCommandFailed += Client_OnCommandFailed;
            ManagementClient.Instance.OnMessageReceived += Client_OnMessageReceived;
            ManagementClient.Instance.OnCommandMessageReceived += Client_OnCommandMessageReceived;
        }

        protected override void OnStart(string[] args)
        {
			Console.WriteLine("Starting service");

			Server.Start();
        }

        protected override void OnStop()
        {
			Console.WriteLine("Stopping service");

			Server.Stop();
        }

        private void StartOpenVPN(RestServer server)
        {
            try
            {
				// Read config
				string configJSON = File.ReadAllText("./config.json");

				OpenVPNConfig config = JsonConvert.DeserializeObject<OpenVPNConfig>(configJSON);

				var serviceConfig = new OpenVpnServiceConfiguration()
                        {
					exePath = config.ExePath,
					configDir = config.ConfigDir,
					configExt = "." + config.ConfigExtension,
					logDir = config.LogDir,
					logAppend = config.LogAppend,
					priorityClass = GetPriorityClass(config.Priority),
                            eventLog = EventLog,
                        };


                        /// Only attempt to start the service
                        /// if openvpn.exe is present. This should help if there are old files
                        /// and registry settings left behind from a previous OpenVPN 32-bit installation
                        /// on a 64-bit system.
				if (!File.Exists(config.ExePath))
                        {
					//throw Exception("OpenVPN binary does not exist at " + config.exePath);
                        }

                        // The necessary OpenVPN configuration file is fetched and saved by the client application
                        // so we have to wait until it's available
                        bool foundConfiguration = false;

                        while (!foundConfiguration)
                        {
					foreach (var configFilename in Directory.EnumerateFiles(config.ConfigDir, "*" + config.ConfigExtension, System.IO.SearchOption.AllDirectories))
                            {
                                try
                                {
                                    foundConfiguration = true;
							var child = new OpenVpnChild(serviceConfig, configFilename);
                                    Subprocesses.Add(child);
                                    child.Start();
                                }
                                catch (Exception e)
                                {
                                    EventLog.WriteEntry("Caught exception " + e.Message + " when starting openvpn for " + configFilename);
                                }
                            }

                            Thread.Sleep(1000);
                        }
                    }
            catch (Exception e)
            {
                EventLog.WriteEntry("Exception occured during OpenVPN service start: " + e.Message + e.StackTrace);
                throw e;
            }
        }

        private void StopOpenVPN(RestServer server)
        {
            foreach (var child in Subprocesses)
            {
                child.StopProcess();
            }
        }

        private RegistryKey GetRegistrySubkey(RegistryView rView)
        {
            try
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, rView)
                    .OpenSubKey("Software\\VPN.ht");
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        private System.Diagnostics.ProcessPriorityClass GetPriorityClass(string priorityString)
        {
			if (String.Equals(priorityString, "IDLE_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
			{
                return System.Diagnostics.ProcessPriorityClass.Idle;
            }
            else if (String.Equals(priorityString, "BELOW_NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.BelowNormal;
            }
            else if (String.Equals(priorityString, "NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.Normal;
            }
            else if (String.Equals(priorityString, "ABOVE_NORMAL_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.AboveNormal;
            }
            else if (String.Equals(priorityString, "HIGH_PRIORITY_CLASS", StringComparison.InvariantCultureIgnoreCase))
            {
                return System.Diagnostics.ProcessPriorityClass.High;
            }
			else
			{
                throw new Exception("Unknown priority name: " + priorityString);
            }
        }

        public static int Main(string[] args)
        {
            DebuggableService service = new OpenVpnService();
            service.Start(args);
            return 0;
        }

        private void Client_OnStateChanged(ClientState clientState, OpenVpnState openVpnState)
        {
        }

        private void Client_OnMessageReceived(string source, string message)
        {
            var client = ManagementClient.Instance;

			Console.WriteLine("[MSG] " + source + ":" + message);

            if (source == "HOLD")
            {
                client.SendCommand("pid");
                client.SendCommand("bytecount", "1");
                client.SendCommand("state", "on");
                client.SendCommand("hold", "release");
            }
            else if( source == "PASSWORD" )
            {
                client.SendCommand("username", "'Auth' " + client.Username);
            }
            else if( source == "BYTECOUNT" )
            {
                int separator = message.IndexOf(",");
                String upload = message.Substring(0, separator++);
                String download = message.Substring(separator, message.Length - separator);
                client.UploadedBytes = Int32.Parse(upload);
                client.DownloadedBytes = Int32.Parse(download);
            }
            else if( source == "STATE" )
            {
                String[] parameters = message.Split(',');
                String timestamp = parameters[0];
                String state = parameters[1];
                String localIP = parameters[3];
                String remoteIP = parameters[4];

                switch (state)
                {
                    case "WAIT":
                        client.OpenVpnState = OpenVpnState.CONNECTING;
                        break;

                    case "AUTH":
                        client.OpenVpnState = OpenVpnState.AUTHENTICATING;
                        break;

                    case "GET_CONFIG":
                    case "ASSIGN_IP":
                    case "ADD_ROUTES":
                        client.OpenVpnState = OpenVpnState.CONFIGURATING;
                        break;

                    case "CONNECTED":
                        client.LocalIP = IPAddress.Parse(localIP);
                        client.RemoteIP = IPAddress.Parse(remoteIP);
                        client.OpenVpnState = OpenVpnState.CONNECTED;
                        client.ConnectionStartTime = DateTime.Now;
                        break;

                    case "RECONNECTING":
                        client.OpenVpnState = OpenVpnState.RECONNECTING;
                        break;
                }
            }
			else if (source == "FATAL")
			{
				client.OpenVpnState = OpenVpnState.DISCONNECTED;
			}
        }

        private void Client_OnCommandMessageReceived(string command, string[] messages)
        {
            foreach (string message in messages)
            {
                EventLog.WriteEntry(message);
            }
        }

        private void Client_OnCommandSucceeded(string command, string message)
        {
            var client = ManagementClient.Instance;

            EventLog.WriteEntry("[OK] " + command + ":" + message);

            // pid=$PID\r\n
            if (command == "pid")
            {
                client.OpenVpnPID = message.Substring(4, message.Length - 6);
            }
            else if (command == "username")
            {
                client.SendCommand("password", "'Auth' " + client.Password);
            }
            else if (command == "signal")
            {
                client.Disconnect();
            }
        }

        private void Client_OnCommandFailed(string command, string message)
        {
            EventLog.WriteEntry("[ERROR] " + command + ":" + message);
        }
    }

    class OpenVpnServiceConfiguration
    {
        public string exePath {get;set;}
        public string configExt {get;set;}
        public string configDir {get;set;}


        public string logDir {get;set;}
        public bool logAppend {get;set;}
        public System.Diagnostics.ProcessPriorityClass priorityClass {get;set;}

        public EventLog eventLog {get;set;}
    }

    class OpenVpnChild
    {
        StreamWriter logFile;
        Process process;
        ProcessStartInfo startInfo;
        System.Timers.Timer restartTimer;
        OpenVpnServiceConfiguration config;
        string configFile;
        string exitEvent;

		public OpenVpnChild(OpenVpnServiceConfiguration config, string configFile)
		{
            this.config = config;
            /// SET UP LOG FILES
            /* Because we will be using the filenames in our closures,
             * so make sure we are working on a copy */
            this.configFile = String.Copy(configFile);
            this.exitEvent = Path.GetFileName(configFile) + "_" + Process.GetCurrentProcess().Id.ToString();
            var justFilename = System.IO.Path.GetFileName(configFile);
            var logFilename = config.logDir + Path.DirectorySeparatorChar + justFilename.Substring(0, justFilename.Length - config.configExt.Length) + ".log";

            // FIXME: if (!init_security_attributes_allow_all (&sa))
            //{
            //    MSG (M_SYSERR, "InitializeSecurityDescriptor start_" PACKAGE " failed");
            //    goto finish;
            //}

            logFile = new StreamWriter(File.Open(logFilename,
                config.logAppend ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read), new UTF8Encoding(false));
            logFile.AutoFlush = true;

            /// SET UP PROCESS START INFO
            string[] procArgs = {
                "--config",
                "\"" + configFile + "\""
            };

            this.startInfo = new System.Diagnostics.ProcessStartInfo()
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,

                FileName = config.exePath,
                Arguments = String.Join(" ", procArgs),
                WorkingDirectory = config.configDir,

                UseShellExecute = false,
                /* create_new_console is not exposed -- but we probably don't need it?*/
            };
        }

        // set exit event so that openvpn will terminate
		public void SignalProcess()
		{
			if (restartTimer != null)
			{
                restartTimer.Stop();
            }
            try
            {
                if (!process.HasExited)
                {

					try
					{
                      var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, exitEvent);

                      process.Exited -= Watchdog; // Don't restart the process after exit

                      waitHandle.Set();
                      waitHandle.Close();
					}
					catch (IOException e)
					{
                      config.eventLog.WriteEntry("IOException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace);
					}
					catch (UnauthorizedAccessException e)
					{
                      config.eventLog.WriteEntry("UnauthorizedAccessException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace);
					}
					catch (WaitHandleCannotBeOpenedException e)
					{
                      config.eventLog.WriteEntry("WaitHandleCannotBeOpenedException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace);
					}
					catch (ArgumentException e)
					{
                      config.eventLog.WriteEntry("ArgumentException creating exit event named '" + exitEvent + "' " + e.Message + e.StackTrace);
                   }
                }
            }
            catch (InvalidOperationException) { }
        }

        // terminate process after a timeout
		public void StopProcess()
		{
			if (restartTimer != null)
			{
                restartTimer.Stop();
            }

            try
            {
                process.Exited -= Watchdog; // Don't restart the process after kill
                process.Kill();
                process.WaitForExit();
            }
			catch (Exception e)
			{
                Console.WriteLine("Exception thrown " + e.Message + e.StackTrace);
            }
        }

		public void Wait()
		{
            process.WaitForExit();
            logFile.Close();
        }

		public void Restart()
		{
			if (restartTimer != null)
			{
                restartTimer.Stop();
            }
            /* try-catch... because there could be a concurrency issue (write-after-read) here? */
            if (!process.HasExited)
            {
                process.Exited -= Watchdog;
                process.Exited += FastRestart; // Restart the process after kill
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    Start();
                }
            }
            else
            {
                Start();
            }
        }

		private void WriteToLog(object sendingProcess, DataReceivedEventArgs e)
		{
            if (e != null)
                logFile.WriteLine(e.Data);
        }

        /// Restart after 10 seconds
        /// For use with unexpected terminations
        private void Watchdog(object sender, EventArgs e)
        {
            config.eventLog.WriteEntry("Process for " + configFile + " exited. Restarting in 10 sec.");

            restartTimer = new System.Timers.Timer(10000);
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += (object source, System.Timers.ElapsedEventArgs ev) =>
                {
                    Start();
                };
            restartTimer.Start();
        }

        /// Restart after 3 seconds
        /// For use with Restart() (e.g. after a resume)
        private void FastRestart(object sender, EventArgs e)
        {
            config.eventLog.WriteEntry("Process for " + configFile + " restarting in 3 sec");
            restartTimer = new System.Timers.Timer(3000);
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += (object source, System.Timers.ElapsedEventArgs ev) =>
                {
                    Start();
                };
            restartTimer.Start();
        }

		public void Start()
		{
            process = new System.Diagnostics.Process();

            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += WriteToLog;
            process.ErrorDataReceived += WriteToLog;
            process.Exited += Watchdog;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.PriorityClass = config.priorityClass;
        }

    }
}
