using System;
using System.Threading;

namespace CustomisableUI
{
    // When the game dies, Windows only records which method failed, not who called it. These two handlers write the
    // full call stack to CustomisableUI.log, which is what is needed to tell whether this mod, another mod or the game
    // itself is responsible. They do nothing unless something has already gone wrong.
    internal static class CrashDiagnostics
    {
        // First-chance exceptions can be frequent and are usually handled somewhere; only the first few null
        // references are even looked at, and only a failing text lookup is written out.
        private const int MaxInspected = 200;
        private const int MaxReported = 5;

        private static int _inspected;
        private static int _reported;
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
            {
                return;
            }
            _installed = true;

            try
            {
                AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
                AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;
            }
            catch (Exception ex)
            {
                Log.Error("Installing crash diagnostics", ex);
            }
        }

        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Info("UNHANDLED EXCEPTION (terminating=" + e.IsTerminating + "):" + Environment.NewLine + e.ExceptionObject);
        }

        // The crash this was added for was a NullReferenceException in GameTexts.FindText, i.e. a localised text being
        // requested while the game's text system wasn't available. The exception's own trace stops at the failing
        // call, so capture the whole stack of the thread at the moment it is thrown.
        private static void OnFirstChance(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            try
            {
                if (!(e.Exception is NullReferenceException) ||
                    Volatile.Read(ref _reported) >= MaxReported ||
                    Interlocked.Increment(ref _inspected) > MaxInspected)
                {
                    return;
                }

                string trace = e.Exception.StackTrace;
                if (trace == null || trace.IndexOf("GameTexts.FindText", StringComparison.Ordinal) < 0)
                {
                    return;
                }

                Interlocked.Increment(ref _reported);
                Log.Info("NullReferenceException in GameTexts.FindText. Call stack at the moment it was thrown:" +
                         Environment.NewLine + Environment.StackTrace);
            }
            catch
            {
                // A diagnostic must never be the thing that throws.
            }
        }
    }
}
