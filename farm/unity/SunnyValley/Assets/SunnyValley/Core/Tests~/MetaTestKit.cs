using System;
using System.IO;
using NUnit.Framework;
using SunnyValley.Core.Meta;

namespace SunnyValley.Core.Tests
{
    /// <summary>Loads the real meta data (Resources/Meta) and builds games in useful states.</summary>
    public static class MetaTestKit
    {
        /// <summary>Day 19700 at 08:00 UTC (device offset 0): a morning, like the model player's first session.</summary>
        public const long T0 = 19700L * 86400 + 8 * 3600;
        public const long Minute = 60;
        public const long Hour = 3600;
        public const long Day = 86400;

        static MetaData data;
        static string metaDir;

        public static string MetaDir
        {
            get
            {
                if (metaDir != null) return metaDir;
                string dir = TestContext.CurrentContext.TestDirectory;
                while (dir != null)
                {
                    string candidate = Path.Combine(dir, "farm", "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta");
                    if (Directory.Exists(candidate)) return metaDir = candidate;
                    candidate = Path.Combine(dir, "unity", "SunnyValley", "Assets", "SunnyValley", "Resources", "Meta");
                    if (Directory.Exists(candidate)) return metaDir = candidate;
                    dir = Path.GetDirectoryName(dir);
                }
                throw new DirectoryNotFoundException("Resources/Meta not found above " + TestContext.CurrentContext.TestDirectory);
            }
        }

        public static string BalanceJson => File.ReadAllText(Path.Combine(MetaDir, "balance.json"));
        public static string ChapterJson => File.ReadAllText(Path.Combine(MetaDir, "chapter1.json"));

        /// <summary>The real data, parsed once (it is immutable).</summary>
        public static MetaData Data => data ??= MetaData.Parse(BalanceJson, ChapterJson);

        /// <summary>Fresh data that a test may modify.</summary>
        public static MetaData FreshData() => MetaData.Parse(BalanceJson, ChapterJson);

        public static MetaGame NewGame(ulong seed = 42) => MetaGame.NewGame(Data, T0, seed);

        /// <summary>Plays the whole chapter 1 with granted stars: the farm is open at farm level 1.</summary>
        public static MetaGame FarmGame(ulong seed = 42, long now = T0)
        {
            var g = MetaGame.NewGame(Data, now, seed);
            FinishChapter(g, now);
            Assert.That(g.FarmOpen, Is.True);
            g.TakeEvents();
            return g;
        }

        public static void FinishChapter(MetaGame g, long now)
        {
            g.State.Wallet.Stars += g.StarsToFinishChapter();
            for (int guard = 0; guard < 1000 && !g.ChapterFinished; guard++)
            {
                var list = g.AvailableTasks();
                Assert.That(list, Is.Not.Empty, "chapter stuck");
                var r = g.CompleteTask(now, list[0].Id);
                Assert.That(r.Ok, Is.True, r.ErrorText);
            }
        }

        /// <summary>Raises the farm level by granting exactly the XP it needs.</summary>
        public static void SetLevel(MetaGame g, int level, long now)
        {
            long need = g.XpForLevel(level) - g.Xp;
            if (need > 0) g.Grant(now, new Reward { Xp = (int)need });
            Assert.That(g.Level, Is.EqualTo(level));
        }

        public static void Give(MetaGame g, string item, int count) =>
            g.State.Barn[item] = (g.State.Barn.TryGetValue(item, out int v) ? v : 0) + count;
    }
}
