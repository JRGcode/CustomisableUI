using System;
using System.Diagnostics;

namespace CustomisableUI
{
    // Holds the current ThemeSnapshot. It is rebuilt from the MCM settings ten times a second rather than on change
    // events alone, which keeps it correct whichever way a value was changed (the settings screen, a preset, a reset).
    internal static class ThemeState
    {
        private static readonly long RefreshInterval = Stopwatch.Frequency / 10;

        private static ThemeSnapshot _real = ThemeSnapshot.Disabled;
        private static int _hotkeyChoice;
        private static long _nextRefresh;

        // True while the player has used the hotkey to see the vanilla look. The settings are untouched; the hooks
        // simply see the "disabled" snapshot until the hotkey is pressed again.
        public static bool Suspended { get; set; }

        public static ThemeSnapshot Current
        {
            get
            {
                RefreshIfDue();
                return Suspended ? ThemeSnapshot.Disabled : _real;
            }
        }

        // Which hotkey the player picked. Read from the settings even while the theme is suspended (the snapshot is
        // "disabled" then, but the key to bring it back must still be known).
        public static int HotkeyChoice
        {
            get
            {
                RefreshIfDue();
                return _hotkeyChoice;
            }
        }

        // Makes the next read rebuild the snapshot straight away.
        public static void Invalidate()
        {
            _nextRefresh = 0;
        }

        private static void RefreshIfDue()
        {
            long now = Stopwatch.GetTimestamp();
            if (now >= _nextRefresh)
            {
                _nextRefresh = now + RefreshInterval;
                Refresh();
            }
        }

        private static void Refresh()
        {
            try
            {
                ModSettings settings = ModSettings.Instance;
                if (settings == null)
                {
                    _real = ThemeSnapshot.Disabled;
                    _hotkeyChoice = 0;
                    return;
                }

                _real = ThemeSnapshot.From(settings);
                _hotkeyChoice = settings.ToggleHotkey.SelectedIndex;
            }
            catch (Exception ex)
            {
                // Settings can legitimately be unavailable very early in start-up. Stay vanilla until they aren't.
                Log.Error("Reading settings", ex);
                _real = ThemeSnapshot.Disabled;
                _hotkeyChoice = 0;
            }
        }
    }
}
