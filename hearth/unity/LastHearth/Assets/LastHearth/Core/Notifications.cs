using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 12 and EVENTS.md 1: local notifications only about what is useful — a builder is free, a squad
    /// is back, an event starts, the Surge opens in 30 minutes, the rooms are full — and never more than
    /// three a day, counting the ones already shown today. Nothing at night: those move to the morning.
    /// The game plans them when it goes to the background and cancels them when it comes back.
    /// </summary>
    public static class NotificationPlanner
    {
        public const int PerDay = 3;
        /// <summary>Quiet hours, local time: from 22:00 to 08:00 notifications wait for the morning.</summary>
        public const int QuietFrom = 22, QuietTo = 8;
        /// <summary>How far ahead the plan reaches; the next time the game is closed it is planned again.</summary>
        public const long Horizon = 2 * 86400;
        /// <summary>Two of the same kind closer than this become one.</summary>
        public const long MergeSeconds = 3600;

        public static List<PlannedNotification> Plan(GameSession s, long now)
        {
            var st = s.State;
            var list = Candidates(s, now);
            foreach (var n in list)
                n.At = Math.Max(now + 60, OutOfQuiet(s, n.At));

            // the same kind within an hour: one notification, at the later moment
            var merged = new List<PlannedNotification>();
            foreach (var n in list.OrderBy(n => n.At))
            {
                var last = merged.LastOrDefault(m => m.Kind == n.Kind);
                if (last != null && n.At - last.At < MergeSeconds)
                {
                    last.At = n.At;
                    if (n.Kind == NotificationKind.BuildDone)
                        last.Text = "Строители свободны: работы закончены";
                    else if (n.Kind == NotificationKind.RouteBack)
                        last.Text = "Отряды вернулись из тумана";
                    else if (n.Kind == NotificationKind.EventStart)
                        last.Text = last.Text + ", " + n.Text;
                    continue;
                }
                merged.Add(n);
            }

            // at most three a day: the useful ones first, the past ones of today count
            var plan = new List<PlannedNotification>();
            foreach (var day in merged.GroupBy(n => s.LocalDay(n.At)))
            {
                int shown = st.Retention.NotifiedAt.Count(t => t <= now && s.LocalDay(t) == day.Key);
                plan.AddRange(day.OrderBy(n => n.Kind).ThenBy(n => n.At).Take(Math.Max(0, PerDay - shown)));
            }
            plan = plan.OrderBy(n => n.At).ToList();

            st.Retention.NotifiedAt.RemoveAll(t => t > now || t < now - Horizon);
            st.Retention.NotifiedAt.AddRange(plan.Select(n => n.At));
            return plan;
        }

        /// <summary>A moment in the quiet hours moves to 08:00 of the (next) morning.</summary>
        static long OutOfQuiet(GameSession s, long at)
        {
            long second = s.LocalSecondOfDay(at);
            if (second >= QuietFrom * 3600L)
                return at - second + 86400 + QuietTo * 3600L;
            if (second < QuietTo * 3600L)
                return at - second + QuietTo * 3600L;
            return at;
        }

        static List<PlannedNotification> Candidates(GameSession s, long now)
        {
            var st = s.State;
            var b = s.Balance;
            var list = new List<PlannedNotification>();
            void Add(long at, NotificationKind kind, string title, string text)
            {
                if (at > now && at <= now + Horizon)
                    list.Add(new PlannedNotification { At = at, Kind = kind, Title = title, Text = text });
            }

            // a builder becomes free: the end of his last job
            foreach (var builder in st.Queue.Builders.Where(x => x.Jobs.Count > 0))
            {
                var job = builder.Jobs[builder.Jobs.Count - 1];
                Add(job.End, NotificationKind.BuildDone, "Стройка готова", "Готово: " + s.JobText(job));
            }

            foreach (var e in st.Expeditions.Where(e => !e.Back))
                Add(e.End, NotificationKind.RouteBack, "Вылазка вернулась", "Отряд вернулся из тумана с добычей");

            // an event of the shared calendar starts (at the local midnight, so in the morning)
            long today = s.LocalDay(now);
            for (long day = today; day <= today + Horizon / 86400; day++)
            {
                foreach (var e in b.Activities.Events)
                {
                    if (st.HearthLevel >= e.Unlock && s.OnCalendar(e, day) && !s.OnCalendar(e, day - 1))
                        Add(day * 86400 - st.UtcOffsetSeconds, NotificationKind.EventStart, "Новое событие", "«" + e.Name + "»");
                }
            }

            // the Surge opens at 19:00: half an hour before, unless today's is settled
            var surge = b.Activities.Surge;
            if (st.HearthLevel >= surge.Unlock)
            {
                for (long day = today; day <= today + 1; day++)
                {
                    if (day > st.SurgeDay)
                        Add(day * 86400 - st.UtcOffsetSeconds + surge.StartHour * 3600L - 1800, NotificationKind.SurgeSoon,
                            "Наплыв через 30 минут", "Туман подступает: защитите крепость вживую");
                }
            }

            // every producing room is full: the production stands
            long full = 0;
            foreach (var room in st.Rooms)
            {
                double rate = s.RoomOutputPerHour(room);
                if (rate <= 0)
                    continue;
                double cap = rate * b.OfflineCapHours;
                long at = now + (long)Math.Ceiling(Math.Max(0, cap - room.Stock) / rate * 3600);
                full = Math.Max(full, at);
            }
            if (full > now)
                Add(full, NotificationKind.StorageFull, "Залы полны", "Выработка стоит: соберите её");
            return list;
        }
    }
}
