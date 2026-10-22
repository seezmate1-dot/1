using System.Collections.Generic;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaSaveTests
    {
        [Test]
        public void SaveAndLoadKeepEverything()
        {
            var g = FarmGame(seed: 99);
            SetLevel(g, 6, T0);
            g.State.Wallet.Coins = 50000;
            g.Plant(T0, 0, "carrot");
            Give(g, "wheat", 9);
            g.Enqueue(T0, "mill", "flour");
            g.BuildWorkshop(T0, "bakery");
            g.ChangeLook("yard_path", "look2");
            g.SetPlacement("mill", 4, 7);
            g.Discard(T0, 2);
            g.StartLevel(T0, g.NextLevel, new[] { Boosters.Bomb });
            string json = g.Save();

            var h = MetaGame.Load(Data, json);
            Assert.That(h, Is.Not.Null);
            Assert.That(h.Save(), Is.EqualTo(json), "a loaded game saves byte-for-byte the same");
            Assert.That(h.Level, Is.EqualTo(6));
            Assert.That(h.Coins, Is.EqualTo(g.Coins));
            Assert.That(h.Plots[0].Crop, Is.EqualTo("carrot"));
            Assert.That(h.Workshop("mill").Jobs.Count, Is.EqualTo(1));
            Assert.That(h.HasWorkshop("bakery"), Is.True);
            Assert.That(h.ChoiceOf("yard_path"), Is.EqualTo("look2"));
            Assert.That(h.State.Layout["mill"], Is.EqualTo(new[] { 4, 7 }));
            Assert.That(h.ActiveLevel, Is.EqualTo(g.ActiveLevel));
            Assert.That(h.Board(T0)[0].Order.Uid, Is.EqualTo(g.Board(T0)[0].Order.Uid));
        }

        [Test]
        public void TheGameContinuesDeterministicallyAfterLoad()
        {
            var a = FarmGame(seed: 3);
            var b = MetaGame.Load(Data, a.Save());
            long now = T0;
            for (int i = 0; i < 20; i++)
            {
                now += 20 * Minute;
                a.Discard(now, i % 9);
                b.Discard(now, i % 9);
                Give(a, "wheat", 1);
                Give(b, "wheat", 1);
                a.Harvest(now, 0); b.Harvest(now, 0);
                a.Plant(now, 0, "wheat"); b.Plant(now, 0, "wheat");
            }
            now += Day;
            Assert.That(b.Save(), Is.EqualTo(a.Save()));
            Assert.That(MetaRng.Next(ref a.State.Rng), Is.EqualTo(MetaRng.Next(ref b.State.Rng)));
        }

        [Test]
        public void BrokenOrEditedSavesAreRejected()
        {
            var g = NewGame();
            string json = g.Save();
            Assert.That(MetaGame.Load(Data, json.Replace("\"coins\\\":300", "\"coins\\\":999999")), Is.Null, "edited");
            Assert.That(MetaGame.Load(Data, "{"), Is.Null);
            Assert.That(MetaGame.Load(Data, ""), Is.Null);
            Assert.That(MetaGame.Load(Data, null), Is.Null);
            Assert.That(SaveSystem.FromJson(SaveSystem.Wrap(SaveSystem.CurrentVersion + 1, "{}")), Is.Null, "from the future");
            Assert.That(SaveSystem.FromJson(SaveSystem.Wrap(0, "{}")), Is.Null);
        }

        [Test]
        public void VersionOneSavesAreMigrated()
        {
            // The prototype kept currencies and lives as flat fields.
            string v1 = "{\"version\":1,\"createdAt\":" + T0 + ",\"lastSeenAt\":" + T0 + ",\"rng\":7," +
                        "\"coins\":1234,\"stars\":5,\"crystals\":17,\"xp\":0,\"level\":1,\"lives\":3,\"livesRegenFrom\":" + T0 + "," +
                        "\"boosters\":{\"Bomb\":2},\"chapter\":{\"doneTasks\":[\"yard_gate\"]}}";
            var s = SaveSystem.FromJson(SaveSystem.Wrap(1, v1));
            Assert.That(s, Is.Not.Null);
            Assert.That(s.Version, Is.EqualTo(SaveSystem.CurrentVersion));
            Assert.That(s.Wallet.Coins, Is.EqualTo(1234));
            Assert.That(s.Wallet.Stars, Is.EqualTo(5));
            Assert.That(s.Wallet.Crystals, Is.EqualTo(17));
            Assert.That(s.Lives.Count, Is.EqualTo(3));
            Assert.That(s.Lives.RegenFrom, Is.EqualTo(T0));
            Assert.That(s.Boosters["Bomb"], Is.EqualTo(2));
            var g = new MetaGame(Data, s);
            Assert.That(g.IsTaskDone("yard_gate"), Is.True);
            Assert.That(g.Lives(T0 + 30 * Minute), Is.EqualTo(4));
            Assert.That(g.Plots, Is.Empty, "missing parts are filled with defaults");
            Assert.That(g.DailyTasks(T0).Count, Is.EqualTo(3));
            // Saved again, it is a current-version save.
            var again = SaveSystem.FromJson(g.Save());
            Assert.That(again.Wallet.Coins, Is.EqualTo(1234));
        }

        [Test]
        public void MigrationsCoverEveryOldVersion()
        {
            Assert.That(SaveSystem.Migrations.Count, Is.EqualTo(SaveSystem.CurrentVersion - 1));
        }

        [Test]
        public void SaveIsStableJsonWithSortedDictionaries()
        {
            var g = NewGame();
            g.State.Boosters["Shuffle"] = 3;
            g.State.Boosters["Bomb"] = 2;
            var keys = new List<string>(g.State.Boosters.Keys);
            Assert.That(keys, Is.Ordered);
            Assert.That(g.Save(), Does.Contain("\\\"wallet\\\""));
        }
    }
}
