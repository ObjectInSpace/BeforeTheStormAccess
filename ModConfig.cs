using BepInEx.Configuration;

namespace BeforeTheStormAccess
{
    /// <summary>
    /// User-tunable settings, persisted by BepInEx to
    /// BepInEx\config\com.amock.beforethestormaccess.cfg. Edit while the game is
    /// closed, or through a future in-game settings layer.
    /// </summary>
    public static class ModConfig
    {
        public static ConfigEntry<bool> AnnounceWindowNames { get; private set; }
        public static ConfigEntry<bool> ReadWindowTextOnOpen { get; private set; }
        public static ConfigEntry<bool> AnnounceOutOfRange { get; private set; }
        public static ConfigEntry<int> InteractHintCount { get; private set; }
        public static ConfigEntry<float> GuidanceProgressSeconds { get; private set; }
        public static ConfigEntry<bool> AnnounceObjectiveChanges { get; private set; }
        public static ConfigEntry<bool> SpeakDialogue { get; private set; }

        /// <summary>Binds all settings. Call once from plugin Awake.</summary>
        public static void Initialize(ConfigFile config)
        {
            AnnounceWindowNames = config.Bind("Verbosity", "AnnounceWindowNames", true,
                "Speak the window name when a window opens.");
            ReadWindowTextOnOpen = config.Bind("Verbosity", "ReadWindowTextOnOpen", true,
                "Read a window's visible text top to bottom when it opens.");
            AnnounceOutOfRange = config.Bind("Verbosity", "AnnounceOutOfRange", true,
                "Say 'out of range' when walking away from an interactive object.");
            InteractHintCount = config.Bind("Verbosity", "InteractHintCount", 3,
                "How many times per session to explain the hold-interact mechanic when " +
                "reaching an object. 0 disables the hint.");
            AnnounceObjectiveChanges = config.Bind("Verbosity", "AnnounceObjectiveChanges", true,
                "Announce new objectives automatically as the story sets them.");
            SpeakDialogue = config.Bind("Verbosity", "SpeakDialogue", true,
                "Read dialogue lines and subtitles automatically. Toggle in game with " +
                "Shift+grave (tilde). Useful when the voice acting alone is preferred.");
            GuidanceProgressSeconds = config.Bind("Navigation", "GuidanceProgressSeconds", 3.5f,
                "Seconds between spoken progress distances while continuous guidance runs " +
                "on a steady course. Course changes always speak immediately.");
        }
    }
}
