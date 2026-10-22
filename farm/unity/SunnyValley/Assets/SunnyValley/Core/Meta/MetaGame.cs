using System;
using System.Collections.Generic;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// The meta game: currencies, lives, the match-3 bridge, chapter 1, the farm economy, the town and
    /// daily retention. One object for the UI; every action takes <c>now</c> (seconds, any monotonic
    /// wall clock — the core never reads the system time) and returns an <see cref="ActionResult"/>.
    /// Things worth a toast (level up, zone done, tool found…) are queued and drained with <see cref="TakeEvents"/>.
    /// </summary>
    public sealed partial class MetaGame
    {
        public MetaData Data { get; }
        public MetaState State { get; }
        Balance B => Data.Balance;

        readonly List<MetaEvent> events = new List<MetaEvent>();

        public MetaGame(MetaData data, MetaState state)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>A fresh game: starting currencies and boosters, full lives, chapter 1 at the yard.</summary>
        public static MetaGame NewGame(MetaData data, long now, ulong seed, int utcOffsetMinutes = 0)
        {
            var b = data.Balance;
            var s = new MetaState
            {
                CreatedAt = now,
                LastSeenAt = now,
                Rng = seed,
                UtcOffsetMinutes = utcOffsetMinutes,
            };
            s.Wallet.Coins = b.Start.Coins;
            s.Wallet.Crystals = b.Start.Crystals;
            s.Wallet.Stars = b.Start.Stars;
            foreach (var kv in b.Start.Boosters) s.Boosters[kv.Key] = kv.Value;
            foreach (var kv in b.Start.Tools) s.Tools[kv.Key] = kv.Value;
            s.Lives.Count = b.Lives.Max;
            s.Lives.RegenFrom = now;
            var g = new MetaGame(data, s);
            g.EnsureDay(now);
            return g;
        }

        /// <summary>Loads a save (any known version). Returns null if the save is broken.</summary>
        public static MetaGame Load(MetaData data, string json)
        {
            var s = SaveSystem.FromJson(json);
            return s == null ? null : new MetaGame(data, s);
        }

        public string Save() => SaveSystem.ToJson(State);

        /// <summary>Events since the last call (level ups, zone completions, found tools…), oldest first.</summary>
        public List<MetaEvent> TakeEvents()
        {
            var r = new List<MetaEvent>(events);
            events.Clear();
            return r;
        }

        /// <summary>Events kept for the UI at most; older ones are dropped if nobody drains them.</summary>
        public const int MaxPendingEvents = 200;

        void Emit(MetaEventKind kind, string id, int value = 0, Reward reward = null, string text = null)
        {
            if (events.Count >= MaxPendingEvents) events.RemoveAt(0);
            events.Add(new MetaEvent { Kind = kind, Id = id, Value = value, Reward = reward, Text = text });
        }

        // ------------------------------------------------------------------ currencies

        public long Coins => State.Wallet.Coins;
        public int Stars => State.Wallet.Stars;
        public int Crystals => State.Wallet.Crystals;
        public long Xp => State.Wallet.Xp;
        public int Level => State.Wallet.Level;
        public bool FarmOpen => State.Farm.Open;

        /// <summary>XP at the start of the current level and needed for the next one (for the progress bar).</summary>
        public long XpForLevel(int level) => Data.LevelDef(level)?.Xp ?? long.MaxValue;

        public int ToolCount(string tool) => Dict.Get(State.Tools, tool);
        public int BoosterCount(string booster) => Dict.Get(State.Boosters, booster);
        public int BarnCount(string item) => Dict.Get(State.Barn, item);

        bool SpendCoins(long amount)
        {
            if (amount < 0 || State.Wallet.Coins < amount) return false;
            State.Wallet.Coins -= amount;
            Stat("coins_spent", amount);
            return true;
        }

        bool SpendCrystals(int amount)
        {
            if (amount < 0 || State.Wallet.Crystals < amount) return false;
            State.Wallet.Crystals -= amount;
            Stat("crystals_spent", amount);
            return true;
        }

        bool HasTools(IDictionary<string, int> tools)
        {
            foreach (var kv in tools)
                if (ToolCount(kv.Key) < kv.Value) return false;
            return true;
        }

        void TakeTools(IDictionary<string, int> tools)
        {
            foreach (var kv in tools)
                Dict.Add(State.Tools, kv.Key, -kv.Value);
        }

        /// <summary>Gives a reward: currencies, XP (with level ups), lives, goods (overflow sold), tools, boosters.</summary>
        public void Grant(long now, Reward r)
        {
            if (r == null) return;
            var w = State.Wallet;
            w.Coins += r.Coins;
            if (r.Coins > 0) Stat("coins_earned", r.Coins);
            w.Stars += r.Stars;
            w.Crystals += r.Crystals;
            if (r.Lives > 0)
            {
                RefreshLives(now);
                State.Lives.Count = Math.Min(B.Lives.Max, State.Lives.Count + r.Lives);
            }
            if (r.InfiniteLivesMinutes > 0)
                State.Lives.InfiniteUntil = Math.Max(State.Lives.InfiniteUntil, now) + r.InfiniteLivesMinutes * 60L;
            if (r.Tools != null)
                foreach (var kv in r.Tools) Dict.Add(State.Tools, kv.Key, kv.Value);
            if (r.Boosters != null)
                foreach (var kv in r.Boosters) Dict.Add(State.Boosters, kv.Key, kv.Value);
            if (r.Items != null)
                foreach (var kv in r.Items) StoreOrSell(kv.Key, kv.Value);
            if (r.Xp > 0) AddXp(now, r.Xp);
        }

        /// <summary>Puts goods into the barn; what does not fit is sold. Returns coins from the overflow.</summary>
        int StoreOrSell(string item, int count)
        {
            if (count <= 0) return 0;
            int fit = Math.Min(count, BarnFree);
            if (fit > 0) Dict.Add(State.Barn, item, fit);
            int over = count - fit;
            if (over <= 0) return 0;
            int coins = over * (Data.Items.TryGetValue(item, out var def) ? def.Sell : 0);
            State.Wallet.Coins += coins;
            return coins;
        }

        void AddXp(long now, long xp)
        {
            var w = State.Wallet;
            w.Xp += xp;
            Stat("xp_earned", xp);
            while (w.Level < Data.MaxLevel && w.Xp >= Data.LevelDef(w.Level + 1).Xp)
            {
                w.Level++;
                var def = Data.LevelDef(w.Level);
                Emit(MetaEventKind.LevelUp, w.Level.ToString(), w.Level, def.Reward,
                    "Уровень фермы " + w.Level + "!");
                Grant(now, def.Reward);
            }
        }

        // ------------------------------------------------------------------ time

        /// <summary>Local day number (for daily tasks and gifts).</summary>
        public long DayOf(long now) => FloorDiv(now + State.UtcOffsetMinutes * 60L, 86400);

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        /// <summary>Seconds left until the given time (0 if past).</summary>
        public static long Left(long now, long at) => Math.Max(0, at - now);

        /// <summary>Crystals to skip the given number of seconds.</summary>
        public int SpeedUpPrice(long seconds)
        {
            if (seconds <= 0) return 0;
            int per = Math.Max(1, B.SpeedUp.SecondsPerCrystal);
            return (int)Math.Max(B.SpeedUp.MinCrystals, (seconds + per - 1) / per);
        }

        /// <summary>Marks the player as present now (call on pause/quit after building the away summary).</summary>
        public void Touch(long now)
        {
            if (now > State.LastSeenAt) State.LastSeenAt = now;
        }

        void Stat(string key, long amount = 1)
        {
            State.Stats.TryGetValue(key, out long v);
            State.Stats[key] = v + amount;
        }

        public long StatValue(string key) => State.Stats.TryGetValue(key, out long v) ? v : 0;

        int Roll(int max) => MetaRng.Range(ref State.Rng, max);
        bool Chance(int percent) => MetaRng.Percent(ref State.Rng, percent);

        // ------------------------------------------------------------------ tools found while playing

        /// <summary>Rolls the harvest tool drop (GDD 6: tools fall out when collecting the harvest).</summary>
        void MaybeDropTool(int percent, Reward into)
        {
            if (!Chance(percent)) return;
            string tool = RandomTool();
            Dict.Add(State.Tools, tool, 1);
            Dict.Add(into.Tools, tool, 1);
            Emit(MetaEventKind.ToolFound, tool, 1, null, "Найден инструмент: " + Data.NameOf(tool));
        }

        string RandomTool()
        {
            var d = B.ToolDrops;
            if (Level >= d.LandFromLevel && Chance(d.LandSharePercent))
                return Tools.Land[Roll(Tools.Land.Length)];
            return Tools.Barn[Roll(Tools.Barn.Length)];
        }

        /// <summary>Lets the UI remember where things stand on the farm map (the core does not use it).</summary>
        public void SetPlacement(string key, int x, int y) => State.Layout[key] = new[] { x, y };
    }
}
