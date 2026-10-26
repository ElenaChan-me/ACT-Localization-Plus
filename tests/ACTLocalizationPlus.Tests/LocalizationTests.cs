using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ACTLocalizationPlus;
using Xunit;

namespace ACTLocalizationPlus.Tests
{
    public class LocalizationTests
    {
        private static readonly Assembly Plugin = typeof(LocalizationPlugin).Assembly;

        [Theory]
        [InlineData("{\"language\": \"ja\", \"theme\": \"dark\"}", "ja")]
        [InlineData("{\"language\":\"KO\"}", "ko")]
        [InlineData("{\"language\": \"zh-Hans\"}", "zh")]
        [InlineData("{\"language\": \"en\"}", "en")]
        [InlineData("{\"language\": \"fr\"}", "ja")]
        [InlineData("not json", "ja")]
        [InlineData(null, "ja")]
        public void ParsesLanguage(string json, string expected)
        {
            Assert.Equal(expected, LocalizationPlugin.ParseLanguage(json));
        }

        [Fact]
        public void ReadsLanguageNextToPlugin()
        {
            string dir = Path.Combine(Path.GetTempPath(), "actloc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.Equal("ja", LocalizationPlugin.ReadLanguage(dir));
                File.WriteAllText(Path.Combine(dir, LocalizationPlugin.SettingsFileName), "{\"language\": \"zh\"}");
                Assert.Equal("zh", LocalizationPlugin.ReadLanguage(dir));
                Assert.Equal("ja", LocalizationPlugin.ReadLanguage(null));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Theory]
        [InlineData("ja")]
        [InlineData("ko")]
        [InlineData("zh")]
        public void EmbedsValidTranslationsForEachLanguage(string language)
        {
            var names = Plugin.GetManifestResourceNames().Where(n => n.StartsWith("ACTLocalizationPlus.Localization." + language + ".")).ToList();
            Assert.Contains(names, n => n.EndsWith(".InternalStrings.xml"));
            Assert.Contains(names, n => n.EndsWith(".FormActMain.xml"));
            foreach (string name in names)
            {
                using (Stream s = Plugin.GetManifestResourceStream(name))
                {
                    XElement root = XDocument.Load(s).Root;
                    Assert.True(root.Name == "InternalStrings" || root.Attribute("Form") != null, name);
                }
            }
        }

        [Fact]
        public void KeepsPlaceholdersInInternalStrings()
        {
            // {0} などの数が原文と違うと、ACT が string.Format で例外を出す
            string upstream = Path.Combine(RepoRoot(), "tools", "upstream", "Advanced Combat Tracker.exe.InternalStrings.xml");
            if (!File.Exists(upstream))
            {
                return;
            }
            var english = XDocument.Load(upstream).Root.Elements("string").ToDictionary(e => (string)e.Attribute("key"), e => (string)e.Attribute("value"));
            foreach (string language in new[] { "ja", "ko", "zh" })
            {
                using (Stream s = Plugin.GetManifestResourceStream("ACTLocalizationPlus.Localization." + language + ".InternalStrings.xml"))
                {
                    foreach (XElement e in XDocument.Load(s).Root.Elements("string"))
                    {
                        string key = (string)e.Attribute("key");
                        Assert.True(english.ContainsKey(key), key);
                        Assert.Equal(Placeholders(english[key]), Placeholders((string)e.Attribute("value")));
                    }
                }
            }
        }

        [Fact]
        public void DoesNotTranslateParsingTerms()
        {
            using (Stream s = Plugin.GetManifestResourceStream("ACTLocalizationPlus.Localization.ja.InternalStrings.xml"))
            {
                var keys = XDocument.Load(s).Root.Elements("string").Select(e => (string)e.Attribute("key")).ToList();
                Assert.DoesNotContain(keys, k => k.Split('-')[0].EndsWith("Term") || k.StartsWith("data-") || k.StartsWith("encounterData-"));
            }
        }

        private static string Placeholders(string text) =>
            string.Join(",", Regex.Matches(text ?? "", @"\{\d+[^}]*\}").Cast<Match>().Select(m => m.Value).OrderBy(x => x));

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".gitignore")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? "";
        }
    }
}
