using System;
using System.IO;
using System.Reflection;

namespace CustomisableUI
{
    // Where this mod's own folder is, so files next to SubModule.xml (UiRoles.txt, Backgrounds\...) can be found.
    internal static class ModulePaths
    {
        private static string _root;

        // <module>\bin\<platform>\CustomisableUI.dll  ->  <module>
        public static string Root
        {
            get
            {
                if (_root == null)
                {
                    _root = Find();
                }
                return _root;
            }
        }

        private static string Find()
        {
            try
            {
                string binFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string module = binFolder == null ? null : Directory.GetParent(Directory.GetParent(binFolder).FullName)?.FullName;
                return module ?? ".";
            }
            catch (Exception ex)
            {
                Log.Error("Finding the module folder", ex);
                return ".";
            }
        }
    }
}
