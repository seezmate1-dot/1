using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    public class BuildQueueTests
    {
        static BuildQueue Queue(int builders)
        {
            var q = new BuildQueue();
            q.EnsureBuilders(builders);
            return q;
        }

        [Test]
        public void ABuilderTakesThreeJobsInARow()
        {
            var q = Queue(1);
            for (int i = 0; i < 3; i++)
                q.Enqueue(q.PickBuilder(3, 0), i + 1, 1, 100, 0);
            Assert.AreEqual(-1, q.PickBuilder(3, 0));
            var jobs = q.Builders[0].Jobs;
            Assert.AreEqual(0, jobs[0].Start);
            Assert.AreEqual(100, jobs[1].Start, "the second job starts when the first ends");
            Assert.AreEqual(300, jobs[2].End);
        }

        [Test]
        public void TheBuilderThatIsFreeFirstGetsTheJob()
        {
            var q = Queue(2);
            q.Enqueue(0, 1, 1, 500, 0);
            Assert.AreEqual(1, q.PickBuilder(3, 0));
        }

        [Test]
        public void ShorteningAJobMovesTheJobsBehindIt()
        {
            var q = Queue(1);
            var first = q.Enqueue(0, 1, 1, 1000, 0);
            var second = q.Enqueue(0, 2, 1, 500, 0);
            long used = q.Shorten(first.Id, 300, 100);
            Assert.AreEqual(300, used);
            Assert.AreEqual(700, first.End);
            Assert.AreEqual(700, second.Start);
            Assert.AreEqual(1200, second.End);
        }

        [Test]
        public void ShorteningNeverGoesPastNow()
        {
            var q = Queue(1);
            var job = q.Enqueue(0, 1, 1, 1000, 0);
            long used = q.Shorten(job.Id, 5000, 400);
            Assert.AreEqual(600, used);
            Assert.AreEqual(400, job.End);
        }
    }
}
