using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    public enum TaskStatus
    {
        /// <summary>The zone is not open yet.</summary>
        ZoneLocked,
        /// <summary>The zone is open but other tasks must be done first.</summary>
        Locked,
        Available,
        Done,
    }

    /// <summary>Chapter 1 «Восстановление» (GDD 5): stars → tasks → story → the farm economy.</summary>
    public sealed partial class MetaGame
    {
        public bool ChapterFinished => State.Chapter.Finished;

        public bool IsTaskDone(string taskId) => State.Chapter.DoneTasks.Contains(taskId);
        public bool IsZoneComplete(string zoneId) => State.Chapter.CompletedZones.Contains(zoneId);

        public bool IsZoneOpen(string zoneId)
        {
            if (!Data.Zones.TryGetValue(zoneId, out var z)) return false;
            foreach (var r in z.Requires)
                if (!IsZoneComplete(r)) return false;
            return true;
        }

        public TaskStatus StatusOf(string taskId)
        {
            var t = Data.Tasks[taskId];
            if (IsTaskDone(taskId)) return TaskStatus.Done;
            if (!IsZoneOpen(t.Zone)) return TaskStatus.ZoneLocked;
            foreach (var r in t.Requires)
                if (!IsTaskDone(r)) return TaskStatus.Locked;
            return TaskStatus.Available;
        }

        /// <summary>Tasks the player can pay for right now (in data order).</summary>
        public List<TaskDef> AvailableTasks()
        {
            var r = new List<TaskDef>();
            foreach (var t in Data.Chapter.Tasks)
                if (StatusOf(t.Id) == TaskStatus.Available) r.Add(t);
            return r;
        }

        /// <summary>Zones open and not complete (usually one; the UI shows the first as current).</summary>
        public List<ZoneDef> OpenZones()
        {
            var r = new List<ZoneDef>();
            foreach (var z in Data.Chapter.Zones)
                if (!IsZoneComplete(z.Id) && IsZoneOpen(z.Id)) r.Add(z);
            return r;
        }

        /// <summary>(done, total) tasks of the zone.</summary>
        public (int done, int total) ZoneProgress(string zoneId)
        {
            var list = Data.ZoneTasks[zoneId];
            int done = 0;
            foreach (var t in list)
                if (IsTaskDone(t.Id)) done++;
            return (done, list.Count);
        }

        /// <summary>Stars still needed to finish the whole chapter.</summary>
        public int StarsToFinishChapter()
        {
            int s = 0;
            foreach (var t in Data.Chapter.Tasks)
                if (!IsTaskDone(t.Id)) s += t.Stars;
            return s;
        }

        /// <summary>The look chosen for a task with choices (null if none / not done).</summary>
        public string ChoiceOf(string taskId) => State.Chapter.Choices.TryGetValue(taskId, out var c) ? c : null;

        /// <summary>
        /// Spends the task's stars, records the look (the first one if not given) and returns the replicas,
        /// the scene and whether the zone / the chapter got complete. The last task opens the farm economy.
        /// </summary>
        public TaskOutcome CompleteTask(long now, string taskId, string choiceId = null)
        {
            if (taskId == null || !Data.Tasks.TryGetValue(taskId, out var t)) return new TaskOutcome { Error = MetaError.UnknownId };
            switch (StatusOf(taskId))
            {
                case TaskStatus.Done: return new TaskOutcome { Error = MetaError.AlreadyDone };
                case TaskStatus.ZoneLocked: return new TaskOutcome { Error = MetaError.ZoneLocked };
                case TaskStatus.Locked: return new TaskOutcome { Error = MetaError.PrerequisitesMissing };
            }
            if (t.Choices.Count > 0)
            {
                if (choiceId == null) choiceId = t.Choices[0].Id;
                if (!t.Choices.Exists(c => c.Id == choiceId)) return new TaskOutcome { Error = MetaError.BadChoice };
            }
            else if (choiceId != null) return new TaskOutcome { Error = MetaError.BadChoice };
            if (State.Wallet.Stars < t.Stars) return new TaskOutcome { Error = MetaError.NotEnoughStars };

            var ch = State.Chapter;
            State.Wallet.Stars -= t.Stars;
            ch.StarsSpent += t.Stars;
            ch.DoneTasks.Add(t.Id);
            if (choiceId != null) ch.Choices[t.Id] = choiceId;
            Stat("tasks_done");
            Progress(now, DailyKinds.SpendStars, t.Stars);

            var o = new TaskOutcome { Task = t, Reward = t.Reward.Clone() };
            o.Lines.AddRange(t.Lines);
            if (t.Scene != null) o.Scene = Data.Scenes[t.Scene];
            Grant(now, t.Reward);

            var (done, total) = ZoneProgress(t.Zone);
            if (done == total && !IsZoneComplete(t.Zone))
            {
                var z = Data.Zones[t.Zone];
                ch.CompletedZones.Add(z.Id);
                o.ZoneCompleted = z;
                o.Reward.Add(z.Reward);
                Grant(now, z.Reward);
                Emit(MetaEventKind.ZoneCompleted, z.Id, 0, z.Reward, "«" + z.Name + "» восстановлен!");
                // The zone's closing scene wins over the task scene only if the task had none.
                if (o.Scene == null && z.OutroScene != null) o.Scene = Data.Scenes[z.OutroScene];
                if (ch.CompletedZones.Count == Data.Chapter.Zones.Count)
                {
                    ch.Finished = true;
                    ch.FinishedAt = now;
                    o.ChapterCompleted = true;
                    if (Data.Chapter.FinaleScene != null) o.Scene = Data.Scenes[Data.Chapter.FinaleScene];
                    o.Reward.Add(Data.Chapter.FinaleReward);
                    Grant(now, Data.Chapter.FinaleReward);
                    Emit(MetaEventKind.ChapterCompleted, Data.Chapter.Id, 0, Data.Chapter.FinaleReward,
                        "Глава «" + Data.Chapter.Title + "» пройдена!");
                    OpenFarm(now);
                }
            }
            return o;
        }

        /// <summary>Changes the look of a finished task (free, any number of times).</summary>
        public ActionResult ChangeLook(string taskId, string choiceId)
        {
            if (taskId == null || !Data.Tasks.TryGetValue(taskId, out var t)) return ActionResult.Fail(MetaError.UnknownId);
            if (!IsTaskDone(taskId)) return ActionResult.Fail(MetaError.NotReady);
            if (choiceId == null || !t.Choices.Exists(c => c.Id == choiceId)) return ActionResult.Fail(MetaError.BadChoice);
            State.Chapter.Choices[taskId] = choiceId;
            return ActionResult.Success();
        }

        /// <summary>The scene to play when a zone opens, if not seen yet (the intro scene of the chapter for the first zone).</summary>
        public SceneDef PendingScene()
        {
            var c = Data.Chapter;
            if (c.IntroScene != null && !SceneSeen(c.IntroScene)) return Data.Scenes[c.IntroScene];
            foreach (var z in OpenZones())
                if (z.IntroScene != null && !SceneSeen(z.IntroScene)) return Data.Scenes[z.IntroScene];
            return null;
        }

        public bool SceneSeen(string sceneId) => State.Chapter.SeenScenes.Contains(sceneId);

        public void MarkSceneSeen(string sceneId)
        {
            if (sceneId != null && !SceneSeen(sceneId)) State.Chapter.SeenScenes.Add(sceneId);
        }
    }
}
