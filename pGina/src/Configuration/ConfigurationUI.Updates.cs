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
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using pGina.Core;
using pGina.Shared.Localization;
using pGina.Shared.Update;

namespace pGina.Configuration
{
    // General tab, Updates group
    public partial class ConfigurationUI
    {
        private ReleaseInfo m_latestRelease = null;

        private void LoadUpdateSettings()
        {
            m_updateCheckCB.Checked = Settings.Get.UpdateCheck;
            m_autoUpdateCB.Checked = Settings.Get.AutoUpdate;

            // what the service found last, until our own check answers
            Version latest;
            string checkedAt = Settings.Get.LastUpdateCheck;
            if (Version.TryParse((string)Settings.Get.LatestVersion, out latest) && latest > Updater.CurrentVersion)
                m_updateStatusLbl.Text = Language.Format("pGina {0} is available (installed: {1}).", latest, Updater.CurrentVersion);
            else
                m_updateStatusLbl.Text = Language.Format("Installed version: {0}.", Updater.CurrentVersion) +
                    (String.IsNullOrEmpty(checkedAt) ? "" : " " + Language.Format("Last check: {0}.", checkedAt.Replace('T', ' ')));

            if (m_updateCheckCB.Checked)
                CheckForUpdates(true);
        }

        private void m_updateCheckBtn_Click(object sender, EventArgs e)
        {
            CheckForUpdates(false);
        }

        private async void CheckForUpdates(bool quiet)
        {
            m_updateCheckBtn.Enabled = false;
            if (!quiet)
                m_updateStatusLbl.Text = Language.T("Checking for updates...");
            string source = Settings.Get.UpdateSource;
            try
            {
                ReleaseInfo release = await Task.Run(() => Updater.GetLatest(source));
                m_latestRelease = release;
                m_updateNotesLink.Visible = true;
                if (Updater.IsNewer(release))
                {
                    m_updateStatusLbl.Text = Language.Format("pGina {0} is available (installed: {1}).", release.Version, Updater.CurrentVersion);
                    m_updateInstallBtn.Enabled = true;
                }
                else
                {
                    m_updateStatusLbl.Text = Language.Format("pGina is up to date ({0}).", Updater.CurrentVersion);
                    m_updateInstallBtn.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                if (!quiet)
                    m_updateStatusLbl.Text = Language.Format("Unable to check for updates: {0}", ex.Message);
            }
            finally
            {
                if (!IsDisposed)
                    m_updateCheckBtn.Enabled = true;
            }
        }

        private void m_updateNotesLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (m_latestRelease != null)
                Process.Start(m_latestRelease.PageUrl);
        }

        private async void m_updateInstallBtn_Click(object sender, EventArgs e)
        {
            ReleaseInfo release = m_latestRelease;
            if (release == null)
                return;
            if (MessageBox.Show(Language.Format("Download and install pGina {0}?\n\nThe computer restarts at the end of the installation. Save your work in other programs first.", release.Version),
                    Language.T("Updates"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            m_updateInstallBtn.Enabled = m_updateCheckBtn.Enabled = false;
            m_updateStatusLbl.Text = Language.Format("Downloading pGina {0}...", release.Version);
            try
            {
                // the same protected folder the service uses: only SYSTEM and administrators write there
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "pGina.fork", "Update");
                string path = await Task.Run(() =>
                {
                    Updater.CreateProtectedFolder(folder);
                    return Updater.Download(release, folder);
                });
                m_updateStatusLbl.Text = Language.Format("Installing pGina {0}...", release.Version);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                m_updateStatusLbl.Text = Language.Format("Unable to download the update: {0}", ex.Message);
                MessageBox.Show(m_updateStatusLbl.Text, Language.T("Updates"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                m_updateInstallBtn.Enabled = true;
            }
            finally
            {
                if (!IsDisposed)
                    m_updateCheckBtn.Enabled = true;
            }
        }
    }
}
