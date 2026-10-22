using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// Lays out every screen and popup (LayoutCatalog) on every device (Devices) and checks the rules of LayoutCheck:
    /// inside the safe area, inside the parent, no overlapping buttons, 44 pt touch targets, text that fits.
    /// </summary>
    public class LayoutTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var screen in Devices.All)
            foreach (var (name, _) in LayoutCatalog.All())
                yield return new TestCaseData(screen.Name, name).SetName($"{name} on {screen.Name}");
        }

        static UiLayout Build(string device, string layout)
        {
            var screen = Devices.All.Single(s => s.Name == device);
            var build = LayoutCatalog.All().Single(l => l.name == layout).build;
            return build(new CanvasFit(screen));
        }

        [TestCaseSource(nameof(Cases))]
        public void KeepsTheRules(string device, string layout)
        {
            var L = Build(device, layout);
            var problems = LayoutCheck.Problems(L);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void TheRequiredScreensAreInTheMatrix()
        {
            foreach (string size in Devices.Required)
                Assert.That(Devices.All.Any(s => s.Name.Contains(size)), size);
        }

        [Test]
        public void EveryLayoutHasItsMainButtons()
        {
            var fit = new CanvasFit(Devices.All[2]);
            var farm = Screens.Farm(fit, new FarmHud());
            foreach (string id in new[] { "farm.play", "farm.coins", "farm.stars", "farm.crystals", "farm.lives", "farm.tasks", "farm.events", "farm.orders", "farm.shop", "farm.settings" })
                Assert.That(farm.Find(id), Is.Not.Null, id);
            var level = Screens.Level(fit, new LevelHud());
            foreach (string id in new[] { "level.pause", "level.moves.value", "level.goal0", "level.board", "level.booster0", "level.booster3" })
                Assert.That(level.Find(id), Is.Not.Null, id);
        }

        [Test]
        public void ThePlayButtonIsBigAndAtTheBottomOfTheSafeArea()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                var play = Screens.Farm(fit, new FarmHud { Level = 12 }).Find("farm.play");
                Assert.That(play.Rect.Y - fit.Safe.Y, Is.EqualTo(Tokens.Margin + Tokens.Gap).Within(0.5f), screen.Name);
                Assert.That(play.Rect.CenterX, Is.EqualTo(fit.Safe.CenterX).Within(0.5f), screen.Name);
                Assert.That(play.Rect.W, Is.GreaterThanOrEqualTo(Tokens.PlayMinWidth - 0.5f), screen.Name);
                Assert.That(play.Find("farm.play.label").Text.Value, Is.EqualTo("Играть 12"));
            }
        }

        [Test]
        public void TheBoardFillsTheWidthOnPhonesAndStaysSquareCelled()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                var board = Screens.Level(fit, new LevelHud()).Find("level.board");
                Assert.That(board.Rect.W, Is.EqualTo(board.Cell * 9).Within(0.01f));
                Assert.That(board.Rect.H, Is.EqualTo(board.Cell * 9).Within(0.01f));
                if (fit.Match == 0 && fit.Height / fit.Width >= 1.7f && board.Cell < Tokens.MaxCell)
                    Assert.That(board.Rect.W, Is.GreaterThan(fit.Safe.W - 2 * Tokens.BoardMargin - 9), screen.Name);
            }
        }

        [Test]
        public void TopRowsHangFromTheSafeTopEvenUnderACutOut()
        {
            var notch = new CanvasFit(Devices.All.Single(s => s.Name.Contains("1170×2532")));
            var farm = Screens.Farm(notch, new FarmHud());
            var coins = farm.Find("farm.coins");
            Assert.That(notch.Safe.YMax - coins.Rect.YMax, Is.EqualTo(Tokens.Margin).Within(0.5f));
            Assert.That(farm.Find("farm.world").Rect.YMax, Is.EqualTo(notch.Height).Within(0.5f), "the farm goes under the cut-out");
        }

        [Test]
        public void ABrokenLayoutIsCaught()
        {
            var fit = new CanvasFit(Devices.All[0]);
            var L = Screens.Farm(fit, new FarmHud());
            // move the shop over the orders button and push settings out of the safe area
            L.Find("farm.shop").OffsetMin = L.Find("farm.orders").OffsetMin;
            L.Find("farm.shop").OffsetMax = L.Find("farm.orders").OffsetMax;
            L.Find("farm.settings").OffsetMax = new V2(500, 500);
            L.Find("farm.play.label").Text.Value = "Играть прямо сейчас без остановки до самого вечера";
            L.Root.Resolve(fit.Canvas);
            TextFit.FitAll(L.Root, fit.Scale);
            var problems = LayoutCheck.Problems(L);
            Assert.That(problems.Any(p => p.Contains("overlap")), string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("safe area")), string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("does not fit")), string.Join("\n", problems));
        }

        [Test]
        public void SmallTouchTargetsAreCaught()
        {
            var fit = new CanvasFit(Devices.All[0]);
            var L = Screens.Level(fit, new LevelHud());
            var pause = L.Find("level.pause");
            pause.LeftMiddle(0, 0, 90, 90);
            L.Root.Resolve(fit.Canvas);
            Assert.That(LayoutCheck.Problems(L).Any(p => p.Contains("level.pause") && p.Contains("finger")));
        }

        /// <summary>Prints the matrix: per device the smallest gap, button and text, for the report.</summary>
        [Test]
        public void Report()
        {
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                float minGap = float.MaxValue, minTouch = float.MaxValue, minText = float.MaxValue, minCell = float.MaxValue;
                int problems = 0;
                foreach (var (name, build) in LayoutCatalog.All())
                {
                    var L = build(fit);
                    problems += LayoutCheck.Problems(L).Count;
                    minGap = Math.Min(minGap, LayoutCheck.MinGap(L));
                    foreach (var n in L.Root.All().Where(n => !n.InheritsFlag(NodeFlags.Hidden)))
                    {
                        if (n.Kind == NodeKind.Board)
                            minCell = Math.Min(minCell, fit.ToPoints(n.Cell));
                        else if (n.Interactive && n.Kind != NodeKind.Scroll)
                            minTouch = Math.Min(minTouch, fit.ToPoints(n.Rect.MinSide));
                        if (n.Text != null)
                            minText = Math.Min(minText, fit.ToPoints(n.Text.FittedSize));
                    }
                }
                TestContext.Out.WriteLine($"{screen.Name,-40} canvas {fit.Width:0}×{fit.Height:0} match {fit.Match} " +
                                          $"safe {fit.Safe.W:0}×{fit.Safe.H:0}  gap ≥ {minGap:0} u, touch ≥ {minTouch:0.#} pt, " +
                                          $"cell ≥ {minCell:0.#} pt, text ≥ {minText:0.#} pt, problems {problems}");
                Assert.That(problems, Is.Zero, screen.Name);
            }
        }

        /// <summary>Writes an SVG wireframe of every layout on every device: the CI uploads them as an artifact.</summary>
        [Test]
        public void WritesPreviews()
        {
            string dir = Environment.GetEnvironmentVariable("UI_PREVIEW_DIR");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "previews");
            Directory.CreateDirectory(dir);
            int files = 0;
            foreach (var screen in Devices.All)
            {
                var fit = new CanvasFit(screen);
                foreach (var (name, build) in LayoutCatalog.All())
                {
                    File.WriteAllText(Path.Combine(dir, $"{name}_{Devices.Slug(screen)}.svg"), SvgPreview.Render(build(fit)));
                    files++;
                }
            }
            TestContext.Out.WriteLine($"{files} previews in {Path.GetFullPath(dir)}");
            Assert.That(files, Is.GreaterThan(0));
        }
    }
}
