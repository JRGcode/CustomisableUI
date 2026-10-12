// Custom backgrounds: the picture behind the main menu. Overview: README.md in this folder.

using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace CustomisableUI.Backgrounds
{
    // The main menu's own background is a video the engine plays behind the interface, which can't be replaced from here.
    // So a picture goes in front of it instead: a full-screen widget behind the menu's own widgets. The video carries on
    // underneath, with its music.
    internal static class MainMenuBackground
    {
        private static readonly Random Chooser = new Random();

        // Called once the main menu has built its Gauntlet layer (see InitialScreenBackgroundPatch).
        public static void Attach(GauntletLayer layer)
        {
            UIContext context = layer?.UIContext;
            Widget root = context?.Root;
            if (root == null)
            {
                return;
            }

            FileSprite sprite = BackgroundImages.Pick(BackgroundKind.MainMenu, Chooser.Next());
            if (sprite == null)
            {
                return;
            }

            // Whether it is shown is decided as it is drawn (BackgroundDraw.Prepare), so switching the setting off, or
            // pressing the vanilla-look hotkey, takes effect at once even on the menu itself.
            var picture = new Widget(context)
            {
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                DoNotAcceptEvents = true,
                Sprite = sprite,
            };
            root.AddChildAtIndex(picture, 0);
            Log.Info("Main menu background: " + sprite.Name);
        }
    }
}
