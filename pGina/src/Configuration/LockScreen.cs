using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace pGina.Configuration
{
    /// <summary>
    /// Lock and sign-in screen background, set through the PersonalizationCSP policy keys
    /// (Windows 10/11 Pro, Enterprise and Education).
    /// </summary>
    static class LockScreen
    {
        const string CspKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";
        const string FilePrefix = "lockscreen-";

        /// <summary>Folder the chosen image is copied to: under the pGina install folder, so only admins can change it.</summary>
        public static string ImageFolder
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "LockScreen"); }
        }

        static RegistryKey OpenHklm()
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        }

        /// <summary>The image currently set, or an empty string when Windows uses its default.</summary>
        public static string Current
        {
            get
            {
                using (RegistryKey hklm = OpenHklm())
                using (RegistryKey key = hklm.OpenSubKey(CspKey, false))
                {
                    return key == null ? "" : (key.GetValue("LockScreenImagePath", "") as string ?? "");
                }
            }
        }

        /// <summary>
        /// Copies the image to the pGina folder and points the policy at the copy. A new file name is
        /// used each time, because Windows caches the image by path.
        /// </summary>
        public static void Set(string source)
        {
            Directory.CreateDirectory(ImageFolder);
            string target = Path.Combine(ImageFolder,
                FilePrefix + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(source).ToLowerInvariant());
            File.Copy(source, target, true);

            using (RegistryKey hklm = OpenHklm())
            using (RegistryKey key = hklm.CreateSubKey(CspKey))
            {
                key.SetValue("LockScreenImagePath", target, RegistryValueKind.String);
                key.SetValue("LockScreenImageUrl", target, RegistryValueKind.String);
                key.SetValue("LockScreenImageStatus", 1, RegistryValueKind.DWord);
            }
            DeleteCopies(target);
        }

        /// <summary>Removes the policy values, so Windows goes back to its own background.</summary>
        public static void Clear()
        {
            using (RegistryKey hklm = OpenHklm())
            using (RegistryKey key = hklm.OpenSubKey(CspKey, true))
            {
                if (key != null)
                {
                    key.DeleteValue("LockScreenImagePath", false);
                    key.DeleteValue("LockScreenImageUrl", false);
                    key.DeleteValue("LockScreenImageStatus", false);
                }
            }
            DeleteCopies(null);
        }

        /// <summary>True when the path is a copy made by Set, so it doesn't need copying again.</summary>
        public static bool IsOwnCopy(string path)
        {
            return !String.IsNullOrEmpty(path) &&
                String.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), ImageFolder, StringComparison.OrdinalIgnoreCase);
        }

        static void DeleteCopies(string keep)
        {
            if (!Directory.Exists(ImageFolder))
                return;
            foreach (string f in Directory.GetFiles(ImageFolder, FilePrefix + "*"))
            {
                if (keep != null && String.Equals(f, keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                try { File.Delete(f); }
                catch (IOException) { } // still in use by LogonUI; removed next time
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
