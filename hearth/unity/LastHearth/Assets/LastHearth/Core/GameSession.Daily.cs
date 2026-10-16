using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    public enum DailyTask
    {
        Collect,
        Build,
        Hunt,
        Campaign,
        Route,
        Surge,
        HeroGrowth,
    }

    /// <summary>MODES.md, 7: daily tasks (3 of 5), the 28-day login calendar and achievements.</summary>
    public sealed partial class GameSession
    {
        /// <summary>How many times a task must be done, and the hearth level that opens it.</summary>
        static readonly (DailyTask task, int times, int unlock, string text)[] TaskList =
        {
            (DailyTask.Collect, 5, 1, "Собрать выработку 5 раз"),
            (DailyTask.Build, 3, 1, "Начать 3 улучшения"),
            (DailyTask.HeroGrowth, 3, 1, "Усилить героев 3 раза"),
            (DailyTask.Campaign, 2, 1, "Провести 2 боя кампании"),
            (DailyTask.Surge, 1, 2, "Отбить Наплыв вживую"),
            (DailyTask.Hunt, 1, 6, "Победить на Охоте"),
            (DailyTask.Route, 1, 9, "Вернуть отряд из вылазки"),
        };

        public string TaskText(DailyTask t) => TaskList.First(x => x.task == t).text;

        public int TaskTimes(DailyTask t) => TaskList.First(x => x.task == t).times;

        /// <summary>Today's five tasks: picked by the day number among the ones the hearth has opened.</summary>
        public List<DailyTask> TodayTasks(long now)
        {
            long day = LocalDay(now);
            if (State.DailyDay != day)
            {
                State.DailyDay = day;
                State.DailyProgress = new int[8];
                State.DailyClaimed = false;
                var open = TaskList.Where(t => State.HearthLevel >= t.unlock).Select(t => t.task).ToList();
                int start = (int)(day % open.Count);
                State.DailyTasks = Enumerable.Range(0, Math.Min(5, open.Count))
                    .Select(i => (int)open[(start + i) % open.Count]).ToList();
            }
            return State.DailyTasks.Select(t => (DailyTask)t).ToList();
        }

        public int TasksDone(long now) => TodayTasks(now).Count(t => State.DailyProgress[(int)t] >= TaskTimes(t));

        void Count(DailyTask task)
        {
            var today = TodayTasks(State.Time);
            State.DailyProgress[(int)task]++;
            if (today.Contains(task) && State.DailyProgress[(int)task] == TaskTimes(task))
                AddSeasonXp(SeasonInfo.TaskXp); // a task done is season points (MODES.md, 7)
        }

        public Fail ClaimDaily(long now)
        {
            var d = Balance.Activities.Daily;
            if (State.DailyClaimed)
                return Fail.AlreadyQueued;
            if (TasksDone(now) < d.TasksNeeded)
                return Fail.RulesNotMet;
            State.DailyClaimed = true;
            State.Wallet[ResourceType.Sparks] += d.TaskSparks;
            State.HourglassSeconds += d.TaskHourglassMinutes * 60;
            GrantTreats(Balance.Pets.TreatsDaily); // pet treats (GDD 8.1)
            AddSeasonXp(SeasonInfo.DailyXp);
            FestivalTasksDone(now);
            return Fail.None;
        }

        /// <summary>A new local day with the game open is a calendar day (days need not follow each other);
        /// its gift waits until it is taken (ClaimCalendar, GameSession.Season.cs).</summary>
        void CheckLogin(long now)
        {
            SyncSeason(now);
            long day = LocalDay(now);
            if (State.LastLoginDay == day)
                return;
            _ = Retention; // an old save counts its paid days before the new one
            State.LastLoginDay = day;
            State.LoginCount++;
            CheckKennel(); // the Owl comes on the 7th day (GDD 8.1)
        }

        /// <summary>Achievements: hearth levels, act bosses and hunt wins; 3000 Sparks in all (ECONOMY.md, 8).</summary>
        public void CheckAchievements()
        {
            for (int l = 2; l <= State.HearthLevel; l++)
                Achieve("hearth" + l, l <= 18 ? 60 : 90);
            int acts = State.CampaignStars.Count / 19;
            for (int a = 1; a <= acts; a++)
                Achieve("act" + a, 60);
            foreach (int wins in new[] { 10, 50, 100, 250, 500 })
            {
                if (State.HuntWins >= wins)
                    Achieve("hunt" + wins, 60);
            }
            CheckKennel(); // pets and companions found by play (GDD 8.1-8.2)
        }

        void Achieve(string key, long sparks)
        {
            if (State.Achievements.Contains(key))
                return;
            State.Achievements.Add(key);
            State.Wallet[ResourceType.Sparks] += sparks;
        }
    }
}
