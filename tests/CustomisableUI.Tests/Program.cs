using System;
using System.IO;
using System.Reflection;

namespace CustomisableUI.Tests
{
    internal static class Program
    {
        public static string GameFolder
        {
            get
            {
                string fromEnvironment = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
                return !string.IsNullOrEmpty(fromEnvironment)
                    ? fromEnvironment
                    : @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord";
            }
        }

        private static int Main()
        {
            if (!Directory.Exists(GameFolder))
            {
                Console.WriteLine("Bannerlord not found at '" + GameFolder + "'. Set BANNERLORD_GAME_DIR.");
                return 1000;
            }

            // The game's assemblies are referenced but not copied, so find them where the game keeps them.
            string[] searchFolders =
            {
                Path.Combine(GameFolder, "bin", "Win64_Shipping_Client"),
                Path.Combine(GameFolder, "Modules", "Native", "bin", "Win64_Shipping_Client"),
                Path.Combine(GameFolder, "Modules", "SandBoxCore", "bin", "Win64_Shipping_Client"),
                Path.Combine(GameFolder, "Modules", "SandBox", "bin", "Win64_Shipping_Client"),
                Path.Combine(GameFolder, "Modules", "Bannerlord.Harmony", "bin", "Win64_Shipping_Client"),
                Path.Combine(GameFolder, "Modules", "Bannerlord.MBOptionScreen", "bin", "Win64_Shipping_Client"),
            };
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string file = new AssemblyName(args.Name).Name + ".dll";
                foreach (string folder in searchFolders)
                {
                    string path = Path.Combine(folder, file);
                    if (File.Exists(path))
                    {
                        return Assembly.LoadFrom(path);
                    }
                }
                return null;
            };

            Console.WriteLine("Customisable UI checks against " + GameFolder);

            Tests.SwatchesChecks();
            Tests.ContrastChecks();
            Tests.SettingsChecks();
            Tests.PresetsChecks();
            Tests.ClassifierChecks();
            Tests.StylingChecks();
            Tests.StatusColoursChecks();
            Tests.CrosshairChecks();
            Tests.TextShadowChecks();
            Tests.RelationsChecks();
            Tests.KeepColoursChecks();
            Tests.ScreenDimmingChecks();
            Tests.GameDataChecks();
            Tests.HooksChecks();

            return Check.Report();
        }
    }
}
