using System;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game
{
    /// <summary>
    /// The player's progress as the interface shell needs it: currencies, lives with their timer, the level reached,
    /// boosters and settings, kept in PlayerPrefs. The data hook for Core/Meta: when the meta (GDD §5–7) lands, its
    /// save replaces this class and ToFarmHud/ToLevelStart read from it; the screens do not change.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        public const int MaxLives = 5;
        public const long LifeSeconds = 30 * 60; // GDD §4.6
        static readonly int[] MovePrices = { 900, 1900, 2900 }; // GDD §4.7
        const string Key = "sunnyvalley.state.v1";

        public long coins = 1000;
        public long stars = 0;
        public long crystals = 20;
        public int lives = MaxLives;
        /// <summary>Unix seconds when the next life was started counting (meaningful below MaxLives).</summary>
        public long lifeClock;
        public int level = 1;
        /// <summary>In-level boosters: Лопата, Грабли, Лейка, Перемешать (generous at the start, GDD §2.4).</summary>
        public int[] boosters = { 3, 3, 3, 2 };
        /// <summary>Pre-level boosters: Вилы, Бомба, Радуга.</summary>
        public int[] preBoosters = { 2, 2, 1 };
        public bool music = true;
        public bool sounds = true;
        public bool vibration = true;

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static GameState Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                    return JsonUtility.FromJson<GameState>(json) ?? new GameState();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Сохранение не читается, начинаем заново: " + e.Message);
            }
            return new GameState();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>Gives back the lives that have grown since lifeClock.</summary>
        public void UpdateLives(long now)
        {
            if (lives >= MaxLives)
            {
                lifeClock = now;
                return;
            }
            long grown = (now - lifeClock) / LifeSeconds;
            if (grown > 0)
            {
                lives = (int)Math.Min(MaxLives, lives + grown);
                lifeClock += grown * LifeSeconds;
                if (lives >= MaxLives)
                    lifeClock = now;
            }
        }

        public long NextLifeIn(long now) => lives >= MaxLives ? 0 : Math.Max(0, LifeSeconds - (now - lifeClock));

        public void LoseLife(long now)
        {
            UpdateLives(now);
            if (lives >= MaxLives)
                lifeClock = now;
            lives = Math.Max(0, lives - 1);
        }

        public static int MovesPrice(int bought) => MovePrices[Math.Min(bought, MovePrices.Length - 1)];

        public FarmHud ToFarmHud(long now)
        {
            UpdateLives(now);
            return new FarmHud
            {
                Coins = coins, Stars = stars, Crystals = crystals, Lives = lives, MaxLives = MaxLives,
                NextLifeSeconds = NextLifeIn(now), Level = level, TasksBadge = stars > 0 ? 1 : 0, EventsBadge = 1,
            };
        }
    }
}
