using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// Workshops with production queues (GDD 6). A queue runs one job at a time; finished goods wait
    /// in their slot until collected (they need room in the barn — boosters from the tool shed do not).
    /// </summary>
    public sealed partial class MetaGame
    {
        public WorkshopState Workshop(string id) => State.Farm.Workshops.Find(w => w.Id == id);
        public bool HasWorkshop(string id) => Workshop(id) != null;

        public ActionResult BuildWorkshop(long now, string id)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            if (id == null || !Data.Workshops.TryGetValue(id, out var def)) return ActionResult.Fail(MetaError.UnknownId);
            if (HasWorkshop(id)) return ActionResult.Fail(MetaError.AlreadyDone);
            if (def.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (Population < def.Population) return ActionResult.Fail(MetaError.NotEnoughPopulation);
            if (!SpendCoins(def.Coins)) return ActionResult.Fail(MetaError.NotEnoughCoins);
            State.Farm.Workshops.Add(new WorkshopState { Id = id, Slots = def.Slots });
            Stat("buildings");
            return ActionResult.Success();
        }

        /// <summary>Recipes of the workshop the farm level allows.</summary>
        public List<RecipeDef> AvailableRecipes(string workshop)
        {
            var r = new List<RecipeDef>();
            if (Data.Workshops.TryGetValue(workshop, out var def))
                foreach (var rc in def.Recipes)
                    if (rc.Level <= Level) r.Add(rc);
            return r;
        }

        public bool CanAfford(RecipeDef recipe) => HasItems(recipe.Inputs);

        /// <summary>Adds a job to the queue, taking the inputs from the barn now.</summary>
        public ActionResult Enqueue(long now, string workshop, string recipe)
        {
            var w = Workshop(workshop);
            if (w == null) return ActionResult.Fail(Data.Workshops.ContainsKey(workshop ?? "") ? MetaError.NotReady : MetaError.UnknownId);
            if (recipe == null || !Data.Recipes.TryGetValue(recipe, out var rc) || Data.RecipeWorkshop[recipe].Id != workshop)
                return ActionResult.Fail(MetaError.UnknownId);
            if (rc.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (w.Jobs.Count >= w.Slots) return ActionResult.Fail(MetaError.QueueFull);
            if (!HasItems(rc.Inputs)) return ActionResult.Fail(MetaError.NotEnoughItems);
            TakeItems(rc.Inputs);
            long start = now;
            if (w.Jobs.Count > 0) start = Math.Max(now, w.Jobs[w.Jobs.Count - 1].EndAt);
            w.Jobs.Add(new JobState { Recipe = recipe, StartAt = start, EndAt = start + rc.Seconds });
            Stat("jobs_started");
            return ActionResult.Success();
        }

        /// <summary>Number of finished jobs waiting to be collected.</summary>
        public int ReadyJobs(long now, string workshop)
        {
            var w = Workshop(workshop);
            if (w == null) return 0;
            int n = 0;
            foreach (var j in w.Jobs)
                if (j.EndAt <= now) n++;
            return n;
        }

        /// <summary>When the whole queue is done (0 when empty).</summary>
        public long QueueEndsAt(string workshop)
        {
            var w = Workshop(workshop);
            return w == null || w.Jobs.Count == 0 ? 0 : w.Jobs[w.Jobs.Count - 1].EndAt;
        }

        /// <summary>Collects finished jobs in order while they fit in the barn.</summary>
        public ActionResult Collect(long now, string workshop)
        {
            var w = Workshop(workshop);
            if (w == null) return ActionResult.Fail(MetaError.UnknownId);
            var r = new Reward();
            bool blocked = false;
            int collected = 0;
            while (w.Jobs.Count > 0 && w.Jobs[0].EndAt <= now)
            {
                var rc = Data.Recipes[w.Jobs[0].Recipe];
                if (Boosters.IsBooster(rc.Output))
                {
                    Dict.Add(State.Boosters, rc.Output, rc.Count);
                    Dict.Add(r.Boosters, rc.Output, rc.Count);
                }
                else
                {
                    if (BarnFree < rc.Count) { blocked = true; break; }
                    Dict.Add(State.Barn, rc.Output, rc.Count);
                    Dict.Add(r.Items, rc.Output, rc.Count);
                }
                w.Jobs.RemoveAt(0);
                collected++;
                Stat("produced", rc.Count);
                Progress(now, DailyKinds.Produce, rc.Count);
            }
            if (collected == 0) return ActionResult.Fail(blocked ? MetaError.BarnFull : MetaError.NothingToCollect);
            return ActionResult.Success(r);
        }

        /// <summary>Crystal price of the next extra slot, or -1 at the maximum.</summary>
        public int NextSlotPrice(string workshop)
        {
            var w = Workshop(workshop);
            if (w == null) return -1;
            var def = Data.Workshops[workshop];
            int bought = w.Slots - def.Slots;
            return bought < def.SlotCrystals.Count ? def.SlotCrystals[bought] : -1;
        }

        public ActionResult BuySlot(long now, string workshop)
        {
            var w = Workshop(workshop);
            if (w == null) return ActionResult.Fail(MetaError.UnknownId);
            int price = NextSlotPrice(workshop);
            if (price < 0) return ActionResult.Fail(MetaError.LimitReached);
            if (!SpendCrystals(price)) return ActionResult.Fail(MetaError.NotEnoughCrystals);
            w.Slots++;
            return ActionResult.Success();
        }

        /// <summary>The job running now (not finished), or null.</summary>
        public JobState RunningJob(long now, string workshop)
        {
            var w = Workshop(workshop);
            if (w == null) return null;
            foreach (var j in w.Jobs)
                if (j.EndAt > now) return j.StartAt <= now ? j : null;
            return null;
        }

        /// <summary>Finishes the running job for crystals; the rest of the queue moves up by the time saved.</summary>
        public ActionResult SpeedUpWorkshop(long now, string workshop)
        {
            var j = RunningJob(now, workshop);
            if (j == null) return ActionResult.Fail(MetaError.NothingToCollect);
            long saved = j.EndAt - now;
            if (!SpendCrystals(SpeedUpPrice(saved))) return ActionResult.Fail(MetaError.NotEnoughCrystals);
            var w = Workshop(workshop);
            bool after = false;
            foreach (var x in w.Jobs)
            {
                if (x == j) { x.EndAt = now; after = true; continue; }
                if (after) { x.StartAt -= saved; x.EndAt -= saved; }
            }
            return ActionResult.Success();
        }
    }
}
