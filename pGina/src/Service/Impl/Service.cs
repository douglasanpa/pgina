﻿/*
	Copyright (c) 2014, pGina Team
	All rights reserved.

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions are met:
		* Redistributions of source code must retain the above copyright
		  notice, this list of conditions and the following disclaimer.
		* Redistributions in binary form must reproduce the above copyright
		  notice, this list of conditions and the following disclaimer in the
		  documentation and/or other materials provided with the distribution.
		* Neither the name of the pGina Team nor the names of its contributors
		  may be used to endorse or promote products derived from this software without
		  specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
	ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
	DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
	(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
	LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
	ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using System.ServiceProcess;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net;

using log4net;

using pGina.Shared.Settings;
using pGina.Shared.Logging;
using pGina.Shared.Interfaces;

using pGina.Core;
using pGina.Core.Messages;
using pGina.Shared.Types;

using Abstractions.Pipes;
using Abstractions.Logging;
using Abstractions.Helpers;

namespace pGina.Service.Impl
{
    public class Service
    {
        private ILog m_logger = LogManager.GetLogger("pGina.Service.Impl");
        private ILog m_abstractLogger = LogManager.GetLogger("Abstractions");
        private PipeServer m_server = null;
        private ObjectCache<int, List<SessionProperties>> m_sessionPropertyCache = new ObjectCache<int, List<SessionProperties>>();

        static Service()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
                Framework.Init();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("pGina", ex.ToString(), EventLogEntryType.Error);
                throw;
            }
        }

        public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                ILog logger = LogManager.GetLogger("pGina.Service.Exception");
                Exception e = args.ExceptionObject as Exception;
                logger.ErrorFormat("CurrentDomain_UnhandledException: {0}", e);
            }
            catch
            {
                // don't kill the existing exception stack with one of our own
            }
        }

        private void HookUpAbstractionsLibraryLogging()
        {
            LibraryLogging.AddListener(LibraryLogging.Level.Debug, m_abstractLogger.DebugFormat);
            LibraryLogging.AddListener(LibraryLogging.Level.Error, m_abstractLogger.ErrorFormat);
            LibraryLogging.AddListener(LibraryLogging.Level.Info, m_abstractLogger.InfoFormat);
            LibraryLogging.AddListener(LibraryLogging.Level.Warn, m_abstractLogger.WarnFormat);
        }

        private void DetachAbstractionsLibraryLogging()
        {
            LibraryLogging.RemoveListener(LibraryLogging.Level.Debug, m_abstractLogger.DebugFormat);
            LibraryLogging.RemoveListener(LibraryLogging.Level.Error, m_abstractLogger.ErrorFormat);
            LibraryLogging.RemoveListener(LibraryLogging.Level.Info, m_abstractLogger.InfoFormat);
            LibraryLogging.RemoveListener(LibraryLogging.Level.Warn, m_abstractLogger.WarnFormat);
        }

        public string[] PluginDirectories
        {
            get { return Core.Settings.Get.PluginDirectories; }
        }

        public Service()
        {
            try
            {
                string pipeName = Core.Settings.Get.ServicePipeName;
                int maxClients = Core.Settings.Get.MaxClients;
                m_logger.DebugFormat("Service created - PipeName: {0} MaxClients: {1}", pipeName, maxClients);
                m_logger.DebugFormat("System Info: {0}", Abstractions.Windows.OsInfo.OsDescription());
                m_server = new PipeServer(pipeName, maxClients, (Func<IDictionary<string, object>, IDictionary<string, object>>)HandleMessage);
                m_logger.DebugFormat("Using plugin directories: ");
                foreach (string dir in PluginDirectories)
                    m_logger.DebugFormat("  {0}", dir);
            }
            catch (Exception e)
            {
                EventLog.WriteEntry("pGina", e.ToString(), EventLogEntryType.Error);
                m_logger.ErrorFormat("Service startup error: {0}", e.ToString());
                throw;
            }
        }

        public void Start()
        {
            m_logger.InfoFormat("Starting service");
            HookUpAbstractionsLibraryLogging();
            m_server.Start();
            PluginDriver.Starting();
        }

        public void Stop()
        {
            m_logger.InfoFormat("Stopping service");
            PluginDriver.Stopping();
            DetachAbstractionsLibraryLogging();
            m_server.Stop();
        }

        public Boolean OnCustomCommand()
        {
            Boolean result = false;
            foreach (IPluginLogoffRequestAddTime plugin in PluginLoader.GetOrderedPluginsOfType<IPluginLogoffRequestAddTime>())
            {
                try
                {
                    if (plugin.LogoffRequestAddTime())
                        result = true;
                }
                catch (Exception e)
                {
                    m_logger.ErrorFormat("Ignoring unhandled exception from {0}: {1}", plugin.Uuid, e);
                    result = false;
                }
            }
            return result;
        }

        public void SessionChange(int sessionID, SessionChangeReason evnt)
        {
            m_logger.InfoFormat("SessionChange:{0} {1}", sessionID, (int)evnt);
            Thread rem_local = new Thread(() => SessionChangeThread(sessionID, evnt));
            rem_local.Start();
        }
        /*
        public void SessionChange(SessionChangeDescription changeDescription)
        {
            m_logger.InfoFormat("SessionChange: {0} -> {1}", changeDescription.SessionId, changeDescription.Reason);

            try
            {
                lock (m_sessionPropertyCache)
                {
                    foreach (IPluginEventNotifications plugin in PluginLoader.GetOrderedPluginsOfType<IPluginEventNotifications>())
                    {
                        try
                        {
                            if (m_sessionPropertyCache.Exists(changeDescription.SessionId))
                                plugin.SessionChange(changeDescription, m_sessionPropertyCache.Get(changeDescription.SessionId));
                            else
                                plugin.SessionChange(changeDescription, null);
                        }
                        catch (Exception e)
                        {
                            m_logger.ErrorFormat("Ignoring unhandled exception from {0}: {1}", plugin.Uuid, e);
                        }
                    }

                    // If this is a logout, remove from our map
                    if (changeDescription.Reason == SessionChangeReason.SessionLogoff && m_sessionPropertyCache.Exists(changeDescription.SessionId))
                        m_sessionPropertyCache.Remove(changeDescription.SessionId);
                }
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("Exception while handling SessionChange event: {0}", e);
            }
        }*/

        // This will be called on seperate threads, 1 per client connection and
        //  represents a connected client - that is, until we return null,
        //  the connection remains open and operations on behalf of this client
        //  should occur in this thread etc.  The current managed thread id
        //  can be used to differentiate between instances if scope requires.
        private IDictionary<string, object> HandleMessage(IDictionary<string, object> msg)
        {
            int instance = Thread.CurrentThread.ManagedThreadId;
            ILog logger = LogManager.GetLogger(string.Format("HandleMessage[{0}]", instance));

            MessageType type = (MessageType) Enum.ToObject(typeof (MessageType), msg["MessageType"]);

            // Very noisy, not usually worth having on, configurable via "TraceMsgTraffic" boolean
            bool traceMsgTraffic = pGina.Core.Settings.Get.GetSetting("TraceMsgTraffic", false);
            if (traceMsgTraffic)
            {
                logger.DebugFormat("{0} message received", type);
            }

            switch (type)
            {
                case MessageType.Disconnect:
                    // We ack, and mark this as LastMessage, which tells the pipe framework
                    //  not to expect further messages
                    IDictionary<string, object> disconnectAck = new EmptyMessage(MessageType.Ack).ToDict();  // Ack
                    disconnectAck["LastMessage"] = true;
                    return disconnectAck;
                case MessageType.Hello:
                    return new EmptyMessage(MessageType.Hello).ToDict();  // Ack with our own hello
                case MessageType.Log:
                    HandleLogMessage(new LogMessage(msg));
                    return new EmptyMessage(MessageType.Ack).ToDict();  // Ack
                case MessageType.LoginRequest:
                    return HandleLoginRequest(new LoginRequestMessage(msg)).ToDict();
                case MessageType.DynLabelRequest:
                    return HandleDynamicLabelRequest(new DynamicLabelRequestMessage(msg)).ToDict();
                case MessageType.LoginInfoChange:
                    return HandleLoginInfoChange(new LoginInfoChangeMessage(msg)).ToDict();
                case MessageType.UserInfoRequest:
                    return HandleUserInfoRequest(new UserInformationRequestMessage(msg)).ToDict();
                case MessageType.ChangePasswordRequest:
                    return HandleChangePasswordRequest(new ChangePasswordRequestMessage(msg)).ToDict();
                default:
                    return null;                // Unknowns get disconnected
            }
        }

        private void HandleLogMessage(LogMessage msg)
        {
            ILog logger = LogManager.GetLogger(string.Format("RemoteLog[{0}]", msg.LoggerName));

            switch (msg.Level.ToLower())
            {
                case "info":
                    logger.InfoFormat("{0}", msg.LoggedMessage);
                    break;
                case "debug":
                    logger.DebugFormat("{0}", msg.LoggedMessage);
                    break;
                case "error":
                    logger.ErrorFormat("{0}", msg.LoggedMessage);
                    break;
                case "warn":
                    logger.WarnFormat("{0}", msg.LoggedMessage);
                    break;
                default:
                    logger.DebugFormat("{0}", msg.LoggedMessage);
                    break;
            }
        }

        private LoginResponseMessage HandleLoginRequest(LoginRequestMessage msg)
        {
            if (String.IsNullOrEmpty(msg.Username))
                return new LoginResponseMessage() { Result = false, Message = "No Username supplied" };
            try
            {
                PluginDriver sessionDriver = new PluginDriver();
                sessionDriver.UserInformation.Username = msg.Username.Trim();
                sessionDriver.UserInformation.Password = msg.Password;

                // check if a plugin still does some logoff work for this user
                Boolean thisUserLogoff = false;
                foreach (IPluginLogoffRequestAddTime plugin in PluginLoader.GetOrderedPluginsOfType<IPluginLogoffRequestAddTime>())
                {
                    if (plugin.LoginUserRequest(sessionDriver.UserInformation.Username))
                        thisUserLogoff = true;
                }
                if (thisUserLogoff)
                    return new LoginResponseMessage() { Result = false, Message = String.Format("Still logoff work to do for user {0}\nWait a view seconds and retry", sessionDriver.UserInformation.Username) };

                BooleanResult result = new BooleanResult() { Success = true, Message = "" };

                if (new[] {LoginRequestMessage.LoginReason.Login, LoginRequestMessage.LoginReason.CredUI}.Contains(msg.Reason))
                {
                    m_logger.DebugFormat("Processing LoginRequest for: {0} in session: {1} reason: {2}", sessionDriver.UserInformation.Username, msg.Session, msg.Reason);

                    Boolean isLoggedIN = false;
                    List<string> Users = Abstractions.WindowsApi.pInvokes.GetInteractiveUserList();
                    foreach (string user in Users)
                    {
                        m_logger.DebugFormat("Interactive user:{0}", user);
                        if (user.EndsWith(sessionDriver.UserInformation.Username, StringComparison.CurrentCultureIgnoreCase))
                        {
                            //the user is still logged in
                            isLoggedIN = true;
                            m_logger.DebugFormat("User:{0} is Locked in Session:{1}", sessionDriver.UserInformation.Username, msg.Session);
                        }
                    }
                    if (!isLoggedIN)
                        result = sessionDriver.PerformLoginProcess();

                    if (result.Success && (!isLoggedIN || msg.Reason == LoginRequestMessage.LoginReason.CredUI))
                    {
                        lock (m_sessionPropertyCache)
                        {
                            List<SessionProperties> ses = new List<SessionProperties>();
                            if (m_sessionPropertyCache.Exists(msg.Session))
                            {
                                ses = m_sessionPropertyCache.Get(msg.Session);
                            }
                            bool partof = false;
                            foreach (SessionProperties sess in ses)
                            {
                                UserInformation ui = sess.GetTrackedSingle<UserInformation>();
                                m_logger.InfoFormat("compare stored-user:{0} this-user:{1}", ui.Username, sessionDriver.UserInformation.Username);
                                if (sessionDriver.UserInformation.Username.Equals(ui.Username, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    partof = true;
                                    m_logger.InfoFormat("contain user {0} in sessioninfo:{1} GUID:{2}", ui.Username, msg.Session, sess.Id);
                                    break;
                                }
                            }
                            if (!partof)
                            {
                                if (isLoggedIN)
                                {
                                    UserInformation ui = FindUserInfoInPropertyCache(sessionDriver.UserInformation.Username);
                                    if (ui != null)
                                    {
                                        sessionDriver.SessionProperties.AddTrackedSingle<UserInformation>(ui);
                                    }
                                }
                                if (msg.Reason == LoginRequestMessage.LoginReason.CredUI)
                                {
                                    sessionDriver.SessionProperties.CREDUI = true;
                                }
                                else
                                {
                                    sessionDriver.SessionProperties.CREDUI = false;
                                }
                                ses.Add(sessionDriver.SessionProperties);
                                m_logger.InfoFormat("add user {0} to sessioninfo:{1} GUID:{2} CREDUI:{3}", sessionDriver.UserInformation.Username, msg.Session, sessionDriver.SessionProperties.Id, (msg.Reason == LoginRequestMessage.LoginReason.CredUI) ? "true" : "false");
                                m_logger.InfoFormat("ses username:{0} description:{1} credui:{2} isLoggedIN:{3}", ses.Last().GetTrackedSingle<UserInformation>().Username, ses.Last().GetTrackedSingle<UserInformation>().Description, ses.Last().CREDUI, isLoggedIN);
                                m_sessionPropertyCache.Add(msg.Session, ses);
                            }
                        }
                    }
                }
                else
                {
                    m_logger.DebugFormat("Parse Request for: {0} in session: {1} reason: {2}", sessionDriver.UserInformation.Username, msg.Session, msg.Reason);
                }

                return new LoginResponseMessage()
                {
                    Result = result.Success,
                    Message = result.Message,
                    Username = sessionDriver.UserInformation.Username,
                    Domain = sessionDriver.UserInformation.Domain,
                    Password = sessionDriver.UserInformation.Password
                };
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("Internal error, unexpected exception while handling login request: {0}", e);
                return new LoginResponseMessage() { Result = false, Message = "Internal error" };
            }
        }

        private DynamicLabelResponseMessage HandleDynamicLabelRequest(DynamicLabelRequestMessage msg)
        {
            switch (msg.Name)
            {
                case "MOTD":
                    string text = Settings.Get.Motd;
                    string motd = FormatMotd(text);
                    return new DynamicLabelResponseMessage() { Name = msg.Name, Text = motd };
                // Others can be added here.
            }
            return new DynamicLabelResponseMessage();
        }

        private UserInformationResponseMessage HandleUserInfoRequest(UserInformationRequestMessage msg)
        {
            lock (m_sessionPropertyCache)
            {
                if (m_sessionPropertyCache.Exists(msg.SessionID))
                {
                    SessionProperties props = m_sessionPropertyCache.Get(msg.SessionID).First();
                    UserInformation userInfo = props.GetTrackedSingle<UserInformation>();

                    return new UserInformationResponseMessage
                    {
                        OriginalUsername = userInfo.OriginalUsername,
                        Username = userInfo.Username,
                        Domain = userInfo.Domain
                    };
                }
            }

            return new UserInformationResponseMessage();
        }

        private EmptyMessage HandleLoginInfoChange(LoginInfoChangeMessage msg)
        {
            m_logger.DebugFormat("Changing login info at request of client, User {0} moving from {1} to {2}", msg.Username, msg.FromSession, msg.ToSession);
            lock (m_sessionPropertyCache)
            {
                if (m_sessionPropertyCache.Exists(msg.FromSession))
                {
                    m_sessionPropertyCache.Add(msg.ToSession, m_sessionPropertyCache.Get(msg.FromSession));
                    m_sessionPropertyCache.Remove(msg.FromSession);
                }
            }
            return new EmptyMessage(MessageType.Ack);
        }

        private string FormatMotd(string text)
        {
            string motd = text;

            // Version
            string pattern = @"\%v";
            if (Regex.IsMatch(motd, pattern))
            {
                string vers = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                motd = Regex.Replace(motd, pattern, vers);
            }

            // IP Address
            pattern = @"\%i";
            if (Regex.IsMatch(motd, pattern))
            {
                // Get IP address of this computer
                IPAddress[] ipList = Dns.GetHostAddresses("");
                string ip = "";
                // Grab the first IPv4 address in the list
                foreach (IPAddress addr in ipList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ip = addr.ToString();
                        break;
                    }
                }
                motd = Regex.Replace(motd, pattern, ip);
            }

            // machine name
            pattern = @"\%m";
            if (Regex.IsMatch(motd, pattern))
            {
                motd = Regex.Replace(motd, pattern, Environment.MachineName);
            }

            // Date
            pattern = @"\%d";
            if (Regex.IsMatch(motd, pattern))
            {
                string today = DateTime.Today.ToString("MMMM dd, yyyy");
                motd = Regex.Replace(motd, pattern, today);
            }

            // DNS name
            pattern = @"\%n";
            if (Regex.IsMatch(motd, pattern))
            {
                string dns = Dns.GetHostName();
                motd = Regex.Replace(motd, pattern, dns);
            }

            return motd;
        }

        /// <summary>
        /// m_sessionPropertyCache must be locked
        /// </summary>
        /// <param name="username"></param>
        /// <returns>Dict<sessionID,pos in the list></returns>
        private Dictionary<int, int> FindUserInPropertyCache(string username)
        {
            Dictionary<int, int> ret = new Dictionary<int, int>();

            List<int> SessionsList = m_sessionPropertyCache.GetAll(); //all pgina watched sessions
            //Dictionary<int, List<string>> othersessioncontext = new Dictionary<int, List<string>>(); //all exept my sessions, a list of usernames in which a process is running
            foreach (int Sessions in SessionsList)
            {
                List<SessionProperties> ses = m_sessionPropertyCache.Get(Sessions);
                for (int x = 0; x < ses.Count; x++)
                {
                    UserInformation uInfo = ses[x].GetTrackedSingle<UserInformation>();
                    if (uInfo.Username.Equals(username, StringComparison.CurrentCultureIgnoreCase))
                    {
                        ret.Add(Sessions, x);
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// m_sessionPropertyCache must be locked
        /// </summary>
        /// <param name="username"></param>
        /// <returns>null == not found</returns>
        private UserInformation FindUserInfoInPropertyCache(string username)
        {
            UserInformation uInfo = null;
            Dictionary<int, int> foundIN = FindUserInPropertyCache(username);
            if (foundIN.Count == 0)
            {
                return null;
            }

            foreach (KeyValuePair<int, int> pair in foundIN)
            {
                List<SessionProperties> sesProps = m_sessionPropertyCache.Get(pair.Key);
                if (pair.Value == 0) //0 is an interactive session
                {
                    return sesProps[pair.Value].GetTrackedSingle<UserInformation>();
                }

                uInfo = sesProps[pair.Value].GetTrackedSingle<UserInformation>();
            }

            return uInfo;
        }

        private void SessionChangeThread(int sessionID, SessionChangeReason evnt)
        {
            m_logger.InfoFormat("SessionChange: {0} -> {1}", sessionID, evnt);
            try
            {
                lock (m_sessionPropertyCache)
                {
                    if (evnt == SessionChangeReason.SessionLogoff)
                    {
                            CREDUIhelper(sessionID);
                    }
                    foreach (IPluginEventNotifications plugin in PluginLoader.GetOrderedPluginsOfType<IPluginEventNotifications>())
                    {
                        if (m_sessionPropertyCache.Exists(sessionID))
                        {
                            plugin.SessionChange(sessionID, evnt, m_sessionPropertyCache.Get(sessionID));
                        }
                        else
                        {
                            plugin.SessionChange(sessionID, evnt, null);
                        }
                    }
                    // If this is a logout, remove from our map
                    if (evnt == SessionChangeReason.SessionLogoff && m_sessionPropertyCache.Exists(sessionID))
                    {
                        m_sessionPropertyCache.Remove(sessionID);
                    }
                }
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("Exception while handling SessionChange event: {0}", e);
            }
        }

        /// <summary>
        /// m_sessionPropertyCache must be locked
        /// </summary>
        /// <param name="session"></param>
        private void CREDUIhelper(int session)
        {
            m_logger.InfoFormat("CREDUIhelper:({0})", session);
            List<SessionProperties> mysessionList = m_sessionPropertyCache.Get(session); //list of all users in my session
            UserInformation userInfo = m_sessionPropertyCache.Get(session).First().GetTrackedSingle<UserInformation>(); //this user is logging of right now (my user)
            List<int> SessionsList = m_sessionPropertyCache.GetAll(); //all pgina watched sessions
            Dictionary<int,List<string>> othersessioncontext = new Dictionary<int,List<string>>(); //all exept my sessions, a list of usernames in which a process is running
            foreach (int Sessions in SessionsList)
            {
                if (session != Sessions) //if not my session
                {
                    //get all usersNames from processes that dont run in my own session (context in which those processes are running)
                    List<string> sesscontext = Abstractions.WindowsApi.pInvokes.GetSessionContext(Sessions);
                    othersessioncontext.Add(Sessions, sesscontext);
                }
            }
            List<string> InteractiveUserList = Abstractions.WindowsApi.pInvokes.GetInteractiveUserList(); //get interactive users



            foreach (SessionProperties s in m_sessionPropertyCache.Get(session))
            {
                m_logger.InfoFormat("info: username:{0} credui:{1} description:{2} session:{3}", s.GetTrackedSingle<UserInformation>().Username, s.CREDUI, s.GetTrackedSingle<UserInformation>().Description, session);
            }
            //catch runas.exe credui processes
            for (int y = 0; y < mysessionList.Count; y++)
            {
                UserInformation allmyuInfo = mysessionList[y].GetTrackedSingle<UserInformation>();
                foreach (int Sessions in SessionsList)
                {
                    if (session != Sessions) //if not my session
                    {
                        // there is a program running as user 'allmyuInfo.Username' in session 'Sessions'
                        // && this user 'allmyuInfo.Username' is not an interactive user
                        m_logger.InfoFormat("{0} '{1}' '{2}'", allmyuInfo.Username, String.Join(" ", othersessioncontext[Sessions]), String.Join(" ", InteractiveUserList));
                        if (othersessioncontext[Sessions].Any(s => s.Equals(allmyuInfo.Username, StringComparison.CurrentCultureIgnoreCase)) && !InteractiveUserList.Any(s => s.ToLower().Contains(Sessions + "\\" + allmyuInfo.Username.ToLower())))
                        {
                            bool hit = false;
                            List<SessionProperties> othersessionList = m_sessionPropertyCache.Get(Sessions); //sessionlist of Sessions (not mine)
                            for (int x = 1; x < othersessionList.Count; x++)
                            {
                                UserInformation ouserInfo = othersessionList[x].GetTrackedSingle<UserInformation>();
                                m_logger.InfoFormat("compare:'{0}' '{1}'", ouserInfo.Username, allmyuInfo.Username);
                                if (ouserInfo.Username.Equals(allmyuInfo.Username, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    // SessionProperties List of 'Sessions' contains the user 'allmyuInfo.Username'
                                    hit = true;
                                }
                            }
                            if (!hit)
                            {
                                //this program was run by using runas or simmilar
                                //push it into the SessionProperties list of 'Sessions'
                                SessionProperties osesprop = new SessionProperties(Guid.NewGuid(), true);
                                osesprop.AddTrackedSingle<UserInformation>(allmyuInfo);
                                othersessionList.Add(osesprop);
                                m_logger.InfoFormat("ive found a program in session:{0} that runs in the context of {1}", Sessions, allmyuInfo.Username);
                                m_logger.InfoFormat("add user:{0} into SessionProperties of session:{1} an set CREDUI to:{2}", allmyuInfo.Username, Sessions, osesprop.CREDUI);
                            }
                            m_sessionPropertyCache.Add(Sessions, othersessionList);// refresh the cache
                        }
                    }
                }
            }




            foreach (SessionProperties s in m_sessionPropertyCache.Get(session))
            {
                m_logger.InfoFormat("info: username:{0} credui:{1} description:{2} session:{3}", s.GetTrackedSingle<UserInformation>().Username, s.CREDUI, s.GetTrackedSingle<UserInformation>().Description, session);
            }
            //set SessionProperties.CREDUI
            for (int y = 0; y < mysessionList.Count; y++)
            {
                UserInformation allmyuInfo = mysessionList[y].GetTrackedSingle<UserInformation>();
                foreach (int Sessions in SessionsList)
                {
                    if (session != Sessions) //if not my session
                    {
                        List<SessionProperties> othersessionList = m_sessionPropertyCache.Get(Sessions); //sessionlist of Sessions (not mine)
                        for (int x = 0; x < othersessionList.Count; x++) //all entries
                        {
                            UserInformation ouserInfo = othersessionList[x].GetTrackedSingle<UserInformation>();
                            m_logger.InfoFormat("compare '{0}' '{1}'", ouserInfo.Username, allmyuInfo.Username);
                            if (ouserInfo.Username.Equals(allmyuInfo.Username, StringComparison.CurrentCultureIgnoreCase))
                            {
                                // there is an entry in the SessionProperties List of session 'Sessions'
                                // ill mark CREDUI of user 'allmyuInfo.Username' in my session 'session' as true
                                // a plugin should not remove or upload a credui user (its up to the plugin dev)
                                if (mysessionList[y].CREDUI != true)
                                {
                                    mysessionList[y].CREDUI = true;
                                    m_logger.InfoFormat("an entry in session:{0} was found from user {1}", Sessions, allmyuInfo.Username);
                                    m_logger.InfoFormat("set CREDUI in SessionProperties of user:{0} in session:{1} to {2}", allmyuInfo.Username, session, mysessionList[y].CREDUI);
                                }
                                // ill mark CREDUI of user 'ouserInfo.Username' in session 'Sessions' as false
                                othersessionList[x].CREDUI = false;
                                m_logger.InfoFormat("set CREDUI in SessionProperties of user:{0} in session:{1} to {2}", ouserInfo.Username, Sessions, othersessionList[x].CREDUI);
                            }
                        }



                        /*
                        if (othersessioncontext[Sessions].Any(s => s.Equals(allmyuInfo.Username, StringComparison.CurrentCultureIgnoreCase)))
                        {
                            // there is a process running in a different session under the context this user
                            // ill mark the sessioninfo struct of my user as credui true
                            // a plugin should not remove or upload a credui user (its up to the plugin dev)
                            //mysessionList.First().CREDUI = true; //the first entry in the list is always an interactive login
                            mysessionList[y].CREDUI = true;
                            m_logger.InfoFormat("a program in session:{0} is running in the context of user {1}", Sessions, allmyuInfo.Username);
                            m_logger.InfoFormat("set CREDUI in SessionProperties of user:{0} in session:{1} to {2}", allmyuInfo.Username, session, mysessionList[y].CREDUI);
                            List<SessionProperties> othersessionList = m_sessionPropertyCache.Get(Sessions); //sessionlist of Sessions (not mine)
                            bool hit = false;
                            for (int x = 0; x < othersessionList.Count; x++)
                            {
                                //one user of this other sessions where a process runs under my user
                                UserInformation ouserInfo = othersessionList[x].GetTrackedSingle<UserInformation>();
                                m_logger.InfoFormat("compare:'{0}' '{1}'", ouserInfo.Username, allmyuInfo.Username);
                                if (ouserInfo.Username.Equals(allmyuInfo.Username, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    // this is the entry
                                    hit = true;
                                    if (x > 0)
                                    {
                                        // user is non interactive
                                        if (userInfo.Username.Equals(ouserInfo.Username, StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            othersessionList[x].CREDUI = false;
                                        }
                                        else
                                        {
                                            othersessionList[x].CREDUI = true;
                                        }
                                        m_logger.InfoFormat("set CREDUI in SessionProperties of user:{0} in session:{1} to {2}", ouserInfo.Username, Sessions, othersessionList[x].CREDUI);
                                    }
                                }
                            }
                            if (!hit)
                            {
                                //this program was run by using runas or simmilar
                                //push it into the SessionProperties list of this session
                                SessionProperties osesprop = mysessionList[y];
                                osesprop.CREDUI = true;
                                osesprop.Id = new Guid(Guid.NewGuid().ToString());
                                othersessionList.Add(osesprop);
                                m_logger.InfoFormat("ive found a program in session:{0} that runs in the context of {1}", Sessions, allmyuInfo.Username);
                                m_logger.InfoFormat("add user:{0} into SessionProperties of session:{1} an set CREDUI to:{2}", allmyuInfo.Username, Sessions, osesprop.CREDUI);
                            }
                            m_sessionPropertyCache.Add(Sessions, othersessionList);// refresh the cache
                        }*/
                    }
                }
            }
            m_sessionPropertyCache.Add(session, mysessionList);// refresh the cache
            foreach (SessionProperties s in m_sessionPropertyCache.Get(session))
            {
                m_logger.InfoFormat("info: username:{0} credui:{1} description:{2} session:{3}", s.GetTrackedSingle<UserInformation>().Username, s.CREDUI, s.GetTrackedSingle<UserInformation>().Description, session);
            }






            /*
            //shutdown/reboot case
            if (mysessionList.First().CREDUI == false)
            {
                //this interactive user is logging out
                foreach (int Sessions in SessionsList)
                {
                    if (session != Sessions) //if not my session
                    {
                        List<SessionProperties> othersessionList = m_sessionPropertyCache.Get(Sessions); //sessionlist of Sessions (not mine)
                        for (int y = othersessionList.Count - 1; y > 0; y--) //only other credui users
                        {
                            if (userInfo.Username.Equals(othersessionList[y].GetTrackedSingle<UserInformation>().Username, StringComparison.CurrentCultureIgnoreCase))
                            {
                                //no process of my users should run anymore anywhere
                                //if so mysessionList.First().CREDUI would be true
                                m_logger.InfoFormat("no process is running in session:{0} under the context of user:{1} removing:{1} in session:{0}", Sessions, userInfo.Username);
                                othersessionList.RemoveAt(y);
                            }
                        }
                        m_sessionPropertyCache.Add(Sessions, othersessionList);// refresh the cache
                    }
                }
            }
            */





            for (int x = mysessionList.Count - 1; x > 0; x--)
            {
                UserInformation myuserInfo = mysessionList[x].GetTrackedSingle<UserInformation>();
                if (InteractiveUserList.Any(s => s.ToLower().Contains(myuserInfo.Username.ToLower())))
                {
                    // a program in my session runs in a different context of an also interactive logged in user
                    // ill remove this SessionProperty from my SessionProperties List
                    mysessionList.RemoveAt(x);
                    m_logger.InfoFormat("user {0} is still interactively logged in", myuserInfo.Username);
                    m_logger.InfoFormat("removing SessionProperties entry of user:{0} from session:{1}", myuserInfo.Username, session);
                }
            }






            /*
            List<string> myCREDUIusernameList = new List<string>(); //my credui usersnames
            for (int x = 1; x < mysessionList.Count; x++)
            {
                UserInformation myCREDUIusername = mysessionList[x].GetTrackedSingle<UserInformation>();
                myCREDUIusernameList.Add(myCREDUIusername.Username);
            }*/
            for (int x = mysessionList.Count - 1; x > 0; x--) //only credui users
            {
                bool hit = false;
                UserInformation myCREDUIusername = mysessionList[x].GetTrackedSingle<UserInformation>();
                foreach (int Sessions in SessionsList)
                {
                    if (session != Sessions) //if not my session
                    {
                        List<SessionProperties> othersessionList = m_sessionPropertyCache.Get(Sessions); //sessionlist of Sessions (not mine)
                        for (int y = othersessionList.Count - 1; y >= 0; y--) //all users
                        {
                            UserInformation oCREDUIusername = othersessionList[y].GetTrackedSingle<UserInformation>();
                            //m_logger.InfoFormat("'{0}' '{1}'", oCREDUIusername.Username, String.Join(" ", myCREDUIusernameList));
                            //if (myCREDUIusernameList.Any(s => s.Equals(oCREDUIusername.Username, StringComparison.CurrentCultureIgnoreCase)))
                            m_logger.InfoFormat("'{0}' '{1}'", myCREDUIusername.Username, oCREDUIusername.Username);
                            if (myCREDUIusername.Username.Equals(oCREDUIusername.Username, StringComparison.CurrentCultureIgnoreCase))
                            {
                                hit = true;
                                m_logger.InfoFormat("found SessionProperties entry in session:{0} that equals username {1} in my session:{2}", Sessions, oCREDUIusername.Username, session);
                                m_logger.InfoFormat("removing the SessionProperties entry from session:{0} of user:{1}", session, oCREDUIusername.Username);
                                mysessionList.RemoveAt(x);
                                break;
                                /*
                                //is the credui programm still running
                                m_logger.InfoFormat("'{0}' '{1}'", String.Join(" ", othersessioncontext[Sessions].ToArray()), oCREDUIusername.Username);
                                if (othersessioncontext[Sessions].Any(s => s.Equals(oCREDUIusername.Username, StringComparison.CurrentCultureIgnoreCase)))
                                {
                                    //remove from my list
                                    m_logger.InfoFormat("the program is still running, removing the SessionProperties entry from session:{0} of user:{1}", session, oCREDUIusername.Username);
                                    mysessionList.RemoveAt(x);
                                }
                                else
                                {
                                    //remove from other list, its not running anymore
                                    m_logger.InfoFormat("the program has been closed, removing the SessionProperties entry from session:{0} of user:{1}", Sessions, oCREDUIusername.Username);
                                    othersessionList.RemoveAt(y);
                                }*/
                            }
                        }
                        m_sessionPropertyCache.Add(Sessions, othersessionList);
                        if (hit)
                        {
                            break;
                        }
                    }
                }
                if (!hit)
                {
                    //this credui user runs only in my session
                    m_logger.InfoFormat("the last program in context of {0} is running in session:{1} set CREDUI to false", mysessionList[x].GetTrackedSingle<UserInformation>().Username, session);
                    mysessionList[x].CREDUI = false;
                }
            }
            // refresh the cache
            m_sessionPropertyCache.Add(session, mysessionList);
        }

        private ChangePasswordResponseMessage HandleChangePasswordRequest(ChangePasswordRequestMessage msg)
        {
            try
            {
                m_logger.DebugFormat("Processing ChangePasswordRequest for: {0} domain: {1} session: {2}", msg.Username, msg.Domain, msg.Session);

                SessionProperties properties = m_sessionPropertyCache.Get(msg.Session).First();
                UserInformation userinfo = properties.GetTrackedSingle<UserInformation>();
                userinfo.oldPassword = msg.OldPassword;
                userinfo.Password = msg.NewPassword;
                properties.AddTrackedSingle<UserInformation>(userinfo);

                ChangePasswordPluginActivityInfo pluginInfo = new ChangePasswordPluginActivityInfo();
                pluginInfo.LoadedPlugins = PluginLoader.GetOrderedPluginsOfType<IPluginChangePassword>();
                BooleanResult Result = new BooleanResult();

                // One success means the final result is a success, and we return the message from
                // the last success. Otherwise, we return the message from the last failure.
                foreach ( IPluginChangePassword plug in PluginLoader.GetOrderedPluginsOfType<IPluginChangePassword>() )
                {
                    // Execute the plugin
                    m_logger.DebugFormat("ChangePassword: executing {0}", plug.Uuid);
                    Result = plug.ChangePassword(properties, pluginInfo);

                    m_logger.DebugFormat("ChangePassword: result from {0} is {1} message: {2}", plug.Uuid, Result.Success, Result.Message);

                    if (!Result.Success)
                    {
                        userinfo.Password = msg.OldPassword;
                        properties.AddTrackedSingle<UserInformation>(userinfo);
                        break;
                    }
                }

                return new ChangePasswordResponseMessage()
                {
                    Result = Result.Success,
                    Message = Result.Message,
                    Username = msg.Username,
                    Domain = msg.Domain
                };
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("Internal error, unexpected exception while handling change password request: {0}", e);
                return new ChangePasswordResponseMessage() { Result = false, Message = "Internal error" };
            }
        }
    }
}
