using SunnyValley.Core.Meta;

namespace SunnyValley.Game.Economy
{
    /// <summary>
    /// A debug save in the middle of chapter 2, for the CI screenshots and the tests: chapter 1 restored, farm level
    /// 7, plots growing and ripe, chickens and cows fed and hungry, the mill and the bakery busy, a dairy, goods and
    /// tools in the barn, a few houses, orders on the board and the truck waiting. Made by playing the real rules
    /// (MetaGame) from a fixed seed, so it is the same every time.
    /// </summary>
    public static class EconomyDebug
    {
        public const ulong Seed = 20261023;

        /// <summary>The game as it stands at <paramref name="now"/>; everything was done in the three hours before.</summary>
        public static MetaGame Game(MetaData data, long now)
        {
            long t = now - 3 * 3600;
            var g = MetaGame.NewGame(data, t, Seed);
            g.State.Wallet.Stars += g.StarsToFinishChapter();
            for (int guard = 0; guard < 1000 && !g.ChapterFinished; guard++)
            {
                var list = g.AvailableTasks();
                if (list.Count == 0)
                    break;
                g.CompleteTask(t, list[0].Id);
            }
            if (!g.FarmOpen)
                return g;

            // farm level 7 with money to build with, crystals for speed-ups, tools for the barn
            long xp = g.XpForLevel(7) - g.Xp;
            g.Grant(t, new Reward { Xp = (int)System.Math.Max(0, xp), Coins = 6000, Crystals = 120 });
            g.Grant(t, new Reward { Tools = { ["hammer"] = 2, ["nails"] = 1, ["paint"] = 2, ["saw"] = 1, ["axe"] = 1 } });
            for (int i = 0; i < 3; i++)
                g.Build(t, "cottage");
            g.Build(t, "cafe");
            g.Build(t, "bench");
            g.BuildWorkshop(t, "bakery");
            g.BuildWorkshop(t, "dairy");
            g.BuildAnimalHouse(t, "cowshed");
            g.BuyAnimal(t, "cow");
            g.BuyAnimal(t, "cow");
            for (int i = 0; i < 4; i++)
                g.BuyPlot(t);
            g.Grant(t, new Reward
            {
                Items =
                {
                    ["wheat"] = 14, ["corn"] = 8, ["carrot"] = 5, ["egg"] = 4, ["milk"] = 3, ["chicken_feed"] = 2, ["cow_feed"] = 1, ["flour"] = 2,
                    ["apple"] = 2, ["tomato"] = 3,
                },
            });

            // the plots: some ripe long ago, some planted along the way, two empty
            long[] planted = { 3 * 3600, 3 * 3600, 40 * 60, 9 * 60, 3 * 60, 50 * 60, 20 * 60, 0 };
            string[] crops = { "wheat", "corn", "sunflower", "carrot", "corn", "strawberry", "sunflower", null };
            for (int i = 0; i < crops.Length && i < g.Plots.Count; i++)
            {
                if (crops[i] != null)
                    g.Plant(now - planted[i], i, crops[i]);
            }

            // animals: two hens fed a while ago (one ready), a cow fed, the rest hungry
            int fed = 0;
            foreach (var a in g.State.Farm.Animals.ToArray())
            {
                if (fed < 3)
                {
                    long ago = a.Type == "chicken" ? (fed == 0 ? 30 * 60 : 8 * 60) : 25 * 60;
                    if (g.Feed(now - ago, a.Uid).Ok)
                        fed++;
                }
            }

            // workshops: the mill grinding, the bakery baking with a loaf done, the dairy idle
            g.Enqueue(now - 6 * 60, "mill", "chicken_feed");
            g.Enqueue(now - 6 * 60, "mill", "cow_feed");
            g.Enqueue(now - 40 * 60, "bakery", "bread");
            g.Enqueue(now - 40 * 60, "bakery", "corn_bread");
            g.Enqueue(now - 40 * 60, "dairy", "cream");

            // the order board: one order handed in a while ago (its card waits), the rest fresh
            var board = g.Board(now - 20 * 60);
            for (int i = 0; i < board.Count; i++)
            {
                if (g.CanFulfil(now - 20 * 60, i))
                {
                    g.Fulfil(now - 20 * 60, i);
                    break;
                }
            }
            g.Discard(now - 5 * 60, board.Count - 1);
            g.Board(now);
            g.Touch(now - 2 * 3600);
            g.TakeEvents();
            return g;
        }
    }
}
