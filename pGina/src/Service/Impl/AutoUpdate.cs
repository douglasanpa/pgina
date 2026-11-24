/*
	Copyright (c) 2026, pGina Team
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

using log4net;

using pGina.Core;
using pGina.Shared.Update;

namespace pGina.Service.Impl
{
    /// <summary>
    /// Checks for new releases (UpdateCheck) and, with AutoUpdate, downloads and installs them.
    ///
    /// Installing stops the pGina service, and a session that is open while it restarts can no
    /// longer be unlocked, so the installer only runs while nobody is logged on. It runs very
    /// silently and restarts the computer when it finishes (the credential provider DLL is only
    /// replaced at a restart). Each version is tried at most once a day.
    /// </summary>
    internal class AutoUpdate : IDisposable
    {
        private static readonly ILog m_logger = LogManager.GetLogger("AutoUpdate");

        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "pGina.fork", "Update");

        private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);
        private static readonly TimeSpan InstallCheckEvery = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan RetryAfter = TimeSpan.FromHours(24);

        private readonly object m_lock = new object();
        private readonly Random m_random = new Random();
        private Timer m_checkTimer;
        private Timer m_installTimer;

        public void Start()
        {
            // spread the checks, so a lab that boots together doesn't hit the server together
            m_checkTimer = new Timer(Check, null, FirstCheck + Jitter(30), Timeout.InfiniteTimeSpan);
            m_installTimer = new Timer(TryInstall, null, TimeSpan.FromMinutes(2), InstallCheckEvery);
        }

        public void Dispose()
        {
            if (m_checkTimer != null) m_checkTimer.Dispose();
            if (m_installTimer != null) m_installTimer.Dispose();
            m_checkTimer = m_installTimer = null;
        }

        private TimeSpan Jitter(int maxMinutes)
        {
            lock (m_random)
                return TimeSpan.FromSeconds(m_random.Next(maxMinutes * 60));
        }

        private void Check(object state)
        {
            try
            {
                bool check = Settings.Get.UpdateCheck;
                bool auto = Settings.Get.AutoUpdate;
                if (!check && !auto)
                    return;

                ReleaseInfo release = Updater.GetLatest(Settings.Get.UpdateSource);
                Settings.Get.LatestVersion = release.Version.ToString();
                Settings.Get.LastUpdateCheck = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
                if (!Updater.IsNewer(release))
                {
                    m_logger.DebugFormat("pGina {0} is the latest release", Updater.CurrentVersion);
                    return;
                }
                m_logger.InfoFormat("pGina {0} is available (installed: {1})", release.Version, Updater.CurrentVersion);
                if (!auto)
                    return;

                lock (m_lock)
                {
                    Updater.CreateProtectedFolder(Folder);
                    string path = Updater.Download(release, Folder);
                    Settings.Get.StagedUpdate = path;
                    Settings.Get.StagedUpdateVersion = release.Version.ToString();
                    Settings.Get.StagedUpdateSha256 = Updater.Sha256(path);
                    RemoveOtherFiles(path);
                    m_logger.InfoFormat("pGina {0} downloaded to {1}; it is installed when nobody is logged on", release.Version, path);
                }
                TryInstall(null);
            }
            catch (Exception e)
            {
                m_logger.WarnFormat("Update check failed: {0}", e.Message);
            }
            finally
            {
                Timer t = m_checkTimer;
                if (t != null)
                    t.Change(CheckEvery + Jitter(60), Timeout.InfiniteTimeSpan);
            }
        }

        private void TryInstall(object state)
        {
            if (!Monitor.TryEnter(m_lock))
                return;
            try
            {
                if (!(bool)Settings.Get.AutoUpdate)
                    return;
                string path = Settings.Get.StagedUpdate;
                if (String.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                Version version;
                if (!Version.TryParse((string)Settings.Get.StagedUpdateVersion, out version) || version <= Updater.CurrentVersion)
                {
                    Discard(path, "it isn't newer than the installed version");
                    return;
                }
                if (!Updater.IsValid(path, Settings.Get.StagedUpdateSha256, version))
                {
                    Discard(path, "its checksum or version changed");
                    return;
                }
                if (Abstractions.WindowsApi.pInvokes.GetInteractiveUserList().Count > 0)
                    return;

                string attempt = Settings.Get.LastUpdateAttempt;
                string[] parts = (attempt ?? "").Split('|');
                DateTime when;
                if (parts.Length == 2 && parts[0] == version.ToString() &&
                    DateTime.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out when) &&
                    DateTime.Now - when < RetryAfter)
                    return;
                Settings.Get.LastUpdateAttempt = version + "|" + DateTime.Now.ToString("s", CultureInfo.InvariantCulture);

                m_logger.InfoFormat("Nobody is logged on: installing pGina {0}; the computer restarts when it finishes", version);
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = String.Format("/VERYSILENT /SUPPRESSMSGBOXES /LOG=\"{0}\"", Path.Combine(Folder, "setup-" + version + ".log")),
                    WorkingDirectory = Folder,
                    UseShellExecute = false,
                });
            }
            catch (Exception e)
            {
                m_logger.ErrorFormat("Unable to install the update: {0}", e);
            }
            finally
            {
                Monitor.Exit(m_lock);
            }
        }

        private void Discard(string path, string reason)
        {
            m_logger.InfoFormat("Discarding {0}: {1}", path, reason);
            try { File.Delete(path); } catch (Exception) { }
            Settings.Get.StagedUpdate = "";
        }

        private static void RemoveOtherFiles(string keep)
        {
            foreach (string f in Directory.GetFiles(Folder, "pGinaSetup-*.exe"))
            {
                if (!String.Equals(f, keep, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(f); } catch (Exception) { }
                }
            }
        }
    }
}
