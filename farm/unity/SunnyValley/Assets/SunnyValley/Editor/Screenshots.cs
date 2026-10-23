using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SunnyValley.Core.Meta;
using SunnyValley.Game;
using SunnyValley.Game.Economy;
using SunnyValley.Game.Match3View;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// Pictures of every screen and popup (LayoutCatalog) on every test screen (Devices), rendered by the real uGUI
    /// objects into a texture of the phone's size, with the parts outside the safe area tinted red. The farm economy
    /// of chapter 2 is pictured from a debug save as well (EconomyDebug: chapter 1 restored, fields growing,
    /// workshops busy, orders on the board): the farm screen and every window over it, filled by the real rules.
    /// Menu «Солнечная долина → Снимки интерфейса», or in batch mode (CI, farm-android.yml)
    /// <c>-executeMethod SunnyValley.EditorTools.Screenshots.CaptureFromCommandLine [-screenshotsPath dir]</c>.
    /// Needs a graphics device: under -nographics there is nothing to render with, and the method says so and fails
    /// (the layout itself is still guarded by farm/ui-tests and their SVG wireframes).
    /// </summary>
    public static class Screenshots
    {
        [MenuItem("Солнечная долина/Снимки интерфейса")]
        public static void FromMenu()
        {
            int n = Capture("Screenshots", Devices.All);
            EditorUtility.RevealInFinder(Path.GetFullPath("Screenshots"));
            Debug.Log($"Снимков: {n}");
        }

        public static void CaptureFromCommandLine()
        {
            string dir = BuildAndroid.Argument("-screenshotsPath");
            if (dir == null)
            {
                // unity-builder passes -customBuildPath build/<target>/<name>: the pictures go next to it
                string custom = BuildAndroid.Argument("-customBuildPath");
                dir = custom != null ? Path.Combine(Path.GetDirectoryName(custom) ?? ".", "screens") : "Screenshots";
            }
            int n = 0;
            try
            {
                n = Capture(dir, Devices.All);
            }
            catch (Exception e)
            {
                Debug.LogError("Снимки не получились: " + e);
            }
            Debug.Log($"Снимков: {n} в {Path.GetFullPath(dir)}");
            EditorApplication.Exit(n > 0 ? 0 : 1);
        }

        public static int Capture(string dir, IEnumerable<ScreenSpec> devices)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("Нет графического устройства (Unity запущен с -nographics): снимки делать нечем");
                return 0;
            }
            Directory.CreateDirectory(dir);
            int count = 0;
            var economy = EconomyShots().ToList();
            foreach (var device in devices)
            {
                foreach (var (name, build) in LayoutCatalog.All())
                {
                    string file = Path.Combine(dir, $"{name}_{Devices.Slug(device)}.png");
                    Render(device, name, build, file);
                    count++;
                }
                // real levels in the middle of a game, one move caught mid-animation (pops, streaks, falls)
                foreach (var (name, level, moves, into) in MidGame)
                {
                    string file = Path.Combine(dir, $"{name}_{Devices.Slug(device)}.png");
                    Render(device, name, null, file, root => BoardCapture.LevelMidGame(root.ScreenLayer, root.Fit, level, moves, into));
                    count++;
                }
                // the farm economy from the debug save: the farm, then each window over it
                foreach (var (name, under, build) in economy)
                {
                    string file = Path.Combine(dir, $"{name}_{Devices.Slug(device)}.png");
                    Render(device, name, null, file, root =>
                    {
                        var farm = UiView.Build(under(root.Fit), root.ScreenLayer);
                        return build == null ? farm : UiView.Build(build(root.Fit), root.PopupLayer);
                    });
                    count++;
                }
            }
            return count;
        }

        /// <summary>The moment the debug save is pictured at: fixed, so the timers read the same on every run.</summary>
        const long EconomyNow = 1_800_000_000;

        /// <summary>
        /// The farm economy's pictures: the farm screen with its level strip and badges, every window of the debug save
        /// (the crop picker for an empty plot, the mill and the bakery, the order board with a card picked), a speed-up
        /// question and «Пока вас не было» with «Собрать всё». Popups lie over the farm screen (build), the farm alone
        /// has none.
        /// </summary>
        static IEnumerable<(string name, Func<CanvasFit, UiLayout> under, Func<CanvasFit, UiLayout> build)> EconomyShots()
        {
            var data = GameState.LoadData();
            long now = EconomyNow;
            var game = EconomyDebug.Game(data, now);
            var away = game.BuildAwaySummary(now);
            var hud = EconomyViews.Hud(new FarmHud
            {
                Coins = game.Coins, Stars = game.Stars, Crystals = game.Crystals, Lives = game.Lives(now), MaxLives = data.Balance.Lives.Max,
                NextLifeSeconds = game.NextLifeIn(now), Level = game.NextLevel,
            }, game, now);
            Func<CanvasFit, UiLayout> farm = f => Screens.Farm(f, hud);
            yield return ("economy_farm", farm, null);

            var views = new List<EconomyView>();
            int empty = game.Plots.ToList().FindIndex(p => p.Crop == null);
            foreach (var (window, arg) in new[]
                     {
                         (EconomyViews.Fields, null), (EconomyViews.Crops, Math.Max(0, empty).ToString()), (EconomyViews.Animals, null),
                         (EconomyViews.Workshops, null), (EconomyViews.Workshop, "mill"), (EconomyViews.Workshop, "bakery"), (EconomyViews.Barn, null),
                         (EconomyViews.Orders, null), (EconomyViews.Truck, null), (EconomyViews.Town, null),
                     })
                views.Add(EconomyViews.Build(window, arg, game, now));
            foreach (var v in views)
            {
                var info = v.Info;
                string name = "economy_" + v.Window + (v.Window == EconomyViews.Workshop ? "_" + v.Arg : "");
                yield return (name, farm, f => Popups.Economy(f, info));
            }
            var ask = views.SelectMany(v => v.Actions.Values).FirstOrDefault(a => a.Confirm != null);
            if (ask != null)
                yield return ("economy_speedup", farm, f => Popups.Confirm(f, ask.Confirm));
            if (!away.IsEmpty)
            {
                var info = new AwayInfo { Away = "Вас не было " + Texts.Duration(away.AwaySeconds), Lines = away.Lines, CollectAll = away.Ready.Count > 0 };
                yield return ("economy_away", farm, f => Popups.Away(f, info));
            }
        }

        /// <summary>What a popup is shown over, for context: the level's popups over the level, the rest over the farm.</summary>
        static Func<CanvasFit, UiLayout> Under(string name)
        {
            if (!name.StartsWith("popup") && name != "toast")
                return null;
            bool overLevel = name.Contains("win") || name.Contains("lose") || name.Contains("settings");
            if (overLevel)
                return f => Screens.Level(f, new LevelHud());
            return f => Screens.Farm(f, new FarmHud());
        }

        /// <summary>Level screens with real levels: crates (7), chicks (13), honey and baskets (34), portals (37).</summary>
        static readonly (string name, int level, int moves, float into)[] MidGame =
        {
            ("level_007_play", 7, 4, 0.3f), ("level_013_play", 13, 6, 0.55f), ("level_034_play", 34, 5, 0.25f), ("level_037_play", 37, 3, 0f),
        };

        static void Render(ScreenSpec spec, string name, Func<CanvasFit, UiLayout> build, string file, Func<UiRoot, UiView> custom = null)
        {
            var rt = new RenderTexture(spec.Width, spec.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var camGo = new GameObject("capture camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = 1 << 5;
            cam.targetTexture = rt;
            cam.enabled = false;
            var root = UiRoot.CreateForCapture(spec, cam);
            try
            {
                var under = custom == null ? Under(name) : null;
                if (under != null)
                    Fill(UiView.Build(under(root.Fit), root.ScreenLayer));
                bool popup = under != null;
                var view = custom != null ? custom(root)
                    : UiView.Build(build(root.Fit), popup ? (name == "toast" ? root.OverlayLayer : root.PopupLayer) : root.ScreenLayer);
                Fill(view);
                MarkUnsafe(root);

                Canvas.ForceUpdateCanvases();
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(spec.Width, spec.Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, spec.Width, spec.Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(root.gameObject);
                UnityEngine.Object.DestroyImmediate(camGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Puts a real level's pieces on a board, if the view has one and it is still empty.</summary>
        static void Fill(UiView view)
        {
            var board = view.Rect("level.board")?.GetComponent<BoardView>();
            if (board != null && !board.HasGame)
                BoardCapture.Fill(view);
        }

        /// <summary>Tints what lies outside the safe area (cut-out, status bar, gesture bar) in translucent red.</summary>
        static void MarkUnsafe(UiRoot root)
        {
            var fit = root.Fit;
            var s = fit.Safe;
            Band(root.OverlayLayer, 0, s.Y);
            Band(root.OverlayLayer, s.YMax, fit.Height - s.YMax);
        }

        static void Band(RectTransform parent, float y, float h)
        {
            if (h <= 0.5f)
                return;
            var go = new GameObject("unsafe", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(0, y);
            rt.offsetMax = new Vector2(0, y + h);
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 0, 0, 0.3f);
            img.raycastTarget = false;
        }
    }
}
