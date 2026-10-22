using System.Collections.Generic;

namespace SunnyValley.Layout
{
    // What the screens show. Plain data the game fills from Core (or the demo data until Core lands);
    // the layout only reads it, so the tests can feed the longest numbers and texts on purpose.

    /// <summary>The farm screen's numbers.</summary>
    public sealed class FarmHud
    {
        public long Coins = 1250;
        public long Stars = 3;
        public long Crystals = 25;
        public int Lives = 5;
        public int MaxLives = 5;
        /// <summary>Seconds until the next life; ignored when lives are full.</summary>
        public long NextLifeSeconds;
        public int Level = 1;
        /// <summary>Red dots on the side buttons (0 = none).</summary>
        public int TasksBadge, EventsBadge, OrdersBadge, ShopBadge;
    }

    /// <summary>One goal of a level: a piece or obstacle icon and how many are left.</summary>
    public sealed class Goal
    {
        public string Icon;
        public int Count;

        public Goal(string icon, int count)
        {
            Icon = icon;
            Count = count;
        }
    }

    /// <summary>The level screen's numbers.</summary>
    public sealed class LevelHud
    {
        public int Level = 1;
        public int Moves = 25;
        public List<Goal> Goals = new List<Goal> { new Goal(Icons.Pieces[0], 20) };
        public int Cols = 9;
        public int Rows = 9;
        /// <summary>In-level boosters (GDD §4.5): Лопата, Грабли, Лейка, Перемешать.</summary>
        public int[] Boosters = { 3, 2, 2, 1 };
    }

    /// <summary>The popup before a level: its goals and the boosters to take along.</summary>
    public sealed class LevelStartInfo
    {
        public int Level = 1;
        public List<Goal> Goals = new List<Goal> { new Goal(Icons.Pieces[0], 20) };
        /// <summary>Pre-level boosters: Вилы, Бомба, Радуга — how many the player has.</summary>
        public int[] Boosters = { 1, 1, 0 };
        public bool[] Picked = { false, false, false };
    }

    public sealed class WinInfo
    {
        public int Level = 1;
        public int Stars = 3;
        public long Coins = 120;
        /// <summary>Stars for the farm (every level won gives one, GDD §3).</summary>
        public int FarmStars = 1;
    }

    public sealed class LoseInfo
    {
        public int Level = 1;
        public List<Goal> Left = new List<Goal> { new Goal(Icons.Pieces[0], 3) };
        /// <summary>The price of +5 moves (900, 1900, 2900, GDD §4.7).</summary>
        public int Price = 900;
        public bool CanAfford = true;
    }

    public sealed class SettingsInfo
    {
        public bool Music = true;
        public bool Sounds = true;
        public bool Vibration = true;
        /// <summary>Opened from a level: shows «Выйти из уровня».</summary>
        public bool InLevel;
        public string Version = "0.1";
    }

    public sealed class TaskItem
    {
        public string Title;
        public int Stars;

        public TaskItem(string title, int stars)
        {
            Title = title;
            Stars = stars;
        }
    }
}
