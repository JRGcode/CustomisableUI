using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MCM.Abstractions;

namespace CustomisableUI.Tests
{
    // Everything the mod shows the player is tagged "{=CUI_Something}English text", so the game can swap in a translation.
    // This finds every such string in the compiled mod, checks each one is tagged and sensible, and keeps the English
    // language file (Module\ModuleData\Languages\EN\strings.xml, the template translators copy) in step with the code.
    //
    // Regenerate that file after changing any text:
    //     dotnet run --project tests/CustomisableUI.Tests -c Release -- --write-strings
    internal static class Translations
    {
        private static readonly Regex Tag = new Regex(@"^\{=([^}]*)\}(.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex ValidId = new Regex(@"^CUI_[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);

        // Hotkey names in the dropdown are key names, not words, so they are shown as they are.
        private static readonly string[] KeyNames = { "Ctrl+Shift+U", "F8", "F9", "F10" };

        // XML comments may not contain a double hyphen, so this can't quote the command line; docs/TRANSLATING.md does.
        private const string HeaderComment =
            "Generated from the mod's code by the tests project (see docs/TRANSLATING.md); do not edit it by hand.\n" +
            "To translate, copy this file as described there and change only the text=\"...\" values.";

        internal sealed class Entry
        {
            public Entry(string where, string raw)
            {
                Where = where;
                Raw = raw;
            }

            public string Where { get; }
            public string Raw { get; }
        }

        public static string RepoRoot()
        {
            string folder = AppContext.BaseDirectory;
            while (folder != null && !File.Exists(Path.Combine(folder, "CustomisableUI.sln")))
            {
                folder = Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar));
            }
            return folder ?? throw new InvalidOperationException("Could not find the repository root (CustomisableUI.sln).");
        }

        public static string EnglishFile()
        {
            return Path.Combine(RepoRoot(), "Module", "ModuleData", "Languages", "EN", "strings.xml");
        }

        // The text without its "{=id}" tag: what an English player sees.
        public static string Plain(string text)
        {
            Match m = Tag.Match(text ?? string.Empty);
            return m.Success ? m.Groups[2].Value : text;
        }

        // Every player-visible string the mod hands to the game, and where it comes from.
        public static List<Entry> Collect(ModSettings settings)
        {
            var all = new List<Entry>();

            // The settings screen, read the way MCM reads it.
            Type discoverer = typeof(ModSettings).BaseType.Assembly.GetType("MCM.Implementation.AttributeSettingsPropertyDiscoverer", true);
            object definitions = discoverer.GetMethod("GetProperties").Invoke(Activator.CreateInstance(discoverer), new object[] { settings });
            foreach (object definition in (IEnumerable)definitions)
            {
                string id = Read(definition, "Id");
                all.Add(new Entry(id + " (name)", Read(definition, "DisplayName")));
                string hint = Read(definition, "HintText");
                if (!string.IsNullOrEmpty(hint))
                {
                    all.Add(new Entry(id + " (hint)", hint));
                }
                all.Add(new Entry(id + " (group)", Read(definition, "GroupName")));
            }

            // Every dropdown list declared on the settings class (FontChoices, StatusChoices... and any a feature adds, such as
            // the custom backgrounds'), found by name so that a new list can't be left out of the template.
            foreach (FieldInfo list in typeof(ModSettings)
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(string[]) && f.Name.EndsWith("Choices", StringComparison.Ordinal)))
            {
                AddAll(all, list.Name, ((string[])list.GetValue(null)).Where(choice => !KeyNames.Contains(choice)));
            }
            AddAll(all, "colour name", Swatches.TextNames());
            all.Add(new Entry("hotkey message (vanilla look)", Hotkey.VanillaLookMessage));
            all.Add(new Entry("hotkey message (theme back)", Hotkey.ThemeBackMessage));

            // MCM adds its own "Default" preset, which is not ours to translate.
            foreach (ISettingsPreset preset in settings.GetBuiltInPresets().Where(p => !p.Id.Equals("default", StringComparison.OrdinalIgnoreCase)))
            {
                all.Add(new Entry("preset " + preset.Id, preset.Name));
            }

            return all;
        }

        // Splits each string into its id and English text, noting anything that would not work as a translatable string.
        // The result lists each id once, in the order a player first meets it.
        public static List<KeyValuePair<string, string>> Resolve(IEnumerable<Entry> entries, List<string> problems)
        {
            var byId = new Dictionary<string, string>();
            var order = new List<string>();

            foreach (Entry entry in entries)
            {
                Match m = Tag.Match(entry.Raw ?? string.Empty);
                if (!m.Success)
                {
                    problems.Add(entry.Where + " has no {=CUI_...} tag: \"" + entry.Raw + "\"");
                    continue;
                }

                string id = m.Groups[1].Value;
                string text = m.Groups[2].Value;
                if (!ValidId.IsMatch(id))
                {
                    problems.Add(entry.Where + " has the id '" + id + "'; ids are CUI_ followed by letters, digits or underscores");
                }
                if (text.Trim().Length == 0)
                {
                    problems.Add(entry.Where + " has no English text");
                }
                if (text.IndexOfAny(new[] { '{', '}' }) >= 0)
                {
                    problems.Add(entry.Where + " contains { or }, which the game would read as a variable: \"" + text + "\"");
                }

                if (byId.TryGetValue(id, out string existing))
                {
                    if (existing != text)
                    {
                        problems.Add(entry.Where + " reuses the id '" + id + "' for different text");
                    }
                }
                else
                {
                    byId[id] = text;
                    order.Add(id);
                }
            }

            return order.Select(id => new KeyValuePair<string, string>(id, byId[id])).ToList();
        }

        // The contents of EN\strings.xml for the given strings.
        public static string RenderEnglish(IEnumerable<KeyValuePair<string, string>> strings)
        {
            var xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            xml.Append("<!--\n  ").Append(HeaderComment.Replace("\n", "\n  ")).Append("\n-->\n");
            xml.Append("<base xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n");
            xml.Append("      xsi:noNamespaceSchemaLocation=\"https://raw.githubusercontent.com/BUTR/Bannerlord.XmlSchemas/master/ModuleLanguage.xsd\">\n");
            xml.Append("  <tags>\n    <tag language=\"English\" />\n  </tags>\n");
            xml.Append("  <strings>\n");
            foreach (KeyValuePair<string, string> pair in strings)
            {
                xml.Append("    <string id=\"").Append(Escape(pair.Key)).Append("\" text=\"").Append(Escape(pair.Value)).Append("\" />\n");
            }
            xml.Append("  </strings>\n");
            xml.Append("</base>\n");
            return xml.ToString();
        }

        // Rewrites the English file from the code. Run after changing any player-visible text.
        public static int WriteEnglish()
        {
            var problems = new List<string>();
            List<KeyValuePair<string, string>> strings = Resolve(Collect(new ModSettings()), problems);
            if (problems.Count > 0)
            {
                foreach (string problem in problems)
                {
                    Console.WriteLine("  " + problem);
                }
                Console.WriteLine("Not written: fix the " + problems.Count + " problem(s) above first.");
                return problems.Count;
            }

            string path = EnglishFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, RenderEnglish(strings), new UTF8Encoding(false));
            Console.WriteLine("Wrote " + strings.Count + " strings to " + path);
            return 0;
        }

        public static void Checks()
        {
            Check.Section("Translations");

            var settings = new ModSettings();
            List<Entry> entries = Collect(settings);
            var problems = new List<string>();
            List<KeyValuePair<string, string>> strings = Resolve(entries, problems);

            foreach (string problem in problems)
            {
                Check.That(false, problem);
            }
            Check.That(problems.Count == 0, "every string shown to the player is tagged for translation (" + entries.Count + " checked)");
            Check.That(strings.Count >= 150, "the English template has its full set of strings (" + strings.Count + ")");
            Check.That(entries.Count(e => e.Where.StartsWith("preset ", StringComparison.Ordinal)) >= 22, "all the presets are among them");
            foreach (string list in new[] { "FontChoices", "ContrastChoices", "StatusChoices", "HotkeyChoices", "BackgroundFitChoices", "LoadingBackgroundChoices" })
            {
                Check.That(entries.Any(e => e.Where.StartsWith(list, StringComparison.Ordinal)), "the " + list + " dropdown is among them");
            }

            // The game's own text system must turn every tagged string into exactly its English text when no translation is
            // loaded: that is what English players (and anyone in an untranslated language) will see.
            var mangled = new List<string>();
            foreach (Entry entry in entries)
            {
                string shown;
                try
                {
                    shown = new TaleWorlds.Localization.TextObject(entry.Raw).ToString();
                }
                catch (Exception ex)
                {
                    mangled.Add(entry.Where + " throws in TextObject: " + ex.GetType().Name);
                    continue;
                }
                if (shown != Plain(entry.Raw))
                {
                    mangled.Add(entry.Where + " shows as \"" + shown + "\" instead of \"" + Plain(entry.Raw) + "\"");
                }
            }
            foreach (string problem in mangled)
            {
                Check.That(false, problem);
            }
            Check.That(mangled.Count == 0, "the game's TextObject shows every tagged string as its plain English text (" + entries.Count + " checked)");

            // Plain() is what English players see: the tag must come off cleanly.
            Check.Equal("Enable Customisable UI", Plain("{=CUI_Enabled}Enable Customisable UI"), "a tag is removed from the text");
            Check.Equal("no tag", Plain("no tag"), "text without a tag is left alone");

            string file = EnglishFile();
            if (!File.Exists(file))
            {
                Check.That(false, "Module\\ModuleData\\Languages\\EN\\strings.xml exists");
                return;
            }

            string expected = RenderEnglish(strings);

            // The game reads this file as XML, so a stray double hyphen in a comment or a bad escape would lose every string.
            try
            {
                var generated = System.Xml.Linq.XDocument.Parse(expected);
                Check.Equal(strings.Count, generated.Descendants("string").Count(), "the generated English file is well-formed XML with every string");
                Check.That(generated.Descendants("string").All(s => (string)s.Attribute("text") == strings.First(p => p.Key == (string)s.Attribute("id")).Value),
                    "the generated English file reads back exactly the text in the code");
            }
            catch (System.Xml.XmlException ex)
            {
                Check.That(false, "the generated English file is well-formed XML: " + ex.Message);
            }

            string actual = File.ReadAllText(file).Replace("\r\n", "\n");
            Check.That(actual == expected, "EN\\strings.xml matches the code (if not, regenerate it: dotnet run --project tests/CustomisableUI.Tests -c Release -- --write-strings)");

            // The language declaration the game reads.
            string declaration = Path.Combine(Path.GetDirectoryName(file), "language_data.xml");
            Check.That(File.Exists(declaration), "EN\\language_data.xml exists");
            if (File.Exists(declaration))
            {
                var doc = System.Xml.Linq.XDocument.Load(declaration);
                Check.Equal("English", (string)doc.Root.Attribute("id"), "the English language is declared as 'English'");
                string stringsFile = (string)doc.Root.Element("LanguageFile")?.Attribute("xml_path");
                Check.Equal("EN/strings.xml", stringsFile, "EN\\language_data.xml points at strings.xml");
            }
        }

        private static string Read(object definition, string property)
        {
            System.Reflection.PropertyInfo info = definition.GetType().GetProperty(property);
            if (info == null)
            {
                throw new InvalidOperationException("MCM's property definition has no '" + property + "'; this check needs updating for this MCM version.");
            }
            return Convert.ToString(info.GetValue(definition)) ?? string.Empty;
        }

        private static void AddAll(List<Entry> all, string where, IEnumerable<string> texts)
        {
            foreach (string text in texts)
            {
                all.Add(new Entry(where + " '" + Plain(text) + "'", text));
            }
        }

        // The XML escapes the strings need, without turning every apostrophe into &apos;.
        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
