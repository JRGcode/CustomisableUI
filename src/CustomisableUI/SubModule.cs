using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CustomisableUI.Backgrounds;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace CustomisableUI
{
    public class SubModule : MBSubModuleBase
    {
        private static readonly Type[] PatchTypes = new[]
            {
                typeof(BrushRendererRenderPatch),
                typeof(WidgetRenderPatch),
                typeof(DrawSpritePatch),
                typeof(BrushTextMaterialPatch),
                typeof(StyleTextMaterialPatch),
                typeof(FontMappingPatch),
                typeof(TextSizePatch),
            }
            .Concat(BackgroundFeature.PatchTypes) // [Backgrounds] its hooks are listed in Backgrounds\BackgroundPatches.cs
            .ToArray();

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Log.Info("Customisable UI " + typeof(SubModule).Assembly.GetName().Version + " loading");
            CrashDiagnostics.Install();

            // Patched one class at a time, so a game update that breaks one hook leaves the others working.
            var harmony = new Harmony("CustomisableUI");
            foreach (Type patch in PatchTypes)
            {
                try
                {
                    harmony.CreateClassProcessor(patch).Patch();
                }
                catch (Exception ex)
                {
                    Log.Error("Applying " + patch.Name, ex);
                }
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            Hotkey.Tick();

            try
            {
                BackgroundFeature.Tick(ThemeState.Current); // [Backgrounds]
            }
            catch (Exception ex)
            {
                Log.Error("Loading the background pictures", ex);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            RoleOverrides.Load(Path.Combine(ModulePaths.Root, "UiRoles.txt"));
        }
    }
}
