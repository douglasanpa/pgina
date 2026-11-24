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
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace pGina.Shared.Update
{
    /// <summary>A published release: version and where its installer and checksum are.</summary>
    public class ReleaseInfo
    {
        public Version Version { get; set; }
        public string Tag { get; set; }
        public string PageUrl { get; set; }
        public string InstallerName { get; set; }
        public string InstallerUrl { get; set; }
        public string ChecksumUrl { get; set; }
    }

    /// <summary>
    /// Finds, downloads and verifies pGina releases published on GitHub (or a mirror with the same
    /// layout). The release page /releases/latest redirects to /releases/tag/v&lt;version&gt;, and the
    /// release carries pGinaSetup-&lt;version&gt;.exe and pGinaSetup-&lt;version&gt;.exe.sha256.
    /// The redirect is used instead of the GitHub API, which allows only 60 requests an hour per
    /// address: a whole lab behind one address would run out.
    /// </summary>
    public static class Updater
    {
        public const string DefaultSource = "https://github.com/douglasanpa/pgina";
        private const string UserAgent = "pGina.fork-updater";
        private const int TimeoutMs = 30000;

        static Updater()
        {
            // .NET 4.8 normally lets Windows choose (TLS 1.3 included); only older settings need TLS 1.2 added
            if (ServicePointManager.SecurityProtocol != SecurityProtocolType.SystemDefault)
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static Version CurrentVersion
        {
            get { return typeof(Updater).Assembly.GetName().Version; }
        }

        /// <summary>The newest release of the source, or an exception when it can't be reached.</summary>
        public static ReleaseInfo GetLatest(string source)
        {
            source = String.IsNullOrWhiteSpace(source) ? DefaultSource : source.Trim().TrimEnd('/');
            HttpWebRequest req = Request(source + "/releases/latest");
            // GET, not HEAD: after a HEAD, .NET reuses the connection and the server closes it
            req.AllowAutoRedirect = false;
            req.KeepAlive = false;
            string location;
            using (HttpWebResponse resp = Response(req))
            {
                location = resp.Headers[HttpResponseHeader.Location];
                if (String.IsNullOrEmpty(location))
                    throw new InvalidDataException(String.Format("{0}/releases/latest did not redirect to a release ({1})", source, (int)resp.StatusCode));
            }

            Match m = Regex.Match(location, @"/releases/tag/(?<tag>v?(?<ver>\d+(\.\d+){1,3}))/?$");
            if (!m.Success)
                throw new InvalidDataException("Unexpected release address: " + location);

            string tag = m.Groups["tag"].Value;
            Version version = Version.Parse(m.Groups["ver"].Value);
            string name = String.Format("pGinaSetup-{0}.exe", m.Groups["ver"].Value);
            string download = String.Format("{0}/releases/download/{1}/", source, tag);
            return new ReleaseInfo
            {
                Version = version,
                Tag = tag,
                PageUrl = String.Format("{0}/releases/tag/{1}", source, tag),
                InstallerName = name,
                InstallerUrl = download + name,
                ChecksumUrl = download + name + ".sha256",
            };
        }

        public static bool IsNewer(ReleaseInfo release)
        {
            return release != null && Normalize(release.Version) > Normalize(CurrentVersion);
        }

        /// <summary>
        /// Downloads the installer into folder and returns its path. It is kept only if its SHA-256
        /// matches the .sha256 file of the release and its file version is the release version;
        /// otherwise an exception is thrown and nothing is left behind.
        /// An installer already there and still valid is not downloaded again.
        /// </summary>
        public static string Download(ReleaseInfo release, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, release.InstallerName);
            string expected = DownloadChecksum(release);
            if (File.Exists(path) && IsValid(path, expected, release.Version))
                return path;

            string tmp = path + ".part";
            try
            {
                using (HttpWebResponse resp = Response(Request(release.InstallerUrl)))
                using (Stream input = resp.GetResponseStream())
                using (FileStream output = File.Create(tmp))
                    input.CopyTo(output);

                string actual = Sha256(tmp);
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(String.Format("Checksum mismatch for {0}: expected {1}, got {2}", release.InstallerName, expected, actual));
                Version fileVersion = FileVersion(tmp);
                if (fileVersion == null || Normalize(fileVersion) != Normalize(release.Version))
                    throw new InvalidDataException(String.Format("{0} has version {1}, expected {2}", release.InstallerName, fileVersion, release.Version));

                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                return path;
            }
            finally
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
        }

        /// <summary>True when the file still has the expected checksum and version.</summary>
        public static bool IsValid(string path, string sha256, Version version)
        {
            try
            {
                return Sha256(path).Equals(sha256, StringComparison.OrdinalIgnoreCase) &&
                    Normalize(FileVersion(path)) == Normalize(version);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The checksum published with the release ("hex  name", as sha256sum writes it).</summary>
        public static string DownloadChecksum(ReleaseInfo release)
        {
            string text;
            using (HttpWebResponse resp = Response(Request(release.ChecksumUrl)))
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                text = reader.ReadToEnd();
            Match m = Regex.Match(text, @"\b[0-9a-fA-F]{64}\b");
            if (!m.Success)
                throw new InvalidDataException("No SHA-256 in " + release.ChecksumUrl);
            return m.Value.ToLowerInvariant();
        }

        public static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream f = File.OpenRead(path))
                return String.Concat(sha.ComputeHash(f).Select(b => b.ToString("x2")));
        }

        /// <summary>
        /// Creates folder so that only SYSTEM and the administrators can write to it: the service runs
        /// what is in there as SYSTEM, so no user may be able to put a file in its place.
        /// </summary>
        public static void CreateProtectedFolder(string folder)
        {
            DirectorySecurity acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            foreach (WellKnownSidType sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
            acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
            if (Directory.Exists(folder))
                Directory.SetAccessControl(folder, acl);
            else
                Directory.CreateDirectory(folder, acl);
        }

        private static Version FileVersion(string path)
        {
            Version v;
            string text = FileVersionInfo.GetVersionInfo(path).FileVersion;
            return Version.TryParse((text ?? "").Trim(), out v) ? v : null;
        }

        // 3.10.1 and 3.10.1.0 are the same version
        private static Version Normalize(Version v)
        {
            if (v == null)
                return new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
        }

        private static HttpWebRequest Request(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent;
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            return req;
        }

        private static HttpWebResponse Response(HttpWebRequest req)
        {
            try
            {
                return (HttpWebResponse)req.GetResponse();
            }
            catch (WebException e) when (e.Response is HttpWebResponse r && (int)r.StatusCode >= 300 && (int)r.StatusCode < 400)
            {
                // .NET reports an unfollowed redirect as an error
                return r;
            }
        }
    }
}
