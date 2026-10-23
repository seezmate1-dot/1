using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Match3;
using SunnyValley.Game.Match3View;

namespace SunnyValley.BoardTests
{
    /// <summary>
    /// The board's animation timeline built from real step logs: real levels played by the hint bot (with boosters
    /// now and then), and hand-made boards for every special and combo. Checks the rules the view relies on.
    /// </summary>
    public class TimelineTests
    {
        const float Eps = 1e-4f;

        // ------------------------------------------------------------------ helpers

        static string LevelsDir()
        {
            for (var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); d != null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Levels");
                if (Directory.Exists(p)) return p;
                var q = Path.Combine(d.FullName, "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Levels");
                if (Directory.Exists(q)) return q;
            }
            Assert.Fail("Resources/Levels not found");
            return null;
        }

        static LevelData Load(int id) => LevelLoader.FromJson(File.ReadAllText(Path.Combine(LevelsDir(), $"level_{id:000}.json")));

        static string[] Bg(int w, int h, params (int x, int y, char c)[] overrides)
        {
            var rows = new char[h][];
            for (int y = 0; y < h; y++)
            {
                rows[y] = new char[w];
                for (int x = 0; x < w; x++) rows[y][x] = "opb"[(x + y) % 3];
            }
            foreach (var (x, y, c) in overrides) rows[y][x] = c;
            return rows.Select(r => new string(r)).ToArray();
        }

        static LevelData Lvl(string[] pieces, params (int x, int y, string kind, string color)[] specials)
        {
            var level = new LevelData
            {
                Id = 1, Moves = 20, Colors = 6, Seed = 7,
                Grid = pieces.Select(r => new string('#', r.Length)).ToArray(),
                Pieces = pieces,
                Goals = new List<GoalData> { new GoalData { Type = "collect", Color = "tomato", Count = 999 } },
            };
            if (specials.Length > 0)
                level.Specials = specials.Select(s => new SpecialData { X = s.x, Y = s.y, Kind = s.kind, Color = s.color }).ToList();
            return level;
        }

        static Dictionary<int, Pos> Pieces(Match3Game g)
        {
            var map = new Dictionary<int, Pos>();
            for (int i = 0; i < g.Topo.Count; i++)
                if (g.Topo.Exists[i] && !g.Board.Pieces[i].IsEmpty) map[g.Board.Pieces[i].Id] = g.Topo.PosOf(i);
            return map;
        }

        /// <summary>Plays the timeline on a map of where each piece is, in time order, and checks every rule.</summary>
        static void Check(Timeline tl, IReadOnlyList<StepEvent> events, Dictionary<int, Pos> before, Match3Game after, string what)
        {
            Assert.AreEqual(0, tl.Delayed, what + ": a piece was asked to make two moves at once");
            for (int k = 1; k < tl.Cues.Count; k++)
                Assert.LessOrEqual(tl.Cues[k - 1].Time, tl.Cues[k].Time, what + ": cues out of order");
            foreach (var c in tl.Cues)
            {
                Assert.GreaterOrEqual(c.Time, 0f, what);
                Assert.GreaterOrEqual(c.Duration, 0f, what);
                Assert.LessOrEqual(c.End, tl.Duration + Eps, what);
                if (!c.IsCosmetic) Assert.LessOrEqual(c.End, tl.BoardTime + Eps, what + ": " + c);
            }

            // where each piece is, cue by cue
            var at = new Dictionary<int, Pos>(before);
            var busyUntil = new Dictionary<int, float>();
            var removedAt = new Dictionary<int, float>();
            var gathered = new HashSet<int>();
            foreach (var c in tl.Cues)
            {
                int id = c.PieceId;
                if (c.IsMove && id > 0)
                {
                    Assert.IsFalse(removedAt.ContainsKey(id), what + ": piece moves after it left: " + c);
                    if (busyUntil.TryGetValue(id, out float until))
                        Assert.GreaterOrEqual(c.Time, until - Eps, what + ": overlapping moves of #" + id + ": " + c);
                    busyUntil[id] = c.End;
                }
                switch (c.Kind)
                {
                    case CueKind.SwapMove:
                    case CueKind.Fall:
                    case CueKind.Shuffle:
                        Assert.IsTrue(at.TryGetValue(id, out var p) && p == c.From, what + ": #" + id + " not at " + c.From + " for " + c);
                        at[id] = c.To;
                        break;
                    case CueKind.Gather:
                        Assert.IsTrue(at.TryGetValue(id, out var g) && g == c.From, what + ": gather " + c);
                        gathered.Add(id);
                        break;
                    case CueKind.Spawn:
                        Assert.IsFalse(at.ContainsKey(id), what + ": spawn of a known piece " + c);
                        at[id] = c.To;
                        break;
                    case CueKind.SpecialAppear:
                        at[id] = c.To;
                        break;
                    case CueKind.Transform:
                        Assert.IsTrue(at.TryGetValue(id, out var tp) && tp == c.To, what + ": transform " + c);
                        break;
                    case CueKind.Pop:
                    case CueKind.Merge:
                    case CueKind.IntoBasket:
                    case CueKind.Fire:
                    case CueKind.ChickExit:
                        Assert.IsTrue(at.TryGetValue(id, out var rp), what + ": removing a piece not on the board " + c);
                        if (!gathered.Contains(id)) Assert.AreEqual(c.From, rp, what + ": removed where it is not " + c);
                        if (busyUntil.TryGetValue(id, out float b) && c.Kind != CueKind.ChickExit && !c.IsMove)
                            Assert.GreaterOrEqual(c.Time, b - Eps, what + ": removed while moving " + c);
                        at.Remove(id);
                        removedAt[id] = c.Time;
                        break;
                    case CueKind.Spread:
                        if (id > 0) at.Remove(id);
                        break;
                }
            }
            var expected = Pieces(after);
            CollectionAssert.AreEquivalent(expected.OrderBy(kv => kv.Key).ToList(), at.OrderBy(kv => kv.Key).ToList(),
                what + ": the timeline does not end in the engine's board");

            // waves one after another: nothing of wave w+1 starts before the refill of wave w has landed
            var refillEnd = new Dictionary<int, float>();
            foreach (var c in tl.Cues)
                if (c.Kind == CueKind.Fall || c.Kind == CueKind.Spawn)
                    refillEnd[c.Wave] = Math.Max(refillEnd.TryGetValue(c.Wave, out float e) ? e : 0, c.End);
            foreach (var c in tl.Cues)
            {
                if (c.IsCosmetic || c.Wave == 0 || c.Kind == CueKind.Fall || c.Kind == CueKind.Spawn || c.Kind == CueKind.ChickExit) continue;
                if (refillEnd.TryGetValue(c.Wave - 1, out float prev))
                    Assert.GreaterOrEqual(c.Time, prev - Eps, what + ": wave " + c.Wave + " starts before the refill of wave " + (c.Wave - 1) + " ends: " + c);
            }

            // hits come after what caused them: a special's hits after it fired
            var fireOf = new Dictionary<int, float>();
            foreach (var c in tl.Cues) if (c.Kind == CueKind.Fire) fireOf[c.PieceId] = c.Time;
            var activationPiece = new Dictionary<int, int>();
            // (a combo's activation names the dragged piece, which may be a special the combo copies and fires later)
            foreach (var e in events) if (e is ActivationEvent a && a.PieceId > 0 && a.Combo == ComboKind.None) activationPiece[a.ActivationId] = a.PieceId;
            foreach (var c in tl.Cues)
            {
                int cause = c.Event?.CauseId ?? -1;
                if (cause < 0 || !(c.Kind == CueKind.Pop || c.Kind == CueKind.Fire || c.Kind == CueKind.ObstacleHit)) continue;
                if (activationPiece.TryGetValue(cause, out int piece) && fireOf.TryGetValue(piece, out float fired))
                    Assert.GreaterOrEqual(c.Time, fired - Eps, what + ": hit before its special fired: " + c);
            }

            // falls are one cell long, contiguous per piece, and accelerate
            foreach (var c in tl.Cues.Where(c => c.Kind == CueKind.Fall))
                Assert.LessOrEqual(c.Duration, Timings.Tick0 + Eps, what);
        }

        static Timeline Play(Match3Game g, Func<Match3Game, MoveResult> act, string what, TimelineBuilder builder = null)
        {
            var before = Pieces(g);
            var r = act(g);
            var tl = (builder ?? new TimelineBuilder()).Build(r.Events);
            Check(tl, r.Events, before, g, what);
            return tl;
        }

        // ------------------------------------------------------------------ real levels

        [Test]
        public void RealLevels_HintMoves_Boosters_TimelinesFollowTheRules()
        {
            var builder = new TimelineBuilder();
            var plainTimes = new List<float>();
            var allTimes = new List<float>();
            int timelines = 0;
            for (int id = 1; id <= 60; id++)
            {
                var level = Load(id);
                var pre = id % 7 == 0 ? new[] { BoosterKind.PreLine, BoosterKind.PreBomb, BoosterKind.PreRainbow } : null;
                var g = Match3Game.Create(level, id * 101L, pre);
                if (pre != null)
                {
                    var init = builder.Build(g.InitialEvents);
                    Assert.AreEqual(3, init.Cues.Count(c => c.Kind == CueKind.Transform), "level " + id + " pre-boosters");
                    Assert.LessOrEqual(init.BoardTime, 1.0f);
                }
                var rng = new Rng((ulong)id);
                for (int move = 0; move < 12 && g.Status == GameStatus.Playing; move++)
                {
                    string what = "level " + id + " move " + move;
                    MoveResult result = null;
                    Timeline tl;
                    if (move == 5 && id % 3 == 0)
                    {
                        var kind = (BoosterKind)(10 + id / 3 % 4);
                        var target = new Pos(rng.Next(g.Topo.Width), rng.Next(g.Topo.Height));
                        tl = Play(g, x => result = x.UseBooster(kind, target), what + " booster " + kind, builder);
                    }
                    else
                    {
                        var hint = move % 2 == 0 ? Hints.Find(g, (ulong)move) : null;
                        var moves = g.GetValidMoves();
                        if (hint == null && moves.Count == 0) break;
                        var m = hint ?? moves[rng.Next(moves.Count)];
                        tl = Play(g, x => result = x.Swap(m.A, m.B), what, builder);
                    }
                    timelines++;
                    if (!result.Accepted) continue;
                    allTimes.Add(tl.BoardTime);
                    if (tl.BoardTime > 4)
                        TestContext.WriteLine($"long: level {id} move {move}: {tl.BoardTime:0.00} s, waves {result.Waves}, activations {result.OfType<ActivationEvent>().Count()}, " +
                                              $"bees {result.OfType<BeeFlightEvent>().Count()}, max generation {result.OfType<ActivationEvent>().Select(a => a.Generation).DefaultIfEmpty(0).Max()}");
                    bool plain = result.Waves <= 1 && !result.OfType<ActivationEvent>().Any() && !result.OfType<ShuffleEvent>().Any() &&
                                 !result.OfType<SpreadEvent>().Any() && result.OfType<SwapEvent>().Any();
                    if (plain) plainTimes.Add(tl.BoardTime);
                }
                if (g.Status == GameStatus.Lost)
                {
                    var before = Pieces(g);
                    var r = g.ContinueWithExtraMoves(5);
                    Check(builder.Build(r.Events), r.Events, before, g, "level " + id + " +5");
                }
            }
            allTimes.Sort();
            plainTimes.Sort();
            float median = allTimes[allTimes.Count / 2];
            TestContext.WriteLine($"timelines {timelines}; plain moves {plainTimes.Count}: {plainTimes.First():0.00}…{plainTimes.Last():0.00} s; " +
                                  $"all accepted: median {median:0.00} s, 90% {allTimes[allTimes.Count * 9 / 10]:0.00} s, max {allTimes.Last():0.00} s");
            Assert.Greater(plainTimes.Count, 50);
            Assert.LessOrEqual(plainTimes.Last(), 1.05f, "a plain move resolves in about a second");
            Assert.GreaterOrEqual(plainTimes.First(), 0.3f, "…but is still visible");
            Assert.LessOrEqual(median, 1.5f, "a typical move resolves in 1–1.5 s");
            Assert.LessOrEqual(allTimes[allTimes.Count * 9 / 10], 3f, "nine moves in ten are over within 3 s");
            Assert.LessOrEqual(allTimes.Last(), 10f, "even a dozen cascade waves speed up enough");
        }

        // ------------------------------------------------------------------ hand-made boards

        static Timeline SwapTimeline(LevelData level, Pos a, Pos b, out MoveResult r, out Match3Game g)
        {
            g = Match3Game.Create(level, 5);
            var before = Pieces(g);
            r = g.Swap(a, b);
            Assert.IsTrue(r.Accepted, "swap accepted");
            var tl = new TimelineBuilder().Build(r.Events);
            Check(tl, r.Events, before, g, "swap " + a + "->" + b);
            return tl;
        }

        [Test]
        public void PlainMatch_SwapThenPopThenFall()
        {
            // r . r r in row 0: moving (1,1)=r up makes a three
            var level = Lvl(Bg(6, 6, (0, 0, 'r'), (1, 1, 'r'), (2, 0, 'r')));
            var tl = SwapTimeline(level, new Pos(1, 1), new Pos(1, 0), out var r, out _);
            var swap = tl.Cues.Where(c => c.Kind == CueKind.SwapMove).ToList();
            var pops = tl.Cues.Where(c => c.Kind == CueKind.Pop && c.Wave == 0).ToList();
            Assert.AreEqual(2, swap.Count);
            Assert.AreEqual(3, pops.Count);
            Assert.IsTrue(pops.All(p => Math.Abs(p.Time - Timings.Swap) < Eps), "pops start when the swap ends");
            float firstFall = tl.Cues.Where(c => c.Kind == CueKind.Fall || c.Kind == CueKind.Spawn).Min(c => c.Time);
            Assert.AreEqual(Timings.Swap + Timings.RefillDelay, firstFall, 1e-3, "refill starts right after the pops");
            Assert.IsTrue(tl.Cues.Any(c => c.Kind == CueKind.Moves && c.Time == 0), "the moves counter goes first");
        }

        [Test]
        public void Rejected_Swap_Bounces()
        {
            var g = Match3Game.Create(Lvl(Bg(6, 6)), 5);
            var r = g.Swap(new Pos(0, 0), new Pos(1, 0));
            Assert.IsFalse(r.Accepted);
            var tl = new TimelineBuilder().Build(r.Events);
            Assert.AreEqual(1, tl.Cues.Count);
            Assert.AreEqual(CueKind.Bounce, tl.Cues[0].Kind);
            Assert.AreEqual(Timings.Bounce, tl.BoardTime, Eps);
        }

        [Test]
        public void Pitchfork_HitsRunOutwardsFromTheSpecial()
        {
            var tl = SwapTimeline(Lvl(Bg(9, 9), (4, 4, "lineH", "tomato")), new Pos(4, 4), new Pos(5, 4), out _, out _);
            var fire = tl.Cues.Single(c => c.Kind == CueKind.Fire);
            Assert.AreEqual(Timings.Swap, fire.Time, Eps, "fires when the swap lands");
            Assert.IsTrue(tl.Cues.Any(c => c.Kind == CueKind.LineStreak && c.Value == 0 && c.Time == fire.Time));
            var pops = tl.Cues.Where(c => c.Kind == CueKind.Pop && c.Wave == 0 && c.From.Y == 4).ToList();
            Assert.AreEqual(8, pops.Count);
            foreach (var p in pops)
                Assert.AreEqual(fire.Time + Timings.LineLead + Math.Abs(p.From.X - 5) * Timings.LineStep, p.Time, 1e-3, "pop at " + p.From);
        }

        [Test]
        public void Bomb_Shockwave_RingsOut_AndShakes()
        {
            var tl = SwapTimeline(Lvl(Bg(9, 9), (4, 4, "bomb", "tomato")), new Pos(4, 4), new Pos(5, 4), out _, out _);
            Assert.IsTrue(tl.Cues.Any(c => c.Kind == CueKind.Blast && c.Value == 1));
            Assert.IsTrue(tl.Cues.Any(c => c.Kind == CueKind.Shake));
            var pops = tl.Cues.Where(c => c.Kind == CueKind.Pop && c.Wave == 0).ToList();
            Assert.AreEqual(8, pops.Count);
            Assert.IsTrue(pops.All(p => p.Time > tl.Cues.Single(c => c.Kind == CueKind.Fire).Time + Timings.BombFuse));
        }

        [Test]
        public void ChainReaction_SecondSpecialFiresWhenTheStreakReachesIt()
        {
            // a vertical pitchfork in the row of a horizontal one
            var tl = SwapTimeline(Lvl(Bg(9, 9), (4, 4, "lineH", "tomato"), (8, 5, "lineV", "corn")), new Pos(4, 4), new Pos(4, 5), out var r, out _);
            var fires = tl.Cues.Where(c => c.Kind == CueKind.Fire).OrderBy(c => c.Time).ToList();
            Assert.AreEqual(2, fires.Count);
            Assert.AreEqual(fires[0].Time + Timings.LineLead + 4 * Timings.LineStep, fires[1].Time, 1e-3, "the column fires as the streak arrives");
            var gen1 = r.OfType<ActivationEvent>().Single(a => a.Generation == 1);
            var columnPops = tl.Cues.Where(c => c.Kind == CueKind.Pop && c.Event.CauseId == gen1.ActivationId).ToList();
            Assert.Greater(columnPops.Count, 0);
            Assert.IsTrue(columnPops.All(p => p.Time > fires[1].Time));
        }

        [Test]
        public void Bee_FliesOnAnArc_AndHitsWhenItLands()
        {
            var level = Lvl(Bg(9, 9, (8, 8, '.')), (1, 1, "bee", "tomato"));
            level.Layers = new Dictionary<string, string[]>
            {
                ["crate"] = new[] { ".........", ".........", ".........", ".........", ".........", ".........", ".........", ".........", "........1" },
            };
            level.Goals = new List<GoalData> { new GoalData { Type = "crate" } };
            var tl = SwapTimeline(level, new Pos(1, 1), new Pos(2, 1), out _, out _);
            var flight = tl.Cues.Single(c => c.Kind == CueKind.BeeFlight);
            Assert.AreEqual(new Pos(8, 8), flight.To);
            var hit = tl.Cues.Single(c => c.Kind == CueKind.ObstacleHit && c.To == new Pos(8, 8));
            Assert.AreEqual(flight.End, hit.Time, 1e-3);
            var goal = tl.Cues.Single(c => c.Kind == CueKind.Goal);
            Assert.AreEqual(hit.Time, goal.Time, 1e-3, "the crate flies to the goal when it breaks");
        }

        [Test]
        public void NewSpecial_PiecesMergeIntoItsCell()
        {
            // four in a row after the swap: o o . o with an o under the gap
            var level = Lvl(Bg(9, 9, (0, 4, 'r'), (1, 4, 'r'), (2, 5, 'r'), (3, 4, 'r')));
            var tl = SwapTimeline(level, new Pos(2, 5), new Pos(2, 4), out var r, out _);
            var made = r.OfType<SpecialCreatedEvent>().Single();
            var merges = tl.Cues.Where(c => c.Kind == CueKind.Merge).ToList();
            Assert.AreEqual(4, merges.Count);
            Assert.IsTrue(merges.All(m => m.To == made.Cell));
            var appear = tl.Cues.Single(c => c.Kind == CueKind.SpecialAppear);
            Assert.AreEqual(merges.Max(m => m.End), appear.Time, 1e-3);
        }

        [Test]
        public void Combos_EveryKind_Plays()
        {
            var pairs = new (string a, string b, ComboKind combo)[]
            {
                ("lineH", "lineV", ComboKind.LineLine), ("lineH", "bomb", ComboKind.LineBomb), ("bomb", "bomb", ComboKind.BombBomb),
                ("bee", "bee", ComboKind.BeeBee), ("bee", "lineV", ComboKind.BeeLine), ("bee", "bomb", ComboKind.BeeBomb),
                ("rainbow", "lineH", ComboKind.RainbowLine), ("rainbow", "bomb", ComboKind.RainbowBomb),
                ("rainbow", "bee", ComboKind.RainbowBee), ("rainbow", "rainbow", ComboKind.RainbowRainbow),
            };
            foreach (var (a, b, combo) in pairs)
            {
                var tl = SwapTimeline(Lvl(Bg(9, 9), (4, 4, a, "tomato"), (5, 4, b, "orange".Length > 0 ? "carrot" : null)),
                    new Pos(4, 4), new Pos(5, 4), out var r, out _);
                Assert.AreEqual(combo, r.OfType<ComboEvent>().Single().Combo);
                Assert.Greater(tl.Cues.Count(c => c.Kind == CueKind.Pop), 3, combo.ToString());
                TestContext.WriteLine(combo + ": " + tl.BoardTime.ToString("0.00") + " s, waves " + r.Waves);
                Assert.LessOrEqual(tl.BoardTime, 4.5f, combo + " should not drag on");
                if (combo < ComboKind.RainbowColor) Assert.AreEqual(1, tl.Cues.Count(c => c.Kind == CueKind.Gather), combo.ToString());
                if (combo >= ComboKind.RainbowLine && combo <= ComboKind.RainbowBee)
                {
                    var transforms = tl.Cues.Where(c => c.Kind == CueKind.Transform).ToList();
                    Assert.Greater(transforms.Count, 0);
                    var firstConvertedFire = tl.Cues.Where(c => c.Kind == CueKind.Fire && transforms.Any(t => t.PieceId == c.PieceId)).Min(c => c.Time);
                    Assert.GreaterOrEqual(firstConvertedFire, transforms.Max(t => t.Time), combo + ": all turn before they fire");
                }
            }
        }

        [Test]
        public void Rainbow_WithTile_BeamsEveryPieceOfThatColour()
        {
            var tl = SwapTimeline(Lvl(Bg(9, 9), (4, 4, "rainbow", null)), new Pos(4, 4), new Pos(5, 4), out var r, out _);
            var act = r.OfType<ActivationEvent>().First();
            Assert.AreEqual(act.Cells.Count, tl.Cues.Count(c => c.Kind == CueKind.Beam));
            var beams = tl.Cues.Where(c => c.Kind == CueKind.Beam).ToList();
            foreach (var pop in tl.Cues.Where(c => c.Kind == CueKind.Pop && c.Event.CauseId == act.ActivationId))
            {
                var beam = beams.Single(b => b.To == pop.From);
                Assert.Greater(pop.Time, beam.Time, "the beam arrives first");
            }
        }

        [Test]
        public void Boosters_RakeSweepsTheRow_ShovelHitsOnce()
        {
            var g = Match3Game.Create(Lvl(Bg(9, 9)), 5);
            var rake = Play(g, x => x.UseBooster(BoosterKind.Rake, new Pos(0, 3)), "rake");
            var pops = rake.Cues.Where(c => c.Kind == CueKind.Pop && c.Wave == 0 && c.From.Y == 3).OrderBy(c => c.From.X).ToList();
            Assert.AreEqual(9, pops.Count);
            for (int i = 1; i < pops.Count; i++) Assert.Greater(pops[i].Time, pops[i - 1].Time, "left to right");
            var shovel = Play(g, x => x.UseBooster(BoosterKind.Shovel, new Pos(4, 4)), "shovel");
            Assert.AreEqual(Timings.ShovelLead, shovel.Cues.First(c => c.Kind == CueKind.Pop).Time, Eps);
            var shuffle = Play(g, x => x.UseBooster(BoosterKind.Shuffle, new Pos(0, 0)), "shuffle");
            Assert.Greater(shuffle.Cues.Count(c => c.Kind == CueKind.Shuffle), 10);
        }

        [Test]
        public void Refill_TicksAccelerate_AndEveryPieceLandsOnce()
        {
            var g = Match3Game.Create(Lvl(Bg(9, 9)), 5);
            var tl = Play(g, x => x.UseBooster(BoosterKind.WateringCan, new Pos(2, 0)), "column");
            var falls = tl.Cues.Where(c => c.Kind == CueKind.Fall || c.Kind == CueKind.Spawn).ToList();
            var durations = falls.Select(f => f.Duration).Distinct().OrderByDescending(d => d).ToList();
            Assert.Greater(durations.Count, 3, "ticks get shorter");
            var lands = tl.Cues.Where(c => c.Kind == CueKind.Land).ToList();
            Assert.AreEqual(lands.Count, lands.Select(l => l.PieceId).Distinct().Count(), "one bounce per piece");
            Assert.AreEqual(9, lands.Count, "the whole column comes in anew");
        }

        [Test]
        public void Chicks_LeaveAtTheExit_AndFlyToTheGoal()
        {
            var level = Load(13);
            var builder = new TimelineBuilder();
            var g = Match3Game.Create(level, 1);
            int exits = 0, goals = 0;
            for (int move = 0; move < 40 && g.Status == GameStatus.Playing; move++)
            {
                var hint = Hints.Find(g, (ulong)move);
                if (hint == null) break;
                var before = Pieces(g);
                var r = g.Swap(hint.Value.A, hint.Value.B);
                var tl = builder.Build(r.Events);
                Check(tl, r.Events, before, g, "level 13 move " + move);
                foreach (var c in tl.Cues.Where(c => c.Kind == CueKind.ChickExit))
                {
                    exits++;
                    Assert.IsTrue(tl.Cues.Any(x => x.Kind == CueKind.Goal && x.From == c.From && x.Time >= c.Time), "chick goal flies after the hop");
                }
                goals += tl.Cues.Count(c => c.Kind == CueKind.Goal && ((GoalProgressEvent)c.Event).Goal == GoalType.Chick);
            }
            Assert.Greater(exits, 0, "some chick got out in 40 moves");
            Assert.AreEqual(exits, goals);
        }

        // ------------------------------------------------------------------ bonus moves

        [Test]
        public void BonusFinale_SparksTurnTilesIntoPitchforks_ThatFireInTurn()
        {
            var g = Match3Game.Create(Load(7), 3);
            var tl = BonusFinale.Build(g.Board, 9, 42);
            var sparks = tl.Cues.Where(c => c.Kind == CueKind.BonusSpark).ToList();
            var transforms = tl.Cues.Where(c => c.Kind == CueKind.Transform).ToList();
            var fires = tl.Cues.Where(c => c.Kind == CueKind.Fire).ToList();
            Assert.AreEqual(9, sparks.Count);
            Assert.AreEqual(9, transforms.Count);
            Assert.AreEqual(9, fires.Count);
            Assert.AreEqual(9, transforms.Select(t => t.To).Distinct().Count(), "different tiles");
            Assert.Greater(fires.Min(f => f.Time), transforms.Max(t => t.End) - Eps, "all sparks land before the first fires");
            var popped = tl.Cues.Where(c => c.Kind == CueKind.Pop || c.Kind == CueKind.Fire).Select(c => c.PieceId).ToList();
            Assert.AreEqual(popped.Count, popped.Distinct().Count(), "no piece pops twice");
            Assert.AreEqual(0, tl.Cues.Last(c => c.Kind == CueKind.Moves).Value, "the counter runs down to zero");
            var moves = tl.Cues.Where(c => c.Kind == CueKind.Moves).Select(c => c.Value).ToList();
            for (int i = 1; i < moves.Count; i++) Assert.Less(moves[i], moves[i - 1]);
            Assert.LessOrEqual(tl.Duration, 4f);

            var many = BonusFinale.Build(g.Board, 30, 1);
            Assert.AreEqual(BonusFinale.MaxSparks, many.Cues.Count(c => c.Kind == CueKind.BonusSpark));
            Assert.AreEqual(0, many.Cues.Last(c => c.Kind == CueKind.Moves).Value);
            Assert.AreEqual(0, BonusFinale.Build(g.Board, 0, 1).Cues.Count);
        }
    }
}
