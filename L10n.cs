using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace DooDesch.Localization
{
    /// <summary>
    /// Minimal gettext-style localization for DooDesch mods, compiled per mod as linked source.
    /// Canonical home: https://github.com/DooDesch-Mods/ScheduleOne-L10n
    ///
    /// The English source string IS the key: wrap player-facing literals in L10n.T(...) and
    /// register one English-to-translation table per language code (in OnInitializeMelon).
    /// Unknown keys (and unknown languages) fall back to the English literal unchanged, so a
    /// mod without tables behaves exactly as before.
    ///
    /// Players can add or override translations without touching the mod:
    ///   UserData/DooDesch/Localization/&lt;ModName&gt;/&lt;code&gt;.json  (e.g. .../RVRepairVan/fr.json)
    /// A flat JSON object mapping the English source text to the translation. On startup the mod
    /// writes _template.en.json into that folder listing every translatable string - copy it,
    /// rename it to a language code and translate the values. User entries win over built-in
    /// ones, and a user file alone is enough to add a whole new language.
    ///
    /// The game itself has no language setting (as of v0.4.5f2 it ships no localization system
    /// at all), so "auto" resolves via the OS language. All DooDesch mods share ONE
    /// MelonPreferences override (category "DooDesch", entry "Language": auto | en | de | ...)
    /// to force a language. The language is resolved once per session; strings are looked up
    /// at build/injection time.
    /// </summary>
    internal static class L10n
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _tables = new Dictionary<string, Dictionary<string, string>>();
        private static Dictionary<string, string> _active;
        private static string _lang;

        /// <summary>Resolved two-letter language code ("en", "de", ...).</summary>
        internal static string Language => _lang ?? (_lang = Detect());

        /// <summary>Register the English-to-translation table for a language code. Call from OnInitializeMelon.</summary>
        internal static void Register(string lang, Dictionary<string, string> table)
        {
            _tables[lang] = table;
            _active = null;   // re-resolve on the next T()
        }

        /// <summary>Translate an English source string; returns it unchanged when no translation exists.</summary>
        internal static string T(string en)
        {
            if (_active == null) _active = BuildActiveTable();
            return _active.TryGetValue(en, out string s) ? s : en;
        }

        /// <summary>Translate + string.Format for lines with runtime values, e.g. T("Pay ${0}", fee).</summary>
        internal static string T(string en, params object[] args) => string.Format(T(en), args);

        // Built-in table for the resolved language, overlaid with the player's own translation
        // file (user entries win). Also drops the key template next to it so translators always
        // have a current list of every string this mod can localize.
        private static Dictionary<string, string> BuildActiveTable()
        {
            var table = _tables.TryGetValue(Language, out var builtIn)
                ? new Dictionary<string, string>(builtIn)
                : new Dictionary<string, string>();
            try
            {
                ExportTemplate();
                string file = Path.Combine(UserDir(), Language + ".json");
                if (File.Exists(file))
                {
                    var user = ParseJsonObject(File.ReadAllText(file));
                    if (user == null)
                        MelonLogger.Warning("[L10n] ignoring invalid translation file (flat JSON object of \"english\": \"translated\" strings expected): " + file);
                    else
                        foreach (var kv in user) table[kv.Key] = kv.Value;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[L10n] user translations failed: " + e.Message); }
            return table;
        }

        private static string UserDir() => Path.Combine(
            MelonEnvironment.UserDataDirectory, "DooDesch", "Localization", typeof(L10n).Assembly.GetName().Name);

        // _template.en.json = every translatable string this mod ships (the union of all registered
        // tables' keys; each key is the English source, so a fresh copy is a valid file as-is).
        // Rewritten every session so it always matches the installed mod version. The "_readme"
        // entry is an ordinary unused key that simply never matches a source string.
        private static void ExportTemplate()
        {
            if (_tables.Count == 0) return;   // no registered table - no key list to offer
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in _tables.Values)
                foreach (var k in t.Keys)
                    keys.Add(k);
            if (keys.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append("{\n  ").Append(Quote("_readme")).Append(": ").Append(Quote(
                "Copy this file to <language code>.json (for example fr.json), then translate the VALUES only" +
                " - the keys are the mod's English source strings and must stay unchanged. Lines you remove" +
                " simply keep their built-in text. Keep placeholders like {0} in your translation." +
                " Set Language in MelonPreferences.cfg under [DooDesch] to force a language (auto = OS language).")).Append(",\n");
            foreach (var k in keys)
                sb.Append("  ").Append(Quote(k)).Append(": ").Append(Quote(k)).Append(",\n");
            sb.Length -= 2;   // trailing comma of the last entry
            sb.Append("\n}\n");

            Directory.CreateDirectory(UserDir());
            File.WriteAllText(Path.Combine(UserDir(), "_template.en.json"), sb.ToString(), new UTF8Encoding(false));
        }

        private static string Detect()
        {
            string pref = "auto";
            try
            {
                var cat = MelonPreferences.CreateCategory("DooDesch", "DooDesch Mods");
                var entry = cat.GetEntry<string>("Language")
                            ?? cat.CreateEntry("Language", "auto", "Language",
                                "Language for all DooDesch mods: auto (= OS language), en, de, ...");
                pref = (entry.Value ?? "auto").Trim().ToLowerInvariant();
            }
            catch { /* prefs unavailable -> auto */ }
            if (pref.Length > 0 && pref != "auto") return pref;

            // The game has no language setting, so auto = OS language. If a future game
            // update ships a real in-game language option, read it here instead.
            switch (Application.systemLanguage)
            {
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                default: return "en";
            }
        }

        // --- tiny flat-JSON reader/writer -------------------------------------------------
        // Translation files are a single JSON object whose keys and values are strings. A
        // hand-rolled parser keeps this dependency-free and identical on the IL2CPP (net6)
        // and Mono (netstandard2.1) backends. Tolerates a UTF-8 BOM and a trailing comma;
        // anything else non-standard makes the file be ignored with a console warning.

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            if (json == null) return null;
            int i = 0;
            var result = new Dictionary<string, string>();
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') return null;
            i++;
            SkipWs(json, ref i);
            if (i < json.Length && json[i] == '}') return result;
            while (true)
            {
                string key = ParseString(json, ref i);
                if (key == null) return null;
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':') return null;
                i++;
                SkipWs(json, ref i);
                string val = ParseString(json, ref i);
                if (val == null) return null;
                result[key] = val;
                SkipWs(json, ref i);
                if (i >= json.Length) return null;
                if (json[i] == ',')
                {
                    i++;
                    SkipWs(json, ref i);
                    if (i < json.Length && json[i] == '}') return result;   // trailing comma
                    continue;
                }
                return json[i] == '}' ? result : null;
            }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '\uFEFF')) i++;
        }

        private static string ParseString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) return null;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) return null;
                        try { sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); } catch { return null; }
                        i += 4;
                        break;
                    default: return null;
                }
            }
            return null;   // unterminated string
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
