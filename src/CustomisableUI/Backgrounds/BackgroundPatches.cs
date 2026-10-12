// Custom backgrounds: this feature's Harmony hooks, and the one entry point the rest of the mod uses. Overview: README.md in
// this folder.

using System;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Loading;

namespace CustomisableUI.Backgrounds
{
    // What SubModule needs to know about this feature: the hooks to apply, and what to run each frame. (The hook that does
    // the actual swapping of pictures is the shared DrawSpritePatch in Patches.cs, which calls BackgroundDraw.)
    internal static class BackgroundFeature
    {
        public static readonly Type[] PatchTypes =
        {
            typeof(InitialScreenBackgroundPatch),
            typeof(LoadingImagePatch),
            typeof(LoadingWindowRenderPatch),
        };

        // Called every frame, on the game's main thread.
        public static void Tick(ThemeSnapshot theme)
        {
            BackgroundImages.Tick(theme);
        }
    }

    // The main menu's screen. Once it has built its Gauntlet layer, one of the player's pictures goes behind it.
    [HarmonyPatch(typeof(GauntletInitialScreen), "OnInitialize")]
    internal static class InitialScreenBackgroundPatch
    {
        private static void Postfix(GauntletInitialScreen __instance)
        {
            if (PatchGuard.Tripped)
            {
                return;
            }

            try
            {
                var layer = AccessTools.Field(typeof(GauntletInitialScreen), "_gauntletLayer")?.GetValue(__instance) as GauntletLayer;
                MainMenuBackground.Attach(layer);
            }
            catch (Exception ex)
            {
                PatchGuard.Report("InitialScreen.OnInitialize", ex);
            }
        }
    }

    // A loading screen has been given a new picture by the game (the loading widget's ImageName changed). Decides what this
    // screen shows: one of the player's pictures, or the game's.
    [HarmonyPatch(typeof(LoadingWindowWidget), "UpdateImage")]
    internal static class LoadingImagePatch
    {
        private static void Postfix()
        {
            if (PatchGuard.Tripped)
            {
                return;
            }

            try
            {
                LoadingPictures.BeginScreen(ThemeState.Current);
            }
            catch (Exception ex)
            {
                PatchGuard.Report("LoadingWindowWidget.UpdateImage", ex);
            }
        }
    }

    // Notes while the loading window's own widget is being drawn, so its picture can be told from the same sprites used
    // elsewhere. This patches the same method as the shared WidgetRenderPatch; Harmony runs both.
    [HarmonyPatch(typeof(Widget), "OnRender")]
    internal static class LoadingWindowRenderPatch
    {
        private static void Prefix(Widget __instance)
        {
            LoadingPictures.InLoadingWindow = __instance is LoadingWindowWidget;
        }

        private static void Finalizer()
        {
            LoadingPictures.InLoadingWindow = false;
        }
    }
}
