using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    public sealed class Goal
    {
        public GoalType Type;
        /// <summary>Only for <see cref="GoalType.Collect"/>.</summary>
        public TileColor Color = TileColor.None;
        public int Target;
        public int Current;

        public bool Done => Current >= Target;
        public int Remaining => Math.Max(0, Target - Current);

        public Goal Clone() => (Goal)MemberwiseClone();
        public override string ToString() => LevelLoader.GoalName(Type) + (Color != TileColor.None ? ":" + TileColors.Name(Color) : "") + " " + Current + "/" + Target;
    }

    /// <summary>A swap the player can make: drag the piece at <see cref="A"/> onto <see cref="B"/>.</summary>
    public readonly struct SwapMove
    {
        public readonly Pos A, B;
        public SwapMove(Pos a, Pos b) { A = a; B = b; }
        public override string ToString() => A + "->" + B;
    }

    /// <summary>
    /// The match-3 engine for one level attempt. Deterministic: the same level, seed and sequence of actions
    /// always produce the same boards and the same step logs. Pure C#, no Unity.
    ///
    /// Typical use from the view:
    /// <code>
    /// var game = Match3Game.Create(level, seed, preBoosters);
    /// // draw game.Board, play game.InitialEvents (pre-level boosters)
    /// var r = game.Swap(a, b);           // or game.UseBooster(BoosterKind.Rake, new Pos(0, row))
    /// foreach (var e in r.Events) ...    // animate exactly what happened
    /// if (game.Status == GameStatus.Lost) game.ContinueWithExtraMoves(5);
    /// </code>
    /// </summary>
    public sealed partial class Match3Game
    {
        public LevelData Level { get; private set; }
        public BoardTopology Topo { get; private set; }
        public Board Board { get; private set; }
        public Rng Rng { get; private set; }
        public List<Goal> Goals { get; private set; }
        public int Colors { get; private set; }
        public int MovesLeft { get; private set; }
        public int MovesUsed { get; private set; }
        public GameStatus Status { get; private set; }
        public int Stars { get; private set; }
        /// <summary>How many times +5 moves was bought in this attempt (prices grow: 900, 1900, 2900 — meta decides).</summary>
        public int Continues { get; private set; }
        /// <summary>How many automatic reshuffles happened (no moves left).</summary>
        public int Reshuffles { get; private set; }
        /// <summary>Events of the setup (pre-level boosters placed). Empty if none.</summary>
        public List<StepEvent> InitialEvents { get; private set; } = new List<StepEvent>();

        /// <summary>Set false for bots: the engine then skips building the step log (much faster).</summary>
        public bool LogEnabled = true;

        int _nextPieceId = 1;
        int _chicksToSpawn;
        int _chickMaxOnBoard;
        int _chickGap;
        int _lastChickSpawnMove = -1000;
        bool _honeyHit, _weedHit;
        int[] _star = new int[2];

        // ------------------------------------------------------------------ creation

        /// <summary>
        /// Starts an attempt. The starting board comes from the level's own seed (the same every attempt, as
        /// designed); <paramref name="attemptSeed"/> drives everything after it (spawns, bee ties, reshuffles,
        /// pre-level booster cells). Omitted = the level seed.
        /// </summary>
        public static Match3Game Create(LevelData level, long? attemptSeed = null, IList<BoosterKind> preBoosters = null)
        {
            var errors = LevelValidator.ValidateStructure(level);
            if (errors.Count > 0) throw new ArgumentException("Invalid level " + level.Id + ": " + string.Join("; ", errors));
            var g = new Match3Game();
            g.Level = level;
            g.Topo = LevelLoader.BuildTopology(level);
            g.Board = LevelLoader.BuildBoard(level, g.Topo);
            g.Rng = new Rng(level.Seed);
            g.Colors = level.Colors;
            g.MovesLeft = level.Moves;
            g.Status = GameStatus.Playing;
            g._chicksToSpawn = level.Chicks?.Spawn ?? 0;
            g._chickMaxOnBoard = level.Chicks?.MaxOnBoard ?? 2;
            g._chickGap = level.Chicks?.Gap ?? 2;
            if (level.Stars != null && level.Stars.Length == 2) g._star = new[] { level.Stars[0], level.Stars[1] };
            else g._star = new[] { Math.Max(1, (int)Math.Ceiling(level.Moves * 0.10)), Math.Max(2, (int)Math.Ceiling(level.Moves * 0.25)) };

            g.Goals = new List<Goal>();
            foreach (var gd in level.Goals)
            {
                var type = LevelLoader.ParseGoalType(gd.Type);
                var goal = new Goal { Type = type, Target = gd.Count };
                if (type == GoalType.Collect) goal.Color = TileColors.Parse(gd.Color);
                if (goal.Target <= 0)
                {
                    var ob = LevelLoader.GoalObstacle(type);
                    if (ob != ObstacleKind.None) goal.Target = LevelLoader.CountObstacle(g.Board, ob);
                    else if (type == GoalType.Chick) goal.Target = LevelLoader.TotalChicks(level);
                }
                g.Goals.Add(goal);
            }

            g.FillInitial();
            // A preset layout can come out dead: reshuffle it (keeps obstacles) so every start is playable.
            if (g.FindMatches().Count > 0 || !g.HasValidMove()) g.Reshuffle(false);
            g.Rng = new Rng(unchecked((attemptSeed ?? level.Seed) * 31 + 17));
            if (preBoosters != null && preBoosters.Count > 0) g.PlacePreBoosters(preBoosters);
            return g;
        }

        Match3Game() { }

        /// <summary>Deep copy for bots and hints. The copy logs only if <paramref name="withLog"/>.</summary>
        public Match3Game Clone(bool withLog = false) => Clone(withLog, null);

        /// <summary>
        /// Deep copy whose future randomness is re-seeded: a bot that looks ahead on it does not see the real
        /// spawns to come (no clairvoyance), only plausible ones.
        /// </summary>
        public Match3Game CloneReseeded(ulong seed) => Clone(false, seed);

        Match3Game Clone(bool withLog, ulong? reseed)
        {
            var g = (Match3Game)MemberwiseClone();
            g.Board = Board.Clone();
            g.Rng = reseed.HasValue ? new Rng(reseed.Value) : Rng.Clone();
            g.Goals = new List<Goal>(Goals.Count);
            foreach (var goal in Goals) g.Goals.Add(goal.Clone());
            g.InitialEvents = new List<StepEvent>();
            g.LogEnabled = withLog;
            g._log = null;
            g._scratch = null;
            g._acts = null;
            return g;
        }

        public bool AllGoalsDone
        {
            get
            {
                foreach (var goal in Goals) if (!goal.Done) return false;
                return true;
            }
        }

        public int StarThreshold2 => _star[0];
        public int StarThreshold3 => _star[1];

        public int StarsFor(int movesLeft) => movesLeft >= _star[1] ? 3 : movesLeft >= _star[0] ? 2 : 1;

        public int ChicksStillToSpawn => _chicksToSpawn;

        // ------------------------------------------------------------------ player actions

        /// <summary>
        /// The player drags the piece at <paramref name="a"/> onto the neighbouring cell <paramref name="b"/>.
        /// Invalid swaps change nothing and spend no move (<see cref="MoveResult.Accepted"/> false).
        /// </summary>
        public MoveResult Swap(Pos a, Pos b)
        {
            BeginAction();
            var reason = CheckSwap(a, b);
            if (reason != null)
            {
                if (_log != null) _log.Add(new SwapRejectedEvent { A = a, B = b, Reason = reason.Value });
                return EndAction(false);
            }
            int ia = Topo.Index(a), ib = Topo.Index(b);
            MovesLeft--;
            MovesUsed++;
            if (_log != null) _log.Add(new MovesChangedEvent { MovesLeft = MovesLeft, Delta = -1 });

            var pa = Board.Pieces[ia];
            var pb = Board.Pieces[ib];
            Board.Pieces[ia] = pb;
            Board.Pieces[ib] = pa;
            if (_log != null) _log.Add(new SwapEvent { A = a, B = b, PieceA = pa.Id, PieceB = pb.Id });

            _wave = 0;
            ClearWaveState();
            var combo = ComboOf(pa, pb);
            if (combo != ComboKind.None)
            {
                RunCombo(combo, ia, ib, pa, pb);
            }
            else
            {
                // Plain swap, or one special (Line/Bee/Bomb) swapped with a tile or chick: matches first,
                // then the moved special fires where it landed (if a match did not already set it off).
                var groups = FindMatches(ia, ib);
                ApplyMatches(groups, ia, ib);
                if (pa.IsSpecial) TriggerIfStill(ib, pa.Id);
                if (pb.IsSpecial) TriggerIfStill(ia, pb.Id);
                ProcessActivations();
            }
            Cascade();
            EndOfMove(spendMove: true);
            return EndAction(true);
        }

        /// <summary>
        /// In-level booster. Shovel hits one cell, Rake a row (target.Y), WateringCan a column (target.X),
        /// Shuffle reshuffles. Free: no move is spent; honey and weed do not spread after it.
        /// </summary>
        public MoveResult UseBooster(BoosterKind kind, Pos target)
        {
            BeginAction();
            if (Status != GameStatus.Playing) return EndAction(false);
            _wave = 0;
            ClearWaveState();
            if (kind == BoosterKind.Shuffle)
            {
                if (!Reshuffle(true)) return EndAction(false);
                if (_log != null) _log.Add(new BoosterEvent { Booster = kind, Target = target });
                EndOfMove(spendMove: false);
                return EndAction(true);
            }
            var cells = new List<int>();
            switch (kind)
            {
                case BoosterKind.Shovel:
                    if (!Topo.IsCell(target)) return EndAction(false);
                    cells.Add(Topo.Index(target));
                    break;
                case BoosterKind.Rake:
                    if (target.Y < 0 || target.Y >= Topo.Height) return EndAction(false);
                    for (int x = 0; x < Topo.Width; x++) if (Topo.IsCell(x, target.Y)) cells.Add(Topo.Index(x, target.Y));
                    break;
                case BoosterKind.WateringCan:
                    if (target.X < 0 || target.X >= Topo.Width) return EndAction(false);
                    for (int y = 0; y < Topo.Height; y++) if (Topo.IsCell(target.X, y)) cells.Add(Topo.Index(target.X, y));
                    break;
                default:
                    return EndAction(false);
            }
            if (cells.Count == 0) return EndAction(false);
            int id = NextId();
            if (_log != null)
            {
                var be = new BoosterEvent { Booster = kind, Target = target, ActivationId = id };
                foreach (var c in cells) be.Cells.Add(Topo.PosOf(c));
                _log.Add(be);
            }
            foreach (var c in cells) Hit(c, HitSource.Booster, id, 0);
            ProcessActivations();
            Cascade();
            EndOfMove(spendMove: false);
            return EndAction(true);
        }

        /// <summary>After a loss: +N moves (5 by default) and play on.</summary>
        public MoveResult ContinueWithExtraMoves(int extra = 5)
        {
            BeginAction();
            if (Status != GameStatus.Lost || extra <= 0) return EndAction(false);
            Continues++;
            MovesLeft += extra;
            Status = GameStatus.Playing;
            if (_log != null) _log.Add(new MovesChangedEvent { MovesLeft = MovesLeft, Delta = extra });
            if (!HasValidMove()) Reshuffle(false);
            return EndAction(true);
        }

        // ------------------------------------------------------------------ swap validation

        RejectReason? CheckSwap(Pos a, Pos b)
        {
            if (Status != GameStatus.Playing) return RejectReason.GameOver;
            if (!a.IsAdjacent(b) || !Topo.IsCell(a) || !Topo.IsCell(b)) return RejectReason.NotAdjacent;
            int ia = Topo.Index(a), ib = Topo.Index(b);
            if (!Board.IsSwappable(ia) || !Board.IsSwappable(ib)) return RejectReason.NotSwappable;
            if (Topo.WallBetween(ia, ib)) return RejectReason.Wall;
            if (!SwapHasEffect(ia, ib)) return RejectReason.NoMatch;
            return null;
        }

        public bool IsValidSwap(Pos a, Pos b) => CheckSwap(a, b) == null;

        bool SwapHasEffect(int ia, int ib)
        {
            var pa = Board.Pieces[ia];
            var pb = Board.Pieces[ib];
            if (pa.IsChick && pb.IsChick) return false;
            bool ra = pa.Special == SpecialKind.Rainbow, rb = pb.Special == SpecialKind.Rainbow;
            if (ra || rb) return pa.IsTile && pb.IsTile; // rainbow + chick does nothing
            if (pa.IsSpecial || pb.IsSpecial) return true;
            Board.Pieces[ia] = pb;
            Board.Pieces[ib] = pa;
            bool m = MatchesAt(ia) || MatchesAt(ib);
            Board.Pieces[ia] = pa;
            Board.Pieces[ib] = pb;
            return m;
        }

        /// <summary>All valid swaps (each pair once, A is the left/upper cell).</summary>
        public List<SwapMove> GetValidMoves()
        {
            var list = new List<SwapMove>();
            if (Status != GameStatus.Playing) return list;
            int w = Topo.Width, h = Topo.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = Topo.Index(x, y);
                    if (!Board.IsSwappable(i)) continue;
                    if (x + 1 < w && PairValid(i, i + 1)) list.Add(new SwapMove(new Pos(x, y), new Pos(x + 1, y)));
                    if (y + 1 < h && PairValid(i, i + w)) list.Add(new SwapMove(new Pos(x, y), new Pos(x, y + 1)));
                }
            return list;
        }

        bool PairValid(int i, int j) =>
            Topo.Exists[j] && Board.IsSwappable(j) && !Topo.WallBetween(i, j) && SwapHasEffect(i, j);

        public bool HasValidMove()
        {
            int w = Topo.Width, h = Topo.Height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = Topo.Index(x, y);
                    if (!Board.IsSwappable(i)) continue;
                    if (x + 1 < w && PairValid(i, i + 1)) return true;
                    if (y + 1 < h && PairValid(i, i + w)) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------ end of move

        void EndOfMove(bool spendMove)
        {
            if (AllGoalsDone)
            {
                Status = GameStatus.Won;
                Stars = StarsFor(MovesLeft);
                if (_log != null) _log.Add(new GameEndEvent { Wave = _wave, Status = Status, Stars = Stars, MovesLeft = MovesLeft });
                return;
            }
            if (spendMove)
            {
                SpreadIfUntouched(ObstacleKind.Honey, _honeyHit);
                SpreadIfUntouched(ObstacleKind.Weed, _weedHit);
            }
            if (MovesLeft <= 0)
            {
                Status = GameStatus.Lost;
                if (_log != null) _log.Add(new GameEndEvent { Wave = _wave, Status = Status, Stars = 0, MovesLeft = 0 });
                return;
            }
            if (!HasValidMove()) Reshuffle(false);
        }

        /// <summary>
        /// Honey: if no honey was hit during the move, one honey cell spreads onto a neighbouring plain tile
        /// (the tile stays, trapped). Weed: same, but the weed takes the neighbour's cell (the tile is gone).
        /// </summary>
        void SpreadIfUntouched(ObstacleKind kind, bool wasHit)
        {
            if (wasHit) return;
            var options = new List<(int from, int to)>();
            int n = Topo.Count, w = Topo.Width;
            for (int i = 0; i < n; i++)
            {
                bool isSource = kind == ObstacleKind.Honey ? Board.Overlay[i] == ObstacleKind.Honey : Board.Blocker[i] == ObstacleKind.Weed;
                if (!isSource) continue;
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (!Topo.IsCell(nx, ny)) continue;
                    int j = Topo.Index(nx, ny);
                    if (Topo.WallBetween(i, j)) continue;
                    if (Board.Blocker[j] != ObstacleKind.None || Board.Overlay[j] != ObstacleKind.None) continue;
                    if (!Board.Pieces[j].IsPlainTile) continue;
                    options.Add((i, j));
                }
            }
            if (options.Count == 0) return;
            var (f, t) = options[Rng.Next(options.Count)];
            var ev = _log != null ? new SpreadEvent { Wave = _wave, Obstacle = kind, From = Topo.PosOf(f), To = Topo.PosOf(t) } : null;
            if (kind == ObstacleKind.Honey)
            {
                Board.Overlay[t] = ObstacleKind.Honey;
                Board.OverlayLayers[t] = 1;
            }
            else
            {
                if (ev != null) ev.ConsumedPieceId = Board.Pieces[t].Id;
                Board.Pieces[t] = default;
                Board.Blocker[t] = ObstacleKind.Weed;
                Board.BlockerLayers[t] = 1;
            }
            if (ev != null) _log.Add(ev);
            // A growing obstacle adds work to an "all of them" goal.
            for (int k = 0; k < Goals.Count; k++)
            {
                var goal = Goals[k];
                if (LevelLoader.GoalObstacle(goal.Type) != kind || goal.Done || !LevelGoalIsAll(goal)) continue;
                goal.Target++;
                if (_log != null) AddEvent(new GoalProgressEvent { GoalIndex = k, Goal = goal.Type, Delta = 0, Current = goal.Current, Target = goal.Target, Cell = Topo.PosOf(t) });
            }
        }

        bool LevelGoalIsAll(Goal goal)
        {
            foreach (var gd in Level.Goals)
                if (LevelLoader.ParseGoalType(gd.Type) == goal.Type) return gd.Count <= 0;
            return false;
        }

        // ------------------------------------------------------------------ log plumbing

        List<StepEvent> _log;
        int _wave;
        int _nextId;

        void BeginAction()
        {
            _log = LogEnabled ? new List<StepEvent>() : null;
            _nextId = 1;
            _wave = 0;
            _honeyHit = false;
            _weedHit = false;
        }

        MoveResult EndAction(bool accepted)
        {
            var r = new MoveResult { Accepted = accepted, Status = Status, MovesLeft = MovesLeft, Waves = _wave + 1 };
            if (_log != null) r.Events = _log;
            _log = null;
            return r;
        }

        int NextId() => _nextId++;

        void AddEvent(StepEvent e)
        {
            if (_log == null) return;
            e.Wave = _wave;
            _log.Add(e);
        }

        internal static readonly int[] Dx = { 0, 0, -1, 1 };
        internal static readonly int[] Dy = { -1, 1, 0, 0 };
    }
}
