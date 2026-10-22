using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SunnyValley.Core.Match3
{
    /// <summary>A player that picks swaps. Used to measure level difficulty and to give hints.</summary>
    public interface IMatch3Bot
    {
        string Name { get; }
        /// <summary>A valid swap for the current board. <paramref name="rng"/> is the bot's own randomness.</summary>
        SwapMove Choose(Match3Game game, Rng rng);
    }

    /// <summary>How good a position is for the bots: goals left (as work), specials on the board, the result.</summary>
    public static class BotEval
    {
        public const double WinValue = 100000;

        public static double Value(Match3Game g)
        {
            if (g.Status == GameStatus.Won) return WinValue + g.MovesLeft * 500;
            double v = -Work(g);
            var b = g.Board;
            for (int i = 0; i < g.Topo.Count; i++)
            {
                var p = b.Pieces[i];
                if (!p.IsSpecial) continue;
                switch (p.Special)
                {
                    case SpecialKind.Rainbow: v += 9; break;
                    case SpecialKind.Bomb: v += 5; break;
                    case SpecialKind.LineH:
                    case SpecialKind.LineV: v += 4; break;
                    case SpecialKind.Bee: v += 3.5; break;
                }
            }
            if (g.Status == GameStatus.Lost) v -= WinValue / 2;
            return v;
        }

        /// <summary>Rough amount of work left to finish all goals (≈ tiles to clear).</summary>
        public static double Work(Match3Game g)
        {
            var b = g.Board;
            var t = g.Topo;
            double work = 0;
            foreach (var goal in g.Goals)
            {
                if (goal.Done) continue;
                double w = 0;
                switch (goal.Type)
                {
                    case GoalType.Collect:
                        w = goal.Remaining * 1.0;
                        break;
                    case GoalType.Grass:
                        for (int i = 0; i < t.Count; i++) w += b.Grass[i] * 2.5;
                        break;
                    case GoalType.Chick:
                    {
                        int onBoard = 0;
                        for (int i = 0; i < t.Count; i++)
                        {
                            if (!b.Pieces[i].IsChick) continue;
                            onBoard++;
                            int d = 0, c = i;
                            while (t.Below[c] >= 0 && !t.ChickExit[c] && d < 20) { c = t.Below[c]; d++; }
                            w += 2 + 1.2 * d;
                        }
                        w += Math.Max(0, goal.Remaining - onBoard) * (2 + 1.2 * t.Height);
                        w *= 1.5;
                        break;
                    }
                    case GoalType.Crop:
                    {
                        int need = 0, grown = 0, beds = 0;
                        for (int i = 0; i < t.Count; i++)
                            if (b.Blocker[i] == ObstacleKind.Bed) { need += b.BedNeed[i]; grown += b.BlockerLayers[i]; beds++; }
                        double perCrop = beds == 0 ? 1 : (double)need / beds;
                        w = Math.Max(0, goal.Remaining * perCrop - grown * 0.8) * 2.5;
                        break;
                    }
                    case GoalType.Basket:
                        for (int i = 0; i < t.Count; i++) if (b.Blocker[i] == ObstacleKind.Basket) w += b.BlockerLayers[i] * 1.3;
                        break;
                    default:
                    {
                        var k = LevelLoader.GoalObstacle(goal.Type);
                        double per = k == ObstacleKind.Stone ? 4 : 3;
                        int cells = 0;
                        for (int i = 0; i < t.Count; i++)
                        {
                            if (b.Overlay[i] == k) { w += b.OverlayLayers[i] * per; cells++; }
                            else if (b.Blocker[i] == k) { w += b.BlockerLayers[i] * per; cells++; }
                        }
                        // Goals that count fewer than all of them: scale down.
                        if (cells > goal.Remaining && cells > 0) w *= (double)goal.Remaining / cells;
                        break;
                    }
                }
                work += w;
            }
            return work;
        }

        public static double Gaussian(Rng rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        /// <summary>Value after playing <paramref name="m"/> on a re-seeded copy (the bot does not see real spawns).</summary>
        public static double Try(Match3Game g, SwapMove m, Rng rng, out Match3Game after, bool immediateOnly = false)
        {
            after = g.CloneReseeded(rng.NextULong());
            after.ImmediateOnly = immediateOnly;
            after.Swap(m.A, m.B);
            return Value(after);
        }
    }

    /// <summary>
    /// «Средний игрок»: judges each move by what it visibly does (its own matches and specials, not the cascades
    /// after), misjudges a little (noise on every estimate) and sometimes just makes some match
    /// (<see cref="RandomMoveChance"/>).
    /// </summary>
    public sealed class AverageBot : IMatch3Bot
    {
        public string Name => "average";
        public double RandomMoveChance = 0.25;
        /// <summary>Standard deviation of the misjudgement, in tiles of work.</summary>
        public double Noise = 2.0;

        public SwapMove Choose(Match3Game game, Rng rng)
        {
            var moves = game.GetValidMoves();
            if (moves.Count == 0) throw new InvalidOperationException("no valid moves");
            if (rng.Chance(RandomMoveChance)) return moves[rng.Next(moves.Count)];
            double best = double.MinValue;
            var choice = moves[0];
            foreach (var m in moves)
            {
                double v = BotEval.Try(game, m, rng, out _, immediateOnly: true) + Noise * BotEval.Gaussian(rng);
                if (v > best) { best = v; choice = m; }
            }
            return choice;
        }
    }

    /// <summary>
    /// «Хороший игрок»: judges every move exactly one ply ahead, then for the best few runs Monte-Carlo
    /// rollouts (each with fresh random spawns) a few moves deep and picks the best average outcome.
    /// </summary>
    public sealed class GoodBot : IMatch3Bot
    {
        public string Name => "good";
        public int Candidates = 5;
        public int Rollouts = 3;
        public int Depth = 2;
        /// <summary>In a rollout each move is the best of this many random valid moves (one ply).</summary>
        public int RolloutSample = 4;

        public SwapMove Choose(Match3Game game, Rng rng)
        {
            var moves = game.GetValidMoves();
            if (moves.Count == 0) throw new InvalidOperationException("no valid moves");
            var scored = new List<(double v, int k)>(moves.Count);
            for (int k = 0; k < moves.Count; k++) scored.Add((BotEval.Try(game, moves[k], rng, out _), k));
            scored.Sort((a, b) => b.v.CompareTo(a.v));
            if (scored[0].v >= BotEval.WinValue || Rollouts <= 0 || Depth <= 0) return moves[scored[0].k];
            int n = Math.Min(Candidates, scored.Count);
            double best = double.MinValue;
            var choice = moves[scored[0].k];
            for (int c = 0; c < n; c++)
            {
                var m = moves[scored[c].k];
                double total = 0;
                for (int r = 0; r < Rollouts; r++)
                {
                    var sim = game.CloneReseeded(rng.NextULong());
                    sim.Swap(m.A, m.B);
                    for (int d = 0; d < Depth && sim.Status == GameStatus.Playing; d++)
                    {
                        var next = sim.GetValidMoves();
                        if (next.Count == 0) break;
                        SwapMove pick = next[rng.Next(next.Count)];
                        double pv = double.MinValue;
                        for (int s = 0; s < RolloutSample; s++)
                        {
                            var cand = next[rng.Next(next.Count)];
                            double v = BotEval.Try(sim, cand, rng, out _);
                            if (v > pv) { pv = v; pick = cand; }
                        }
                        sim.Swap(pick.A, pick.B);
                    }
                    total += BotEval.Value(sim);
                }
                double avg = total / Rollouts;
                if (avg > best) { best = avg; choice = m; }
            }
            return choice;
        }
    }

    public sealed class SimReport
    {
        public int LevelId;
        public string Bot;
        public int Games;
        public int Wins;
        public double WinRate => Games == 0 ? 0 : (double)Wins / Games;
        /// <summary>Average moves left in won games.</summary>
        public double AvgMovesLeft;
        /// <summary>Average share of goal work done in lost games (how close the losses were).</summary>
        public double AvgLossProgress;
        public double AvgStars;
        public int Reshuffles;

        public override string ToString() =>
            "level " + LevelId + " " + Bot + ": " + Wins + "/" + Games + " (" + (WinRate * 100).ToString("0") + "%), moves left " +
            AvgMovesLeft.ToString("0.0") + ", loss progress " + (AvgLossProgress * 100).ToString("0") + "%";
    }

    /// <summary>Plays a level many times with a bot and reports the win rate.</summary>
    public static class LevelSimulator
    {
        /// <summary>One attempt; returns the finished game (Won or Lost).</summary>
        public static Match3Game PlayOne(LevelData level, IMatch3Bot bot, long attemptSeed, int maxActions = 500)
        {
            var g = Match3Game.Create(level, attemptSeed);
            g.LogEnabled = false;
            var rng = new Rng(unchecked(attemptSeed * 7919 + 13));
            for (int k = 0; k < maxActions && g.Status == GameStatus.Playing; k++)
            {
                if (!g.HasValidMove()) break;
                var m = bot.Choose(g, rng);
                if (!g.Swap(m.A, m.B).Accepted) break;
            }
            return g;
        }

        /// <summary>
        /// Plays <paramref name="games"/> attempts (seeds seedBase, seedBase+1, …) in parallel. Deterministic:
        /// each attempt depends only on its seed. <paramref name="botFactory"/> makes one bot per attempt.
        /// </summary>
        public static SimReport Run(LevelData level, Func<IMatch3Bot> botFactory, int games, long seedBase = 1, int threads = 0)
        {
            var won = new bool[games];
            var movesLeft = new int[games];
            var progress = new double[games];
            var stars = new int[games];
            var reshuffles = new int[games];
            string name = botFactory().Name;
            var opts = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Environment.ProcessorCount };
            Parallel.For(0, games, opts, i =>
            {
                var g0 = Match3Game.Create(level, seedBase + i);
                double work0 = Math.Max(1e-9, BotEval.Work(g0));
                var g = PlayOne(level, botFactory(), seedBase + i);
                won[i] = g.Status == GameStatus.Won;
                movesLeft[i] = g.MovesLeft;
                stars[i] = g.Status == GameStatus.Won ? g.Stars : 0;
                progress[i] = 1 - BotEval.Work(g) / work0;
                reshuffles[i] = g.Reshuffles;
            });
            var r = new SimReport { LevelId = level.Id, Bot = name, Games = games };
            double ml = 0, lp = 0, st = 0;
            int losses = 0;
            for (int i = 0; i < games; i++)
            {
                if (won[i]) { r.Wins++; ml += movesLeft[i]; st += stars[i]; }
                else { losses++; lp += progress[i]; }
                r.Reshuffles += reshuffles[i];
            }
            r.AvgMovesLeft = r.Wins > 0 ? ml / r.Wins : 0;
            r.AvgStars = r.Wins > 0 ? st / r.Wins : 0;
            r.AvgLossProgress = losses > 0 ? lp / losses : 0;
            return r;
        }
    }

    /// <summary>Hint for the player (after a few idle seconds): the move the good bot's first ply likes best.</summary>
    public static class Hints
    {
        public static SwapMove? Find(Match3Game game, ulong seed = 1)
        {
            var moves = game.GetValidMoves();
            if (moves.Count == 0) return null;
            var rng = new Rng(seed);
            double best = double.MinValue;
            SwapMove choice = moves[0];
            foreach (var m in moves)
            {
                double v = BotEval.Try(game, m, rng, out _);
                if (v > best) { best = v; choice = m; }
            }
            return choice;
        }
    }
}
