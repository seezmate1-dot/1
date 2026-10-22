using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// Local JSON save: an envelope { version, checksum, state }. Old versions are upgraded step by step
    /// by <see cref="Migrations"/> (each takes the state JSON of version v and returns version v+1),
    /// so a new field or a moved one never loses progress.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>
        /// Version history:
        /// 1 — the prototype: currencies and lives were flat fields of the state ("coins", "stars",
        ///     "crystals", "xp", "level", "lives", "livesRegenFrom").
        /// 2 — currencies moved into "wallet", lives into "lives" { count, regenFrom, infiniteUntil }.
        /// </summary>
        public const int CurrentVersion = 2;

        const string Salt = "sunny-valley/meta";

        /// <summary>Upgrade steps: index 0 turns version 1 into 2, and so on.</summary>
        public static readonly List<Func<JObject, JObject>> Migrations = new List<Func<JObject, JObject>>
        {
            MigrateV1ToV2,
        };

        sealed class Envelope
        {
            public int Version;
            public string Checksum = "";
            public string State = "";
        }

        public static string ToJson(MetaState state)
        {
            state.Version = CurrentVersion;
            string body = MetaJson.Write(state);
            var env = new Envelope { Version = CurrentVersion, Checksum = Hash(body), State = body };
            return MetaJson.Write(env, indented: true);
        }

        /// <summary>Reads a save of any known version. Returns null if the file is broken, edited or from the future.</summary>
        public static MetaState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                var env = MetaJson.Read<Envelope>(json);
                if (env == null || env.State == null || env.Checksum != Hash(env.State))
                    return null;
                if (env.Version < 1 || env.Version > CurrentVersion)
                    return null;
                var obj = JObject.Parse(env.State);
                for (int v = env.Version; v < CurrentVersion; v++)
                {
                    obj = Migrations[v - 1](obj);
                    obj["version"] = v + 1;
                }
                var state = obj.ToObject<MetaState>(JsonSerializer.Create(MetaJson.Settings));
                if (state == null)
                    return null;
                Repair(state);
                state.Version = CurrentVersion;
                return state;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Wraps a raw state JSON of the given version into a valid envelope (tests, tools, imports).</summary>
        public static string Wrap(int version, string stateJson)
        {
            var env = new Envelope { Version = version, Checksum = Hash(stateJson), State = stateJson };
            return MetaJson.Write(env, indented: true);
        }

        static JObject MigrateV1ToV2(JObject s)
        {
            var wallet = new JObject
            {
                ["coins"] = s["coins"] ?? 0,
                ["stars"] = s["stars"] ?? 0,
                ["crystals"] = s["crystals"] ?? 0,
                ["xp"] = s["xp"] ?? 0,
                ["level"] = s["level"] ?? 1,
            };
            var lives = new JObject
            {
                ["count"] = s["lives"] ?? 0,
                ["regenFrom"] = s["livesRegenFrom"] ?? 0,
                ["infiniteUntil"] = 0,
            };
            foreach (var key in new[] { "coins", "stars", "crystals", "xp", "level", "lives", "livesRegenFrom" })
                s.Remove(key);
            s["wallet"] = wallet;
            s["lives"] = lives;
            return s;
        }

        /// <summary>Fills what a hand-made or partial save may lack, so the game never meets a null.</summary>
        static void Repair(MetaState s)
        {
            s.Wallet ??= new Wallet();
            if (s.Wallet.Level < 1) s.Wallet.Level = 1;
            s.Tools ??= new SortedDictionary<string, int>();
            s.Boosters ??= new SortedDictionary<string, int>();
            s.Barn ??= new SortedDictionary<string, int>();
            s.Lives ??= new LivesState();
            s.Match3 ??= new Match3State();
            if (s.Match3.NextLevel < 1) s.Match3.NextLevel = 1;
            s.Chapter ??= new ChapterState();
            s.Chapter.DoneTasks ??= new List<string>();
            s.Chapter.Choices ??= new SortedDictionary<string, string>();
            s.Chapter.CompletedZones ??= new List<string>();
            s.Chapter.SeenScenes ??= new List<string>();
            s.Farm ??= new FarmState();
            s.Farm.Plots ??= new List<PlotState>();
            s.Farm.Trees ??= new List<TreeState>();
            s.Farm.AnimalHouses ??= new List<string>();
            s.Farm.Animals ??= new List<AnimalState>();
            s.Farm.Workshops ??= new List<WorkshopState>();
            foreach (var w in s.Farm.Workshops)
                w.Jobs ??= new List<JobState>();
            s.Orders ??= new OrdersState();
            s.Orders.Board ??= new List<OrderCard>();
            s.Orders.Truck ??= new TruckState();
            s.Orders.Truck.Crates ??= new List<TruckCrate>();
            s.Town ??= new TownState();
            s.Town.Buildings ??= new SortedDictionary<string, int>();
            s.Daily ??= new DailyState();
            s.Daily.Tasks ??= new List<DailyTaskState>();
            s.Stats ??= new SortedDictionary<string, long>();
            s.Layout ??= new SortedDictionary<string, int[]>();
        }

        static string Hash(string body)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + body));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte x in bytes)
                    sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
