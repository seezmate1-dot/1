using System;

namespace SunnyValley.Core.Match3
{
    /// <summary>A cell on the board. X is the column (0 = left), Y is the row (0 = top); gravity pulls to +Y.</summary>
    public readonly struct Pos : IEquatable<Pos>
    {
        public readonly int X;
        public readonly int Y;

        public Pos(int x, int y) { X = x; Y = y; }

        public static readonly Pos None = new Pos(-1, -1);

        public bool IsNone => X < 0 || Y < 0;
        public bool Equals(Pos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Pos p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(Pos a, Pos b) => a.Equals(b);
        public static bool operator !=(Pos a, Pos b) => !a.Equals(b);
        public bool IsAdjacent(Pos o) => Math.Abs(X - o.X) + Math.Abs(Y - o.Y) == 1;
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    /// <summary>The six crops. Early levels use the first 4–5. Rainbow and chicks have <see cref="None"/>.</summary>
    public enum TileColor
    {
        None = -1,
        Tomato = 0,
        Corn = 1,
        Apple = 2,
        Carrot = 3,
        Eggplant = 4,
        Blueberry = 5,
    }

    public enum PieceKind : byte
    {
        Empty = 0,
        /// <summary>An ordinary crop or a special piece (see <see cref="SpecialKind"/>).</summary>
        Tile = 1,
        /// <summary>A chick: falls like a tile, can be swapped, never matches, immune to blasts, rescued at an exit cell.</summary>
        Chick = 2,
    }

    public enum SpecialKind : byte
    {
        None = 0,
        /// <summary>Вилы, horizontal: made from a horizontal 4-line, clears its row.</summary>
        LineH = 1,
        /// <summary>Вилы, vertical: made from a vertical 4-line, clears its column.</summary>
        LineV = 2,
        /// <summary>Пчела: made from a 2×2 square, hits its own cross then flies to the most useful cell.</summary>
        Bee = 3,
        /// <summary>Бочка-бомба: made from an L or T, blasts 3×3.</summary>
        Bomb = 4,
        /// <summary>Радуга-подсолнух: made from a 5-line, colourless, removes every tile of one colour.</summary>
        Rainbow = 5,
    }

    /// <summary>Things that sit on a cell together with (overlay) or instead of (blocker) a piece, or under it (grass).</summary>
    public enum ObstacleKind : byte
    {
        None = 0,
        // under the piece
        Grass = 1,
        // over the piece (the piece stays in the cell and cannot move while it is there)
        Ice = 2,
        Chain = 3,
        Honey = 4,
        // instead of a piece (the cell is solid)
        Crate = 5,
        Stone = 6,
        Weed = 7,
        Bed = 8,
        Basket = 9,
    }

    /// <summary>Why a cell was hit. Decides which obstacles take damage (see docs: «Что чем бьётся»).</summary>
    public enum HitSource : byte
    {
        /// <summary>The cell's own piece was part of a match.</summary>
        Match = 0,
        /// <summary>The cell is side-adjacent to a match.</summary>
        Adjacent = 1,
        /// <summary>Вилы, horizontal or vertical (also crosses and 3-line combos).</summary>
        Line = 2,
        /// <summary>A bomb blast (3×3, 5×5) or the bomb part of a combo.</summary>
        Blast = 3,
        /// <summary>A bee's own cross or the cell it flew to.</summary>
        Bee = 4,
        /// <summary>A rainbow removing a colour or rainbow+rainbow clearing the board.</summary>
        Rainbow = 5,
        /// <summary>An in-level booster: shovel, rake, watering can.</summary>
        Booster = 6,
        /// <summary>A pre-level booster or a rainbow+special conversion replaced the piece (no damage, internal).</summary>
        Transform = 7,
    }

    public enum GoalType
    {
        /// <summary>Collect N tiles of a colour (any way a tile of that colour leaves the board counts).</summary>
        Collect = 0,
        /// <summary>Clear grass: counts cells whose last grass layer is removed.</summary>
        Grass = 1,
        Crate = 2,
        Ice = 3,
        Stone = 4,
        Honey = 5,
        Weed = 6,
        Chain = 7,
        /// <summary>Rescue chicks (bring them to an exit cell).</summary>
        Chick = 8,
        /// <summary>Harvest crops from garden beds.</summary>
        Crop = 9,
        /// <summary>Fill baskets.</summary>
        Basket = 10,
    }

    public enum BoosterKind
    {
        // pre-level, placed on the board at start
        PreLine = 0,
        PreBomb = 1,
        PreRainbow = 2,
        // in-level, free action (no move spent)
        Shovel = 10,
        Rake = 11,
        WateringCan = 12,
        Shuffle = 13,
    }

    public enum GameStatus
    {
        Playing = 0,
        Won = 1,
        /// <summary>Out of moves. <see cref="Match3Game.ContinueWithExtraMoves"/> resumes play.</summary>
        Lost = 2,
    }

    public enum MatchShape
    {
        Three = 0,
        Square = 1,
        Four = 2,
        LT = 3,
        Five = 4,
    }

    public enum ComboKind
    {
        None = 0,
        /// <summary>Вилы+вилы: the row and the column through the centre.</summary>
        LineLine,
        /// <summary>Вилы+бомба: three rows and three columns through the centre.</summary>
        LineBomb,
        /// <summary>Бомба+бомба: 5×5.</summary>
        BombBomb,
        /// <summary>Пчела+пчела: own cross, then three bees fly.</summary>
        BeeBee,
        /// <summary>Пчела+вилы: the bee carries the pitchfork to its target, where it fires.</summary>
        BeeLine,
        /// <summary>Пчела+бомба: the bee carries the bomb to its target, where it explodes.</summary>
        BeeBomb,
        /// <summary>Радуга+обычная фишка: removes every tile of that colour.</summary>
        RainbowColor,
        /// <summary>Радуга+вилы: every tile of the pitchfork's colour becomes a pitchfork and fires.</summary>
        RainbowLine,
        RainbowBomb,
        RainbowBee,
        /// <summary>Радуга+радуга: every cell is hit once.</summary>
        RainbowRainbow,
    }

    public static class TileColors
    {
        public static readonly string[] Names = { "tomato", "corn", "apple", "carrot", "eggplant", "blueberry" };
        public const int Max = 6;

        public static TileColor Parse(string name)
        {
            if (string.IsNullOrEmpty(name)) return TileColor.None;
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase)) return (TileColor)i;
            throw new FormatException("Unknown colour '" + name + "'");
        }

        public static string Name(TileColor c) => c == TileColor.None ? "none" : Names[(int)c];

        /// <summary>One-letter codes used in level layouts: r y g o p b.</summary>
        public const string Letters = "rygopb";
    }
}
