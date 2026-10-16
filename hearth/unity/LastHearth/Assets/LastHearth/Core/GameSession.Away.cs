using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 9.3: «Пока вас не было». The fortress is caught up as usual (Advance) while the summary
    /// writes down what happened: production, upkeep, finished jobs, fights against the fortress,
    /// children, squads back from the fog, events that started or ended, the season and the warm week.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>The summary is shown after at least this long away.</summary>
        public const int AwaySummaryMinutes = 30;

        public string JobText(BuildJob job)
        {
            if (job.IsHearth)
                return "Очаг → ур. " + job.ToLevel;
            if (job.IsClearing)
                return "Завал расчищен (этаж " + (job.Floor + 1) + ")";
            var room = FindRoom(job.Target);
            return (room == null ? "Зал" : Balance.Room(room.Type).Name) + " → ур. " + job.ToLevel;
        }

        static string Stars(int stars) => new string('★', stars) + new string('☆', Math.Max(0, 3 - stars));

        /// <summary>Catches the fortress up to <paramref name="now"/> and says what happened meanwhile.</summary>
        public OfflineSummary ReturnAfterAbsence(long now)
        {
            var sum = new OfflineSummary { From = State.Time, To = Math.Max(now, State.Time) };
            double hours = sum.Seconds / 3600.0;
            var stock = State.Rooms.ToDictionary(r => r.Id, r => r.Stock);
            var rates = State.Rooms.ToDictionary(r => r.Id, r => RoomOutputPerHour(r));
            var wallet = State.Wallet.Clone();
            var rewards = State.Rewards.Clone();
            var away = new HashSet<int>(State.Expeditions.Where(e => !e.Back).Select(e => e.Id));
            var known = new HashSet<string>(State.Events.Select(RunKey));
            var open = new HashSet<string>(State.Events.Where(r => !r.Closed).Select(RunKey));
            var log = State.PvpLog.ToList();
            int season = SeasonNumber;
            long warm = Retention.WarmUntil, frost = State.FrostUntil;

            Action<BuildJob> onJob = job => sum.Builds.Add(JobText(job));
            Action<Dweller> onChild = child => sum.Births.Add(child.Name);
            Action<SurgeReward> onSurge = r => sum.Fights.Add(r.Stars > 0
                ? string.Format("Наплыв отбит без вас: {0}, +{1} жара", Stars(r.Stars), r.Ember)
                : "Наплыв прорвался: дальние этажи промёрзли до утра");
            JobCompleted += onJob;
            ChildBorn += onChild;
            SurgeSettled += onSurge;
            try
            {
                Advance(now);
            }
            finally
            {
                JobCompleted -= onJob;
                ChildBorn -= onChild;
                SurgeSettled -= onSurge;
            }

            // production into the rooms' stock, and what did not fit
            foreach (var room in State.Rooms)
            {
                var produces = Balance.Room(room.Type).Produces;
                if (produces == null)
                    continue;
                stock.TryGetValue(room.Id, out double before);
                rates.TryGetValue(room.Id, out double rate);
                double got = Math.Max(0, room.Stock - before);
                sum.Produced[produces.Value] += (long)Math.Floor(got);
                double cap = RoomOutputPerHour(room) * (Balance.OfflineCapHours + SpecValue(room, SpecEffect.StockHours));
                if (cap > 0 && room.Stock >= cap - 0.5)
                {
                    sum.FullRooms++;
                    sum.Lost[produces.Value] += (long)Math.Floor(Math.Max(0, rate * hours - got));
                }
            }
            sum.CoalBurnt = Math.Max(0, wallet[ResourceType.Coal] - State.Wallet[ResourceType.Coal]);
            sum.FoodEaten = Math.Max(0, wallet[ResourceType.Food] - State.Wallet[ResourceType.Food]);
            sum.OutOfCoal = State.OutOfCoal;
            for (int i = 0; i < ResourceBag.Count; i++)
            {
                var t = (ResourceType)i;
                long came = t == ResourceType.Coal || t == ResourceType.Food ? 0 : Math.Max(0, State.Wallet[t] - wallet[t]);
                sum.Earned[t] = came + Math.Max(0, State.Rewards[t] - rewards[t]);
            }

            // arena defenses and raids written into the log meanwhile (it keeps the last 30)
            int overlap = Math.Min(log.Count, State.PvpLog.Count);
            while (overlap > 0 && !log.Skip(log.Count - overlap).SequenceEqual(State.PvpLog.Take(overlap)))
                overlap--;
            sum.Fights.AddRange(State.PvpLog.Skip(overlap));

            foreach (var e in State.Expeditions.Where(e => e.Back && away.Contains(e.Id)))
            {
                string team = string.Join(", ", e.Team.Select(id => FindDweller(id)?.Name).Where(n => n != null));
                sum.Routes.Add(string.Format("{0}: вернулись из тумана", team == "" ? "Отряд" : team));
            }

            foreach (var run in State.Events)
            {
                string key = RunKey(run), name = "«" + EventInfo(run.Id).Name + "»";
                if (!known.Contains(key) && run.Start <= sum.To)
                    sum.EventsStarted.Add(name + (run.Personal ? " (для вас, на сутки)" : ""));
                if (run.Closed && (open.Contains(key) || !known.Contains(key)))
                    sum.EventsEnded.Add(name);
            }

            if (SeasonNumber != season)
                sum.Other.Add(string.Format("Сезон {0} завершён{1}; начался сезон {2}", season,
                    Retention.LastSeasonAutoGifts > 0 ? ", невзятые награды выданы (" + Retention.LastSeasonAutoGifts + ")" : "", SeasonNumber));
            if (Retention.WarmUntil > warm && Retention.WarmUntil > sum.To)
                sum.Other.Add(string.Format("Тёплая неделя: +{0:0}% выработки ещё {1} ч", Balance.Activities.Surge.WarmWeek.Output * 100,
                    (Retention.WarmUntil - sum.To + 1800) / 3600));
            if (State.FrostUntil > frost && State.FrostUntil > sum.To)
                sum.Other.Add("Дальние этажи промёрзли до утра");
            if (sum.OutOfCoal)
                sum.Other.Add("Уголь кончился: залы промерзают");
            if (sum.FullRooms > 0)
                sum.Other.Add(string.Format("Залы полны ({0}): соберите выработку", sum.FullRooms));
            return sum;
        }
    }
}
