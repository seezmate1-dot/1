using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>«Пока вас не было» (GDD 8): what grew and got made while the game was closed.</summary>
    public sealed class AwaySummary
    {
        public long AwaySeconds;
        /// <summary>Goods that became ready to collect: item id → units (crops, fruit, animal goods, workshop goods).</summary>
        public SortedDictionary<string, int> Ready = new SortedDictionary<string, int>();
        /// <summary>Boosters the tool shed finished.</summary>
        public SortedDictionary<string, int> Boosters = new SortedDictionary<string, int>();
        public int LivesRestored;
        public bool LivesFull;
        public bool TruckBack;
        public int NewOrders;
        /// <summary>Russian lines for the welcome-back window, e.g. «Пшеница ×12 созрела».</summary>
        public List<string> Lines = new List<string>();
        public bool IsEmpty => Lines.Count == 0;
    }

    public sealed partial class MetaGame
    {
        /// <summary>Minimum absence that deserves a summary window.</summary>
        public const long AwayThresholdSeconds = 5 * 60;

        /// <summary>
        /// Call when the game comes back (start or resume): builds the summary of what happened since the
        /// player was last seen, then marks them as present. Production itself needs no catching up:
        /// every timer is an absolute time.
        /// </summary>
        public AwaySummary Resume(long now)
        {
            var s = BuildAwaySummary(now);
            Touch(now);
            EnsureDay(now);
            return s;
        }

        public AwaySummary BuildAwaySummary(long now)
        {
            long since = State.LastSeenAt;
            var s = new AwaySummary { AwaySeconds = Math.Max(0, now - since) };
            if (s.AwaySeconds < AwayThresholdSeconds) return s;
            bool Became(long at) => at > since && at <= now;

            int livesBefore = State.Lives.Count;
            int livesNow = Lives(now);
            s.LivesRestored = Math.Max(0, livesNow - livesBefore);
            s.LivesFull = s.LivesRestored > 0 && livesNow >= B.Lives.Max;

            if (FarmOpen)
            {
                foreach (var p in State.Farm.Plots)
                    if (p.Crop != null && Became(p.ReadyAt)) Dict.Add(s.Ready, p.Crop, Data.Crops[p.Crop].Yield);
                foreach (var t in State.Farm.Trees)
                    if (Became(t.ReadyAt)) Dict.Add(s.Ready, Data.Trees[t.Type].Item, Data.Trees[t.Type].Yield);
                foreach (var a in State.Farm.Animals)
                    if (a.Fed && Became(a.ReadyAt)) Dict.Add(s.Ready, Data.Animals[a.Type].Product, 1);
                foreach (var w in State.Farm.Workshops)
                    foreach (var j in w.Jobs)
                        if (Became(j.EndAt))
                        {
                            var rc = Data.Recipes[j.Recipe];
                            Dict.Add(Boosters.IsBooster(rc.Output) ? s.Boosters : s.Ready, rc.Output, rc.Count);
                        }
                foreach (var c in State.Orders.Board)
                    if (c.Order == null && Became(c.AvailableAt)) s.NewOrders++;
                var truck = State.Orders.Truck;
                s.TruckBack = TruckUnlocked && truck.AwayUntil > 0 && Became(truck.AwayUntil);
            }

            s.Lines.Add("Вас не было " + Texts.Duration(s.AwaySeconds) + ".");
            foreach (var kv in s.Ready)
                s.Lines.Add(Data.NameOf(kv.Key) + " ×" + kv.Value + " — " + ReadyWord(kv.Key));
            foreach (var kv in s.Boosters)
                s.Lines.Add("Сарай инструментов сделал: " + Data.NameOf(kv.Key) + " ×" + kv.Value);
            if (s.LivesFull) s.Lines.Add("Все жизни восстановлены.");
            else if (s.LivesRestored > 0) s.Lines.Add("Восстановлено жизней: " + s.LivesRestored + ".");
            if (s.NewOrders > 0) s.Lines.Add("Новых заказов на доске: " + s.NewOrders + ".");
            if (s.TruckBack) s.Lines.Add("Грузовик вернулся и ждёт груз.");
            if (s.Lines.Count == 1) s.Lines.Clear();
            return s;
        }

        string ReadyWord(string item)
        {
            var kind = Data.Items.TryGetValue(item, out var d) ? d.Kind : "";
            switch (kind)
            {
                case ItemKinds.Crop: return "созрело";
                case ItemKinds.Fruit: return "поспело";
                case ItemKinds.Animal: return "можно собрать";
                default: return "готово";
            }
        }

        /// <summary>
        /// Local notifications to schedule when the app goes to background (GDD 8: at most 3 a day):
        /// all fields ready, lives full, the longest workshop queue done. Times are absolute.
        /// </summary>
        public List<(long at, string text)> PlanNotifications(long now)
        {
            var r = new List<(long, string)>();
            if (FarmOpen)
            {
                long crops = 0;
                foreach (var p in State.Farm.Plots)
                    if (p.Crop != null) crops = Math.Max(crops, p.ReadyAt);
                if (crops > now) r.Add((crops, "Урожай созрел — пора собирать!"));
                long jobs = 0;
                foreach (var w in State.Farm.Workshops)
                    if (w.Jobs.Count > 0) jobs = Math.Max(jobs, w.Jobs[w.Jobs.Count - 1].EndAt);
                if (jobs > now) r.Add((jobs, "Мастерские закончили работу."));
            }
            long lives = LivesFullIn(now);
            if (lives > 0) r.Add((now + lives, "Жизни восстановлены — можно играть!"));
            r.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            if (r.Count > 3) r.RemoveRange(3, r.Count - 3);
            return r;
        }
    }
}
