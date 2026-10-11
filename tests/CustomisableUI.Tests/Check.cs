using System;
using System.Collections.Generic;

namespace CustomisableUI.Tests
{
    // A deliberately tiny assertion helper: no framework, no package downloads. Failures are collected rather than
    // thrown, so one run reports everything that is wrong.
    internal static class Check
    {
        private static readonly List<string> Failures = new List<string>();
        private static int _passed;

        public static int FailureCount => Failures.Count;

        public static void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("== " + name);
        }

        public static void That(bool condition, string description)
        {
            if (condition)
            {
                _passed++;
                return;
            }

            Failures.Add(description);
            Console.WriteLine("   FAIL  " + description);
        }

        public static void Equal<T>(T expected, T actual, string description)
        {
            That(EqualityComparer<T>.Default.Equals(expected, actual),
                description + "  (expected " + expected + ", got " + actual + ")");
        }

        public static void Near(float expected, float actual, float tolerance, string description)
        {
            That(Math.Abs(expected - actual) <= tolerance,
                description + "  (expected " + expected.ToString("0.000") + ", got " + actual.ToString("0.000") + ")");
        }

        public static void Note(string text)
        {
            Console.WriteLine("   " + text);
        }

        public static int Report()
        {
            Console.WriteLine();
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine(_passed + " checks passed, " + Failures.Count + " failed");
            foreach (string failure in Failures)
            {
                Console.WriteLine("  FAILED: " + failure);
            }
            return Failures.Count;
        }
    }
}
