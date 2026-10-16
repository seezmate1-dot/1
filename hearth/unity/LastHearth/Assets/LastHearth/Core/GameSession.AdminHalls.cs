using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// The developer's menu, page «Залы» (GameRunner.Rooms.cs): shortcuts for the work of the halls
    /// (GameSession.Rooms.cs). Like the rest of the menu they change the state the way the game would.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>A new board of recruits in the tavern now (empty without a tavern).</summary>
        public List<Recruit> AdminRefreshBoard(long now)
        {
            Advance(now);
            if (!TavernOpen())
                return new List<Recruit>();
            var tavern = Halls.Tavern;
            tavern.BoardAt = now / BoardSeconds * BoardSeconds;
            tavern.Board = NewBoard(Halls.Seed++);
            return tavern.Board;
        }

        /// <summary>The current research ends now; returns it, or null when nothing is researched.</summary>
        public ResearchNodeData AdminFinishResearch(long now)
        {
            Advance(now);
            var node = CurrentResearch;
            if (node == null)
                return null;
            Halls.ResearchEnd = now;
            Advance(now);
            return node;
        }

        /// <summary>Every step of the tree is researched (the bonuses apply at once); returns how many were new.</summary>
        public int AdminResearchAll(long now)
        {
            Advance(now);
            int added = 0;
            foreach (var n in ResearchNodes.Where(n => !Researched(n.Id)))
            {
                Halls.Researched.Add(n.Id);
                added++;
            }
            Halls.Research = "";
            return added;
        }

        /// <summary>Every training point in progress is gained now; returns how many.</summary>
        public int AdminFinishTraining(long now)
        {
            Advance(now);
            int before = Halls.PointsTrained;
            foreach (var slot in Halls.Training)
                slot.End = Math.Min(slot.End, now);
            Advance(now);
            return Halls.PointsTrained - before;
        }

        /// <summary>Every record of the archive is found and read (the reading in progress ends too); returns how many were new.</summary>
        public int AdminUnlockRecords(long now)
        {
            Advance(now);
            Halls.Reading = "";
            int added = 0;
            foreach (var r in Records.Where(r => !RecordRead(r.Id)))
            {
                Halls.Read.Add(r.Id);
                added++;
            }
            return added;
        }

        /// <summary>The chapel's gift is given again now (even if today's was taken); Locked without a chapel.</summary>
        public Fail AdminChapelGift(long now, out ChapelGift gift, out long amount)
        {
            Halls.GiftDay = -1;
            return ClaimGift(now, out gift, out amount);
        }

        /// <summary>Puts a hero at a rank of ascension (clamped to 0..5); returns the rank set, -1 for not a hero.</summary>
        public int AdminSetAscension(int heroId, int rank)
        {
            var d = FindDweller(heroId);
            if (d == null || !d.IsHero)
                return -1;
            rank = Math.Max(0, Math.Min(rank, HallsNumbers.Chapel.Ranks.Count));
            if (rank == 0)
                Halls.Ascension.Remove(heroId);
            else
                Halls.Ascension[heroId] = rank;
            return rank;
        }

        /// <summary>The injured and the sick are healed now; returns how many.</summary>
        public int AdminHealAll(long now)
        {
            Advance(now);
            int healed = Patients(now).Count;
            foreach (var d in State.Dwellers)
                d.InjuredUntil = Math.Min(d.InjuredUntil, now);
            Halls.SickUntil.Clear();
            Halls.KnownInjury.Clear();
            return healed;
        }
    }
}
