using System;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// The town around the farm (GDD 6): houses bring residents, services let the town hold more of
    /// them, decorations give beauty and XP, land expansions (saw + axe) open lots for new buildings.
    /// Workshops and animal houses need enough residents to work in them.
    /// </summary>
    public sealed partial class MetaGame
    {
        public int Built(string id) => Dict.Get(State.Town.Buildings, id);

        public int Population
        {
            get
            {
                int p = 0;
                foreach (var h in B.Town.Houses) p += h.Population * Built(h.Id);
                return p;
            }
        }

        public int PopulationCap
        {
            get
            {
                int c = B.Town.BasePopulationCap;
                foreach (var s in B.Town.Services) c += s.Capacity * Built(s.Id);
                return c;
            }
        }

        public int Beauty
        {
            get
            {
                int b = 0;
                foreach (var d in Data.Town.Values) b += d.Beauty * Built(d.Id);
                return b;
            }
        }

        public int Lots
        {
            get
            {
                int l = B.Town.BaseLots;
                for (int i = 0; i < State.Farm.Expansions && i < B.Expansions.Count; i++) l += B.Expansions[i].Lots;
                return l;
            }
        }

        /// <summary>Lots taken by houses and services (decorations stand anywhere).</summary>
        public int LotsUsed
        {
            get
            {
                int n = 0;
                foreach (var h in B.Town.Houses) n += Built(h.Id);
                foreach (var s in B.Town.Services) n += Built(s.Id);
                return n;
            }
        }

        /// <summary>Why this building cannot be built now (None = it can).</summary>
        public MetaError CanBuild(string id)
        {
            if (!FarmOpen) return MetaError.FarmClosed;
            if (id == null || !Data.Town.TryGetValue(id, out var def)) return MetaError.UnknownId;
            if (def.Level > Level) return MetaError.Locked;
            if (def.Max > 0 && Built(id) >= def.Max) return MetaError.LimitReached;
            if (def.Kind != TownKinds.Decoration && LotsUsed >= Lots) return MetaError.NotEnoughSpace;
            if (def.Kind == TownKinds.House && Population + def.Population > PopulationCap) return MetaError.NotEnoughPopulation;
            if (State.Wallet.Coins < def.Coins) return MetaError.NotEnoughCoins;
            if (State.Wallet.Stars < def.Stars) return MetaError.NotEnoughStars;
            return MetaError.None;
        }

        public ActionResult Build(long now, string id)
        {
            var e = CanBuild(id);
            if (e != MetaError.None) return ActionResult.Fail(e);
            var def = Data.Town[id];
            SpendCoins(def.Coins);
            State.Wallet.Stars -= def.Stars;
            Dict.Add(State.Town.Buildings, id, 1);
            Stat("buildings");
            var r = new Reward { Xp = def.Xp };
            Grant(now, r);
            return ActionResult.Success(r);
        }

        /// <summary>The next land expansion, or null when all are done.</summary>
        public ExpansionDef NextExpansion =>
            State.Farm.Expansions < B.Expansions.Count ? B.Expansions[State.Farm.Expansions] : null;

        public ActionResult Expand(long now)
        {
            if (!FarmOpen) return ActionResult.Fail(MetaError.FarmClosed);
            var e = NextExpansion;
            if (e == null) return ActionResult.Fail(MetaError.LimitReached);
            if (e.Level > Level) return ActionResult.Fail(MetaError.Locked);
            if (!HasTools(e.Tools)) return ActionResult.Fail(MetaError.NotEnoughTools);
            if (State.Wallet.Coins < e.Coins) return ActionResult.Fail(MetaError.NotEnoughCoins);
            SpendCoins(e.Coins);
            TakeTools(e.Tools);
            State.Farm.Expansions++;
            Stat("expansions");
            return ActionResult.Success();
        }
    }
}
