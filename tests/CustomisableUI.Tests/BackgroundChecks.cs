using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CustomisableUI.Backgrounds;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace CustomisableUI.Tests
{
    // The custom backgrounds feature (the Backgrounds folder of the mod): everything that can be checked without the game's
    // renderer. Loading a picture into a texture needs the running engine, so the tests stand in for it; that part is
    // covered by trying it in the game.
    internal static class BackgroundChecks
    {
        // Stands in for a texture the engine would have made.
        private sealed class FakeTexture : ITexture
        {
            public bool IsValid => true;

            public int Width => 1920;

            public int Height => 1080;

            public string Name { get; set; } = "fake";

            public bool IsLoaded() => true;

            public void Release()
            {
            }
        }

        // An area of the given size at the top left. The corners are System.Numerics vectors, which the framework and the
        // game both define, so they are set by reflection rather than by naming the type.
        private static Rectangle2D Area(float width, float height)
        {
            object boxed = Rectangle2D.Create();
            void SetCorner(string field, float x, float y)
            {
                FieldInfo corner = typeof(Rectangle2D).GetField(field);
                corner.SetValue(boxed, Activator.CreateInstance(corner.FieldType, x, y));
            }

            SetCorner("TopLeft", 0f, 0f);
            SetCorner("TopRight", width, 0f);
            SetCorner("BottomRight", width, height);
            SetCorner("BottomLeft", 0f, height);
            return (Rectangle2D)boxed;
        }

        // A sprite as the game's sprite data makes them: a named part of a category's numbered sheets.
        private static Sprite GameSprite(string category, string name, int sheet)
        {
            var part = new SpritePart(name, new SpriteCategory(category, 12, false), 1920, 1080) { SheetID = sheet };
            SpriteNinePatchParameters nine = default(SpriteNinePatchParameters);
            return new SpriteGeneric(name, part, in nine);
        }

        private static FileSprite FakeSprite(string name)
        {
            return new FileSprite(name, new TaleWorlds.TwoDimension.Texture(new FakeTexture()), 1920, 1080);
        }

        private static ThemeSnapshot Snapshot(bool enabled, LoadingBackgroundMode loading, bool stretch = false)
        {
            return new ThemeSnapshot
            {
                Background = new BackgroundStyle { Enabled = enabled, LoadingScreens = loading, Stretch = stretch },
            };
        }

        public static void Run()
        {
            Check.Section("Custom backgrounds");

            Fitting();
            Settings();
            LoadingSlots();
            DrawHook();
            Folders();
            LoadingScreens();
            MainThread();
        }

        // ---- fitting a picture to an area -----------------------------------------------------------------------------
        private static void Fitting()
        {
            FileSprite.Cover(1920f, 1080f, 1920f / 1080f, out float minU, out float minV, out float maxU, out float maxV);
            Check.That(minU == 0f && minV == 0f && maxU == 1f && maxV == 1f, "a picture the shape of the area is shown whole");

            FileSprite.Cover(2000f, 1000f, 1f, out minU, out minV, out maxU, out maxV);
            Check.Near(0.25f, minU, 0.0001f, "a picture twice as wide as the area loses a quarter from each side");
            Check.Near(0.75f, maxU, 0.0001f, "...so the right edge is at three quarters");
            Check.That(minV == 0f && maxV == 1f, "...and keeps its full height");

            FileSprite.Cover(1000f, 2000f, 1f, out minU, out minV, out maxU, out maxV);
            Check.Near(0.25f, minV, 0.0001f, "a picture twice as tall as the area loses a quarter from the top and bottom");
            Check.Near(0.75f, maxV, 0.0001f, "...so the bottom edge is at three quarters");
            Check.That(minU == 0f && maxU == 1f, "...and keeps its full width");

            FileSprite.Cover(1920f, 1080f, 0f, out minU, out minV, out maxU, out maxV);
            Check.That(minU == 0f && maxV == 1f, "no area shape means the whole picture");
            FileSprite.Cover(0f, 0f, 2f, out minU, out minV, out maxU, out maxV);
            Check.That(minU == 0f && maxU == 1f && minV == 0f && maxV == 1f, "an empty picture doesn't divide by zero");

            FileSprite sprite = FakeSprite("cui_background:test");
            Check.Equal(1920, sprite.Width, "the sprite has the picture's width");
            Check.Equal(1080, sprite.Height, "...and height");
            Check.That(sprite.Texture != null && sprite.Texture.IsLoaded(), "...and hands the draw call its texture");

            sprite.PrepareDraw(1000f, 500f, false);
            Vec2 min = sprite.GetMinUvs();
            Vec2 max = sprite.GetMaxUvs();
            Check.That(min.x == 0f && max.x == 1f, "an area wider than the picture shows its full width");
            Check.Near((1f - (1920f / 1080f) / 2f) / 2f, min.y, 0.0001f, "...and crops the top (" + min.y.ToString("0.000") + ")");
            Check.Near(1f - min.y, max.y, 0.0001f, "...and the same from the bottom");

            sprite.PrepareDraw(1000f, 500f, true);
            Check.That(sprite.GetMinUvs().y == 0f && sprite.GetMaxUvs().y == 1f, "stretching shows the whole picture whatever the area");

            BackgroundDraw.Fit(sprite, 1000f, 500f, false);
            Check.Near(min.y, sprite.GetMinUvs().y, 0.0001f, "fitting it to an area crops it as above");
            BackgroundDraw.Fit(sprite, 0f, 0f, false);
            Check.That(sprite.GetMinUvs().y == 0f && sprite.GetMaxUvs().y == 1f, "an area with no size shows the whole picture");
        }

        // ---- the settings, as the hooks read them -----------------------------------------------------------------------
        private static void Settings()
        {
            var settings = new ModSettings();
            BackgroundStyle defaults = BackgroundStyle.From(settings);
            Check.That(defaults.Enabled, "custom backgrounds default to on (nothing changes until pictures are added)");
            Check.Equal(LoadingBackgroundMode.Mine, defaults.LoadingScreens, "loading screens default to the player's pictures");
            Check.That(!defaults.Stretch, "pictures default to being cropped, not stretched");

            settings.LoadingScreens.SelectedIndex = 1;
            Check.Equal(LoadingBackgroundMode.Both, BackgroundStyle.From(settings).LoadingScreens, "the second choice is mixed");
            settings.LoadingScreens.SelectedIndex = 2;
            Check.Equal(LoadingBackgroundMode.Game, BackgroundStyle.From(settings).LoadingScreens, "the third choice is the game's only");
            Check.That(!BackgroundStyle.From(settings).UsesLoadingPictures, "...which uses none of the player's loading pictures");
            settings.BackgroundFit.SelectedIndex = 1;
            Check.That(BackgroundStyle.From(settings).Stretch, "the second fit is stretch");
            settings.CustomBackgrounds = false;
            Check.That(!BackgroundStyle.From(settings).Enabled, "off is off");

            ThemeSnapshot snapshot = ThemeSnapshot.From(new ModSettings());
            Check.That(snapshot.Background.Enabled, "the theme snapshot carries the feature's settings");
            Check.That(!ThemeSnapshot.Disabled.Background.Enabled, "...and the disabled snapshot (master switch off, hotkey) shows none of it");
        }

        // ---- which sprites are the game's loading pictures ---------------------------------------------------------------
        private static void LoadingSlots()
        {
            Check.Equal(7, LoadingPictures.Slot(GameSprite("ui_loading", "loading_08", 8)), "loading_08 is the base game's 8th picture");
            Check.Equal(0, LoadingPictures.Slot(GameSprite("ui_loading", "loading_01", 1)), "loading_01 is the first");
            Check.Equal(11, LoadingPictures.Slot(GameSprite("ui_loading", "loading_12", 12)), "loading_12 is the last");
            Check.Equal(2, LoadingPictures.Slot(GameSprite("ui_naval_loading", "empire", 3)), "War Sails' pictures, named after cultures, count too");
            Check.Equal(9, LoadingPictures.Slot(GameSprite("ui_naval_loading", "vlandia", 10)), "...all of them");
            Check.Equal(-1, LoadingPictures.Slot(GameSprite("ui_general", "loading_08", 8)), "a sprite of that name from another category is not one");
            Check.Equal(-1, LoadingPictures.Slot(GameSprite("ui_mploading", "loading_08", 8)), "multiplayer's loading pictures are left alone");
            Check.Equal(-1, LoadingPictures.Slot(GameSprite("ui_encyclopedia", "empire", 3)), "a culture's name in another category is not one");
            Check.Equal(-1, LoadingPictures.Slot(FakeSprite("cui_background:x")), "the player's own pictures are not the game's");

            Check.That(BackgroundDraw.Concerns(FakeSprite("cui_background:x")), "the feature's own pictures are its concern");
            Check.That(BackgroundDraw.Concerns(GameSprite("ui_naval_loading", "pirate", 7)), "...and the game's loading pictures");
            Check.That(!BackgroundDraw.Concerns(GameSprite("ui_general", "panel_bg", 1)), "...but no other sprite");
        }

        // ---- the shared draw hook's calls into the feature ------------------------------------------------------------
        private static void DrawHook()
        {
            FileSprite own = FakeSprite("cui_background:test");
            Sprite drawn = own;
            Rectangle2D area = Area(1000f, 500f);

            Check.That(!BackgroundDraw.Prepare(new ThemeSnapshot(), ref drawn, ref area), "with custom backgrounds off its pictures are not drawn");
            drawn = own;
            Check.That(BackgroundDraw.Prepare(Snapshot(true, LoadingBackgroundMode.Mine), ref drawn, ref area), "with them on, its pictures are drawn");
            Check.That(ReferenceEquals(drawn, own), "...as they are");

            Sprite plain = GameSprite("ui_general", "panel_bg", 1);
            Sprite unchanged = plain;
            Check.That(BackgroundDraw.Prepare(Snapshot(true, LoadingBackgroundMode.Mine), ref unchanged, ref area) && ReferenceEquals(unchanged, plain),
                "any other sprite is left as it is");
        }

        // ---- the folders -----------------------------------------------------------------------------------------------
        private static void Folders()
        {
            string root = NewRoot();
            try
            {
                BackgroundImages.RootOverride = root;
                BackgroundImages.Forget();
                Check.Equal(0, BackgroundImages.Images(BackgroundKind.Loading).Count, "a folder that doesn't exist has no pictures");
                Check.That(BackgroundImages.Pick(BackgroundKind.Loading, 3) == null, "...so there is nothing to pick");
                Check.That(BackgroundImages.FolderFor(BackgroundKind.MainMenu).EndsWith("MainMenu", StringComparison.Ordinal), "the main menu's pictures go in MainMenu");
                Check.That(BackgroundImages.FolderFor(BackgroundKind.Loading).EndsWith("Loading", StringComparison.Ordinal), "the loading pictures go in Loading");

                string loading = Path.Combine(root, "Loading");
                Directory.CreateDirectory(Path.Combine(loading, "sub"));
                foreach (string name in new[] { "b.PNG", "a.jpg", "c.jpeg", "notes.txt", "d.gif", "readme" })
                {
                    File.WriteAllText(Path.Combine(loading, name), "x");
                }
                File.WriteAllText(Path.Combine(loading, "sub", "inner.png"), "x");

                BackgroundImages.Forget();
                string[] listed = BackgroundImages.Images(BackgroundKind.Loading).Select(Path.GetFileName).ToArray();
                Check.Equal("a.jpg,b.PNG,c.jpeg", string.Join(",", listed), "PNG and JPG files are listed, in name order, whatever the case");
                Check.Equal(0, BackgroundImages.Images(BackgroundKind.MainMenu).Count, "the other folder is separate");

                File.WriteAllText(Path.Combine(loading, "e.png"), "x");
                Check.Equal(3, BackgroundImages.Images(BackgroundKind.Loading).Count, "a new file isn't seen until the list is read again...");
                BackgroundImages.Forget();
                Check.Equal(4, BackgroundImages.Images(BackgroundKind.Loading).Count, "...which happens every couple of seconds");
            }
            finally
            {
                CleanUp(root);
            }
        }

        // ---- what a loading screen shows -------------------------------------------------------------------------------
        private static void LoadingScreens()
        {
            string root = NewRoot();
            try
            {
                string loading = Path.Combine(root, "Loading");
                Directory.CreateDirectory(loading);
                foreach (string name in new[] { "a.png", "b.png", "c.png" })
                {
                    File.WriteAllText(Path.Combine(loading, name), "x");
                }

                BackgroundImages.RootOverride = root;
                BackgroundImages.Loader = path => FakeSprite("t:" + Path.GetFileName(path));
                BackgroundImages.Forget();
                LoadingPictures.Reset();
                LoadingPictures.InLoadingWindow = false;

                Sprite basic = GameSprite("ui_loading", "loading_08", 8);
                Sprite naval = GameSprite("ui_naval_loading", "empire", 3);
                string NameOf(FileSprite sprite) => sprite?.Name;

                // Outside the loading window (behind some campaign menus, the profile screen): by the game's own picture number.
                BackgroundStyle mine = Snapshot(true, LoadingBackgroundMode.Mine).Background;
                Check.Equal("t:b.png", NameOf(LoadingPictures.ReplacementFor(mine, basic)), "loading_08 elsewhere is the second of three pictures (slot 7)");
                Check.Equal("t:c.png", NameOf(LoadingPictures.ReplacementFor(mine, naval)), "War Sails' third picture is the third of three (slot 2)");
                Check.That(LoadingPictures.ReplacementFor(Snapshot(true, LoadingBackgroundMode.Both).Background, basic) != null, "mixed still gives those their own picture");
                Check.That(LoadingPictures.ReplacementFor(Snapshot(true, LoadingBackgroundMode.Game).Background, basic) == null, "the game's only gives none");
                Check.That(LoadingPictures.ReplacementFor(Snapshot(false, LoadingBackgroundMode.Mine).Background, basic) == null, "off gives none");
                Check.That(LoadingPictures.ReplacementFor(mine, GameSprite("ui_general", "panel_bg", 1)) == null, "and no other sprite is replaced");

                // On a loading screen: the picture decided when it began.
                LoadingPictures.InLoadingWindow = true;
                Check.That(LoadingPictures.ReplacementFor(mine, basic) == null, "before a screen has begun there is no decision, so the game's picture shows");

                LoadingPictures.BeginScreen(Snapshot(true, LoadingBackgroundMode.Mine));
                Check.That(LoadingPictures.ScreenUsesMine, "with only mine, a loading screen shows one of them");
                string first = NameOf(LoadingPictures.ReplacementFor(mine, basic));
                Check.That(first != null, "...a picture that was found (" + first + ")");
                Check.Equal(first, NameOf(LoadingPictures.ReplacementFor(mine, basic)), "...the same one every time it is drawn");

                var previous = -1;
                bool repeated = false;
                for (int i = 0; i < 100; i++)
                {
                    LoadingPictures.BeginScreen(Snapshot(true, LoadingBackgroundMode.Mine));
                    repeated |= LoadingPictures.ScreenPick == previous;
                    previous = LoadingPictures.ScreenPick;
                }
                Check.That(!repeated, "no loading screen repeats the picture before it when there is a choice");

                LoadingPictures.BeginScreen(Snapshot(true, LoadingBackgroundMode.Game));
                Check.That(!LoadingPictures.ScreenUsesMine && LoadingPictures.ReplacementFor(mine, basic) == null,
                    "with the game's only, none of mine is used");
                LoadingPictures.BeginScreen(Snapshot(false, LoadingBackgroundMode.Mine));
                Check.That(!LoadingPictures.ScreenUsesMine, "with custom backgrounds off, none is used");

                int mineScreens = 0;
                int gameScreens = 0;
                for (int i = 0; i < 400; i++)
                {
                    LoadingPictures.BeginScreen(Snapshot(true, LoadingBackgroundMode.Both));
                    if (LoadingPictures.ScreenUsesMine)
                    {
                        mineScreens++;
                    }
                    else
                    {
                        gameScreens++;
                    }
                }
                Check.That(mineScreens > 120 && gameScreens > 120, "mixed shows both, about half each (" + mineScreens + " mine, " + gameScreens + " the game's of 400)");

                // No pictures at all: nothing to show, whatever the mode.
                Directory.Delete(loading, true);
                BackgroundImages.Forget();
                LoadingPictures.BeginScreen(Snapshot(true, LoadingBackgroundMode.Mine));
                Check.That(!LoadingPictures.ScreenUsesMine, "with an empty folder the game's pictures show");
            }
            finally
            {
                LoadingPictures.InLoadingWindow = false;
                LoadingPictures.Reset();
                CleanUp(root);
            }
        }

        // ---- textures are only made on the main thread --------------------------------------------------------------
        private static void MainThread()
        {
            string root = NewRoot();
            try
            {
                string loading = Path.Combine(root, "Loading");
                Directory.CreateDirectory(loading);
                foreach (string name in new[] { "a.png", "b.png", "c.png" })
                {
                    File.WriteAllText(Path.Combine(loading, name), "x");
                }

                BackgroundImages.RootOverride = root;
                BackgroundImages.Loader = path => FakeSprite("t:" + Path.GetFileName(path));
                BackgroundImages.Forget();

                BackgroundImages.Tick(Snapshot(true, LoadingBackgroundMode.Game));
                Check.Equal(0, BackgroundImages.LoadedCount, "with the game's loading pictures only, none of the player's are loaded ahead");

                // (The tick above made this thread the main one.) A picture that has not been loaded can't be loaded from another thread.
                FileSprite fromWorker = null;
                RunOnAnotherThread(() => fromWorker = BackgroundImages.Pick(BackgroundKind.Loading, 0));
                Check.That(fromWorker == null && BackgroundImages.LoadedCount == 0, "another thread can't create a texture, so a picture not yet loaded isn't used");

                FileSprite fromMain = BackgroundImages.Pick(BackgroundKind.Loading, 0);
                Check.That(fromMain != null && BackgroundImages.LoadedCount == 1, "the main thread can");
                RunOnAnotherThread(() => fromWorker = BackgroundImages.Pick(BackgroundKind.Loading, 0));
                Check.That(ReferenceEquals(fromWorker, fromMain), "...and then any thread can use it");

                BackgroundImages.Forget();
                BackgroundImages.Tick(Snapshot(true, LoadingBackgroundMode.Mine));
                Check.Equal(1, BackgroundImages.LoadedCount, "ticking loads the loading pictures ahead of time, one at a time");
                BackgroundImages.Tick(Snapshot(true, LoadingBackgroundMode.Mine));
                Check.Equal(1, BackgroundImages.LoadedCount, "...not faster than a few a second");
            }
            finally
            {
                CleanUp(root);
            }
        }

        private static void RunOnAnotherThread(Action action)
        {
            var thread = new Thread(() => action());
            thread.Start();
            thread.Join();
        }

        private static string NewRoot()
        {
            return Path.Combine(Path.GetTempPath(), "customisable-ui-test-backgrounds-" + Guid.NewGuid().ToString("N"));
        }

        private static void CleanUp(string root)
        {
            BackgroundImages.RootOverride = null;
            BackgroundImages.Loader = null;
            BackgroundImages.Forget();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
