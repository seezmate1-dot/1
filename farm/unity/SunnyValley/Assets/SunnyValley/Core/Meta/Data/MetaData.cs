using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace SunnyValley.Core.Meta
{
    /// <summary>Shared JSON settings: camelCase fields, dictionary keys kept as they are.</summary>
    public static class MetaJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = false },
            },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Formatting = Formatting.None,
        };

        public static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
        public static string Write(object o, bool indented = false) =>
            JsonConvert.SerializeObject(o, indented ? Formatting.Indented : Formatting.None, Settings);
    }

    /// <summary>
    /// All meta data (economy + chapter 1), parsed and indexed. Immutable after loading.
    /// The Unity layer reads Resources/Meta/balance.json and chapter1.json as text and calls <see cref="Parse"/>.
    /// </summary>
    public sealed class MetaData
    {
        public const string BalanceResource = "Meta/balance";
        public const string ChapterResource = "Meta/chapter1";

        public Balance Balance { get; private set; }
        public ChapterDef Chapter { get; private set; }

        public readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public readonly Dictionary<string, CropDef> Crops = new Dictionary<string, CropDef>();
        public readonly Dictionary<string, TreeDef> Trees = new Dictionary<string, TreeDef>();
        public readonly Dictionary<string, AnimalDef> Animals = new Dictionary<string, AnimalDef>();
        public readonly Dictionary<string, AnimalHouseDef> AnimalHouses = new Dictionary<string, AnimalHouseDef>();
        public readonly Dictionary<string, WorkshopDef> Workshops = new Dictionary<string, WorkshopDef>();
        public readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>();
        /// <summary>Recipe id → its workshop.</summary>
        public readonly Dictionary<string, WorkshopDef> RecipeWorkshop = new Dictionary<string, WorkshopDef>();
        public readonly Dictionary<string, BoosterDef> BoosterDefs = new Dictionary<string, BoosterDef>();
        public readonly Dictionary<string, ToolDef> ToolDefs = new Dictionary<string, ToolDef>();
        public readonly Dictionary<string, TownBuildingDef> Town = new Dictionary<string, TownBuildingDef>();
        public readonly Dictionary<string, ExpansionDef> Expansions = new Dictionary<string, ExpansionDef>();
        public readonly Dictionary<string, DailyTaskDef> DailyTasks = new Dictionary<string, DailyTaskDef>();

        public readonly Dictionary<string, ZoneDef> Zones = new Dictionary<string, ZoneDef>();
        public readonly Dictionary<string, TaskDef> Tasks = new Dictionary<string, TaskDef>();
        public readonly Dictionary<string, SceneDef> Scenes = new Dictionary<string, SceneDef>();
        public readonly Dictionary<string, CharacterDef> Characters = new Dictionary<string, CharacterDef>();
        /// <summary>Zone id → its tasks in data order.</summary>
        public readonly Dictionary<string, List<TaskDef>> ZoneTasks = new Dictionary<string, List<TaskDef>>();

        /// <summary>Item id → how it is made: a CropDef, TreeDef, AnimalDef or RecipeDef (null for match-3 goods).</summary>
        public readonly Dictionary<string, object> Producer = new Dictionary<string, object>();

        public static MetaData Parse(string balanceJson, string chapterJson)
        {
            var b = MetaJson.Read<Balance>(balanceJson) ?? throw new FormatException("balance.json is empty");
            var c = MetaJson.Read<ChapterDef>(chapterJson) ?? throw new FormatException("chapter1.json is empty");
            return new MetaData(b, c);
        }

        public MetaData(Balance balance, ChapterDef chapter)
        {
            Balance = balance;
            Chapter = chapter;
            Index();
        }

        void Index()
        {
            var b = Balance;
            foreach (var x in b.Items) Items[x.Id] = x;
            foreach (var x in b.Crops) { Crops[x.Item] = x; Producer[x.Item] = x; }
            foreach (var x in b.Trees) { Trees[x.Id] = x; Producer[x.Item] = x; }
            foreach (var x in b.AnimalHouses) AnimalHouses[x.Id] = x;
            foreach (var x in b.Animals) { Animals[x.Id] = x; Producer[x.Product] = x; }
            foreach (var w in b.Workshops)
            {
                Workshops[w.Id] = w;
                foreach (var r in w.Recipes)
                {
                    Recipes[r.Id] = r;
                    RecipeWorkshop[r.Id] = w;
                    if (!Producer.ContainsKey(r.Output))
                        Producer[r.Output] = r;
                }
            }
            foreach (var x in b.Boosters) BoosterDefs[x.Id] = x;
            foreach (var x in b.Tools) ToolDefs[x.Id] = x;
            foreach (var list in new[] { b.Town.Houses, b.Town.Services, b.Town.Decorations })
                foreach (var x in list)
                    Town[x.Id] = x;
            foreach (var x in b.Expansions) Expansions[x.Id] = x;
            foreach (var x in b.Daily.Tasks) DailyTasks[x.Id] = x;

            var c = Chapter;
            foreach (var x in c.Characters) Characters[x.Id] = x;
            foreach (var x in c.Scenes) Scenes[x.Id] = x;
            foreach (var z in c.Zones)
            {
                Zones[z.Id] = z;
                ZoneTasks[z.Id] = new List<TaskDef>();
            }
            foreach (var t in c.Tasks)
            {
                Tasks[t.Id] = t;
                if (t.Zone != null && ZoneTasks.TryGetValue(t.Zone, out var list))
                    list.Add(t);
            }
        }

        /// <summary>Russian name of any item, tool, booster, building, animal, tree, recipe output or character.</summary>
        public string NameOf(string id)
        {
            if (id == null) return "";
            if (Items.TryGetValue(id, out var i)) return i.Name;
            if (ToolDefs.TryGetValue(id, out var t)) return t.Name;
            if (BoosterDefs.TryGetValue(id, out var bo)) return bo.Name;
            if (Workshops.TryGetValue(id, out var w)) return w.Name;
            if (AnimalHouses.TryGetValue(id, out var h)) return h.Name;
            if (Animals.TryGetValue(id, out var a)) return a.Name;
            if (Trees.TryGetValue(id, out var tr)) return tr.Name;
            if (Town.TryGetValue(id, out var tb)) return tb.Name;
            if (Expansions.TryGetValue(id, out var e)) return e.Name;
            if (Characters.TryGetValue(id, out var ch)) return ch.Name;
            if (Zones.TryGetValue(id, out var z)) return z.Name;
            if (Recipes.TryGetValue(id, out var r)) return NameOf(r.Output);
            return id;
        }

        public LevelDef LevelDef(int level)
        {
            var list = Balance.Levels;
            if (level < 1) return list[0];
            return level <= list.Count ? list[level - 1] : null;
        }

        public int MaxLevel => Balance.Levels.Count;

        /// <summary>Value of a per-level list (index = level-1; the last value repeats).</summary>
        public static int ByLevel(List<int> list, int level)
        {
            if (list == null || list.Count == 0) return 0;
            int i = Math.Max(0, Math.Min(list.Count - 1, level - 1));
            return list[i];
        }

        /// <summary>Things the given farm level unlocks (for the level-up screen).</summary>
        public List<string> UnlocksAt(int level)
        {
            var r = new List<string>();
            foreach (var c in Balance.Crops)
                if (Items[c.Item].Level == level) r.Add(c.Item);
            foreach (var t in Balance.Trees) if (t.Level == level) r.Add(t.Id);
            foreach (var h in Balance.AnimalHouses) if (h.Level == level) r.Add(h.Id);
            foreach (var a in Balance.Animals) if (a.Level == level) r.Add(a.Id);
            foreach (var w in Balance.Workshops)
            {
                if (w.Level == level) r.Add(w.Id);
                foreach (var rc in w.Recipes)
                    if (rc.Level == level && rc.Level != w.Level) r.Add(rc.Id);
            }
            foreach (var tb in Town.Values) if (tb.Level == level) r.Add(tb.Id);
            foreach (var e in Balance.Expansions) if (e.Level == level) r.Add(e.Id);
            if (Balance.Truck.Level == level) r.Add("truck");
            return r;
        }
    }
}
