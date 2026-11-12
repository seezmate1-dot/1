using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Game.Farm;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// The farm map's interaction model (Game/Economy/Farm, docs/FARM_UX.md) without Unity: fingers → gestures (a tap
    /// against a drag, a double tap, a pinch), the camera (rubber edges, glide, clamped zoom), where the working
    /// farm stands on the real map, what a tap and a dragged tool do through the real MetaGame.
    /// </summary>
    public class FarmInteractionTests
    {
        const long T0 = 19700L * 86400 + 8 * 3600;

        static MetaData data;
        static FarmLayoutDef layout;

        static string Dir([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetDirectoryName(here);

        static string Meta(string file) =>
            File.ReadAllText(Path.Combine(Dir(), "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta", file));

        static MetaData Data => data ??= MetaData.Parse(Meta("balance.json"), Meta("chapter1.json"));
        static FarmLayoutDef Layout => layout ??= FarmLayoutDef.Parse(Meta("chapter1_layout.json"));

        static MetaGame Fresh()
        {
            var g = MetaGame.NewGame(Data, T0, 42);
            g.State.Wallet.Stars += g.StarsToFinishChapter();
            while (!g.ChapterFinished)
                Assert.That(g.CompleteTask(T0, g.AvailableTasks()[0].Id).Ok, Is.True);
            g.TakeEvents();
            return g;
        }

        static MetaGame Rich()
        {
            var g = Fresh();
            g.Grant(T0, new Reward { Xp = (int)(g.XpForLevel(Data.MaxLevel) - g.Xp) });
            g.State.Wallet.Coins = 99_999_999;
            foreach (var d in Data.Balance.Town.Houses)
                for (int i = 0; i < 40; i++)
                    g.Build(T0, d.Id);
            foreach (var w in Data.Balance.Workshops)
                g.BuildWorkshop(T0, w.Id);
            foreach (var h in Data.Balance.AnimalHouses)
                g.BuildAnimalHouse(T0, h.Id);
            foreach (var a in Data.Balance.Animals)
                while (g.BuyAnimal(T0, a.Id).Ok) { }
            foreach (var t in Data.Balance.Trees)
                while (g.BuyTree(T0, t.Id).Ok) { }
            while (g.BuyPlot(T0).Ok) { }
            g.TakeEvents();
            return g;
        }

        // ------------------------------------------------------------------ gestures

        static TouchGestures Touch() => new TouchGestures { UnitsPerDp = 3 };

        static List<GestureKind> Kinds(IEnumerable<Gesture> g) => g.Select(e => e.Kind).ToList();

        [Test]
        public void SlipInsideTheSlopIsATap()
        {
            var t = Touch();
            var all = new List<Gesture>();
            all.AddRange(t.Down(0, new P2(100, 100), 0));
            all.AddRange(t.Move(0, new P2(118, 110), 0.05f)); // 20.6 units < 30 (10 dp)
            all.AddRange(t.Up(0, new P2(118, 110), 0.1f));
            Assert.That(Kinds(all), Is.EqualTo(new[] { GestureKind.Press, GestureKind.Tap }));
        }

        [Test]
        public void MovePastTheSlopIsADragAndNeverATap()
        {
            var t = Touch();
            var all = new List<Gesture>();
            all.AddRange(t.Down(0, new P2(100, 100), 0));
            all.AddRange(t.Move(0, new P2(140, 100), 0.02f));
            all.AddRange(t.Move(0, new P2(160, 100), 0.04f));
            // back to where it started: still a drag
            all.AddRange(t.Move(0, new P2(100, 100), 0.06f));
            all.AddRange(t.Up(0, new P2(100, 100), 0.5f));
            Assert.That(Kinds(all), Is.EqualTo(new[]
            {
                GestureKind.Press, GestureKind.PressEnd, GestureKind.PanStart, GestureKind.Pan, GestureKind.Pan, GestureKind.PanEnd,
            }));
            // the jump over the slop is swallowed: the first delta is from where the drag began
            var pans = all.Where(e => e.Kind == GestureKind.Pan).ToList();
            Assert.That(pans[0].Delta.X, Is.EqualTo(20).Within(0.01));
        }

        [Test]
        public void TheSlopIsPhysical()
        {
            // the same 25 pixels: a drag on a low density screen, a slip on a dense one
            foreach (var (perDp, drag) in new[] { (1.5f, true), (4f, false) })
            {
                var t = new TouchGestures { UnitsPerDp = perDp };
                t.Down(0, new P2(0, 0), 0);
                var e = t.Move(0, new P2(25, 0), 0.02f);
                Assert.That(e.Any(x => x.Kind == GestureKind.PanStart), Is.EqualTo(drag));
            }
        }

        [Test]
        public void TwoTapsCloseTogetherAreADoubleTap()
        {
            var t = Touch();
            t.Down(0, new P2(100, 100), 0);
            Assert.That(Kinds(t.Up(0, new P2(100, 100), 0.08f)), Is.EqualTo(new[] { GestureKind.Tap }));
            t.Down(0, new P2(130, 110), 0.2f);
            Assert.That(Kinds(t.Up(0, new P2(130, 110), 0.27f)), Is.EqualTo(new[] { GestureKind.DoubleTap }));
            // a third is a new first tap
            t.Down(0, new P2(130, 110), 0.4f);
            Assert.That(Kinds(t.Up(0, new P2(130, 110), 0.45f)), Is.EqualTo(new[] { GestureKind.Tap }));
            // too late, or too far: two taps
            t.Down(0, new P2(130, 110), 1.0f);
            Assert.That(Kinds(t.Up(0, new P2(130, 110), 1.05f)), Is.EqualTo(new[] { GestureKind.Tap }));
            t.Down(0, new P2(500, 110), 1.1f);
            Assert.That(Kinds(t.Up(0, new P2(500, 110), 1.15f)), Is.EqualTo(new[] { GestureKind.Tap }));
        }

        [Test]
        public void HeldFingerIsALongPressNotATap()
        {
            var t = Touch();
            t.Down(0, new P2(100, 100), 0);
            Assert.That(t.Tick(0.3f), Is.Empty);
            Assert.That(Kinds(t.Tick(0.5f)), Is.EqualTo(new[] { GestureKind.PressEnd, GestureKind.LongPress }));
            Assert.That(t.Tick(0.6f), Is.Empty);
            Assert.That(t.Up(0, new P2(100, 100), 0.7f), Is.Empty);
        }

        [Test]
        public void SecondFingerPinchesAndTheOneLeftDragsOnWithoutATap()
        {
            var t = Touch();
            t.Down(0, new P2(400, 500), 0);
            var e = t.Down(1, new P2(600, 500), 0.05f);
            Assert.That(Kinds(e), Is.EqualTo(new[] { GestureKind.PressEnd, GestureKind.PinchStart }));
            var p = t.Move(1, new P2(800, 500), 0.1f).Single();
            Assert.That(p.Kind, Is.EqualTo(GestureKind.Pinch));
            Assert.That(p.Scale, Is.EqualTo(2).Within(0.001));
            Assert.That(p.Point.X, Is.EqualTo(600).Within(0.01));
            Assert.That(p.Delta.X, Is.EqualTo(100).Within(0.01));
            Assert.That(Kinds(t.Up(1, new P2(800, 500), 0.2f)), Is.EqualTo(new[] { GestureKind.PinchEnd, GestureKind.PanStart }));
            var pan = t.Move(0, new P2(410, 500), 0.25f).Single();
            Assert.That(pan.Kind, Is.EqualTo(GestureKind.Pan));
            Assert.That(pan.Delta.X, Is.EqualTo(10).Within(0.01));
            Assert.That(Kinds(t.Up(0, new P2(410, 500), 0.3f)), Is.EqualTo(new[] { GestureKind.PanEnd }));
            Assert.That(t.Fingers, Is.EqualTo(0));
        }

        [Test]
        public void FingersTooCloseDoNotZoom()
        {
            var t = Touch();
            t.Down(0, new P2(400, 500), 0);
            t.Down(1, new P2(420, 500), 0.05f);
            Assert.That(t.Move(1, new P2(440, 500), 0.1f).Single().Scale, Is.EqualTo(1));
        }

        [Test]
        public void FlickHasSpeedAndARestedFingerHasNone()
        {
            var t = Touch();
            t.Down(0, new P2(0, 0), 0);
            for (int i = 1; i <= 6; i++)
                t.Move(0, new P2(i * 40, 0), i * 0.016f);
            var end = t.Up(0, new P2(240, 0), 6 * 0.016f + 0.001f).Single();
            Assert.That(end.Delta.X, Is.EqualTo(2500).Within(200));

            t.Down(0, new P2(0, 0), 1);
            for (int i = 1; i <= 6; i++)
                t.Move(0, new P2(i * 40, 0), 1 + i * 0.016f);
            end = t.Up(0, new P2(240, 0), 1.5f).Single();
            Assert.That(end.Delta.Length, Is.EqualTo(0));
        }

        [Test]
        public void CancelledTouchTapsNothing()
        {
            var t = Touch();
            t.Down(0, new P2(0, 0), 0);
            Assert.That(Kinds(t.Cancel(0, 0.05f)), Is.EqualTo(new[] { GestureKind.PressEnd }));
        }

        // ------------------------------------------------------------------ camera

        static FarmCamera Camera() => new FarmCamera(5600, 4400) { ViewW = 1080, ViewH = 1920, InsetTop = 200, InsetBottom = 400 };

        [Test]
        public void PinchZoomStaysInItsRangeAndKeepsThePointUnderTheFingers()
        {
            var c = Camera();
            var at = new P2(200, -300);
            var before = c.ViewToMap(at);
            c.ZoomAbout(at, 1.2f);
            var after = c.ViewToMap(at);
            Assert.That(after.X, Is.EqualTo(before.X).Within(0.01));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(0.01));

            for (int i = 0; i < 200; i++)
                c.ZoomAbout(at, 1.1f);
            Assert.That(c.Zoom, Is.LessThanOrEqualTo(FarmCamera.MaxZoom * FarmCamera.ZoomStretch + 0.001f));
            Assert.That(c.Zoom, Is.GreaterThan(FarmCamera.MaxZoom));
            c.Release(new P2(0, 0));
            for (int i = 0; i < 120; i++)
                c.Step(1 / 60f);
            Assert.That(c.Zoom, Is.EqualTo(FarmCamera.MaxZoom).Within(0.002));

            for (int i = 0; i < 200; i++)
                c.ZoomAbout(at, 0.9f);
            Assert.That(c.Zoom, Is.GreaterThanOrEqualTo(c.LowestZoom / FarmCamera.ZoomStretch - 0.001f));
            c.Release(new P2(0, 0));
            for (int i = 0; i < 120; i++)
                c.Step(1 / 60f);
            Assert.That(c.Zoom, Is.EqualTo(c.LowestZoom).Within(0.002));
            // the map covers the view at the lowest zoom
            Assert.That(5600 * c.LowestZoom, Is.GreaterThanOrEqualTo(1080 - 0.5f));
            Assert.That(4400 * c.LowestZoom, Is.GreaterThanOrEqualTo(1920 - 0.5f));
            Assert.That(c.Moving, Is.False);
        }

        [Test]
        public void WheelZoomIsClampedHard()
        {
            var c = Camera();
            for (int i = 0; i < 100; i++)
                c.ZoomAbout(new P2(0, 0), 1.3f, false);
            Assert.That(c.Zoom, Is.EqualTo(FarmCamera.MaxZoom));
        }

        [Test]
        public void DragPastTheEdgeStretchesLessAndLessAndSpringsBack()
        {
            var c = Camera();
            c.Bounds(c.Zoom, out _, out float maxX, out _, out _);
            c.Position = new P2(maxX, 0);
            float last = maxX, lastStep = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                c.DragBy(new P2(30, 0));
                float step = c.Position.X - last;
                Assert.That(step, Is.LessThanOrEqualTo(lastStep + 0.001f));
                lastStep = step;
                last = c.Position.X;
            }
            Assert.That(c.Position.X, Is.GreaterThan(maxX + 50));
            Assert.That(c.Position.X, Is.LessThanOrEqualTo(maxX + FarmCamera.MaxOverscroll + 0.01f));
            // pulling back in is not resisted
            float x = c.Position.X;
            c.DragBy(new P2(-30, 0));
            Assert.That(c.Position.X, Is.EqualTo(x - 30).Within(0.01));
            c.Release(new P2(4000, 0));
            for (int i = 0; i < 180 && c.Step(1 / 60f); i++) { }
            Assert.That(c.Moving, Is.False);
            Assert.That(c.Position.X, Is.EqualTo(maxX).Within(0.01));
        }

        [Test]
        public void FlickGlidesSlowsAndStopsInsideTheLimits()
        {
            var c = Camera();
            c.Position = new P2(0, 0);
            c.Release(new P2(-1500, 600));
            float speed = c.Velocity.Length, travelled = 0;
            var p0 = c.Position;
            int frames = 0;
            while (c.Step(1 / 60f) && frames++ < 600)
            {
                Assert.That(c.Velocity.Length, Is.LessThanOrEqualTo(speed + 0.001f));
                speed = c.Velocity.Length;
            }
            travelled = P2.Distance(p0, c.Position);
            Assert.That(frames, Is.InRange(20, 200));
            Assert.That(travelled, Is.InRange(150, 600));
            var clamped = c.ClampPosition(c.Position, c.Zoom);
            Assert.That(P2.Distance(clamped, c.Position), Is.LessThan(0.01));

            // a wild flick into an edge ends on the edge
            c.Bounds(c.Zoom, out _, out float edge, out _, out _);
            c.Position = new P2(edge - 300, 0);
            c.Release(new P2(99999, 0));
            Assert.That(c.Velocity.Length, Is.LessThanOrEqualTo(FarmCamera.MaxSpeed + 0.01f));
            for (int i = 0; i < 900 && c.Step(1 / 60f); i++) { }
            c.Bounds(c.Zoom, out _, out float maxX, out _, out _);
            Assert.That(c.Position.X, Is.EqualTo(maxX).Within(0.01));
            // a finger stops the glide
            c.Release(new P2(-2000, 0));
            c.Grab();
            Assert.That(c.Step(1 / 60f), Is.False);
        }

        [Test]
        public void InsetsLetEveryMapPointOutFromUnderTheHud()
        {
            var c = Camera();
            foreach (var z in new[] { c.LowestZoom, 0.8f, 1.25f, 1.6f })
            {
                c.Zoom = z;
                foreach (var m in new[] { new P2(0, 0), new P2(5600, 0), new P2(0, 4400), new P2(5600, 4400), new P2(2800, 70) })
                {
                    c.Position = c.Target(m, z);
                    var v = c.MapToView(m);
                    Assert.That(v.Y, Is.GreaterThanOrEqualTo(-960 + 400 - 0.5f), "under the play button at zoom " + z + " " + m);
                    Assert.That(v.Y, Is.LessThanOrEqualTo(960 - 200 + 0.5f), "under the plates at zoom " + z + " " + m);
                    var back = c.ViewToMap(v);
                    Assert.That(P2.Distance(back, m), Is.LessThan(0.05));
                }
            }
        }

        [Test]
        public void DoubleTapGoesCloseAndBack()
        {
            var c = Camera();
            c.Zoom = FarmCamera.StartZoom;
            var at = new P2(100, 100);
            var m = c.ViewToMap(at);
            c.DoubleTapTarget(at, out var pos, out float zoom);
            Assert.That(zoom, Is.EqualTo(FarmCamera.CloseZoom));
            c.Position = pos;
            c.Zoom = zoom;
            Assert.That(P2.Distance(c.ViewToMap(at), m), Is.LessThan(0.5));
            c.DoubleTapTarget(at, out _, out zoom);
            Assert.That(zoom, Is.EqualTo(FarmCamera.StartZoom));
        }

        [Test]
        public void ToolNearTheEdgeSlidesTheMap()
        {
            var c = Camera();
            Assert.That(c.EdgePan(c.FreeCentre).Length, Is.EqualTo(0));
            Assert.That(c.EdgePan(new P2(-530, 0)).X, Is.GreaterThan(0));
            Assert.That(c.EdgePan(new P2(530, 0)).X, Is.LessThan(0));
            Assert.That(c.EdgePan(new P2(0, -960 + 400 + 20)).Y, Is.GreaterThan(0));
            Assert.That(c.EdgePan(new P2(0, 960 - 200 - 20)).Y, Is.LessThan(0));
        }

        // ------------------------------------------------------------------ the sites on the map

        [Test]
        public void EverySiteStandsOnTheMapAndNoneCoversAnother()
        {
            foreach (var g in new[] { Fresh(), EconomyDebug.Game(Data, T0), Rich() })
            {
                var map = FarmSiteMap.Build(g, Layout);
                Assert.That(map.Sites.Count, Is.GreaterThan(15));
                Assert.That(map.Sites.Select(s => s.Id).Distinct().Count(), Is.EqualTo(map.Sites.Count));
                foreach (var s in map.Sites)
                {
                    Assert.That(s.X - s.W / 2, Is.GreaterThanOrEqualTo(0), s.Id);
                    Assert.That(s.X + s.W / 2, Is.LessThanOrEqualTo(Layout.Map.W), s.Id);
                    Assert.That(s.Y, Is.GreaterThanOrEqualTo(0), s.Id);
                    Assert.That(s.Y + s.H, Is.LessThanOrEqualTo(Layout.Map.H), s.Id);
                    // its own middle finds it at every zoom
                    foreach (var z in new[] { 0.45f, 0.8f, 1.6f })
                        Assert.That(map.At(s.Centre, z), Is.SameAs(s), s.Id + " at zoom " + z);
                }
                for (int i = 0; i < map.Sites.Count; i++)
                {
                    for (int j = i + 1; j < map.Sites.Count; j++)
                    {
                        FarmSite a = map.Sites[i], b = map.Sites[j];
                        float ox = Math.Min(a.X + a.W / 2, b.X + b.W / 2) - Math.Max(a.X - a.W / 2, b.X - b.W / 2);
                        float oy = Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y);
                        // rows of animals and trees may lap a little (the near one is in front), never by half
                        bool covers = ox > Math.Min(a.W, b.W) * 0.5f && oy > Math.Min(a.H, b.H) * 0.5f;
                        Assert.That(covers, Is.False, a.Id + " over " + b.Id);
                    }
                }
            }
        }

        [Test]
        public void RichFarmShowsEverything()
        {
            var g = Rich();
            var map = FarmSiteMap.Build(g, Layout);
            Assert.That(map.Sites.Count(s => s.Kind == SiteKind.Plot), Is.EqualTo(g.Plots.Count));
            Assert.That(g.Plots.Count, Is.LessThanOrEqualTo(FarmSiteMap.PlotColumns * FarmSiteMap.PlotRows));
            Assert.That(map.Sites.Count(s => s.Kind == SiteKind.Tree), Is.EqualTo(g.State.Farm.Trees.Count));
            Assert.That(map.Sites.Count(s => s.Kind == SiteKind.Animal), Is.EqualTo(g.State.Farm.Animals.Count));
            Assert.That(map.Sites.Count(s => s.Kind == SiteKind.Workshop), Is.EqualTo(Data.Balance.Workshops.Count));
            Assert.That(map.Sites.Count(s => s.Kind == SiteKind.Pen), Is.EqualTo(Data.Balance.AnimalHouses.Count));
        }

        [Test]
        public void SignatureChangesWhenSomethingIsBoughtOrBuilt()
        {
            var g = Fresh();
            g.State.Wallet.Coins = 100000;
            string s0 = FarmSiteMap.SignatureOf(g);
            g.Plant(T0, 0, "wheat");
            Assert.That(FarmSiteMap.SignatureOf(g), Is.EqualTo(s0));
            Assert.That(g.BuyPlot(T0).Ok, Is.True);
            string s1 = FarmSiteMap.SignatureOf(g);
            Assert.That(s1, Is.Not.EqualTo(s0));
            Assert.That(g.BuyAnimal(T0, "chicken").Ok, Is.True);
            Assert.That(FarmSiteMap.SignatureOf(g), Is.Not.EqualTo(s1));
        }

        [Test]
        public void SmallThingsGetAFingerSizedTarget()
        {
            var g = Fresh();
            var map = FarmSiteMap.Build(g, Layout);
            var hen = map.Sites.First(s => s.Kind == SiteKind.Animal);
            // zoomed far out a hen is 46 canvas units: a finger 60 map units off its edge still gets it
            var near = new P2(hen.X, hen.Y + hen.H + 60);
            Assert.That(map.At(near, 0.35f), Is.SameAs(hen));
            Assert.That(map.At(near, 1.6f), Is.Null);
        }

        // ------------------------------------------------------------------ taps

        [Test]
        public void StatusFollowsThePlot()
        {
            var g = Fresh();
            var map = FarmSiteMap.Build(g, Layout);
            var plot = map.Find("plot.0");
            Assert.That(FarmSiteMap.Status(plot, g, T0).Phase, Is.EqualTo(SitePhase.Empty));
            Assert.That(g.Plant(T0, 0, "wheat").Ok, Is.True);
            var st = FarmSiteMap.Status(plot, g, T0 + 30);
            Assert.That(st.Phase, Is.EqualTo(SitePhase.Growing));
            Assert.That(st.Progress, Is.EqualTo(0.25f).Within(0.001));
            Assert.That(st.Left, Is.EqualTo(90));
            Assert.That(st.Stage, Is.EqualTo(1));
            Assert.That(FarmSiteMap.Status(plot, g, T0 + 100).Stage, Is.EqualTo(3));
            st = FarmSiteMap.Status(plot, g, T0 + 120);
            Assert.That(st.Phase, Is.EqualTo(SitePhase.Ready));
            Assert.That(st.Stage, Is.EqualTo(4));
            Assert.That(st.Attention, Is.True);
        }

        [Test]
        public void TapTakesWhatIsReadyAndOpensAMenuOtherwise()
        {
            var g = Fresh();
            var map = FarmSiteMap.Build(g, Layout);
            var plot = map.Find("plot.0");
            Assert.That(FarmMenus.Primary(plot, g, T0), Is.Null);
            var menu = FarmMenus.For(plot, g, T0);
            Assert.That(menu.Items.Count(i => i.Tool != null && i.Tool.Kind == ToolKind.Seed), Is.GreaterThanOrEqualTo(2));
            Assert.That(menu.Items.Count(i => i.Locked), Is.EqualTo(1), "one closed crop as a teaser");
            Assert.That(menu.Items.First(i => i.Locked).Reason, Does.Contain("уровне"));

            g.Plant(T0, 0, "wheat");
            Assert.That(FarmMenus.Primary(plot, g, T0 + 10), Is.Null);
            menu = FarmMenus.For(plot, g, T0 + 10);
            Assert.That(menu.Progress, Is.GreaterThan(0));
            Assert.That(menu.Items.Single().Action.Confirm, Is.Not.Null, "crystals are spent only after a question");

            int before = g.BarnCount("wheat");
            var take = FarmMenus.Primary(plot, g, T0 + 500);
            Assert.That(take, Is.Not.Null);
            Assert.That(take.Confirm, Is.Null);
            Assert.That(take.Run(T0 + 500).Ok, Is.True);
            Assert.That(g.BarnCount("wheat"), Is.EqualTo(before + 2));

            Assert.That(FarmMenus.Primary(map.Find("barn"), g, T0).Window, Is.EqualTo(EconomyViews.Barn));
            Assert.That(FarmMenus.Primary(map.Find("orders"), g, T0).Window, Is.EqualTo(EconomyViews.Orders));
        }

        [Test]
        public void HungryHenIsFedByATapAndTheFeedToolFollows()
        {
            var g = Fresh();
            var map = FarmSiteMap.Build(g, Layout);
            var hens = map.Sites.Where(s => s.Kind == SiteKind.Animal).ToList();
            Assert.That(hens.Count, Is.EqualTo(3));
            var before = FarmSiteMap.Status(hens[0], g, T0);
            Assert.That(before.Phase, Is.EqualTo(SitePhase.Hungry));
            Assert.That(FarmMenus.Primary(hens[0], g, T0).Run(T0).Ok, Is.True);
            var tool = FarmMenus.FollowUp(hens[0], before, map, g, T0);
            Assert.That(tool.Kind, Is.EqualTo(ToolKind.Feed));
            var stroke = new ToolStroke(g, map, tool);
            Assert.That(stroke.Sweep(hens[1].Centre, hens[2].Centre, T0).Count(h => h.Ok), Is.EqualTo(2));
            Assert.That(g.BarnCount("chicken_feed"), Is.EqualTo(0));
            Assert.That(FarmMenus.FollowUp(hens[2], before, map, g, T0), Is.Null, "nobody else is hungry");

            // fed: the timer and a speed-up; then the egg by a tap
            var m = FarmMenus.For(hens[0], g, T0 + 60);
            Assert.That(m.Sub, Is.Not.Null);
            Assert.That(FarmMenus.Primary(hens[0], g, T0 + 60), Is.Null);
            Assert.That(FarmMenus.Primary(hens[0], g, T0 + 1200).Run(T0 + 1200).Reward.Items["egg"], Is.EqualTo(1));
            // hungry again and no feed left: the menu shows the way to the mill
            m = FarmMenus.For(hens[0], g, T0 + 1200);
            Assert.That(m.Items[0].Enabled, Is.False);
            Assert.That(m.Items[0].Reason, Does.Contain("Нет корма"));
            Assert.That(m.Items[1].Action.Window, Is.EqualTo(EconomyViews.Workshop));
            Assert.That(FarmMenus.Primary(hens[0], g, T0 + 1200), Is.Null);
        }

        [Test]
        public void WorkshopMenuStartsRecipesAndSaysWhatIsMissing()
        {
            var g = Fresh();
            var map = FarmSiteMap.Build(g, Layout);
            var mill = map.Find("workshop.mill");
            var m = FarmMenus.For(mill, g, T0);
            Assert.That(m.Queue.Count, Is.EqualTo(3));
            var feed = m.Items.First(i => i.Name.StartsWith("Корм для кур") || i.Icon == "item.chicken_feed");
            Assert.That(feed.Enabled, Is.True);
            Assert.That(feed.Action.Run(T0).Ok, Is.True);
            Assert.That(FarmSiteMap.Status(mill, g, T0 + 10).Phase, Is.EqualTo(SitePhase.Growing));
            Assert.That(FarmMenus.For(mill, g, T0 + 10).Queue[0].State, Is.EqualTo(Layout_SlotRunning));
            // ready: a tap collects
            Assert.That(FarmSiteMap.Status(mill, g, T0 + 400).Phase, Is.EqualTo(SitePhase.Ready));
            Assert.That(FarmMenus.Primary(mill, g, T0 + 400).Run(T0 + 400).Ok, Is.True);
            // drain the wheat: the recipe greys and names what is short
            g.State.Barn["wheat"] = 1;
            feed = FarmMenus.For(mill, g, T0 + 400).Items.First(i => i.Icon == "item.chicken_feed");
            Assert.That(feed.Enabled, Is.False);
            Assert.That(feed.Reason, Does.Contain("пшеница 1/2"));
            Assert.That(m.Items.Last().Action.Window, Is.EqualTo(EconomyViews.Workshop));

            // a workshop not built: its price; a closed one: its level
            var bakery = FarmMenus.For(map.Find("workshop.bakery"), g, T0);
            Assert.That(bakery.Items.Single().Locked, Is.True);
            Assert.That(bakery.Items.Single().Label, Is.EqualTo("Ур. 2"));
        }

        const SunnyValley.Layout.SlotState Layout_SlotRunning = SunnyValley.Layout.SlotState.Running;

        [Test]
        public void EveryMenuOfEveryFarmIsSmallAndEveryButtonWorks()
        {
            foreach (var make in new Func<MetaGame>[] { Fresh, () => EconomyDebug.Game(Data, T0), Rich })
            {
                var g0 = make();
                var map0 = FarmSiteMap.Build(g0, Layout);
                foreach (var site in map0.Sites)
                {
                    var menu = FarmMenus.For(site, g0, T0);
                    Assert.That(menu.Items.Count, Is.LessThanOrEqualTo(FarmMenus.MaxItems), site.Id);
                    Assert.That(menu.Title, Is.Not.Empty, site.Id);
                    if (FarmMenus.Primary(site, g0, T0) == null && site.Kind != SiteKind.Pen)
                        Assert.That(menu.Items.Count, Is.GreaterThan(0), site.Id + ": a tap must offer something");
                    for (int i = 0; i < menu.Items.Count; i++)
                    {
                        // each button on its own copy of the game
                        var g = make();
                        var map = FarmSiteMap.Build(g, Layout);
                        var item = FarmMenus.For(map.Find(site.Id), g, T0).Items[i];
                        Assert.That(item.Icon, Is.Not.Null, site.Id);
                        if (!item.Enabled)
                        {
                            Assert.That(item.Reason, Is.Not.Empty, site.Id + " " + item.Name + ": a disabled button says why");
                            continue;
                        }
                        Assert.That(item.Tool != null || item.Action != null, Is.True, site.Id + " " + item.Name);
                        if (item.Tool != null)
                        {
                            var hit = new ToolStroke(g, map, item.Tool).Touch(map.Find(site.Id), T0);
                            Assert.That(hit, Is.Not.Null, site.Id + " " + item.Name);
                            Assert.That(hit.Ok, Is.True, site.Id + " " + item.Name + ": " + hit.Error);
                        }
                        else if (item.Action.Kind == ActionKind.Run)
                        {
                            var r = item.Action.Run(T0);
                            Assert.That(r.Ok, Is.True, site.Id + " " + item.Name + ": " + r.Error);
                        }
                        else if (item.Action.Kind == ActionKind.Open)
                        {
                            Assert.That(EconomyViews.Build(item.Action.Window, item.Action.Arg, g, T0), Is.Not.Null);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ dragged tools

        [Test]
        public void SeedsDraggedAlongARowPlantThatRowOnce()
        {
            var g = Fresh();
            g.Grant(T0, new Reward { Xp = (int)(g.XpForLevel(12) - g.Xp), Coins = 20000 });
            g.TakeEvents();
            while (g.Plots.Count < 12)
                Assert.That(g.BuyPlot(T0).Ok, Is.True);
            long coins = g.Coins;
            var map = FarmSiteMap.Build(g, Layout);
            var stroke = new ToolStroke(g, map, FarmTool.Seed("wheat"));
            // one fast swipe: from left of plot 0 to right of plot 5 between two frames
            var a = map.Find("plot.0").Centre;
            var b = map.Find("plot.5").Centre;
            var hits = stroke.Sweep(new P2(a.X - 150, a.Y), new P2(b.X + 150, b.Y), T0);
            Assert.That(hits.Select(h => h.Site.Id), Is.EqualTo(new[] { "plot.0", "plot.1", "plot.2", "plot.3", "plot.4", "plot.5" }));
            Assert.That(hits.All(h => h.Ok && h.Coins == 1), Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins - 6));
            for (int i = 0; i < 12; i++)
                Assert.That(g.Plots[i].Crop, Is.EqualTo(i < 6 ? "wheat" : null), "plot " + i);
            // back over the same row: nothing twice
            Assert.That(stroke.Sweep(new P2(b.X + 150, b.Y), new P2(a.X - 150, a.Y), T0), Is.Empty);
            Assert.That(stroke.Done, Is.EqualTo(6));
            // a diagonal takes what lies under it, in order
            var c = map.Find("plot.11").Centre;
            var diag = stroke.Sweep(b, c, T0);
            Assert.That(diag.Select(h => h.Site.Id), Is.EqualTo(new[] { "plot.11" }));
        }

        [Test]
        public void SeedsStopWhenTheMoneyAndTheBarnRunOut()
        {
            var g = Fresh();
            g.State.Wallet.Coins = 2;
            g.State.Barn["wheat"] = 1;
            var map = FarmSiteMap.Build(g, Layout);
            var stroke = new ToolStroke(g, map, FarmTool.Seed("wheat"));
            var hits = stroke.Sweep(map.Find("plot.0").Centre, map.Find("plot.5").Centre, T0);
            // two for coins, one from the barn, the fourth stops the drag; the rest are not tried
            Assert.That(hits.Count, Is.EqualTo(4));
            Assert.That(hits.Take(3).All(h => h.Ok), Is.True);
            Assert.That(hits[2].Coins, Is.EqualTo(0));
            Assert.That(hits[3].Error, Is.EqualTo(MetaError.NotEnoughCoins));
            Assert.That(stroke.Stopped, Is.True);
            Assert.That(stroke.StopError, Is.EqualTo(MetaError.NotEnoughCoins));
            Assert.That(g.Plots.Count(p => p.Crop != null), Is.EqualTo(3));
        }

        [Test]
        public void SickleCutsOnlyTheRipeAndStopsAtAFullBarn()
        {
            var g = Fresh();
            g.State.Wallet.Coins = 100;
            for (int i = 0; i < 6; i++)
                g.Plant(T0, i, i < 4 ? "wheat" : "corn");
            var map = FarmSiteMap.Build(g, Layout);
            long now = T0 + 150; // wheat ripe, corn not
            var stroke = new ToolStroke(g, map, FarmTool.Sickle());
            var hits = stroke.Sweep(map.Find("plot.0").Centre, map.Find("plot.5").Centre, now);
            Assert.That(hits.Select(h => h.Site.Id), Is.EqualTo(new[] { "plot.0", "plot.1", "plot.2", "plot.3" }));
            Assert.That(stroke.Total.Items["wheat"], Is.EqualTo(8));
            Assert.That(g.Plots[4].Crop, Is.EqualTo("corn"));

            // a full barn stops the next harvest once
            now = T0 + 400;
            g.State.Barn["wheat"] = g.BarnCapacity - (g.BarnUsed - g.BarnCount("wheat")) - 1;
            stroke = new ToolStroke(g, map, FarmTool.Sickle());
            hits = stroke.Sweep(map.Find("plot.0").Centre, map.Find("plot.5").Centre, now);
            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].Error, Is.EqualTo(MetaError.BarnFull));
            Assert.That(stroke.Stopped, Is.True);
            Assert.That(g.Plots[4].Crop, Is.EqualTo("corn"));
        }

        [Test]
        public void SickleAfterATapOnlyWhenMoreIsRipe()
        {
            var g = Fresh();
            g.State.Wallet.Coins = 100;
            g.Plant(T0, 0, "wheat");
            g.Plant(T0, 1, "wheat");
            var map = FarmSiteMap.Build(g, Layout);
            long now = T0 + 200;
            // the starting apple trees are ripe too: take them out of the picture
            foreach (var t in g.State.Farm.Trees)
                t.ReadyAt = now + 1000;
            var p0 = map.Find("plot.0");
            var before = FarmSiteMap.Status(p0, g, now);
            FarmMenus.Primary(p0, g, now).Run(now);
            Assert.That(FarmMenus.FollowUp(p0, before, map, g, now).Kind, Is.EqualTo(ToolKind.Sickle));
            var p1 = map.Find("plot.1");
            before = FarmSiteMap.Status(p1, g, now);
            FarmMenus.Primary(p1, g, now).Run(now);
            Assert.That(FarmMenus.FollowUp(p1, before, map, g, now), Is.Null);
        }

        // ------------------------------------------------------------------ the fan of round buttons

        [Test]
        public void MenuFanStaysInTheViewAndItsButtonsDoNotTouch()
        {
            float l = -540 + 24, r = 540 - 24, b = -960 + 420, t = 960 - 220;
            foreach (int n in new[] { 1, 2, 3, 5, 6, 8 })
            {
                foreach (var anchor in new[] { new P2(0, 0), new P2(-520, 0), new P2(520, 300), new P2(0, 700), new P2(300, -500), new P2(-500, 720) })
                {
                    foreach (float radius in new[] { 60f, 150f })
                    {
                        var fan = RadialLayout.Place(n, anchor, radius, l, b, r, t);
                        string what = n + " at " + anchor + " r" + radius;
                        Assert.That(fan.Points.Length, Is.EqualTo(n));
                        for (int i = 0; i < n; i++)
                        {
                            var p = fan.Points[i];
                            Assert.That(p.X - RadialLayout.Item / 2, Is.GreaterThanOrEqualTo(l - 0.5f), what);
                            Assert.That(p.X + RadialLayout.Item / 2, Is.LessThanOrEqualTo(r + 0.5f), what);
                            Assert.That(p.Y - RadialLayout.Item / 2, Is.GreaterThanOrEqualTo(b - 0.5f), what);
                            Assert.That(p.Y + RadialLayout.Item / 2, Is.LessThanOrEqualTo(t + 0.5f), what);
                            for (int j = i + 1; j < n; j++)
                                Assert.That(P2.Distance(p, fan.Points[j]), Is.GreaterThanOrEqualTo(RadialLayout.Item + 4), what + ": " + i + " and " + j);
                        }
                        Assert.That(fan.Title.Y, Is.InRange(b, t), what);
                    }
                }
            }
            // the button is a finger's size
            Assert.That(RadialLayout.Item, Is.GreaterThanOrEqualTo(FarmSiteMap.MinTouch));
            // under the top plates the fan opens downwards
            Assert.That(RadialLayout.Place(3, new P2(0, 700), 60, l, b, r, t).Below, Is.True);
            Assert.That(RadialLayout.Place(3, new P2(0, 0), 60, l, b, r, t).Below, Is.False);
        }

        /// <summary>Writes where everything stands for the storyboards (farm/docs/farm-ux/storyboards.py) when asked.</summary>
        [Test]
        public void DumpSitesForTheStoryboards()
        {
            string to = Environment.GetEnvironmentVariable("FARM_SITES_DUMP");
            if (string.IsNullOrEmpty(to))
                Assert.Pass();
            foreach (var (name, g) in new[] { ("fresh", Fresh()), ("debug", EconomyDebug.Game(Data, T0)), ("rich", Rich()) })
            {
                var map = FarmSiteMap.Build(g, Layout);
                var lines = new List<string>();
                foreach (var s in map.Sites)
                {
                    var st = FarmSiteMap.Status(s, g, T0);
                    lines.Add(string.Join("\t", s.Id, s.Kind, s.Ref ?? "-", s.X.ToString("0"), s.Y.ToString("0"), s.W.ToString("0"), s.H.ToString("0"), st.Phase, st.Item ?? "-",
                        st.Progress.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), st.Stage));
                }
                File.WriteAllLines(Path.Combine(to, "sites_" + name + ".tsv"), lines);
            }
        }
    }
}
