using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using LastHearth.Core.Surge;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>GDD 9.3 and 12: the login calendar, the season, the warm week, the monster of the week,
    /// the summary «Пока вас не было» and the notification planner.</summary>
    public class RetentionTests
    {
        const long T0 = TestData.Start; // 08:00 UTC of a Friday
        const long Hour = 3600, Day = 86400;

        static GameSession Fortress(int hearth)
        {
            var s = TestData.NewSession();
            s.Combat = TestData.Combat();
            s.State.HearthLevel = hearth;
            s.State.Queue.EnsureBuilders(s.BuilderCount());
            s.State.Wallet[ResourceType.Coal] = 100000;
            s.State.Wallet[ResourceType.Food] = 100000;
            return s;
        }

        static GameSession Heroes(int hearth)
        {
            var s = Fortress(hearth);
            s.State.Dwellers.Clear();
            foreach (var c in new[] { HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest })
            {
                var d = s.AddDweller(c.ToString(), new[] { 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth, 5 + 2 * hearth });
                d.HeroClass = c;
                d.HeroLevel = 2 * hearth;
                d.SkillLevel = 3;
            }
            return s;
        }

        // ------------------------------------------------------------------ the login calendar

        [Test]
        public void TheCalendarGivesOneGiftPerDayWithTheGameOpen()
        {
            var s = Fortress(5);
            s.Advance(T0);
            Assert.AreEqual(1, s.CalendarPending, "the first day");
            long sparks = s.State.Wallet[ResourceType.Sparks];
            Assert.AreEqual(Fail.None, s.ClaimCalendar(T0));
            Assert.AreEqual(sparks + s.GiftAmount(s.Calendar[0]), s.State.Wallet[ResourceType.Sparks]);
            Assert.AreEqual(Fail.NotToday, s.ClaimCalendar(T0 + 3 * Hour), "once a day");
            Assert.AreEqual(1, s.CalendarIndex);
            s.Advance(T0 + Day);
            Assert.AreEqual(1, s.CalendarPending, "the next day");
            long treats = s.Treats;
            Assert.AreEqual(Fail.None, s.ClaimCalendar(T0 + Day));
            Assert.AreEqual(GiftKind.Treats, s.Calendar[1].Kind);
            Assert.AreEqual(treats + s.GiftAmount(s.Calendar[1]), s.Treats);
        }

        [Test]
        public void MissedDaysTakeNothingAway()
        {
            var s = Fortress(5);
            s.Advance(T0);
            s.ClaimCalendar(T0);
            s.ClaimCalendar(T0 + Day); // day 2
            // a week away: the calendar goes on from day 3, nothing is reset
            s.Advance(T0 + 9 * Day);
            Assert.AreEqual(1, s.CalendarPending);
            Assert.AreEqual(2, s.CalendarIndex);
            // days opened but not claimed wait: three days later all three can be taken
            s.Advance(T0 + 10 * Day);
            s.Advance(T0 + 11 * Day);
            Assert.AreEqual(3, s.CalendarPending);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(Fail.None, s.ClaimCalendar(T0 + 11 * Day));
            Assert.AreEqual(Fail.NotToday, s.ClaimCalendar(T0 + 11 * Day));
            Assert.AreEqual(5, s.CalendarIndex);
        }

        [Test]
        public void TheCalendarGoesRoundAfterTwentyEightDaysAndPaysItsBudget()
        {
            var s = Fortress(1); // before the Surge: only the calendar gives hourglasses
            long sparks = s.State.Wallet[ResourceType.Sparks];
            long hourglass = s.State.HourglassSeconds;
            for (int day = 0; day < 28; day++)
                Assert.AreEqual(Fail.None, s.ClaimCalendar(T0 + day * Day), "day " + (day + 1));
            Assert.AreEqual(2, s.CalendarRound);
            Assert.AreEqual(0, s.CalendarIndex);
            Assert.AreEqual(s.Balance.Activities.Daily.CalendarSparks, s.State.Wallet[ResourceType.Sparks] - sparks - AchievementSparks(s));
            Assert.AreEqual(60 * 60, s.State.HourglassSeconds - hourglass, "four quarter-hour hourglasses");
        }

        static long AchievementSparks(GameSession s) => s.State.Achievements.Count * 60;

        [Test]
        public void AnOldSaveKeepsTheDaysItWasPaidFor()
        {
            var s = Fortress(5);
            s.State.LoginCount = 9;
            s.State.LastLoginDay = s.LocalDay(T0);
            s.State.Retention = new RetentionState(); // a save from before the claims
            var loaded = new GameSession(s.Balance, SaveSystem.FromJson(SaveSystem.ToJson(s.State))) { Combat = s.Combat };
            loaded.Advance(T0);
            Assert.AreEqual(0, loaded.CalendarPending, "today's gift was paid by the old calendar");
            Assert.AreEqual(9, loaded.CalendarIndex);
            loaded.Advance(T0 + Day);
            Assert.AreEqual(1, loaded.CalendarPending);
        }

        // ------------------------------------------------------------------ the season

        [Test]
        public void OrdinaryPlayFillsTheSeasonTrack()
        {
            var s = Fortress(10);
            var x = s.SeasonInfo;
            Assert.AreEqual(30, x.Track.Count);
            Assert.AreEqual(42, x.Days);
            Assert.IsFalse(s.SeasonPremium, "the premium track is switched off");
            s.Advance(T0);
            Assert.AreEqual(1, s.SeasonNumber);
            s.ClaimCalendar(T0);
            Assert.AreEqual(x.CalendarXp, s.SeasonXp);
            // a task done counts once, when it is done
            s.TodayTasks(T0);
            s.State.DailyTasks[0] = (int)DailyTask.Collect;
            var task = DailyTask.Collect;
            var room = s.State.Rooms.First(r => s.Balance.Room(r.Type).Produces != null);
            for (int i = 0; i < s.TaskTimes(task) + 2; i++)
            {
                room.Stock = 10;
                s.Collect(room.Id);
            }
            Assert.AreEqual(x.CalendarXp + x.TaskXp, s.SeasonXp);
            foreach (var t in s.TodayTasks(T0))
                s.State.DailyProgress[(int)t] = s.TaskTimes(t);
            Assert.AreEqual(Fail.None, s.ClaimDaily(T0));
            Assert.AreEqual(x.CalendarXp + x.TaskXp + x.DailyXp, s.SeasonXp);
        }

        [Test]
        public void StepsAreClaimedOnceAndOnlyWhenReached()
        {
            var s = Fortress(10);
            s.Advance(T0);
            var x = s.SeasonInfo;
            s.State.Retention.SeasonXp = x.StepXp * 2 + x.StepXp / 2;
            Assert.AreEqual(2, s.SeasonStep);
            Assert.AreEqual(x.StepXp / 2, s.SeasonStepXp);
            Assert.AreEqual(Fail.RulesNotMet, s.ClaimSeason(2, T0));
            long treats = s.Treats;
            Assert.AreEqual(Fail.None, s.ClaimSeason(0, T0));
            Assert.AreEqual(treats + s.GiftAmount(x.Track[0]), s.Treats);
            Assert.AreEqual(Fail.AlreadyQueued, s.ClaimSeason(0, T0));
            Assert.AreEqual(1, s.SeasonGiftsReady);
            Assert.AreEqual(1, s.ClaimSeasonAll(T0));
            // the whole track: decorations are cosmetic gifts of the catalogue
            s.State.Retention.SeasonXp = x.StepXp * 100;
            Assert.AreEqual(30, s.SeasonStep, "no more than 30 steps");
            Assert.AreEqual(28, s.ClaimSeasonAll(T0));
            foreach (var g in x.Track.Where(g => g.Kind == GiftKind.Decor))
                Assert.AreEqual(1, s.DecorOwned(g.Decor), g.Decor);
        }

        [Test]
        public void ASeasonEndsAfterSixWeeksAndGivesTheGiftsNotTaken()
        {
            var s = Fortress(10);
            s.Advance(T0);
            var x = s.SeasonInfo;
            long end = s.SeasonEnds(T0);
            Assert.AreEqual(42 * Day, end - (s.LocalDay(T0) * Day - s.State.UtcOffsetSeconds));
            s.State.Retention.SeasonXp = x.StepXp * 3; // treats, essence, an hourglass
            s.ClaimSeason(0, T0);
            long essence = s.State.Wallet[ResourceType.Essence], hourglass = s.State.HourglassSeconds;
            s.Advance(end - 1);
            Assert.AreEqual(1, s.SeasonNumber);
            var summary = s.ReturnAfterAbsence(end + Hour);
            Assert.AreEqual(2, s.SeasonNumber, "the next season starts at once");
            Assert.AreEqual(0, s.SeasonXp);
            Assert.AreEqual(0, s.SeasonStep);
            Assert.AreEqual(2, s.State.Retention.LastSeasonAutoGifts);
            Assert.Greater(s.State.Wallet[ResourceType.Essence], essence, "the essence step was given");
            Assert.GreaterOrEqual(s.State.HourglassSeconds, hourglass + 15 * 60, "and the hourglass");
            Assert.IsTrue(summary.Other.Any(o => o.StartsWith("Сезон 1 завершён")), string.Join("; ", summary.Other));
            Assert.AreEqual(end + 42 * Day, s.SeasonEnds(end + Hour));
        }

        [Test]
        public void EventThresholdsAndWonHuntsAreSeasonPoints()
        {
            var s = Heroes(12);
            s.Advance(T0);
            int xp = s.SeasonXp;
            var run = s.ActiveRuns(T0).FirstOrDefault();
            Assert.IsNotNull(run, "a newcomer's personal run is open");
            s.AddPoints(run, GameSession.EventTarget[run.Id] * 0.35);
            s.Advance(T0 + 1);
            Assert.AreEqual(xp + run.Thresholds * s.SeasonInfo.EventXp, s.SeasonXp);
            s.Advance(T0 + 2);
            Assert.AreEqual(xp + run.Thresholds * s.SeasonInfo.EventXp, s.SeasonXp, "counted once");
        }

        // ------------------------------------------------------------------ warm week and the monster of the week

        [Test]
        public void SevenSurgesWithoutADefeatGiveAWarmDay()
        {
            var s = Heroes(8);
            s.Advance(T0);
            var room = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            var tim = s.AddDweller("Тим", new[] { 9, 9, 9, 9, 9 });
            tim.Level = s.MaxDwellerLevel; // a veteran: a week of work raises no stat meanwhile (GameSession.People.cs)
            s.Assign(tim.Id, room.Id);
            double plain = s.RoomOutputPerHour(room);
            long evening = T0 + 11 * Hour;
            for (int day = 0; day < 7; day++)
            {
                Assert.AreEqual(Fail.None, s.StartSurge(evening + day * Day, out var d), "day " + day);
                d.Brain = SurgeDefense.Careful();
                s.FinishSurge(d, evening + day * Day, out var reward);
                Assert.Greater(reward.Stars, 0);
                Assert.AreEqual(day < 6, !s.WarmWeekOn(evening + day * Day), "day " + day);
            }
            Assert.AreEqual(7, s.SurgeStreak);
            Assert.AreEqual(plain * (1 + s.Balance.Activities.Surge.WarmWeek.Output), s.RoomOutputPerHour(room), plain * 1e-9);
            s.Advance(evening + 7 * Day);
            Assert.IsFalse(s.WarmWeekOn(evening + 7 * Day), "for one day");
            Assert.AreEqual(plain, s.RoomOutputPerHour(room), plain * 1e-9);
        }

        [Test]
        public void ADefeatStartsTheStreakAgain()
        {
            var s = Fortress(5);
            s.Advance(T0);
            s.State.Retention.SurgeStreak = 6;
            s.State.Dwellers.Clear(); // nobody to defend: the Surge breaks through
            s.Advance(T0 + 11 * Hour);
            s.StartSurge(T0 + 11 * Hour, out var d);
            s.FinishSurge(d, T0 + 11 * Hour, out var reward);
            Assert.AreEqual(0, reward.Stars);
            Assert.AreEqual(0, s.SurgeStreak);
            Assert.IsFalse(s.WarmWeekOn(T0 + 11 * Hour));
        }

        [Test]
        public void TheMonsterOfTheWeekComesOnSundayAndPaysDoubleOnce()
        {
            var s = Heroes(8);
            Assert.IsNull(s.WeeklyMonster(T0), "Friday");
            long sunday = T0 + 2 * Day;
            Assert.AreEqual(6, s.Weekday(sunday));
            var family = s.WeeklyMonster(sunday);
            Assert.IsNotNull(family);
            Assert.AreNotEqual(family, s.WeeklyMonster(sunday + 7 * Day), "another family next week");
            Assert.AreEqual(Fail.NotToday, s.StartHunt(family.Value, 1, T0, out _, weekly: true));

            Assert.AreEqual(Fail.None, s.StartHunt(family.Value, 1, sunday, out var weekly, weekly: true));
            Assert.Greater(weekly.BossMaxHp, 0);
            var normal = Heroes(8);
            normal.StartHunt(normal.HuntDens(sunday).Contains(family.Value) ? family.Value : normal.HuntDens(sunday)[0], 1, sunday, out var plain);
            if (plain.Family.Family == family.Value)
                Assert.AreEqual(plain.BossMaxHp * s.Balance.Activities.Hunt.Weekly.HpMult, weekly.BossMaxHp, 1e-6, "stronger");
            weekly.Brain = Brains.AutoBattle();
            s.FinishHunt(weekly, out var reward);
            Assert.IsTrue(weekly.Won);
            Assert.IsTrue(reward.Weekly);
            long single = (long)System.Math.Round(s.Balance.Activities.Hunt.CoresByLevel[0] * s.Combat.StarReward[reward.Stars]);
            Assert.AreEqual((long)System.Math.Round(single * s.Balance.Activities.Hunt.Weekly.CoreMult), reward.Cores, 1);
            Assert.IsNull(s.WeeklyMonster(sunday + Hour), "beaten for this week");
            Assert.IsNotNull(s.WeeklyMonster(sunday + 7 * Day));
        }

        // ------------------------------------------------------------------ «Пока вас не было»

        [Test]
        public void TheSummaryTellsWhatHappenedWhileAway()
        {
            var s = Fortress(5);
            s.Advance(T0);
            var mine = s.State.Rooms.First(r => r.Type == RoomType.CoalMine);
            mine.Stock = 0;
            s.UpgradeRoom(s.State.Rooms.First(r => r.Level < 5).Id, T0);
            var summary = s.ReturnAfterAbsence(T0 + 18 * Hour); // 02:00: the evening is over
            Assert.AreEqual(18 * Hour, summary.Seconds);
            Assert.IsTrue(summary.Worth);
            Assert.Greater(summary.Produced[ResourceType.Coal], 0, "the coal mine worked");
            Assert.AreEqual((long)System.Math.Floor(mine.Stock), summary.Produced[ResourceType.Coal], 1);
            Assert.Greater(summary.CoalBurnt, 0, "the hearth burnt coal");
            Assert.Greater(summary.FoodEaten, 0);
            Assert.AreEqual(1, summary.Builds.Count, string.Join("; ", summary.Builds));
            Assert.Greater(summary.FullRooms, 0, "18 hours fill the 10-hour stock");
            Assert.Greater(summary.Lost[ResourceType.Coal], 0, "and the rest did not fit");
            Assert.IsTrue(summary.Fights.Any(f => f.StartsWith("Наплыв")), "the evening Surge was defended without the player");
        }

        [Test]
        public void TheSummaryListsBirthsRoutesEventsAndArenaDefenses()
        {
            var s = Heroes(16);
            s.Advance(T0);
            // a child on the way
            var home = s.State.Rooms.First(r => r.Type == RoomType.Bedrooms);
            var man = s.AddDweller("Олег", new[] { 10, 10, 10, 10, 10 }, Sex.Male);
            var woman = s.AddDweller("Рада", new[] { 10, 10, 10, 10, 10 }, Sex.Female);
            Assert.AreEqual(Fail.None, s.StartPairing(home.Id, man.Id, woman.Id, T0));
            // a squad out in the fog
            var walker = s.AddDweller("Мара", new[] { 10, 10, 10, 10, 10 }, Sex.Female);
            s.State.Expeditions.Add(new Expedition { Id = 99, Team = { walker.Id }, Start = T0, End = T0 + 2 * Hour, Seed = 3, HearthLevel = 16 });
            walker.RouteId = 99;
            var summary = s.ReturnAfterAbsence(T0 + 3 * Day);
            Assert.AreEqual(1, summary.Births.Count, "the child was born");
            Assert.AreEqual(1, summary.Routes.Count);
            StringAssert.Contains("Мара", summary.Routes[0]);
            Assert.Greater(summary.EventsStarted.Count, 0, "events opened meanwhile");
            Assert.IsTrue(summary.Fights.Any(f => f.Contains("оборону")), "ghosts attacked the arena defense: " + string.Join("; ", summary.Fights));
        }

        [Test]
        public void AShortAbsenceIsNotWorthASummary()
        {
            var s = Fortress(5);
            s.Advance(T0);
            var summary = s.ReturnAfterAbsence(T0 + 10 * 60);
            Assert.IsFalse(summary.Worth);
            Assert.IsFalse(s.ReturnAfterAbsence(T0 + 10 * 60).Worth, "no time passed");
        }

        // ------------------------------------------------------------------ notifications

        [Test]
        public void NoMoreThanThreeNotificationsADayAndNoneAtNight()
        {
            var s = Heroes(16);
            s.Advance(T0);
            // many builds finishing at different times, a squad, full rooms, events and the Surge
            for (int i = 0; i < 9; i++)
                s.State.Queue.Builders[i % s.State.Queue.Builders.Count].Jobs.Add(new BuildJob { Id = 500 + i, Target = BuildJob.HearthTarget, Start = T0, End = T0 + (i + 1) * 2 * Hour, ToLevel = 17 });
            s.State.Expeditions.Add(new Expedition { Id = 98, Start = T0, End = T0 + 3 * Hour });
            var plan = NotificationPlanner.Plan(s, T0);
            Assert.Greater(plan.Count, 0);
            foreach (var day in plan.GroupBy(n => s.LocalDay(n.At)))
                Assert.LessOrEqual(day.Count(), NotificationPlanner.PerDay, "day " + day.Key);
            foreach (var n in plan)
            {
                long hour = s.LocalSecondOfDay(n.At) / Hour;
                Assert.IsTrue(hour >= NotificationPlanner.QuietTo && hour < NotificationPlanner.QuietFrom, n.Text + " at " + hour);
                Assert.Greater(n.At, T0);
                Assert.LessOrEqual(n.At, T0 + NotificationPlanner.Horizon + Day);
            }
            Assert.IsTrue(plan.Any(n => n.Kind == NotificationKind.BuildDone), "a free builder is the most useful");
            CollectionAssert.AreEqual(plan.Select(n => n.At).OrderBy(t => t).ToList(), plan.Select(n => n.At).ToList(), "in time order");

            var sink = new NoNotifications();
            foreach (var n in plan)
                sink.Schedule(n);
            Assert.AreEqual(plan.Count, sink.Scheduled.Count);
            sink.CancelAll();
            Assert.AreEqual(0, sink.Scheduled.Count);
        }

        [Test]
        public void NotificationsShownTodayCountTowardsTheLimit()
        {
            var s = Heroes(16);
            s.Advance(T0);
            for (int i = 0; i < 6; i++)
                s.State.Queue.Builders[i % s.State.Queue.Builders.Count].Jobs.Add(new BuildJob { Id = 600 + i, Target = BuildJob.HearthTarget, Start = T0, End = T0 + Hour + i * 2 * Hour, ToLevel = 17 });
            var first = NotificationPlanner.Plan(s, T0);
            int today = first.Count(n => s.LocalDay(n.At) == s.LocalDay(T0));
            Assert.AreEqual(NotificationPlanner.PerDay, today);
            // two hours later the first one has been shown: only two more today
            long later = first[0].At + 60;
            var second = NotificationPlanner.Plan(s, later);
            Assert.AreEqual(NotificationPlanner.PerDay - 1, second.Count(n => s.LocalDay(n.At) == s.LocalDay(later)));
        }

        [Test]
        public void TheSameKindCloseTogetherIsOneNotification()
        {
            var s = Fortress(5);
            s.Advance(T0);
            s.State.Queue.EnsureBuilders(2);
            s.State.Queue.Builders[0].Jobs.Add(new BuildJob { Id = 700, Target = BuildJob.HearthTarget, Start = T0, End = T0 + 2 * Hour, ToLevel = 6 });
            s.State.Queue.Builders[1].Jobs.Add(new BuildJob { Id = 701, Target = BuildJob.HearthTarget, Start = T0, End = T0 + 2 * Hour + 1200, ToLevel = 6 });
            var builds = NotificationPlanner.Plan(s, T0).Where(n => n.Kind == NotificationKind.BuildDone).ToList();
            Assert.AreEqual(1, builds.Count);
            Assert.AreEqual(T0 + 2 * Hour + 1200, builds[0].At, "when both are free");
        }
    }
}
