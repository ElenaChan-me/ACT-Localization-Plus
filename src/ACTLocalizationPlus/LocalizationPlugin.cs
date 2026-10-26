using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;
using Advanced_Combat_Tracker;

[assembly: AssemblyTitle("ACT Localization Plus")]
[assembly: AssemblyDescription("ACT の画面を日本語・韓国語・中国語（簡体字）で表示するプラグイン")]

namespace ACTLocalizationPlus
{
    /// <summary>
    /// ACT の画面の文字列を、選んだ言語に置き換える。
    /// ACT 自身のローカライズの仕組み（ImportControlTextXML と LocalizationStrings）を使うので、ACT の動作には手を入れない。
    /// 訳の無い文字列は英語のまま表示される。
    /// </summary>
    public class LocalizationPlugin : IActPluginV1
    {
        public const string SettingsFileName = "xivtoolhub-settings.json";
        public const string DefaultLanguage = "ja";
        private const string ResourcePrefix = "ACTLocalizationPlus.Localization.";
        private const string InternalStringsName = "InternalStrings.xml";
        // 設定ファイルが壊れていても大きなファイルを読み込まないようにする
        private const long MaxSettingsBytes = 64 * 1024;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            string language = ReadLanguage(PluginDirectory());
            try
            {
                if (language == "en")
                {
                    pluginStatusText.Text = "English (no translation)";
                }
                else
                {
                    int strings = ApplyInternalStrings(language);
                    // 画面の部品の文字列は、ACT の起動処理の途中でしか置き換えられない（起動後に読むと例外が多発する）
                    int forms = ActGlobals.oFormActMain.InitActDone ? 0 : ApplyControlText(language);
                    pluginStatusText.Text = ActGlobals.oFormActMain.InitActDone
                        ? $"{language}: {strings} strings (restart ACT to translate the screens)"
                        : $"{language}: {strings} strings, {forms} screens";
                }
            }
            catch (Exception ex)
            {
                // 訳せなくても ACT はそのまま使えるようにする
                pluginStatusText.Text = "Localization failed: " + ex.Message;
            }
            // 設定項目は無いので、プラグインのタブは出さない
            pluginScreenSpace.Parent?.Controls.Remove(pluginScreenSpace);
        }

        public void DeInitPlugin()
        {
        }

        private static string PluginDirectory()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
        }

        /// <summary>
        /// XIVToolHub がプラグインの隣に書き出す設定から表示言語を読む。無ければ日本語にする。
        /// </summary>
        public static string ReadLanguage(string directory)
        {
            try
            {
                if (directory == null)
                {
                    return DefaultLanguage;
                }
                var file = new FileInfo(Path.Combine(directory, SettingsFileName));
                if (!file.Exists || file.Length > MaxSettingsBytes)
                {
                    return DefaultLanguage;
                }
                return ParseLanguage(File.ReadAllText(file.FullName));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return DefaultLanguage;
            }
        }

        public static string ParseLanguage(string json)
        {
            // .NET Framework 4.8 には標準の JSON ライブラリが無いので、必要な1項目だけを読む
            Match m = Regex.Match(json ?? "", "\"language\"\\s*:\\s*\"([A-Za-z-]+)\"");
            string code = m.Success ? m.Groups[1].Value.ToLowerInvariant() : DefaultLanguage;
            switch (code)
            {
                case "ja":
                case "ko":
                case "en":
                    return code;
                case "zh":
                case "zh-cn":
                case "zh-hans":
                    return "zh";
                default:
                    return DefaultLanguage;
            }
        }

        private static IEnumerable<string> ResourceNames(string language) =>
            Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix + language + ".", StringComparison.Ordinal));

        private static int ApplyInternalStrings(string language)
        {
            string name = ResourceNames(language).FirstOrDefault(n => n.EndsWith(InternalStringsName, StringComparison.Ordinal));
            if (name == null)
            {
                return 0;
            }
            var strings = ActGlobals.ActLocalization.LocalizationStrings;
            int count = 0;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                foreach (var e in XDocument.Load(s).Root.Elements("string"))
                {
                    string key = (string)e.Attribute("key");
                    string value = (string)e.Attribute("value");
                    // ACT の版によって無い項目もあるので、ある項目だけを変える
                    if (key != null && value != null && strings.ContainsKey(key))
                    {
                        strings[key].DisplayedText = value;
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 画面ごとの XML（Form 属性に画面の型名）を、ActGlobals にある同じ型の画面に読み込ませる。
        /// </summary>
        private static int ApplyControlText(string language)
        {
            var forms = typeof(ActGlobals).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => typeof(Form).IsAssignableFrom(f.FieldType))
                .ToList();
            int count = 0;
            foreach (string name in ResourceNames(language).Where(n => !n.EndsWith(InternalStringsName, StringComparison.Ordinal)))
            {
                byte[] data;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    data = ms.ToArray();
                }
                string formName;
                using (var ms = new MemoryStream(data))
                {
                    formName = (string)XDocument.Load(ms).Root.Attribute("Form");
                }
                foreach (var field in forms.Where(f => f.FieldType.Name == formName))
                {
                    object form = field.GetValue(null);
                    MethodInfo import = form?.GetType().GetMethod("ImportControlTextXML", new[] { typeof(Stream) });
                    if (import == null)
                    {
                        continue;
                    }
                    try
                    {
                        using (var ms = new MemoryStream(data))
                        {
                            import.Invoke(form, new object[] { ms });
                        }
                        count++;
                    }
                    catch (TargetInvocationException)
                    {
                        // 1つの画面で失敗しても、ほかの画面は訳す
                    }
                }
            }
            return count;
        }
    }
}
