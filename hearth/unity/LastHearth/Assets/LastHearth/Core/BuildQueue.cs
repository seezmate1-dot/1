using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    [Serializable]
    public sealed class BuildJob
    {
        public const int HearthTarget = -1;

        public int Id;
        /// <summary>Room id, or HearthTarget.</summary>
        public int Target;
        public int ToLevel;
        public long Start;
        public long End;

        [Newtonsoft.Json.JsonIgnore]
        public bool IsHearth => Target == HearthTarget;
        public bool IsRunning(long now) => Start <= now && now < End;
        public long Remaining(long now) => Math.Max(0, End - Math.Max(now, Start));
    }

    [Serializable]
    public sealed class Builder
    {
        /// <summary>Jobs in order: the first one runs, the rest wait (GDD 9.2: up to three).</summary>
        public List<BuildJob> Jobs = new List<BuildJob>();

        public long FreeAt(long now) => Jobs.Count == 0 ? now : Math.Max(now, Jobs[Jobs.Count - 1].End);
    }

    /// <summary>
    /// Builders with a queue of up to three jobs each. Jobs run one after another without the
    /// player; the pacing model showed that without the queue early levels take twice as long.
    /// </summary>
    [Serializable]
    public sealed class BuildQueue
    {
        public List<Builder> Builders = new List<Builder>();
        public int NextJobId = 1;

        public void EnsureBuilders(int count)
        {
            while (Builders.Count < count)
                Builders.Add(new Builder());
        }

        public IEnumerable<BuildJob> AllJobs()
        {
            foreach (var builder in Builders)
            {
                foreach (var job in builder.Jobs)
                    yield return job;
            }
        }

        public bool IsQueued(int target)
        {
            foreach (var job in AllJobs())
            {
                if (job.Target == target)
                    return true;
            }
            return false;
        }

        /// <summary>The builder that can take a job and finish it first, or -1 if every queue is full.</summary>
        public int PickBuilder(int perBuilder, long now)
        {
            int best = -1;
            for (int i = 0; i < Builders.Count; i++)
            {
                if (Builders[i].Jobs.Count >= perBuilder)
                    continue;
                if (best < 0 || Builders[i].FreeAt(now) < Builders[best].FreeAt(now))
                    best = i;
            }
            return best;
        }

        public BuildJob Enqueue(int builder, int target, int toLevel, long seconds, long now)
        {
            var b = Builders[builder];
            long start = b.FreeAt(now);
            var job = new BuildJob { Id = NextJobId++, Target = target, ToLevel = toLevel, Start = start, End = start + seconds };
            b.Jobs.Add(job);
            return job;
        }

        public BuildJob Find(int jobId)
        {
            foreach (var job in AllJobs())
            {
                if (job.Id == jobId)
                    return job;
            }
            return null;
        }

        /// <summary>The job that finishes first among all builders, or null.</summary>
        public BuildJob NextToFinish()
        {
            BuildJob next = null;
            foreach (var builder in Builders)
            {
                if (builder.Jobs.Count > 0 && (next == null || builder.Jobs[0].End < next.End))
                    next = builder.Jobs[0];
            }
            return next;
        }

        public void Remove(BuildJob job)
        {
            foreach (var builder in Builders)
            {
                if (builder.Jobs.Remove(job))
                    return;
            }
        }

        /// <summary>
        /// Shortens a job by up to <paramref name="seconds"/> and moves the jobs waiting behind it.
        /// Returns the seconds actually used.
        /// </summary>
        public long Shorten(int jobId, long seconds, long now)
        {
            foreach (var builder in Builders)
            {
                int index = builder.Jobs.FindIndex(j => j.Id == jobId);
                if (index < 0)
                    continue;
                var job = builder.Jobs[index];
                long used = Math.Min(seconds, job.Remaining(now));
                if (used <= 0)
                    return 0;
                job.End -= used;
                for (int i = index + 1; i < builder.Jobs.Count; i++)
                {
                    var next = builder.Jobs[i];
                    long newStart = Math.Max(now, builder.Jobs[i - 1].End);
                    long shift = next.Start - newStart;
                    if (shift <= 0)
                        break;
                    next.Start -= shift;
                    next.End -= shift;
                }
                return used;
            }
            return 0;
        }
    }
}
