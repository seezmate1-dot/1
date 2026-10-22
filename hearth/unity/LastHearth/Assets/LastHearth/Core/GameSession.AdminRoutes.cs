using System;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>The developer's menu, page «Вылазки» (GameRunner.Admin.cs): routes, notes, map pieces, specializations.</summary>
    public sealed partial class GameSession
    {
        /// <summary>Every team on the way passes its points and comes back now; returns how many came back.</summary>
        public int AdminFinishRoutes(long now)
        {
            Advance(now);
            int count = 0;
            foreach (var e in State.Expeditions.Where(e => !e.Back))
            {
                e.End = Math.Min(e.End, State.Time);
                count++;
            }
            Advance(now);
            return count;
        }

        /// <summary>Today's routes can be started again; returns how many are left.</summary>
        public int AdminRefillRoutes(long now)
        {
            RoutesLeft(now);
            State.RoutesStarted = 0;
            return RoutesLeft(now);
        }

        /// <summary>The next notes of the story go into the journal; returns how many there are now.</summary>
        public int AdminGiveNotes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int note = NextNote(null);
                if (note < 0)
                    break;
                State.RouteNotes.Add(note);
            }
            return State.RouteNotes.Count;
        }

        public void AdminClearNotes() => State.RouteNotes.Clear();

        /// <summary>Pieces of maps for the Treasure hunters; returns how many are kept.</summary>
        public int AdminGiveFragments(int count)
        {
            State.MapFragments = Math.Max(0, State.MapFragments + count);
            return State.MapFragments;
        }
    }
}
