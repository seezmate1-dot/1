using System.Collections.Generic;
using LastHearth.Core;
using NUnit.Framework;

namespace LastHearth.Core.Tests
{
    /// <summary>MODES.md 4.5: real money is off; the IStore stub sells nothing and says so.</summary>
    public class StoreTests
    {
        [Test]
        public void NoStoreSellsNothingAndAnswersAtOnce()
        {
            IStore store = new NoStore();
            Assert.IsFalse(store.Available);
            Assert.AreEqual(0, store.Products.Count);
            StoreResult? result = null;
            StoreReceipt receipt = new StoreReceipt();
            store.Purchase("sparks_small", (r, rc) =>
            {
                result = r;
                receipt = rc;
            });
            Assert.AreEqual(StoreResult.Disabled, result);
            Assert.IsNull(receipt);
            IReadOnlyList<StoreReceipt> restored = null;
            store.Restore(list => restored = list);
            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.Count);
            Assert.DoesNotThrow(() => store.Purchase("x", null));
        }
    }
}
