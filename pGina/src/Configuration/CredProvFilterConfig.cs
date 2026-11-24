/*
	Copyright (c) 2017, pGina Team
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

using Microsoft.Win32;

using pGina.Shared.Localization;

namespace pGina.Configuration
{
    class CredProvFilterConfig
    {
        private static readonly string CRED_PROV_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers";

        // pGina's own credential provider, never listed: hiding it would leave no way to log on
        public static readonly Guid PginaProvider = new Guid("d0befefb-3d2c-44da-bbad-3b2d04557246");
        public static readonly Guid PasswordProvider = new Guid("60b78e88-ead8-445c-9cfd-0b87f74ea6cd");

        // Names people recognize for the Windows providers, in the order they are listed
        private static readonly KeyValuePair<Guid, string>[] KnownNames =
        {
            Known("60b78e88-ead8-445c-9cfd-0b87f74ea6cd", Language.T("Windows password and \"Other user\"")),
            Known("cb82ea12-9f71-446d-89e1-8d0924e1256e", Language.T("Windows Hello PIN")),
            Known("bec09223-b018-416d-a0ac-523971b639f5", Language.T("Windows Hello fingerprint or face")),
            Known("2135f72a-90b5-4ed3-a7f1-8bb705ac276a", Language.T("Picture password")),
            Known("f8a1793b-7873-4046-b2a7-1f318747f427", Language.T("Security key (FIDO)")),
            Known("8fd7e19c-3bf7-489b-a72c-846ab3678c96", Language.T("Smart card")),
            Known("94596c7e-3744-41ce-893e-bbf09122f76a", Language.T("Smart card PIN")),
            Known("1b283861-754f-4022-ad47-a5eaaa618894", Language.T("Smart card reader selection")),
            Known("1ee7337f-85ac-45e2-a23c-37c753209769", Language.T("Smart card for apps")),
            Known("3dd6bec0-8193-4ffe-ae25-e08e39ea4063", Language.T("Network providers")),
            Known("48b4e58d-2791-456c-9091-d524c6c706f2", Language.T("Companion device")),
            Known("27fbdb57-b613-4af2-9d7e-4fa7a66c21ad", Language.T("Trusted signal (dynamic lock)")),
            Known("25cbb996-92ed-457e-b28c-4774084bd562", Language.T("Windows generic provider")),
            Known("600e7adb-da3e-41a4-9225-3c0399e88c0c", Language.T("CNG credentials (UAC prompts)")),
            Known("d6886603-9d2f-4eb2-b667-1971041fa96b", Language.T("Windows Hello for Business")),
            Known("f8a0b131-5f68-486c-8040-7e8fc3c85bb6", Language.T("Microsoft account")),
            Known("e74e57b0-6c6d-44d5-9cda-fb2df5ed7435", Language.T("Certificate")),
        };

        private static KeyValuePair<Guid, string> Known(string uuid, string name)
        {
            return new KeyValuePair<Guid, string>(new Guid(uuid), name);
        }

        public static string FriendlyName(Guid uuid, string registryName)
        {
            foreach (KeyValuePair<Guid, string> k in KnownNames)
                if (k.Key == uuid)
                    return k.Value;
            return registryName;
        }

        private static int SortOrder(Guid uuid)
        {
            for (int i = 0; i < KnownNames.Length; i++)
                if (KnownNames[i].Key == uuid)
                    return i;
            return KnownNames.Length;
        }

        public static void WindowsDefault(List<CredProv> list)
        {
            foreach (CredProv cp in list)
            {
                cp.FilterLogon = false;
                cp.FilterUnlock = false;
                cp.FilterChangePass = false;
                cp.FilterCredUI = false;
            }
        }

        public static List<CredProv> LoadCredProvsAndFilterSettings()
        {
            Dictionary<Guid, CredProv> result = new Dictionary<Guid, CredProv>();

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(CRED_PROV_KEY))
            {
                if (key != null)
                {
                    string[] subKeys = key.GetSubKeyNames();
                    foreach (string sub in subKeys)
                    {
                        using (RegistryKey cpKey = key.OpenSubKey(sub))
                        {
                            if (cpKey != null)
                            {
                                CredProv credProv = null;
                                try
                                {
                                    credProv = new CredProv
                                    {
                                        Uuid = new Guid(sub)
                                    };
                                }
                                catch (System.FormatException)
                                {
                                    // Ignore entries that are not GUIDs
                                    continue;
                                }

                                object name = cpKey.GetValue("");

                                if (name != null)
                                {
                                    credProv.Name = name.ToString();
                                }
                                else
                                {
                                    credProv.Name = credProv.Uuid.ToString();
                                }

                                if (!result.ContainsKey(credProv.Uuid))
                                {
                                    result.Add(credProv.Uuid, credProv);
                                }
                            }
                        }
                    }
                }
            }

            // Load filter settings from registry
            string[] filterSettings = pGina.Core.Settings.Get.CredentialProviderFilters;
            List<CredProv> filterSettingsList = new List<CredProv>();
            foreach (string s in filterSettings)
                filterSettingsList.Add(CredProv.FromRegString(s));

            // Merge registry settings into the dictionary
            foreach (CredProv cp in filterSettingsList)
            {
                if (result.ContainsKey(cp.Uuid))
                {
                    cp.Name = result[cp.Uuid].Name;
                    result[cp.Uuid] = cp;
                }
            }

            result.Remove(PginaProvider);
            foreach (CredProv cp in result.Values)
                cp.DisplayName = FriendlyName(cp.Uuid, cp.Name);

            return result.Values
                .OrderBy(cp => SortOrder(cp.Uuid))
                .ThenBy(cp => cp.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static void SaveFilterSettings(List<CredProv> list)
        {
            List<string> filters = new List<string>();
            foreach (CredProv cp in list)
            {
                if (cp.FilterEnabled())
                {
                    filters.Add(cp.ToRegString());
                }
            }

            string[] result = filters.ToArray();
            pGina.Core.Settings.Get.CredentialProviderFilters = result;
        }
    }


    class CredProv
    {
        public string Name { get; set; }
        public Guid Uuid { get; set; }

        public bool FilterLogon { get; set; }
        public bool FilterUnlock { get; set; }
        public bool FilterChangePass { get; set; }
        public bool FilterCredUI { get; set; }

        public string DisplayName { get; set; }

        // What the check list shows: whether the provider appears at logon and unlock
        public bool Shown
        {
            get { return !FilterLogon && !FilterUnlock; }
            set { FilterLogon = !value; FilterUnlock = !value; }
        }

        public override string ToString()
        {
            return DisplayName ?? Name;
        }

        public bool FilterEnabled()
        {
            return FilterLogon || FilterUnlock || FilterChangePass || FilterCredUI;
        }

        public string ToRegString()
        {
            int filter = 0;

            if (FilterLogon) filter |= 0x1;
            if (FilterUnlock) filter |= 0x2;
            if (FilterChangePass) filter |= 0x4;
            if (FilterCredUI) filter |= 0x8;

            return string.Format("{{{0}}}\t{1}", Uuid.ToString(), filter);
        }

        public static CredProv FromRegString(string str)
        {
            string[] parts = str.Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
            CredProv result = new CredProv
            {
                Uuid = new Guid(parts[0])
            };
            int filter = Convert.ToInt32(parts[1]);
            if ((filter & 0x1) != 0) result.FilterLogon = true;
            if ((filter & 0x2) != 0) result.FilterUnlock = true;
            if ((filter & 0x4) != 0) result.FilterChangePass = true;
            if ((filter & 0x8) != 0) result.FilterCredUI = true;
            return result;
        }
    }
}
