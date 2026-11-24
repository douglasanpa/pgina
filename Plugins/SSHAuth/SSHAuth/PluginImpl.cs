using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using log4net;
using Renci.SshNet;
using Renci.SshNet.Common;
using pGina.Shared.Interfaces;
using pGina.Shared.Types;
using pGina.Shared.Localization;

namespace pGina.Plugin.SSHAuth
{
    public class PluginImpl : IPluginAuthentication, IPluginConfiguration
    {
        private ILog m_logger = LogManager.GetLogger("SSHAuth");

        #region Init-plugin
        public static Guid PluginUuid
        {
            get { return new Guid("{CC35057C-ACA8-499C-B127-AE4CF978F238}"); }
        }

        public PluginImpl()
        {
            using (Process me = Process.GetCurrentProcess())
            {
                m_logger.DebugFormat("Plugin initialized on {0} in PID: {1} Session: {2}", Environment.MachineName, me.Id, me.SessionId);
            }
        }

        public string Name
        {
            get { return "SSHAuth"; }
        }

        public string Description
        {
            get { return "SSHAuth plugin"; }
        }

        public Guid Uuid
        {
            get { return PluginUuid; }
        }

        public string Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }
        #endregion

        public void Starting() { }
        public void Stopping() { }

        public void Configure()
        {
            pGina.Plugin.SSHAuth.Configuration myDialog = new pGina.Plugin.SSHAuth.Configuration();
            myDialog.ShowDialog();
        }

        public BooleanResult AuthenticateUser(SessionProperties properties)
        {
            UserInformation userInfo = properties.GetTrackedSingle<UserInformation>();
            string sshHost = Settings.Store.Host;
            string sshPort = Settings.Store.Port;
            int port;

            if (!int.TryParse(sshPort, out port) || port < 1 || port > 65535)
            {
                return new BooleanResult() { Success = false, Message = Language.Format("Invalid SSH port: {0}", sshPort) };
            }

            // Same check as the old native helper: open a session and try password authentication.
            // The server host key is not verified.
            try
            {
                using (SshClient client = new SshClient(sshHost, port, userInfo.Username, userInfo.Password))
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                    client.Connect();
                    client.Disconnect();
                }
                return new BooleanResult() { Success = true };
            }
            catch (SshAuthenticationException e)
            {
                return new BooleanResult() { Success = false, Message = Language.Format("SSH authentication failed for user {0} ({1})", userInfo.Username, e.Message) };
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("SSH connection to {0}:{1} failed: {2}", sshHost, port, e);
                return new BooleanResult() { Success = false, Message = Language.Format("SSH connection to {0}:{1} failed ({2})", sshHost, port, e.Message) };
            }
        }
    }
}
