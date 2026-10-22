using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Match3
{
    /// <summary>A movable thing in a cell. Value type: the board stores them in an array, so cloning is a copy.</summary>
    public struct Piece : IEquatable<Piece>
    {
        /// <summary>Unique for the whole game, never reused: the view tracks pieces by it.</summary>
        public int Id;
        public PieceKind Kind;
        /// <summary>Colour of a tile, including Line/Bee/Bomb specials. None for rainbows and chicks.</summary>
        public TileColor Color;
        public SpecialKind Special;

        public bool IsEmpty => Kind == PieceKind.Empty;
        public bool IsTile => Kind == PieceKind.Tile;
        public bool IsChick => Kind == PieceKind.Chick;
        public bool IsSpecial => Kind == PieceKind.Tile && Special != SpecialKind.None;
        public bool IsPlainTile => Kind == PieceKind.Tile && Special == SpecialKind.None;

        public bool Equals(Piece o) => Id == o.Id && Kind == o.Kind && Color == o.Color && Special == o.Special;
        public override bool Equals(object obj) => obj is Piece p && Equals(p);
        public override int GetHashCode() => Id;

        public override string ToString()
        {
            if (Kind == PieceKind.Empty) return "empty";
            if (Kind == PieceKind.Chick) return "chick#" + Id;
            return TileColors.Name(Color) + (Special == SpecialKind.None ? "" : "/" + Special) + "#" + Id;
        }
    }

    /// <summary>
    /// The static shape of a level: which cells exist, walls, where each cell's piece falls to, spawners,
    /// portals and chick exits. Shared (read-only) between a board and its clones.
    /// </summary>
    public sealed class BoardTopology
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Exists;
        /// <summary>Index of the cell a piece in this cell falls into (straight down or through a portal), -1 if none.</summary>
        public readonly int[] Below;
        /// <summary>Reverse of <see cref="Below"/>: the cell that feeds this one from above, -1 if none.</summary>
        public readonly int[] Above;
        public readonly bool[] Spawner;
        public readonly bool[] ChickExit;
        public readonly bool[] WallDown;
        public readonly bool[] WallRight;
        public readonly bool[] PortalEntrance;
        public readonly bool[] PortalExit;
        /// <summary>Cell indices ordered so that every cell comes before the cell above it (bottom of each fall chain first).</summary>
        public readonly int[] BottomUpOrder;

        public BoardTopology(int width, int height)
        {
            Width = width;
            Height = height;
            int n = width * height;
            Exists = new bool[n];
            Below = new int[n];
            Above = new int[n];
            Spawner = new bool[n];
            ChickExit = new bool[n];
            WallDown = new bool[n];
            WallRight = new bool[n];
            PortalEntrance = new bool[n];
            PortalExit = new bool[n];
            BottomUpOrder = new int[n];
        }

        public int Count => Width * Height;
        public int Index(int x, int y) => y * Width + x;
        public int Index(Pos p) => p.Y * Width + p.X;
        public Pos PosOf(int i) => new Pos(i % Width, i / Width);
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsCell(int x, int y) => InBounds(x, y) && Exists[y * Width + x];
        public bool IsCell(Pos p) => IsCell(p.X, p.Y);

        /// <summary>True if a wall separates two side-adjacent cells.</summary>
        public bool WallBetween(int a, int b)
        {
            int ax = a % Width, ay = a / Width, bx = b % Width, by = b / Width;
            if (ay == by) return WallRight[Math.Min(ax, bx) + ay * Width];
            return WallDown[ax + Math.Min(ay, by) * Width];
        }

        /// <summary>Wires Below/Above from the mask, walls and portals, and computes the processing order.</summary>
        public void Build(IList<(int from, int to)> portals)
        {
            int n = Count;
            for (int i = 0; i < n; i++) { Below[i] = -1; Above[i] = -1; }
            for (int y = 0; y < Height - 1; y++)
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y), j = Index(x, y + 1);
                    if (Exists[i] && Exists[j] && !WallDown[i]) { Below[i] = j; Above[j] = i; }
                }
            if (portals != null)
                foreach (var (from, to) in portals)
                {
                    Below[from] = to;
                    Above[to] = from;
                    PortalEntrance[from] = true;
                    PortalExit[to] = true;
                }
            // Depth = number of steps to the end of the fall chain; deeper (lower) cells first.
            var depth = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (!Exists[i]) { depth[i] = int.MaxValue; continue; }
                int d = 0, c = i, guard = 0;
                while (Below[c] >= 0 && guard++ < n) { c = Below[c]; d++; }
                depth[i] = d;
            }
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);
            // Stable: by depth, then bottom row first, then left to right.
            order.Sort((a, b) =>
            {
                int c = depth[a].CompareTo(depth[b]);
                if (c != 0) return c;
                c = (b / Width).CompareTo(a / Width);
                return c != 0 ? c : (a % Width).CompareTo(b % Width);
            });
            order.CopyTo(BottomUpOrder);
        }

        /// <summary>True if following Below from this cell ever loops (portal cycles are invalid).</summary>
        public bool HasFallCycle()
        {
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                int c = i, steps = 0;
                while (c >= 0 && steps <= n) { c = Below[c]; steps++; }
                if (steps > n) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Mutable state of the board: a piece per cell plus three obstacle layers: grass under the piece,
    /// an overlay on the piece (ice, chain, honey) and a blocker instead of the piece (crate, stone, weed,
    /// bed, basket). Arrays indexed by y * Width + x.
    /// </summary>
    public sealed class Board
    {
        public readonly BoardTopology Topo;
        public readonly Piece[] Pieces;
        public readonly int[] Grass;
        public readonly ObstacleKind[] Overlay;
        public readonly int[] OverlayLayers;
        public readonly ObstacleKind[] Blocker;
        /// <summary>Crate/stone/weed: layers left. Bed: growth so far. Basket: tiles still wanted.</summary>
        public readonly int[] BlockerLayers;
        /// <summary>Bed: hits needed for one crop.</summary>
        public readonly int[] BedNeed;
        public readonly TileColor[] BasketColor;

        public Board(BoardTopology topo)
        {
            Topo = topo;
            int n = topo.Count;
            Pieces = new Piece[n];
            Grass = new int[n];
            Overlay = new ObstacleKind[n];
            OverlayLayers = new int[n];
            Blocker = new ObstacleKind[n];
            BlockerLayers = new int[n];
            BedNeed = new int[n];
            BasketColor = new TileColor[n];
            for (int i = 0; i < n; i++) BasketColor[i] = TileColor.None;
        }

        public Board Clone()
        {
            var b = new Board(Topo);
            Array.Copy(Pieces, b.Pieces, Pieces.Length);
            Array.Copy(Grass, b.Grass, Grass.Length);
            Array.Copy(Overlay, b.Overlay, Overlay.Length);
            Array.Copy(OverlayLayers, b.OverlayLayers, OverlayLayers.Length);
            Array.Copy(Blocker, b.Blocker, Blocker.Length);
            Array.Copy(BlockerLayers, b.BlockerLayers, BlockerLayers.Length);
            Array.Copy(BedNeed, b.BedNeed, BedNeed.Length);
            Array.Copy(BasketColor, b.BasketColor, BasketColor.Length);
            return b;
        }

        public int Width => Topo.Width;
        public int Height => Topo.Height;
        public bool IsCell(Pos p) => Topo.IsCell(p);
        public Piece GetPiece(Pos p) => Pieces[Topo.Index(p)];
        public int GetGrass(Pos p) => Grass[Topo.Index(p)];
        public ObstacleKind GetOverlay(Pos p, out int layers) { int i = Topo.Index(p); layers = OverlayLayers[i]; return Overlay[i]; }
        public ObstacleKind GetBlocker(Pos p, out int layers) { int i = Topo.Index(p); layers = BlockerLayers[i]; return Blocker[i]; }

        /// <summary>Cell can hold a piece right now (exists and no blocker).</summary>
        public bool IsOpen(int i) => Topo.Exists[i] && Blocker[i] == ObstacleKind.None;

        /// <summary>The piece may be swapped by the player: a tile or chick with no ice/chain/honey on it.</summary>
        public bool IsSwappable(int i) => Topo.Exists[i] && !Pieces[i].IsEmpty && Overlay[i] == ObstacleKind.None;

        /// <summary>The piece falls with gravity: no overlay holds it.</summary>
        public bool IsMovable(int i) => !Pieces[i].IsEmpty && Overlay[i] == ObstacleKind.None;

        /// <summary>Colour this cell matches with, or None. Chained pieces match in place; ice and honey hide them.</summary>
        public TileColor MatchColor(int i)
        {
            var p = Pieces[i];
            if (p.Kind != PieceKind.Tile || p.Color == TileColor.None) return TileColor.None;
            var o = Overlay[i];
            if (o == ObstacleKind.Ice || o == ObstacleKind.Honey) return TileColor.None;
            return p.Color;
        }

        /// <summary>Simple text dump for tests and debugging: one char per cell.</summary>
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Topo.Index(x, y);
                    char c;
                    if (!Topo.Exists[i]) c = ' ';
                    else if (Blocker[i] != ObstacleKind.None) c = Blocker[i] switch
                    {
                        ObstacleKind.Crate => '#', ObstacleKind.Stone => '@', ObstacleKind.Weed => 'w',
                        ObstacleKind.Bed => 'B', ObstacleKind.Basket => 'U', _ => '?',
                    };
                    else
                    {
                        var p = Pieces[i];
                        if (p.IsEmpty) c = '.';
                        else if (p.IsChick) c = 'C';
                        else if (p.Special == SpecialKind.Rainbow) c = 'W';
                        else
                        {
                            c = TileColors.Letters[(int)p.Color];
                            if (p.Special != SpecialKind.None) c = char.ToUpperInvariant(c);
                        }
                    }
                    sb.Append(c);
                }
                if (y < Height - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
