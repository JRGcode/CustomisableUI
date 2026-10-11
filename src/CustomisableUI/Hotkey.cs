using System;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace CustomisableUI
{
    // A key that flips between the player's theme and the vanilla look. It is polled once a frame from the sub-module's
    // tick, so it works everywhere, including on the settings screen itself, which makes it the way out of a theme
    // that has made the interface hard to read.
    internal static class Hotkey
    {
        public static void Tick()
        {
            try
            {
                if (!WasPressed(ThemeState.HotkeyChoice))
                {
                    return;
                }

                ThemeState.Suspended = !ThemeState.Suspended;
                Announce(ThemeState.Suspended
                    ? "Customisable UI: showing the vanilla look. Press the hotkey again to bring your theme back."
                    : "Customisable UI: your theme is back.");
            }
            catch (Exception ex)
            {
                // The input system isn't always ready during start-up and shutdown.
                Log.Error("Hotkey", ex);
            }
        }

        // The index is the position in ModSettings.HotkeyChoices.
        private static bool WasPressed(int choice)
        {
            switch (choice)
            {
                case 1:
                    return Input.IsKeyPressed(InputKey.U) && CtrlDown() && ShiftDown();
                case 2:
                    return Input.IsKeyPressed(InputKey.F8);
                case 3:
                    return Input.IsKeyPressed(InputKey.F9);
                case 4:
                    return Input.IsKeyPressed(InputKey.F10);
                default:
                    return false;
            }
        }

        private static bool CtrlDown()
        {
            return Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
        }

        private static bool ShiftDown()
        {
            return Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
        }

        // A message in the game's own message feed. It doesn't appear on every screen (the main menu has none), which
        // is fine: the change itself is visible.
        private static void Announce(string message)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
            }
            catch
            {
            }
        }
    }
}
