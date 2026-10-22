using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaRetentionTests
    {
        [Test]
        public void ThreeDailyTasksOfDifferentKindsWithoutTheFarmBeforeChapterEnd()
        {
            var g = NewGame();
            var tasks = g.DailyTasks(T0);
            Assert.That(tasks.Count, Is.EqualTo(3));
            var defs = tasks.Select(t => Data.DailyTasks[t.Id]).ToList();
            Assert.That(defs.Select(d => d.Kind).Distinct().Count(), Is.EqualTo(3));
            Assert.That(defs.All(d => !d.Farm), "farm tasks wait for the farm");
        }

        [Test]
        public void DailyTasksCountProgressAndPay()
        {
            var g = NewGame(seed: 5);
            // Make the day's tasks known: wins, plays and stars.
            g.State.Daily.Tasks.Clear();
            g.State.Daily.Tasks.Add(new DailyTaskState { Id = "win3" });
            g.State.Daily.Tasks.Add(new DailyTaskState { Id = "play5" });
            g.State.Daily.Tasks.Add(new DailyTaskState { Id = "stars3" });
            Assert.That(g.ClaimDailyTask(T0, 0).Error, Is.EqualTo(MetaError.NotReady));
            for (int i = 0; i < 5; i++)
            {
                int lv = g.NextLevel;
                g.StartLevel(T0, lv);
                g.FinishLevel(T0, new LevelResult { Level = lv, Won = i % 2 == 0, Stars = 1 });
            }
            var tasks = g.DailyTasks(T0);
            Assert.That(tasks[0].Progress, Is.EqualTo(3));
            Assert.That(tasks[1].Progress, Is.EqualTo(5));
            g.State.Wallet.Stars = 5;
            g.CompleteTask(T0, "yard_gate");
            g.CompleteTask(T0, "yard_weeds");
            g.CompleteTask(T0, "yard_path");
            Assert.That(g.DailyTaskDone(tasks[2]), Is.True);
            long coins = g.Coins;
            Assert.That(g.ClaimDailyTask(T0, 0).Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins + Data.DailyTasks["win3"].Reward.Coins));
            Assert.That(g.ClaimDailyTask(T0, 0).Error, Is.EqualTo(MetaError.AlreadyClaimed));
            Assert.That(g.DailyChestReady(T0), Is.False);
            g.ClaimDailyTask(T0, 1);
            g.ClaimDailyTask(T0, 2);
            Assert.That(g.DailyChestReady(T0), Is.True);
            int crystals = g.Crystals;
            Assert.That(g.ClaimDailyChest(T0).Ok, Is.True);
            Assert.That(g.Crystals, Is.EqualTo(crystals + Data.Balance.Daily.Chest.Crystals));
            Assert.That(g.ClaimDailyChest(T0).Error, Is.EqualTo(MetaError.AlreadyClaimed));
        }

        [Test]
        public void ANewDayBringsNewTasks()
        {
            var g = FarmGame();
            long day = g.State.Daily.Day;
            g.DailyTasks(T0 + Day);
            Assert.That(g.State.Daily.Day, Is.EqualTo(day + 1));
            Assert.That(g.State.Daily.Tasks.All(t => t.Progress == 0 && !t.Claimed));
            // With the farm open, farm tasks join the pool.
            bool sawFarm = false;
            for (int d = 2; d < 30; d++)
                sawFarm |= g.DailyTasks(T0 + d * Day).Any(t => Data.DailyTasks[t.Id].Farm);
            Assert.That(sawFarm, Is.True);
            Assert.That(g.TakeEvents().Any(e => e.Kind == MetaEventKind.NewDay));
        }

        [Test]
        public void DayBoundaryFollowsTheDeviceTimeZone()
        {
            var g = MetaGame.NewGame(Data, T0, 1, utcOffsetMinutes: 180);
            // T0 is 08:00 UTC = 11:00 at UTC+3; 21:30 UTC is already the next day there.
            Assert.That(g.DayOf(T0 + 13 * Hour + 30 * Minute), Is.EqualTo(g.DayOf(T0) + 1));
            Assert.That(g.DayOf(T0 + 12 * Hour), Is.EqualTo(g.DayOf(T0)));
        }

        [Test]
        public void LoginGiftOncePerDayOnASevenDayCycle()
        {
            var g = NewGame();
            Assert.That(g.LoginGiftReady(T0), Is.True);
            Assert.That(g.NextLoginGiftDay, Is.EqualTo(1));
            long coins = g.Coins;
            Assert.That(g.ClaimLoginGift(T0).Ok, Is.True);
            Assert.That(g.Coins, Is.EqualTo(coins + Data.Balance.Daily.Login[0].Coins));
            Assert.That(g.ClaimLoginGift(T0 + Hour).Error, Is.EqualTo(MetaError.AlreadyClaimed));
            // Missing days does not reset the cycle (no pressure).
            Assert.That(g.ClaimLoginGift(T0 + 5 * Day).Ok, Is.True);
            Assert.That(g.NextLoginGiftDay, Is.EqualTo(3));
            for (int d = 6; d < 11; d++) g.ClaimLoginGift(T0 + d * Day);
            Assert.That(g.NextLoginGiftDay, Is.EqualTo(1), "after day 7 the cycle restarts");
        }

        [Test]
        public void AwaySummaryListsWhatGrewAndWasMade()
        {
            var g = FarmGame();
            SetLevel(g, 2, T0);
            g.Plant(T0, 0, "wheat");
            g.Plant(T0, 1, "carrot");
            Give(g, "wheat", 6);
            g.Enqueue(T0, "mill", "flour");
            g.Enqueue(T0, "mill", "flour");
            var hen = g.State.Farm.Animals[0];
            g.Feed(T0, hen.Uid);
            g.State.Lives.InfiniteUntil = 0;   // the chapter finale gave infinite lives
            g.StartLevel(T0, 1);
            g.FinishLevel(T0, new LevelResult { Level = 1, Won = false });
            g.Touch(T0);

            var s = g.Resume(T0 + 3 * Hour);
            Assert.That(s.IsEmpty, Is.False);
            Assert.That(s.AwaySeconds, Is.EqualTo(3 * Hour));
            Assert.That(s.Ready["wheat"], Is.EqualTo(2));
            Assert.That(s.Ready["carrot"], Is.EqualTo(2));
            Assert.That(s.Ready["flour"], Is.EqualTo(2));
            Assert.That(s.Ready["egg"], Is.EqualTo(1));
            Assert.That(s.LivesFull, Is.True);
            Assert.That(s.Lines[0], Does.StartWith("Вас не было 3 ч"));
            Assert.That(s.Lines.Any(l => l.Contains("Пшеница ×2")));
            Assert.That(s.Lines.Any(l => l.Contains("Все жизни восстановлены")));
            // Nothing is collected by the summary itself, and a quick return shows nothing.
            Assert.That(g.Plots[0].Crop, Is.EqualTo("wheat"));
            Assert.That(g.Resume(T0 + 3 * Hour + 60).IsEmpty, Is.True);
            Assert.That(g.Resume(T0 + 4 * Hour).Ready.Count, Is.EqualTo(0), "already reported");
        }

        [Test]
        public void NotificationsAreAtMostThreeAndInTheFuture()
        {
            var g = FarmGame();
            g.Plant(T0, 0, "corn");
            Give(g, "wheat", 4);
            Give(g, "corn", 2);
            g.Enqueue(T0, "mill", "chicken_feed");
            g.State.Lives.InfiniteUntil = 0;
            g.StartLevel(T0, g.NextLevel);
            g.AbandonLevel(T0);
            var n = g.PlanNotifications(T0);
            Assert.That(n.Count, Is.InRange(1, 3));
            Assert.That(n.All(x => x.at > T0));
            Assert.That(n.Select(x => x.at), Is.Ordered);
            Assert.That(n.Any(x => x.text.Contains("Жизни")));
        }
    }
}
