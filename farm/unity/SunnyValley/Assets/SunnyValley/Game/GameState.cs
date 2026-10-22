using System;
using System.IO;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game
{
    /// <summary>
    /// The player's progress: the real meta game (Core/Meta MetaGame — currencies, lives, chapter 1, the farm) saved
    /// as a file in Application.persistentDataPath, plus the device settings (music, sounds, vibration) in PlayerPrefs.
    /// The save is written to a temporary file and moved over the old one, the previous save kept as .bak, so a
    /// phone switched off mid-write never loses the farm. The screens read what they show through ToFarmHud and the
    /// helpers below; every rule lives in MetaGame.
    /// </summary>
    public sealed class GameState
    {
        const string SettingsKey = "sunnyvalley.settings.v1";
        public const string SaveFile = "sunnyvalley_save.json";

        public readonly MetaGame Meta;
        public MetaData Data => Meta.Data;

        public bool music = true;
        public bool sounds = true;
        public bool vibration = true;

        GameState(MetaGame meta) => Meta = meta;

        /// <summary>Seconds of the wall clock: the meta never reads the time itself.</summary>
        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFile);

        static MetaData data;

        /// <summary>The game data (balance + chapter 1) from Resources/Meta, parsed once.</summary>
        public static MetaData LoadData()
        {
            if (data != null)
                return data;
            var balance = Resources.Load<TextAsset>(MetaData.BalanceResource);
            var chapter = Resources.Load<TextAsset>(MetaData.ChapterResource);
            if (balance == null || chapter == null)
                throw new InvalidOperationException("Нет данных игры в Resources/Meta (balance.json, chapter1.json)");
            data = MetaData.Parse(balance.text, chapter.text);
            return data;
        }

        /// <summary>The saved game, the backup if the save is broken, or a new game.</summary>
        public static GameState Load()
        {
            var d = LoadData();
            MetaGame meta = null;
            foreach (string path in new[] { SavePath, SavePath + ".bak" })
            {
                try
                {
                    if (File.Exists(path))
                        meta = MetaGame.Load(d, File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Сохранение не читается (" + path + "): " + e.Message);
                }
                if (meta != null)
                    break;
            }
            if (meta == null)
            {
                int offset = (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes);
                ulong seed = (ulong)DateTime.UtcNow.Ticks ^ 0x9E3779B97F4A7C15UL;
                meta = MetaGame.NewGame(d, Now, seed, offset);
            }
            var state = new GameState(meta);
            state.LoadSettings();
            return state;
        }

        void LoadSettings()
        {
            string s = PlayerPrefs.GetString(SettingsKey, "111");
            music = s.Length > 0 && s[0] == '1';
            sounds = s.Length > 1 && s[1] == '1';
            vibration = s.Length > 2 && s[2] == '1';
        }

        public void Save()
        {
            PlayerPrefs.SetString(SettingsKey, (music ? "1" : "0") + (sounds ? "1" : "0") + (vibration ? "1" : "0"));
            PlayerPrefs.Save();
            try
            {
                string path = SavePath, tmp = path + ".tmp";
                File.WriteAllText(tmp, Meta.Save());
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", true);
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось сохранить игру: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ what the screens show

        public int MaxLives => Data.Balance.Lives.Max;

        /// <summary>Pre-level boosters in the order of the start popup: Вилы (Line), Бомба, Радуга.</summary>
        public int[] PreBoosters()
        {
            var r = new int[Boosters.PreLevel.Length];
            for (int i = 0; i < r.Length; i++)
                r[i] = Meta.BoosterCount(Boosters.PreLevel[i]);
            return r;
        }

        /// <summary>In-level boosters in the order of the level screen: Лопата, Грабли, Лейка, Перемешать.</summary>
        public int[] InLevelBoosters()
        {
            var r = new int[Boosters.InLevel.Length];
            for (int i = 0; i < r.Length; i++)
                r[i] = Meta.BoosterCount(Boosters.InLevel[i]);
            return r;
        }

        /// <summary>Restoration tasks the stars on hand pay for right now (the red dot on «Задания»).</summary>
        public int AffordableTasks()
        {
            if (Meta.ChapterFinished)
                return 0;
            int n = 0;
            foreach (var t in Meta.AvailableTasks())
            {
                if (t.Stars <= Meta.Stars)
                    n++;
            }
            return n;
        }

        public FarmHud ToFarmHud(long now)
        {
            bool infinite = Meta.InfiniteLives(now);
            int lives = Meta.Lives(now);
            return new FarmHud
            {
                Coins = Meta.Coins, Stars = Meta.Stars, Crystals = Meta.Crystals,
                Lives = infinite ? MaxLives : lives, MaxLives = MaxLives,
                NextLifeSeconds = infinite ? Meta.InfiniteLivesLeft(now) : Meta.NextLifeIn(now),
                InfiniteLives = infinite,
                Level = Meta.NextLevel, TasksBadge = AffordableTasks(), EventsBadge = 0,
                OrdersBadge = 0,
            };
        }
    }
}
