using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace CustomisableUI
{
    public class SubModule : MBSubModuleBase
    {
        private static readonly Type[] PatchTypes =
        {
            typeof(BrushRendererRenderPatch),
            typeof(WidgetRenderPatch),
            typeof(DrawSpritePatch),
            typeof(BrushTextMaterialPatch),
            typeof(StyleTextMaterialPatch),
            typeof(FontMappingPatch),
            typeof(TextSizePatch),
        };

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
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // <module>\bin\<platform>\CustomisableUI.dll  ->  <module>\UiRoles.txt
            string binFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string moduleFolder = binFolder == null ? null : Directory.GetParent(Directory.GetParent(binFolder).FullName)?.FullName;
            RoleOverrides.Load(moduleFolder == null ? "UiRoles.txt" : Path.Combine(moduleFolder, "UiRoles.txt"));
        }
    }
}
