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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

using Microsoft.Win32;

namespace pGina.Shared.Localization
{
    /// <summary>
    /// Translates user-visible text. The English text in the code is the key, and the translations
    /// come from a gettext catalog embedded in each assembly ("Languages\pt-BR.po", resource name
    /// "pGina.Language.pt-BR.po"), which can be edited with any PO editor. Text without a translation
    /// stays in English. Log messages are not translated.
    /// </summary>
    public static class Language
    {
        public const string English = "en-US";
        public const string Portuguese = "pt-BR";

        /// <summary>Languages offered in the configuration, in display order.</summary>
        public static readonly string[] Supported = { English, Portuguese };

        private static string s_current = null;
        private static readonly Dictionary<Assembly, Dictionary<string, string>> s_catalogs = new Dictionary<Assembly, Dictionary<string, string>>();
        private static readonly object s_lock = new object();

        /// <summary>
        /// Language set in the configuration ("Language" value under the pGina registry key).
        /// Empty means the Windows display language.
        /// </summary>
        public static string Configured
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(Settings.pGinaDynamicSettings.pGinaRoot, false))
                    {
                        return key == null ? "" : (key.GetValue("Language", "") as string ?? "");
                    }
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }

        /// <summary>The language in use: en-US or pt-BR.</summary>
        public static string Current
        {
            get
            {
                if (s_current == null)
                    s_current = Detect();
                return s_current;
            }
        }

        /// <summary>Reads the configured language again, after it was changed.</summary>
        public static void Reload()
        {
            s_current = null;
        }

        private static string Detect()
        {
            // the environment variable is for tests and screenshots
            string name = Environment.GetEnvironmentVariable("PGINA_LANGUAGE");
            if (String.IsNullOrEmpty(name))
                name = Configured;
            if (String.IsNullOrEmpty(name))
                name = CultureInfo.CurrentUICulture.Name;
            return Normalize(name);
        }

        /// <summary>Maps any culture name to a supported language.</summary>
        public static string Normalize(string name)
        {
            if (!String.IsNullOrEmpty(name) && name.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
                return Portuguese;
            return English;
        }

        /// <summary>Name of a language in that language, for the language selector.</summary>
        public static string NativeName(string name)
        {
            switch (name)
            {
                case Portuguese: return "Português (Brasil)";
                case English: return "English";
                default: return name;
            }
        }

        /// <summary>Translates text of the calling assembly.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string T(string text)
        {
            return Translate(Assembly.GetCallingAssembly(), text);
        }

        /// <summary>Translates a format string of the calling assembly, then formats it.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Format(string format, params object[] args)
        {
            return String.Format(Translate(Assembly.GetCallingAssembly(), format), args);
        }

        public static string Translate(Assembly assembly, string text)
        {
            if (String.IsNullOrEmpty(text) || Current == English)
                return text;

            // catalogs use \n; resources and designer texts may use \r\n
            string key = text.Replace("\r\n", "\n");
            string result;
            if (!Catalog(assembly).TryGetValue(key, out result) &&
                // common texts live in pGina.Shared
                !Catalog(typeof(Language).Assembly).TryGetValue(key, out result))
            {
                return text;
            }
            return text.Contains("\r\n") ? result.Replace("\n", "\r\n") : result;
        }

        /// <summary>
        /// Translates a form or control and all its children: texts, tooltips, list and grid columns
        /// and menus. Text typed by the user (text boxes, combo boxes) is left alone.
        /// </summary>
        public static void Apply(Control root)
        {
            if (Current == English)
                return;

            Assembly assembly = root.GetType().Assembly;
            List<ToolTip> toolTips = FindToolTips(root);
            ApplyControl(assembly, root, toolTips);
        }

        private static void ApplyControl(Assembly assembly, Control c, List<ToolTip> toolTips)
        {
            if (IsCaption(c))
                c.Text = Translate(assembly, c.Text);

            foreach (ToolTip tt in toolTips)
            {
                string tip = tt.GetToolTip(c);
                if (!String.IsNullOrEmpty(tip))
                    tt.SetToolTip(c, Translate(assembly, tip));
            }

            if (c is ListView)
            {
                foreach (ColumnHeader col in ((ListView)c).Columns)
                    col.Text = Translate(assembly, col.Text);
            }
            else if (c is DataGridView)
            {
                foreach (DataGridViewColumn col in ((DataGridView)c).Columns)
                {
                    col.HeaderText = Translate(assembly, col.HeaderText);
                    col.ToolTipText = Translate(assembly, col.ToolTipText);
                }
            }
            else if (c is ToolStrip)
            {
                foreach (ToolStripItem item in ((ToolStrip)c).Items)
                    ApplyItem(assembly, item);
            }

            if (c.ContextMenuStrip != null)
            {
                foreach (ToolStripItem item in c.ContextMenuStrip.Items)
                    ApplyItem(assembly, item);
            }

            foreach (Control child in c.Controls)
                ApplyControl(assembly, child, toolTips);
        }

        private static void ApplyItem(Assembly assembly, ToolStripItem item)
        {
            item.Text = Translate(assembly, item.Text);
            item.ToolTipText = Translate(assembly, item.ToolTipText);
            if (item is ToolStripDropDownItem)
            {
                foreach (ToolStripItem sub in ((ToolStripDropDownItem)item).DropDownItems)
                    ApplyItem(assembly, sub);
            }
        }

        private static bool IsCaption(Control c)
        {
            // read-only text boxes hold help text set in the designer
            if (c is TextBoxBase)
                return ((TextBoxBase)c).ReadOnly;
            return !(c is ComboBox || c is ListControl || c is UpDownBase || c is DataGridView || c is WebBrowser);
        }

        private static List<ToolTip> FindToolTips(Control root)
        {
            List<ToolTip> result = new List<ToolTip>();
            for (Type t = root.GetType(); t != null && t != typeof(Form) && t != typeof(UserControl); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (typeof(ToolTip).IsAssignableFrom(f.FieldType))
                    {
                        ToolTip tt = f.GetValue(root) as ToolTip;
                        if (tt != null && !result.Contains(tt))
                            result.Add(tt);
                    }
                }
            }
            return result;
        }

        private static Dictionary<string, string> Catalog(Assembly assembly)
        {
            lock (s_lock)
            {
                Dictionary<string, string> catalog;
                if (!s_catalogs.TryGetValue(assembly, out catalog))
                {
                    catalog = LoadCatalog(assembly, Current);
                    s_catalogs[assembly] = catalog;
                }
                return catalog;
            }
        }

        private static Dictionary<string, string> LoadCatalog(Assembly assembly, string language)
        {
            Dictionary<string, string> catalog = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Language." + language + ".po", StringComparison.OrdinalIgnoreCase));
                if (name == null)
                    return catalog;
                using (Stream s = assembly.GetManifestResourceStream(name))
                using (StreamReader reader = new StreamReader(s, Encoding.UTF8))
                {
                    Po.Read(reader, catalog);
                }
            }
            catch (Exception)
            {
                // a broken catalog means English text, never a failure
            }
            return catalog;
        }
    }

    /// <summary>Minimal reader for gettext PO files: msgid/msgstr pairs, multi-line strings and escapes.</summary>
    internal static class Po
    {
        public static void Read(TextReader reader, Dictionary<string, string> catalog)
        {
            StringBuilder id = null, str = null, current = null;
            bool fuzzy = false;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("#,") && line.Contains("fuzzy"))
                {
                    Add(catalog, id, str, fuzzy);
                    id = str = current = null;
                    fuzzy = true;
                }
                else if (line.StartsWith("msgid "))
                {
                    Add(catalog, id, str, fuzzy);
                    if (str != null)
                        fuzzy = false;
                    id = new StringBuilder(Unquote(line.Substring(6)));
                    str = null;
                    current = id;
                }
                else if (line.StartsWith("msgstr "))
                {
                    str = new StringBuilder(Unquote(line.Substring(7)));
                    current = str;
                }
                else if (line.StartsWith("\"") && current != null)
                {
                    current.Append(Unquote(line));
                }
                else if (line.Length == 0)
                {
                    Add(catalog, id, str, fuzzy);
                    id = str = current = null;
                    fuzzy = false;
                }
            }
            Add(catalog, id, str, fuzzy);
        }

        private static void Add(Dictionary<string, string> catalog, StringBuilder id, StringBuilder str, bool fuzzy)
        {
            if (fuzzy || id == null || str == null || id.Length == 0 || str.Length == 0)
                return;
            catalog[id.ToString().Replace("\r\n", "\n")] = str.ToString().Replace("\r\n", "\n");
        }

        private static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);

            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(n); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
