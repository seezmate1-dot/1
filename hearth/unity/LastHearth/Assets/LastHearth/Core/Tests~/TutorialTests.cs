using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>MODES.md, 10: the scripted first session advances on real game events and never blocks.</summary>
    public class TutorialTests
    {
        const long T0 = TestData.Start;

        static int H => TestData.Balance().Grid.HearthFloor;

        static GameSession NewSession()
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            return s;
        }

        [Test]
        public void ANewGameStartsTheTutorialAndAnOldSaveLoadsWithItDone()
        {
            var s = NewSession();
            Assert.AreEqual(TutorialStep.Welcome, s.TutorialState.Step);
            Assert.IsTrue(s.TutorialState.Active);

            // a save written before the tutorial existed has no "Tutorial" field
            var json = JObject.Parse(JsonConvert.SerializeObject(s.State));
            json.Remove("Tutorial");
            var old = JsonConvert.DeserializeObject<GameState>(json.ToString());
            var loaded = new GameSession(s.Balance, old);
            Assert.AreEqual(TutorialStep.Done, loaded.TutorialState.Step);
            Assert.IsFalse(loaded.TutorialState.Active);

            // and the step survives a save
            s.TutorialNext(T0);
            var again = SaveSystem.FromJson(SaveSystem.ToJson(s.State));
            Assert.AreEqual(TutorialStep.Collect, again.Tutorial.Step);
        }

        [Test]
        public void TheWholeTutorialIsPlayedThroughWithRealCommands()
        {
            var s = NewSession();
            var st = s.State;
            long now = T0;
            int changes = 0;
            s.TutorialChanged += _ => changes++;

            s.TutorialNext(now);
            Assert.AreEqual(TutorialStep.Collect, s.TutorialState.Step);

            var mine = st.Rooms.First(r => r.Type == RoomType.CoalMine);
            Assert.Greater(s.Collect(mine.Id), 0, "the mine has something to collect within the first seconds");
            Assert.AreEqual(TutorialStep.Hearth, s.TutorialState.Step);

            Assert.AreEqual(Fail.None, s.UpgradeHearth(now));
            Assert.AreEqual(TutorialStep.Assign, s.TutorialState.Step);

            now += 60;
            s.Advance(now);
            Assert.AreEqual(2, st.HearthLevel);
            var refugee = st.Dwellers.First(d => d.RoomId < 0 && !d.IsHero);
            var kitchen = st.Rooms.First(r => r.Type == RoomType.Kitchen);
            Assert.AreEqual(Fail.None, s.Assign(refugee.Id, kitchen.Id));
            Assert.AreEqual(TutorialStep.Clear, s.TutorialState.Step);

            Assert.AreEqual(Fail.None, s.ClearObstacle(H + 1, 2, now));
            Assert.AreEqual(TutorialStep.Build, s.TutorialState.Step);

            now += s.ClearSeconds(H + 1, 2) + 1;
            s.Advance(now);
            Assert.AreEqual(Fail.None, s.BuildRoom(RoomType.Barracks, H + 1, 2, now, out _));
            Assert.AreEqual(TutorialStep.Upgrade, s.TutorialState.Step);

            Assert.AreEqual(Fail.None, s.UpgradeRoom(kitchen.Id, now));
            Assert.AreEqual(TutorialStep.HeroLevel, s.TutorialState.Step);

            var mara = st.Dwellers.First(d => d.IsHero);
            Assert.IsFalse(s.TutorialWaits());
            Assert.AreEqual(Fail.None, s.LevelUpHero(mara.Id));
            Assert.AreEqual(TutorialStep.Promote, s.TutorialState.Step);
            Assert.IsTrue(s.TutorialWaits(), "the barracks are still being built");

            now += 600;
            s.Advance(now); // the barracks are built
            Assert.IsFalse(s.TutorialWaits());
            var tim = st.Dwellers.First(d => d.Name == "Тим");
            Assert.AreEqual(Fail.None, s.Promote(tim.Id, HeroClass.Archer));
            Assert.AreEqual(TutorialStep.Campaign, s.TutorialState.Step);
            Assert.IsTrue(s.TutorialWaits(), "the campaign opens at the hearth level of its first mission");

            // later the hearth reaches the campaign (a grown squad, as a player has it by then)
            st.HearthLevel = s.Mission(0).Rank;
            Assert.IsFalse(s.TutorialWaits());
            var classes = new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest };
            int size = CombatCalibrationTests.SquadSize(st.HearthLevel);
            for (int i = 0; i < size; i++)
            {
                var d = i < st.Dwellers.Count ? st.Dwellers[i] : s.AddDweller("Герой" + i, new[] { 5, 5, 5, 5, 5 });
                d.HeroClass = classes[i % classes.Length];
                d.HeroLevel = 2 * st.HearthLevel;
                d.SkillLevel = 1 + d.HeroLevel / 6;
                for (int k = 0; k < d.Stats.Length; k++)
                    d.Stats[k] = 5 + 2 * st.HearthLevel;
            }
            Assert.AreEqual(Fail.None, s.StartMission(0, now, out var battle));
            battle.Brain = Brains.Careful();
            battle.RunToEnd();
            Assert.IsTrue(battle.Won);
            s.FinishMission(battle, out _);
            Assert.AreEqual(TutorialStep.Surge, s.TutorialState.Step);

            s.TutorialNext(now);
            Assert.AreEqual(TutorialStep.Done, s.TutorialState.Step);
            Assert.IsFalse(s.TutorialState.Skipped);
            Assert.AreEqual(now, s.TutorialState.FinishedAt);
            Assert.AreEqual(Tutorial.Steps.Length, changes, "every step announced its end");
            Assert.IsTrue(s.TutorialWaits(), "nothing to show once it is done");
        }

        [Test]
        public void AStepAlreadyDoneIsNotAskedForAgain()
        {
            var s = NewSession();
            // the player does things before Mara asks: collect and upgrade the hearth during the welcome
            s.CollectAll();
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
            Assert.AreEqual(TutorialStep.Welcome, s.TutorialState.Step, "a talking step waits for its tap");
            s.TutorialNext(T0);
            Assert.AreEqual(TutorialStep.Assign, s.TutorialState.Step, "collect and hearth are passed at once");
        }

        [Test]
        public void ATapMovesOnlyTalkingStepsAndOtherEventsDoNotMoveAStep()
        {
            var s = NewSession();
            s.TutorialNext(T0);
            Assert.AreEqual(TutorialStep.Collect, s.TutorialState.Step);
            s.TutorialNext(T0);
            Assert.AreEqual(TutorialStep.Collect, s.TutorialState.Step, "a step of an action waits for the action");
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
            Assert.AreEqual(TutorialStep.Collect, s.TutorialState.Step, "another action is remembered, not taken for this one");
        }

        [Test]
        public void TheTutorialCanBeSkippedAndNothingIsBlockedAfterIt()
        {
            var s = NewSession();
            s.SkipTutorial(T0);
            Assert.AreEqual(TutorialStep.Done, s.TutorialState.Step);
            Assert.IsTrue(s.TutorialState.Skipped);
            s.CollectAll();
            Assert.AreEqual(Fail.None, s.UpgradeHearth(T0));
            s.TutorialNext(T0);
            Assert.AreEqual(TutorialStep.Done, s.TutorialState.Step);
        }

        [Test]
        public void TheTutorialEndsByItselfOnceTheHearthOutgrowsIt()
        {
            var s = NewSession();
            s.State.HearthLevel = Tutorial.EndsAtHearth;
            s.Advance(T0 + 1);
            Assert.AreEqual(TutorialStep.Done, s.TutorialState.Step);
            Assert.IsFalse(s.TutorialState.Skipped);
        }

        [Test]
        public void TheAdminRestartsJumpsAndFinishes()
        {
            var s = NewSession();
            s.SkipTutorial(T0);
            s.RestartTutorial(T0 + 5);
            Assert.AreEqual(TutorialStep.Welcome, s.TutorialState.Step);
            Assert.AreEqual(T0 + 5, s.TutorialState.StartedAt);
            Assert.IsFalse(s.TutorialState.Skipped);

            s.CollectAll();
            s.JumpTutorial(TutorialStep.Collect, T0);
            Assert.AreEqual(TutorialStep.Collect, s.TutorialState.Step, "a jump waits for the step's own action");
            s.State.Rooms.First(r => r.Type == RoomType.CoalMine).Stock = 50;
            s.CollectAll();
            Assert.AreEqual(TutorialStep.Hearth, s.TutorialState.Step);

            s.JumpTutorial(TutorialStep.Done, T0);
            Assert.IsFalse(s.TutorialState.Active);
        }

        [Test]
        public void EveryStepSpeaksShortlyInRussian()
        {
            var steps = System.Enum.GetValues(typeof(TutorialStep)).Cast<TutorialStep>().Where(x => x != TutorialStep.Done).ToList();
            Assert.AreEqual(steps.Count, Tutorial.Steps.Length);
            foreach (var step in steps)
            {
                var info = Tutorial.Of(step);
                Assert.IsNotNull(info, step.ToString());
                Assert.LessOrEqual(info.Text.Length, 80, "shorter than two lines (MODES.md, 10): " + step);
                Assert.IsTrue(info.Text.Any(c => c >= 'А' && c <= 'я'), step.ToString());
            }
            Assert.IsTrue(Tutorial.Talks(TutorialStep.Welcome));
            Assert.IsTrue(Tutorial.Talks(TutorialStep.Surge));
            Assert.IsFalse(Tutorial.Talks(TutorialStep.Build));
        }
    }
}
