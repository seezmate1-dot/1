using System.Linq;
using NUnit.Framework;
using SunnyValley.Core.Meta;
using static SunnyValley.Core.Tests.MetaTestKit;

namespace SunnyValley.Core.Tests
{
    public class MetaChapterTests
    {
        [Test]
        public void OnlyTheYardIsOpenAtTheStart()
        {
            var g = NewGame();
            Assert.That(g.OpenZones().Select(z => z.Id), Is.EqualTo(new[] { "yard" }));
            Assert.That(g.AvailableTasks().Select(t => t.Id), Is.EqualTo(new[] { "yard_gate" }));
            Assert.That(g.StatusOf("house_porch"), Is.EqualTo(TaskStatus.ZoneLocked));
            Assert.That(g.StatusOf("yard_weeds"), Is.EqualTo(TaskStatus.Locked));
            Assert.That(g.PendingScene()?.Id, Is.EqualTo("arrival"), "the intro scene plays first");
            g.MarkSceneSeen("arrival");
            Assert.That(g.PendingScene(), Is.Null);
        }

        [Test]
        public void TaskCostsStarsAndGivesItsLines()
        {
            var g = NewGame();
            Assert.That(g.CompleteTask(T0, "yard_gate").Error, Is.EqualTo(MetaError.NotEnoughStars));
            g.State.Wallet.Stars = 3;
            var o = g.CompleteTask(T0, "yard_gate");
            Assert.That(o.Ok, Is.True);
            Assert.That(g.Stars, Is.EqualTo(2));
            Assert.That(o.Lines, Is.Not.Empty);
            Assert.That(o.Lines[0].Who, Is.EqualTo("anya"));
            Assert.That(o.Task.Title, Is.EqualTo("Поднять упавшие ворота"));
            Assert.That(g.IsTaskDone("yard_gate"));
            Assert.That(g.CompleteTask(T0, "yard_gate").Error, Is.EqualTo(MetaError.AlreadyDone));
            Assert.That(g.StatusOf("yard_weeds"), Is.EqualTo(TaskStatus.Available));
        }

        [Test]
        public void PrerequisitesAndZoneLocksAreEnforced()
        {
            var g = NewGame();
            g.State.Wallet.Stars = 50;
            Assert.That(g.CompleteTask(T0, "yard_fence").Error, Is.EqualTo(MetaError.PrerequisitesMissing));
            Assert.That(g.CompleteTask(T0, "house_porch").Error, Is.EqualTo(MetaError.ZoneLocked));
            Assert.That(g.CompleteTask(T0, "nope").Error, Is.EqualTo(MetaError.UnknownId));
            Assert.That(g.Stars, Is.EqualTo(50), "refused tasks cost nothing");
        }

        [Test]
        public void ChoosingALookAndChangingItLater()
        {
            var g = NewGame();
            g.State.Wallet.Stars = 10;
            g.CompleteTask(T0, "yard_gate");
            g.CompleteTask(T0, "yard_weeds");
            var task = Data.Tasks["yard_path"];
            Assert.That(task.Choices.Count, Is.EqualTo(3));
            Assert.That(g.CompleteTask(T0, "yard_path", "look9").Error, Is.EqualTo(MetaError.BadChoice));
            Assert.That(g.CompleteTask(T0, "yard_path", "look2").Ok, Is.True);
            Assert.That(g.ChoiceOf("yard_path"), Is.EqualTo("look2"));
            Assert.That(g.ChangeLook("yard_path", "look3").Ok, Is.True);
            Assert.That(g.ChoiceOf("yard_path"), Is.EqualTo("look3"));
            Assert.That(g.ChangeLook("yard_path", "look7").Error, Is.EqualTo(MetaError.BadChoice));
            Assert.That(g.ChangeLook("yard_fence", "look1").Error, Is.EqualTo(MetaError.NotReady));
            Assert.That(g.CompleteTask(T0, "yard_junk", "look1").Error, Is.EqualTo(MetaError.BadChoice), "no looks on this task");
            // No choice given = the first look.
            g.CompleteTask(T0, "yard_junk");
            g.CompleteTask(T0, "yard_fence");
            Assert.That(g.ChoiceOf("yard_fence"), Is.EqualTo("look1"));
        }

        [Test]
        public void TaskWithSceneReturnsTheScene()
        {
            var g = NewGame();
            g.State.Wallet.Stars = 10;
            g.CompleteTask(T0, "yard_gate");
            g.CompleteTask(T0, "yard_weeds");
            var o = g.CompleteTask(T0, "yard_junk");
            Assert.That(o.Scene?.Id, Is.EqualTo("matvey_meets"));
            Assert.That(o.Scene.Lines.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void FinishingAZonePaysItsRewardAndOpensTheNext()
        {
            var g = NewGame();
            int stars = Data.ZoneTasks["yard"].Sum(t => t.Stars);
            g.State.Wallet.Stars = stars;
            long coins = g.Coins;
            TaskOutcome last = null;
            while (g.AvailableTasks().Any(t => t.Zone == "yard"))
                last = g.CompleteTask(T0, g.AvailableTasks().First(t => t.Zone == "yard").Id);
            Assert.That(g.Stars, Is.EqualTo(0));
            Assert.That(last.ZoneCompleted?.Id, Is.EqualTo("yard"));
            Assert.That(last.Scene?.Id, Is.EqualTo("first_evening"));
            Assert.That(g.Coins, Is.EqualTo(coins + Data.Zones["yard"].Reward.Coins));
            Assert.That(g.IsZoneComplete("yard"));
            Assert.That(g.OpenZones().Select(z => z.Id), Is.EqualTo(new[] { "house" }));
            Assert.That(g.ZoneProgress("yard"), Is.EqualTo((9, 9)));
            Assert.That(g.TakeEvents().Exists(e => e.Kind == MetaEventKind.ZoneCompleted && e.Id == "yard"));
        }

        [Test]
        public void FinishingTheChapterOpensTheFarm()
        {
            var g = NewGame();
            int total = g.StarsToFinishChapter();
            Assert.That(total, Is.EqualTo(Data.Chapter.Tasks.Sum(t => t.Stars)));
            g.State.Wallet.Stars = total;
            TaskOutcome last = null;
            while (!g.ChapterFinished)
                last = g.CompleteTask(T0 + 100, g.AvailableTasks()[0].Id);
            Assert.That(last.ChapterCompleted, Is.True);
            Assert.That(last.Scene.Id, Is.EqualTo("festival"));
            Assert.That(g.Stars, Is.EqualTo(0));
            Assert.That(g.FarmOpen, Is.True);
            Assert.That(g.State.Chapter.FinishedAt, Is.EqualTo(T0 + 100));
            var b = Data.Balance;
            Assert.That(g.Plots.Count, Is.EqualTo(b.Plots.Start));
            Assert.That(g.HasWorkshop("mill"), Is.True, "the restored mill becomes the first workshop");
            Assert.That(g.AnimalCount("chicken"), Is.EqualTo(3), "the restored coop has Vera's hens");
            Assert.That(g.TreeCount("apple_tree"), Is.EqualTo(2), "the old orchard bears apples");
            Assert.That(g.Board(T0 + 100).Count(c => c.Order != null), Is.EqualTo(9));
            var ev = g.TakeEvents();
            Assert.That(ev.Exists(e => e.Kind == MetaEventKind.ChapterCompleted));
            Assert.That(ev.Exists(e => e.Kind == MetaEventKind.FarmOpened));
            Assert.That(g.StarsToFinishChapter(), Is.EqualTo(0));
            Assert.That(g.ZoneProgress("mill").done, Is.EqualTo(Data.ZoneTasks["mill"].Count));
        }

        [Test]
        public void EveryTaskHasAReplicaAndEveryZoneAName()
        {
            foreach (var t in Data.Chapter.Tasks)
            {
                Assert.That(t.Lines, Is.Not.Empty, t.Id);
                Assert.That(t.Title, Does.Match("[А-Яа-яЁё]"), t.Id);
            }
        }
    }
}
