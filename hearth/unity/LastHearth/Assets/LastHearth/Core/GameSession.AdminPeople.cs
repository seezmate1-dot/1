using System;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// The developer's menu, page «Жители» (GameRunner.People.cs): shortcuts for the levels and traits of the dwellers
    /// and the spirit of the fortress (GameSession.People.cs). Like the rest of the menu they change the state the way
    /// the game would: a set spirit then follows its target again, levels bring their stat points and traits.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Sets the spirit now (0-100); it moves towards its target from here. Returns the new value.</summary>
        public double AdminSetMorale(double value, long now)
        {
            Advance(now);
            var p = People;
            p.Morale = Math.Max(0, Math.Min(100, value));
            p.MoraleAt = State.Time;
            if (!InDespair)
                p.LowSince = 0;
            else if (p.LowSince <= 0)
                p.LowSince = State.Time;
            return p.Morale;
        }

        /// <summary>Experience to a dweller (as if earned: the trait counts); returns the levels gained.</summary>
        public int AdminGiveXp(int dwellerId, double xp)
        {
            var d = FindDweller(dwellerId);
            return d == null ? 0 : GainXp(d, xp);
        }

        /// <summary>Raises a dweller by whole levels (with their stat points and traits); returns the level now.</summary>
        public int AdminAddLevels(int dwellerId, int levels)
        {
            var d = FindDweller(dwellerId);
            if (d == null)
                return 0;
            EnsureDweller(d);
            for (int i = 0; i < levels && d.Level < MaxDwellerLevel; i++)
                AddXp(d, Math.Max(0, XpToNext(d.Level) - d.Xp));
            return d.Level;
        }

        /// <summary>Adds a trait the dweller does not have (up to three); returns whether it was added.</summary>
        public bool AdminAddTrait(int dwellerId, Trait trait)
        {
            var d = FindDweller(dwellerId);
            if (d == null)
                return false;
            EnsureDweller(d);
            if (d.Traits.Contains(trait) || d.Traits.Count >= 3)
                return false;
            d.Traits.Add(trait);
            return true;
        }

        /// <summary>Takes a trait away (a dweller keeps at least one); returns whether it was removed.</summary>
        public bool AdminRemoveTrait(int dwellerId, Trait trait)
        {
            var d = FindDweller(dwellerId);
            return d?.Traits != null && d.Traits.Count > 1 && d.Traits.Remove(trait);
        }

        /// <summary>
        /// The desertion check now, without waiting for the hours of despair: in despair the next dweller leaves at once;
        /// otherwise nobody does. Returns who left, or null.
        /// </summary>
        public Dweller AdminDesertionCheck(long now)
        {
            Advance(now);
            return CheckDesertion(State.Time, true);
        }

        /// <summary>Every deserter comes back for free (if the fortress has room); returns how many.</summary>
        public int AdminRecallAll(long now)
        {
            Advance(now);
            int n = 0;
            var p = People;
            while (p.Deserters.Count > 0 && !PopulationFull())
            {
                var x = p.Deserters[0];
                p.Deserters.RemoveAt(0);
                x.Dweller.RoomId = -1;
                x.Dweller.RouteId = 0;
                EnsureDweller(x.Dweller);
                State.Dwellers.Add(x.Dweller);
                if (x.Rarity != Rarity.Common)
                    Halls.Rarity[x.Dweller.Id] = x.Rarity;
                n++;
            }
            return n;
        }

        /// <summary>News of the fortress now (as if it happened).</summary>
        public void AdminNews(MoraleNews id, long now)
        {
            Advance(now);
            AddNews(id);
        }

        /// <summary>The rarity of a dweller as if hired so (common: none recorded).</summary>
        public Rarity AdminSetRarity(int dwellerId, Rarity rarity)
        {
            if (FindDweller(dwellerId) == null)
                return Rarity.Common;
            if (rarity == Rarity.Common)
                Halls.Rarity.Remove(dwellerId);
            else
                Halls.Rarity[dwellerId] = rarity;
            return RarityOf(dwellerId);
        }

        /// <summary>The dweller of the list after this one (for the menu's picker), or the first.</summary>
        public Dweller AdminNextDweller(int dwellerId)
        {
            var list = State.Dwellers;
            if (list.Count == 0)
                return null;
            int i = list.FindIndex(d => d.Id == dwellerId);
            return list[(i + 1) % list.Count];
        }

        /// <summary>The trait after this one in the list of balance.json (for the menu's picker).</summary>
        public Trait AdminNextTrait(Trait t)
        {
            var all = TraitList.Select(x => x.Id).ToList();
            if (all.Count == 0)
                return t;
            int i = all.IndexOf(t);
            return all[(i + 1) % all.Count];
        }
    }
}
