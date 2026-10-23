using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Layout;

namespace SunnyValley.Ui.Tests
{
    /// <summary>
    /// The farm economy's windows (Game/Economy) built from real games over balance.json — a farm just opened, the
    /// debug save of the CI screenshots, a farm at the last level with everything built and the barn full — laid out
    /// on every device, with every question they can ask. Then every button of every window is pressed on a copy of
    /// the game: it does what it says (the core action, another window, a toast) and never throws.
    /// </summary>
    public class EconomyLayoutTests
    {
        const long T0 = 19700L * 86400 + 8 * 3600;

        static MetaData data;

        static MetaData Data
        {
            get
            {
                if (data == null)
                {
                    string dir = Path.Combine(Dir(), "..", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta");
                    data = MetaData.Parse(File.ReadAllText(Path.Combine(dir, "balance.json")), File.ReadAllText(Path.Combine(dir, "chapter1.json")));
                }
                return data;
            }
        }

        static string Dir([System.Runtime.CompilerServices.CallerFilePath] string here = "") => Path.GetDirectoryName(here);

        static MetaGame FreshFarm()
        {
            var g = MetaGame.NewGame(Data, T0, 42);
            g.State.Wallet.Stars += g.StarsToFinishChapter();
            while (!g.ChapterFinished)
                Assert.That(g.CompleteTask(T0, g.AvailableTasks()[0].Id).Ok, Is.True);
            g.TakeEvents();
            return g;
        }

        /// <summary>The last level, every building, every plot, tree and animal, the barn stuffed with every good, no crystals.</summary>
        static MetaGame RichFarm()
        {
            var g = FreshFarm();
            g.Grant(T0, new Reward { Xp = (int)(g.XpForLevel(Data.MaxLevel) - g.Xp) });
            g.State.Wallet.Coins = 99_999_999;
            g.State.Wallet.Stars = 999;
            foreach (var t in Data.Balance.Tools)
                g.State.Tools[t.Id] = 999;
            while (g.Expand(T0).Ok) { }
            foreach (var d in Data.Balance.Town.Services)
                g.Build(T0, d.Id);
            foreach (var d in Data.Balance.Town.Houses)
                while (g.Build(T0, d.Id).Ok) { }
            foreach (var d in Data.Balance.Town.Decorations)
                g.Build(T0, d.Id);
            foreach (var h in Data.Balance.AnimalHouses)
                g.BuildAnimalHouse(T0, h.Id);
            foreach (var w in Data.Balance.Workshops)
                g.BuildWorkshop(T0, w.Id);
            foreach (var w in Data.Balance.Workshops)
                while (g.BuySlot(T0, w.Id).Ok) { }
            foreach (var a in Data.Balance.Animals)
                while (g.BuyAnimal(T0, a.Id).Ok) { }
            foreach (var t in Data.Balance.Trees)
                while (g.BuyTree(T0, t.Id).Ok) { }
            while (g.BuyPlot(T0).Ok) { }
            while (g.UpgradeBarn(T0).Ok) { }
            g.State.Tools["saw"] = 0;
            g.State.Tools["hammer"] = 0;
            foreach (var it in Data.Balance.Items)
                g.State.Barn[it.Id] = 999;
            var crops = Data.Balance.Crops;
            for (int i = 0; i < g.Plots.Count; i++)
                g.Plant(T0, i, crops[i % crops.Count].Item);
            foreach (var a in g.State.Farm.Animals.ToArray())
                g.Feed(T0, a.Uid);
            foreach (var w in Data.Balance.Workshops)
            {
                for (int k = 0; k < 6; k++)
                    g.Enqueue(T0, w.Id, w.Recipes[(k * 3) % w.Recipes.Count].Id);
            }
            g.State.Wallet.Crystals = 0;
            g.TakeEvents();
            return g;
        }

        /// <summary>Games and the moments they are looked at.</summary>
        static IEnumerable<(string name, MetaGame game, long now)> Games()
        {
            var fresh = FreshFarm();
            yield return ("fresh", fresh, T0);
            var debug = EconomyDebug.Game(Data, T0 + 3 * 3600);
            yield return ("debug", debug, T0 + 3 * 3600);
            yield return ("debug-later", debug, T0 + 9 * 3600);
            var rich = RichFarm();
            yield return ("rich", rich, T0 + 60);
            yield return ("rich-ready", rich, T0 + 30 * 86400);
            // the truck away on its trip
            var trip = RichFarm();
            var truck = trip.Truck(T0);
            for (int i = 0; i < truck.Crates.Count; i++)
                trip.LoadCrate(T0, i);
            Assert.That(trip.SendTruck(T0).Ok, Is.True, "the truck leaves");
            yield return ("truck-away", trip, T0 + 600);
        }

        /// <summary>Every window of a game: all the lists, a workshop view per workshop, the crop picker per empty plot.</summary>
        static IEnumerable<EconomyView> Views(MetaGame g, long now)
        {
            foreach (string w in new[] { EconomyViews.Fields, EconomyViews.Animals, EconomyViews.Workshops, EconomyViews.Barn, EconomyViews.Orders, EconomyViews.Truck, EconomyViews.Town })
                yield return EconomyViews.Build(w, null, g, now);
            foreach (var w in Data.Balance.Workshops)
                yield return EconomyViews.Build(EconomyViews.Workshop, w.Id, g, now);
            yield return EconomyViews.Build(EconomyViews.Crops, "0", g, now);
            int empty = g.Plots.ToList().FindIndex(p => p.Crop == null);
            if (empty >= 0)
                yield return EconomyViews.Build(EconomyViews.Crops, empty.ToString(), g, now);
            var board = g.Board(now);
            for (int i = 0; i < board.Count; i++)
                yield return EconomyViews.Build(EconomyViews.Orders, i.ToString(), g, now);
        }

        static void AssertClean(UiLayout L, string what)
        {
            var problems = LayoutCheck.Problems(L);
            Assert.That(problems, Is.Empty, what + "\n" + string.Join("\n", problems));
        }

        [Test]
        public void EveryWindowOfRealGamesFitsEveryScreen()
        {
            int windows = 0;
            foreach (var (name, g, now) in Games())
            {
                var views = Views(g, now).ToList();
                foreach (var screen in Devices.All)
                {
                    var fit = new CanvasFit(screen);
                    AssertClean(Screens.Farm(fit, EconomyViews.Hud(new FarmHud(), g, now)), name + " farm");
                    foreach (var v in views)
                    {
                        AssertClean(Popups.Economy(fit, v.Info), name + " " + v.Window + " " + v.Arg);
                        foreach (var a in v.Actions.Values.Where(a => a.Confirm != null))
                            AssertClean(Popups.Confirm(fit, a.Confirm), name + " " + v.Window + ": " + a.Confirm.Text);
                        windows++;
                    }
                }
            }
            TestContext.Out.WriteLine($"{windows} windows laid out");
        }

        [Test]
        public void EveryButtonHasAnActionAndEveryActionAButton()
        {
            var fit = new CanvasFit(Devices.All[2]);
            foreach (var (name, g, now) in Games())
            foreach (var v in Views(g, now))
            {
                var L = Popups.Economy(fit, v.Info);
                var buttons = L.Root.All().Where(n => n.Kind == NodeKind.Button && !n.InheritsFlag(NodeFlags.Hidden)).Select(n => n.Id).ToList();
                foreach (string id in buttons)
                {
                    if (id == v.Info.Id + ".panel.close")
                        continue;
                    Assert.That(v.Action(id), Is.Not.Null, $"{name} {v.Window}: {id} does nothing");
                }
                foreach (string id in v.Actions.Keys)
                    Assert.That(buttons, Does.Contain(id), $"{name} {v.Window}: an action for a missing button {id}");
            }
        }

        [Test]
        public void EveryButtonCanBePressed()
        {
            int pressed = 0, worked = 0;
            foreach (var (name, g, now) in Games())
            {
                // looking at the board reveals the orders whose wait is over (the core's rule): do it before the copy
                g.Board(now);
                g.Truck(now);
                g.TakeEvents();
                string save = g.Save();
                foreach (var v0 in Views(g, now))
                {
                    foreach (string id in v0.Actions.Keys.ToList())
                    {
                        // on a copy, built again from the copy, so the original stays as it was
                        var copy = MetaGame.Load(Data, save);
                        var v = EconomyViews.Build(v0.Window, v0.Arg, copy, now);
                        var a = v.Action(id);
                        Assert.That(a, Is.Not.Null, $"{name} {v.Window} {id}: the same view has the same buttons");
                        pressed++;
                        switch (a.Kind)
                        {
                            case ActionKind.Run:
                                var r = a.Run(now);
                                Assert.That(r, Is.Not.Null);
                                Assert.That(r.Ok || !string.IsNullOrEmpty(r.ErrorText), $"{name} {v.Window} {id}: a refusal says why");
                                if (r.Ok)
                                    worked++;
                                Assert.That(MetaGame.Load(Data, copy.Save()), Is.Not.Null, "still saves");
                                break;
                            case ActionKind.Open:
                                Assert.That(EconomyViews.All, Does.Contain(a.Window));
                                Assert.That(EconomyViews.Build(a.Window, a.Arg, copy, now), Is.Not.Null);
                                break;
                            case ActionKind.Select:
                                Assert.That(EconomyViews.Build(v.Window, a.Arg, copy, now).Arg, Is.EqualTo(a.Arg));
                                break;
                            default:
                                Assert.That(a.Text, Is.Not.Empty, $"{name} {v.Window} {id}");
                                break;
                        }
                    }
                }
                Assert.That(g.Save(), Is.EqualTo(save), name + ": building views never changes the game");
            }
            TestContext.Out.WriteLine($"{pressed} buttons pressed, {worked} actions done");
            Assert.That(worked, Is.GreaterThan(50));
        }

        [Test]
        public void TheDebugSaveHasSomethingInEveryWindow()
        {
            long now = T0 + 3 * 3600;
            var g = EconomyDebug.Game(Data, now);
            Assert.That(g.FarmOpen, Is.True);
            Assert.That(g.Level, Is.EqualTo(7));
            Assert.That(g.Plots.Any(p => p.Crop != null && p.ReadyAt > now), "a plot growing");
            Assert.That(g.Plots.Any(p => p.Crop != null && p.ReadyAt <= now), "a plot ripe");
            Assert.That(g.Plots.Any(p => p.Crop == null), "a plot empty");
            Assert.That(g.State.Farm.Animals.Any(a => a.Fed && a.ReadyAt > now), "an animal fed");
            Assert.That(g.State.Farm.Animals.Any(a => !a.Fed), "an animal hungry");
            Assert.That(g.RunningJob(now, "mill"), Is.Not.Null, "the mill grinding");
            Assert.That(g.ReadyJobs(now, "bakery"), Is.GreaterThan(0), "bread done");
            Assert.That(g.Board(now).Count(c => c.Order != null), Is.GreaterThan(5), "orders on the board");
            Assert.That(g.Board(now).Any(c => c.Order == null), "a card waiting");
            Assert.That(g.TruckUnlocked, Is.True);
            Assert.That(g.BuildAwaySummary(now).IsEmpty, Is.False, "something to say on return");
            var hud = EconomyViews.Hud(new FarmHud(), g, now);
            Assert.That(hud.FieldsBadge, Is.GreaterThan(0));
            Assert.That(hud.WorkshopsBadge, Is.GreaterThan(0));
        }

        [Test]
        public void TapAPlotPickACropWaitHarvest()
        {
            var g = FreshFarm();
            long now = T0;
            var fields = EconomyViews.Build(EconomyViews.Fields, null, g, now);
            // row 0 is the «Грядки» caption, row 1 the first plot
            var open = fields.Action("fields.row1.do");
            Assert.That(open.Kind, Is.EqualTo(ActionKind.Open));
            Assert.That(open.Window, Is.EqualTo(EconomyViews.Crops));
            var crops = EconomyViews.Build(open.Window, open.Arg, g, now);
            var carrot = crops.Info.Rows.FindIndex(r => r.Title == "Морковь");
            Assert.That(crops.Info.Rows[carrot].Dim, Is.True, "carrot opens at level 2");
            Assert.That(crops.Action("crops.row" + carrot + ".do").Kind, Is.EqualTo(ActionKind.Toast));
            var plant = crops.Action("crops.row0.do");
            Assert.That(plant.Close, Is.True, "the picker closes after planting");
            Assert.That(plant.Run(now).Ok, Is.True);
            Assert.That(g.Plots[0].Crop, Is.EqualTo("wheat"));

            fields = EconomyViews.Build(EconomyViews.Fields, null, g, now + 30);
            var row = fields.Info.Rows[1];
            Assert.That(row.Progress, Is.GreaterThan(0).And.LessThan(1), "a timer bar");
            Assert.That(row.ProgressText, Is.EqualTo("1:30"));
            Assert.That(row.Button.Icon, Is.EqualTo(Icons.Crystal), "speed-up for crystals");
            Assert.That(fields.Action("fields.row1.do").Confirm, Is.Not.Null, "asked before spending crystals");

            fields = EconomyViews.Build(EconomyViews.Fields, null, g, now + 120);
            Assert.That(fields.Info.Rows[1].Button.Text, Is.EqualTo("Собрать"));
            int wheat = g.BarnCount("wheat");
            Assert.That(fields.Action("fields.row1.do").Run(now + 120).Ok, Is.True);
            Assert.That(g.BarnCount("wheat"), Is.EqualTo(wheat + 2));
        }

        [Test]
        public void AWorkshopShowsWhatIsMissingAndQueues()
        {
            var g = FreshFarm();
            long now = T0;
            var mill = EconomyViews.Build(EconomyViews.Workshop, "mill", g, now);
            Assert.That(mill.Info.Queue.Count, Is.EqualTo(Data.Workshops["mill"].Slots + 1), "the slots and one to buy");
            Assert.That(mill.Info.Queue.Last().State, Is.EqualTo(SlotState.Buy));
            var feed = mill.Info.Rows[0];
            Assert.That(feed.Chips.Select(c => c.Text), Is.EquivalentTo(new[] { g.BarnCount("corn") + "/1", g.BarnCount("wheat") + "/2" }));
            Assert.That(feed.Chips.All(c => !c.Missing), "the start goods make chicken feed");
            Assert.That(mill.Action("workshop.row0.do").Run(now).Ok, Is.True);
            mill = EconomyViews.Build(EconomyViews.Workshop, "mill", g, now + 10);
            Assert.That(mill.Info.Queue[0].State, Is.EqualTo(SlotState.Running));
            Assert.That(mill.Info.Footer.Count, Is.EqualTo(2), "collect and speed up");
            g.State.Barn.Clear();
            mill = EconomyViews.Build(EconomyViews.Workshop, "mill", g, now + 10);
            Assert.That(mill.Info.Rows[0].Chips.All(c => c.Missing), "an empty barn: all inputs missing");
            Assert.That(mill.Info.Rows[0].Button.Tone, Is.EqualTo(Tone.Grey));
        }

        [Test]
        public void HungryAnimalsPointToTheMill()
        {
            var g = FreshFarm();
            g.State.Barn.Remove("chicken_feed");
            var v = EconomyViews.Build(EconomyViews.Animals, null, g, T0);
            var way = v.Actions.Values.FirstOrDefault(a => a.Kind == ActionKind.Open);
            Assert.That(way, Is.Not.Null);
            Assert.That(way.Window, Is.EqualTo(EconomyViews.Workshop));
            Assert.That(way.Arg, Is.EqualTo("mill"));
        }

        [Test]
        public void TheOrderBoardHasNineCardsAndActsOnThePickedOne()
        {
            var g = FreshFarm();
            var v = EconomyViews.Build(EconomyViews.Orders, null, g, T0);
            Assert.That(v.Info.Rows.Count, Is.EqualTo(9));
            Assert.That(v.Info.Rows.Count(r => r.Picked), Is.EqualTo(1));
            int picked = v.Info.Rows.FindIndex(r => r.Picked);
            int other = Enumerable.Range(0, 9).First(i => i != picked && g.Board(T0)[i].Order != null);
            var pick = v.Action("orders.card" + other);
            Assert.That(pick.Kind, Is.EqualTo(ActionKind.Select));
            v = EconomyViews.Build(EconomyViews.Orders, pick.Arg, g, T0);
            Assert.That(v.Info.Rows[other].Picked, Is.True);
            var discard = v.Action("orders.foot1");
            Assert.That(discard.Confirm, Is.Not.Null, "asked first");
            Assert.That(discard.Run(T0).Ok, Is.True);
            Assert.That(g.Board(T0)[other].Order, Is.Null);
            v = EconomyViews.Build(EconomyViews.Orders, null, g, T0 + 60);
            Assert.That(v.Info.Rows[other].Dim, Is.True, "the card waits");
            Assert.That(v.Info.Rows[other].Sub, Is.EqualTo(Format.Timer(Data.Balance.Orders.DiscardCooldownSeconds - 60)));
        }

        [Test]
        public void TheFarmLevelStripShowsTheXp()
        {
            var g = FreshFarm();
            g.Grant(T0, new Reward { Xp = 5 });
            var hud = EconomyViews.Hud(new FarmHud(), g, T0);
            var L = Screens.Farm(new CanvasFit(Devices.All[2]), hud);
            Assert.That(L.Find("farm.zone"), Is.Null, "no restoration strip any more");
            Assert.That(L.Find("farm.tasks"), Is.Null);
            Assert.That(L.Find("farm.xp.level").Text.Value, Is.EqualTo("Уровень фермы 1"));
            Assert.That(L.Find("farm.xp.bar.label").Text.Value, Is.EqualTo("5 / " + g.XpForLevel(2)));
            foreach (string id in new[] { "farm.fields", "farm.animals", "farm.workshops", "farm.barn", "farm.town", "farm.orders", "farm.play" })
                Assert.That(L.Find(id), Is.Not.Null, id);
            Assert.That(EconomyViews.LevelHint(g), Does.StartWith("До 2 уровня"));
        }
    }
}
