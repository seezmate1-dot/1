using System;
using System.Collections.Generic;

namespace LastHearth.Core
{
    /// <summary>How a purchase ended.</summary>
    public enum StoreResult
    {
        Purchased,
        /// <summary>Purchases are off (MODES.md 4.5: the game is free, no payments at the start).</summary>
        Disabled,
        Cancelled,
        Pending,
        Failed,
        UnknownProduct,
    }

    /// <summary>What could be sold one day (GDD 13): Sparks packs, the premium season track, cosmetics, resource packs in the weekly limit.</summary>
    public enum StoreProductKind
    {
        Sparks,
        SeasonPremium,
        Cosmetic,
        ResourcePack,
    }

    [Serializable]
    public sealed class StoreProduct
    {
        public string Id = "";
        public StoreProductKind Kind;
        public string Title = "";
        /// <summary>The price as the store shows it, in the player's currency ("149 ₽").</summary>
        public string Price = "";
        /// <summary>Sparks a Sparks pack gives.</summary>
        public long Sparks;
    }

    /// <summary>A finished purchase as the store reports it; the server will check the receipt later.</summary>
    public sealed class StoreReceipt
    {
        public string ProductId = "";
        public string TransactionId = "";
        public string Payload = "";
    }

    /// <summary>
    /// MODES.md 4.5: the place where Google Play Billing and the App Store plug in later without
    /// changing the game. The game asks the store for its products and buys through it; until real
    /// money is turned on, NoStore answers that purchases are off.
    /// </summary>
    public interface IStore
    {
        /// <summary>False while purchases are off or the store is unreachable: the game shows no prices.</summary>
        bool Available { get; }

        IReadOnlyList<StoreProduct> Products { get; }

        /// <summary>Starts a purchase; the callback comes when the store answers (maybe much later).</summary>
        void Purchase(string productId, Action<StoreResult, StoreReceipt> done);

        /// <summary>Restores non-consumable purchases (the premium track) on a new phone.</summary>
        void Restore(Action<IReadOnlyList<StoreReceipt>> done);
    }

    /// <summary>The store of a free game: nothing to buy, every purchase answers Disabled at once.</summary>
    public sealed class NoStore : IStore
    {
        static readonly StoreProduct[] None = new StoreProduct[0];
        static readonly StoreReceipt[] NoReceipts = new StoreReceipt[0];

        public bool Available => false;

        public IReadOnlyList<StoreProduct> Products => None;

        public void Purchase(string productId, Action<StoreResult, StoreReceipt> done) => done?.Invoke(StoreResult.Disabled, null);

        public void Restore(Action<IReadOnlyList<StoreReceipt>> done) => done?.Invoke(NoReceipts);
    }
}
