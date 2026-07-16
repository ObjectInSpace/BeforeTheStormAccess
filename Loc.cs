using System;
using System.Collections.Generic;
using System.IO;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// Localization for mod speech. English is embedded; other languages load from
    /// optional override files next to the plugin DLL named
    /// "BeforeTheStormAccess.strings.{lang}.txt" (UTF-8, one "key=value" per line,
    /// # comments). The active language follows the game's text-language setting
    /// (T_A7F32A9D.s_language) and switches live via the game's language-changed
    /// callback. Missing keys fall back to English.
    /// </summary>
    public static class Loc
    {
        private static bool _initialized;
        private static string _currentLang = "en";

        private static readonly Dictionary<string, string> _english = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

        /// <summary>
        /// Initializes the string tables, selects the active language, and hooks
        /// the game's language-changed notification.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            InitializeStrings();
            _initialized = true;
            RefreshLanguage();

            try
            {
                T_A7F32A9D.RegisterPostLanguageChanged(RefreshLanguage);
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("Loc: could not register language-changed callback: " + ex.Message);
            }

            WriteTranslationTemplate();
        }

        /// <summary>
        /// Writes all English strings to BeforeTheStormAccess.strings.template.txt
        /// next to the DLL on every start, so translators always have a current
        /// key list. Copy it to strings.{lang}.txt and translate the values.
        /// </summary>
        private static void WriteTranslationTemplate()
        {
            try
            {
                string directory = Path.GetDirectoryName(typeof(Loc).Assembly.Location);
                string file = Path.Combine(directory, "BeforeTheStormAccess.strings.template.txt");
                using (StreamWriter writer = new StreamWriter(file, false, System.Text.Encoding.UTF8))
                {
                    writer.WriteLine("# Before the Storm Access translation template.");
                    writer.WriteLine("# Copy to BeforeTheStormAccess.strings.{lang}.txt (e.g. .de.txt, .fr.txt,");
                    writer.WriteLine("# .es.txt, .es-mx.txt, .it.txt, .pt.txt, .zh-hans.txt, .ja.txt) and");
                    writer.WriteLine("# translate the part after each equals sign. {0}-style placeholders must stay.");
                    foreach (KeyValuePair<string, string> pair in _english)
                    {
                        writer.WriteLine(pair.Key + "=" + pair.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("Loc: could not write translation template: " + ex.Message);
            }
        }

        /// <summary>
        /// Re-reads the game's language and reloads the override file for it.
        /// </summary>
        public static void RefreshLanguage()
        {
            string lang = "en";
            try
            {
                lang = T_A7F32A9D.s_language.ToString().ToLowerInvariant().Replace('_', '-');
            }
            catch
            {
                // Game localization not up yet; stay on English.
            }

            if (lang == "n" || lang.Length == 0)
                lang = "en";

            if (lang == _currentLang && (_overrides.Count > 0 || lang == "en"))
                return;

            _currentLang = lang;
            LoadOverrides(lang);
        }

        /// <summary>The currently selected language code (lowercase, e.g. "en", "de", "zh-hans").</summary>
        public static string CurrentLanguage
        {
            get
            {
                if (!_initialized)
                    Initialize();
                return _currentLang;
            }
        }

        /// <summary>
        /// Gets a localized string for the provided key.
        /// </summary>
        public static string Get(string key)
        {
            if (!_initialized)
                Initialize();

            string value;
            if (_overrides.TryGetValue(key, out value))
                return value;

            if (_english.TryGetValue(key, out value))
                return value;

            return key;
        }

        /// <summary>
        /// Gets a localized formatted string for the provided key.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        private static void LoadOverrides(string lang)
        {
            _overrides.Clear();
            if (lang == "en")
                return;

            try
            {
                string directory = Path.GetDirectoryName(typeof(Loc).Assembly.Location);
                string file = Path.Combine(directory, "BeforeTheStormAccess.strings." + lang + ".txt");
                if (!File.Exists(file))
                {
                    Main.Log.LogInfo("Loc: no override file for '" + lang + "'; using English.");
                    return;
                }

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;

                    int split = line.IndexOf('=');
                    if (split <= 0)
                        continue;

                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    if (key.Length > 0 && value.Length > 0)
                    {
                        _overrides[key] = value;
                    }
                }

                Main.Log.LogInfo("Loc: loaded " + _overrides.Count + " strings for '" + lang + "'.");
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning("Loc: failed to load overrides for '" + lang + "': " + ex.Message);
                _overrides.Clear();
            }
        }

        private static void Add(string key, string english)
        {
            _english[key] = english;
        }

        private static void InitializeStrings()
        {
            Add("mod_loaded", "Before the Storm Access loaded.");
            Add("help_text",
                "Before the Storm Access keys. " +
                "F1: this help. " +
                "Grave accent: repeat last announcement, or read the current choices " +
                "whenever a choice is on screen. " +
                "Shift grave, the tilde: toggle automatic dialogue reading on or off. " +
                "F9: toggle debug logging. " +
                "F10: write a diagnostic snapshot to the log file. " +
                "Right bracket: next interactive object, nearest first. " +
                "Left bracket: previous interactive object. " +
                "Backslash: repeat the selected object with fresh distance and direction. " +
                "N: spoken walking route to the selected object, using the game's own paths; " +
                "press N again while walking for a fresh route. " +
                "Shift N: toggle continuous guidance, a spoken beacon that calls the clock " +
                "direction whenever your course should change, announces progress, and says " +
                "arrived when you reach the object. " +
                "O: current objective. Shift O: current location. " +
                "New objectives are announced automatically. " +
                "Distances are in meters; directions use clock positions where 12 is straight ahead. " +
                "Dialogue lines and subtitles are read automatically, and windows such as " +
                "menus, the journal, and phone texts are read when they open. " +
                "When an object is in reach, its name and actions are announced. " +
                "Dialogue choices and object actions are read with their direction: up, right, " +
                "down, or left, matching W, D, S, A or the corresponding controller buttons.");
            Add("debug_mode_enabled", "Debug mode enabled.");
            Add("debug_mode_disabled", "Debug mode disabled.");
            Add("nothing_to_repeat", "Nothing to repeat.");
            Add("dialogue_reading_on", "Dialogue reading on.");
            Add("dialogue_reading_off", "Dialogue reading off.");

            // Choice directions (match eInteractMenu order: up, right, down, left).
            Add("dir_up", "up");
            Add("dir_right", "right");
            Add("dir_down", "down");
            Add("dir_left", "left");

            Add("choices_prefix", "Choices:");
            Add("backtalk_prefix", "Backtalk:");
            Add("choice_entry", "{0}: {1}");

            // End-of-episode choices recap (ChoicesWindow).
            Add("choices_recap_prefix", "Choices for");
            Add("choices_decision", "Decision {0}:");
            Add("choices_you_chose", "you chose {0}");
            Add("choices_alternative", "The other option: {0}");
            Add("choices_option_stat", "{0}, {1} percent");
            // Spoken once per recap when the world/friends percentages are absent.
            // They come from the online stats server, so they are unavailable offline
            // or when not signed in.
            Add("choices_stats_offline", "World and friends stats unavailable: not connected to the internet.");
            Add("choices_stats_not_signed_in", "World and friends stats unavailable: not signed in.");
            Add("choices_stats_unavailable", "World and friends stats unavailable.");

            Add("window_opened", "{0} window.");
            Add("diagnostics_dumped", "Diagnostics written to log.");

            Add("out_of_range", "out of range");
            Add("hotspot_nearby", "{0} nearby.");
            Add("interact_hint", "Hold {0}, then press the action's direction key.");
            Add("key_unknown", "the interact key");
            Add("no_hotspots", "No interactive objects found nearby.");
            Add("no_browser_target", "No object selected. Press right bracket to find the nearest object.");
            Add("hotspot_cycle", "{0} of {1}: {2}, {3}.");
            Add("hotspot_detail", "{0}, {1}.");
            Add("distance_clock", "{0} meters at {1} o'clock");
            Add("elev_slightly_up", "slightly up");
            Add("elev_up_high", "up high");
            Add("elev_slightly_down", "slightly down");
            Add("elev_down_low", "down low");
            Add("nav_aim", "Aim at {0} o'clock");

            Add("bt_started", "Backtalk challenge against {0}. Win {1} points.");
            Add("bt_unknown_opponent", "your opponent");
            Add("bt_point_won", "Point. {0} of {1}.");
            Add("bt_point_lost", "Lost point. {0} of {1}.");
            Add("bt_won", "You won the backtalk.");
            Add("bt_lost", "You lost the backtalk.");
            Add("bt_time_low", "Hurry.");

            Add("objective_prefix", "Objective: {0}");
            Add("objective_new", "New objective: {0}");
            Add("no_objective", "No current objective.");
            Add("location_prefix", "Location: {0}");
            Add("no_location", "Location unknown.");

            Add("nav_route_header", "Route to {0}: {1} meters total.");
            Add("nav_leg", "turn to {0} o'clock, {1} meters");
            Add("nav_partial", "Warning: the route is partial and may not reach the target.");
            Add("nav_no_route", "No walkable route to {0} was found.");
            Add("nav_more_legs", "More legs follow; press N again along the way.");
            Add("nav_guidance_on", "Guidance to {0} started. Follow the clock calls; Shift N stops.");
            Add("nav_guidance_off", "Guidance stopped.");
            Add("nav_arrived", "Arrived at {0}.");
            Add("nav_route_lost", "Route lost. Guidance stopped.");
            Add("nav_tick_hour", "{0} o'clock, {1} meters");
            Add("nav_tick_dist", "{0} meters");

            Add("slider_percent", "{0} percent");
            Add("press_key_to_bind", "press a key to bind");
            Add("key_unbound", "unbound");
            Add("key_left_mouse", "left mouse button");
            Add("key_right_mouse", "right mouse button");
            Add("key_middle_mouse", "middle mouse button");
            Add("key_grave", "grave accent");
        }
    }
}
