// Custom backgrounds: the player's picture files, and getting them into the engine. Overview: README.md in this folder.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TaleWorlds.Engine.GauntletUI;
using EngineTexture = TaleWorlds.Engine.Texture;

namespace CustomisableUI.Backgrounds
{
    // The player's own pictures: PNG or JPG files in <module>\Backgrounds\<MainMenu|Loading>. The folders are read when a
    // picture is wanted (at most every couple of seconds), so adding a file needs no restart.
    internal static class BackgroundImages
    {
        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
        private static readonly long RefreshInterval = Stopwatch.Frequency * 2;

        private static readonly object Gate = new object();
        private static readonly Dictionary<BackgroundKind, Listing> Listings = new Dictionary<BackgroundKind, Listing>();
        private static readonly Dictionary<string, FileSprite> Sprites = new Dictionary<string, FileSprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static int _mainThreadId;
        private static long _lastTick;

        // The tests look in a folder of their own instead of the module's, and stand in for the engine when loading.
        internal static string RootOverride { get; set; }

        internal static Func<string, FileSprite> Loader { get; set; }

        private sealed class Listing
        {
            public long CheckedAt;
            public string[] Files = new string[0];
        }

        public static string FolderFor(BackgroundKind kind)
        {
            string root = RootOverride ?? Path.Combine(ModulePaths.Root, "Backgrounds");
            return Path.Combine(root, kind == BackgroundKind.MainMenu ? "MainMenu" : "Loading");
        }

        // The picture files for a kind, in name order.
        public static IReadOnlyList<string> Images(BackgroundKind kind)
        {
            lock (Gate)
            {
                long now = Stopwatch.GetTimestamp();
                if (!Listings.TryGetValue(kind, out Listing listing))
                {
                    listing = new Listing { CheckedAt = now - RefreshInterval };
                    Listings[kind] = listing;
                }

                if (now - listing.CheckedAt >= RefreshInterval)
                {
                    listing.CheckedAt = now;
                    listing.Files = Scan(FolderFor(kind));
                }
                return listing.Files;
            }
        }

        // Forgets what was listed and loaded, so the next look at the folders is a fresh one (used by the tests).
        internal static void Forget()
        {
            lock (Gate)
            {
                Listings.Clear();
                Sprites.Clear();
                Failed.Clear();
                _mainThreadId = 0;
                _lastTick = 0;
            }
        }

        // How many pictures are loaded and ready (used by the tests).
        internal static int LoadedCount
        {
            get
            {
                lock (Gate)
                {
                    return Sprites.Count;
                }
            }
        }

        private static string[] Scan(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    return new string[0];
                }

                return Directory.GetFiles(folder)
                    .Where(file => Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log.Error("Reading the Backgrounds folder " + folder, ex);
                return new string[0];
            }
        }

        // ---- loading pictures into the engine ----------------------------------------------------------------------

        // The engine's textures are only made on the game's main thread. A loading screen is drawn while the game is busy
        // loading, often from another thread, so the pictures it will want are loaded ahead of time (see Tick) and a
        // picture that isn't ready yet is simply not used that time.
        private static bool OnMainThread => _mainThreadId == 0 || Environment.CurrentManagedThreadId == _mainThreadId;

        // Called every frame from the game's main-thread tick. Loads the loading-screen pictures one at a time, so they
        // are waiting when a loading screen needs them.
        public static void Tick(ThemeSnapshot theme)
        {
            _mainThreadId = Environment.CurrentManagedThreadId;

            long now = Stopwatch.GetTimestamp();
            if (now - _lastTick < Stopwatch.Frequency / 4)
            {
                return;
            }
            _lastTick = now;

            if (!theme.Background.UsesLoadingPictures)
            {
                return;
            }

            foreach (string path in Images(BackgroundKind.Loading))
            {
                if (!IsHandled(path))
                {
                    Load(path);
                    return;
                }
            }
        }

        // True if the picture has been loaded, or has been tried and failed.
        private static bool IsHandled(string path)
        {
            string key = KeyFor(path);
            if (key == null)
            {
                return true;
            }
            lock (Gate)
            {
                return Sprites.ContainsKey(key) || Failed.Contains(key);
            }
        }

        // Identifies a version of a file, so that replacing a picture loads the new one.
        private static string KeyFor(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return path + "|" + info.LastWriteTimeUtc.Ticks + "|" + info.Length;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The picture at `index` (wrapping round the list), loaded the first time it is wanted. A picture that can't be
        // used is skipped for the next one; null means there is nothing usable.
        public static FileSprite Pick(BackgroundKind kind, int index)
        {
            IReadOnlyList<string> files = Images(kind);
            if (files.Count == 0)
            {
                return null;
            }

            long start = index & int.MaxValue;
            for (int i = 0; i < files.Count; i++)
            {
                FileSprite sprite = Load(files[(int)((start + i) % files.Count)]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private static FileSprite Load(string path)
        {
            string key = KeyFor(path);
            if (key == null)
            {
                return null;
            }

            lock (Gate)
            {
                if (Sprites.TryGetValue(key, out FileSprite cached))
                {
                    return cached;
                }
                if (Failed.Contains(key) || !OnMainThread)
                {
                    return null;
                }

                try
                {
                    FileSprite sprite = Loader != null ? Loader(path) : FromEngine(path);
                    Sprites[key] = sprite;
                    Log.Info("Custom background loaded: " + Path.GetFileName(path) + " (" + sprite.Width + "x" + sprite.Height + ")");
                    return sprite;
                }
                catch (Exception ex)
                {
                    Failed.Add(key);
                    Log.Error("Loading the custom background " + Path.GetFileName(path), ex);
                    return null;
                }
            }
        }

        private static FileSprite FromEngine(string path)
        {
            EngineTexture engine = EngineTexture.CreateFromMemory(File.ReadAllBytes(path));
            if (engine == null || engine.Width <= 0 || engine.Height <= 0)
            {
                throw new InvalidDataException("the game could not read it as a picture (use PNG or JPG)");
            }

            var texture = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(engine));
            return new FileSprite("cui_background:" + Path.GetFileName(path), texture, engine.Width, engine.Height);
        }
    }
}
