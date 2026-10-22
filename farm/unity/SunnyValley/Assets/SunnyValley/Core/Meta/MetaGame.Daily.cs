using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>Daily retention (GDD 8): three daily tasks with a chest, and a login gift on a 7-day cycle.</summary>
    public sealed partial class MetaGame
    {
        /// <summary>Starts a new day's tasks if the local day changed. Called by every daily accessor.</summary>
        public void EnsureDay(long now)
        {
            long day = DayOf(now);
            var d = State.Daily;
            if (d.Day == day) return;
            d.Day = day;
            d.ChestClaimed = false;
            d.Tasks.Clear();
            var pool = new List<DailyTaskDef>();
            foreach (var t in B.Daily.Tasks)
                if (DailyTaskPossible(t)) pool.Add(t);
            // One task per kind, picked deterministically from the save's generator.
            var usedKinds = new HashSet<string>();
            while (d.Tasks.Count < B.Daily.PerDay && pool.Count > 0)
            {
                var t = pool[Roll(pool.Count)];
                pool.Remove(t);
                if (!usedKinds.Add(t.Kind)) continue;
                d.Tasks.Add(new DailyTaskState { Id = t.Id });
            }
            Emit(MetaEventKind.NewDay, day.ToString(), 0, null, null);
        }

        bool DailyTaskPossible(DailyTaskDef t)
        {
            if (t.Farm && !FarmOpen) return false;
            // Stars are spent on chapter tasks; after the chapter, only special buildings take them.
            if (t.Kind == DailyKinds.SpendStars && ChapterFinished) return false;
            if (t.Kind == DailyKinds.FeedAnimals && State.Farm.Animals.Count == 0) return false;
            return true;
        }

        public IReadOnlyList<DailyTaskState> DailyTasks(long now)
        {
            EnsureDay(now);
            return State.Daily.Tasks;
        }

        public bool DailyTaskDone(DailyTaskState t) => t.Progress >= Data.DailyTasks[t.Id].Target;

        void Progress(long now, string kind, int amount)
        {
            EnsureDay(now);
            foreach (var t in State.Daily.Tasks)
            {
                var def = Data.DailyTasks[t.Id];
                if (def.Kind != kind || t.Progress >= def.Target) continue;
                t.Progress = System.Math.Min(def.Target, t.Progress + amount);
                if (t.Progress >= def.Target)
                    Emit(MetaEventKind.DailyTaskDone, def.Id, 0, def.Reward, "Задание дня выполнено: " + def.Text);
            }
        }

        public ActionResult ClaimDailyTask(long now, int index)
        {
            EnsureDay(now);
            var list = State.Daily.Tasks;
            if (index < 0 || index >= list.Count) return ActionResult.Fail(MetaError.UnknownId);
            var t = list[index];
            if (t.Claimed) return ActionResult.Fail(MetaError.AlreadyClaimed);
            if (!DailyTaskDone(t)) return ActionResult.Fail(MetaError.NotReady);
            t.Claimed = true;
            var r = Data.DailyTasks[t.Id].Reward.Clone();
            Grant(now, r);
            return ActionResult.Success(r);
        }

        public bool DailyChestReady(long now)
        {
            EnsureDay(now);
            if (State.Daily.ChestClaimed || State.Daily.Tasks.Count == 0) return false;
            foreach (var t in State.Daily.Tasks)
                if (!t.Claimed) return false;
            return true;
        }

        public ActionResult ClaimDailyChest(long now)
        {
            if (!DailyChestReady(now))
                return ActionResult.Fail(State.Daily.ChestClaimed ? MetaError.AlreadyClaimed : MetaError.NotReady);
            State.Daily.ChestClaimed = true;
            var r = B.Daily.Chest.Clone();
            Grant(now, r);
            return ActionResult.Success(r);
        }

        public bool LoginGiftReady(long now) => State.Daily.LastLoginGiftDay != DayOf(now);

        /// <summary>The gift that the next claim gives (day 1..7 of the cycle).</summary>
        public Reward NextLoginGift => B.Daily.Login[State.Daily.LoginGifts % B.Daily.Login.Count];
        public int NextLoginGiftDay => State.Daily.LoginGifts % B.Daily.Login.Count + 1;

        public ActionResult ClaimLoginGift(long now)
        {
            if (!LoginGiftReady(now)) return ActionResult.Fail(MetaError.AlreadyClaimed);
            var r = NextLoginGift.Clone();
            State.Daily.LastLoginGiftDay = DayOf(now);
            State.Daily.LoginGifts++;
            Grant(now, r);
            return ActionResult.Success(r);
        }
    }
}
