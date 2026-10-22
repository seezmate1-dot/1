using System;
using System.Collections.Generic;
using System.Linq;

namespace SunnyValley.Core.Meta
{
    /// <summary>
    /// Checks the meta data for mistakes a game would only show much later: broken references, impossible
    /// chains, cycles in task prerequisites, tasks without a replica, non-Russian text, and the GDD
    /// limits (8 zones, 8–20 tasks per zone, 1–3 stars, 3 looks, crops 2 min … 8 h).
    /// </summary>
    public static class DataValidator
    {
        public static List<string> Validate(MetaData d)
        {
            var p = new List<string>();
            ValidateBalance(d, p);
            ValidateChapter(d, p);
            return p;
        }

        static bool Russian(string s) => !string.IsNullOrWhiteSpace(s) && s.Any(c => c >= 'А' && c <= 'я' || c == 'ё' || c == 'Ё');

        static void ValidateBalance(MetaData d, List<string> p)
        {
            var b = d.Balance;
            var ids = new HashSet<string>();
            void Unique(string id, string what)
            {
                if (string.IsNullOrEmpty(id)) p.Add(what + ": пустой id");
                else if (!ids.Add(id)) p.Add(what + ": повтор id " + id);
            }
            foreach (var x in b.Items) { Unique(x.Id, "товар"); if (!Russian(x.Name)) p.Add("товар " + x.Id + ": нет русского названия"); }
            foreach (var x in b.Tools) { Unique(x.Id, "инструмент"); if (!Russian(x.Name)) p.Add("инструмент " + x.Id + ": нет названия"); }
            foreach (var x in b.Boosters) { Unique(x.Id, "буст"); if (!Russian(x.Name)) p.Add("буст " + x.Id + ": нет названия"); }
            foreach (var x in b.Trees) Unique(x.Id, "дерево");
            foreach (var x in b.AnimalHouses) Unique(x.Id, "дом животных");
            foreach (var x in b.Animals) Unique(x.Id, "животное");
            foreach (var x in b.Workshops) Unique(x.Id, "мастерская");
            foreach (var x in d.Town.Values) { Unique(x.Id, "здание"); if (!Russian(x.Name)) p.Add("здание " + x.Id + ": нет названия"); }
            foreach (var x in b.Expansions) Unique(x.Id, "расширение");

            foreach (var id in Boosters.All)
                if (!d.BoosterDefs.ContainsKey(id)) p.Add("нет буста " + id);
            foreach (var bo in b.Boosters)
                if (!Boosters.IsBooster(bo.Id) || (bo.Kind == "pre") != Boosters.IsPreLevel(bo.Id)) p.Add("буст " + bo.Id + ": неверный вид");
            foreach (var id in Tools.All)
                if (!d.ToolDefs.ContainsKey(id)) p.Add("нет инструмента " + id);

            foreach (var it in b.Items)
            {
                if (Array.IndexOf(ItemKinds.All, it.Kind) < 0) p.Add("товар " + it.Id + ": неизвестный вид " + it.Kind);
                if (it.Sell <= 0 || it.OrderCoins < it.Sell || it.Xp <= 0) p.Add("товар " + it.Id + ": цены (продажа ≤ заказ, опыт > 0)");
                if (it.Kind != ItemKinds.Match3 && !d.Producer.ContainsKey(it.Id)) p.Add("товар " + it.Id + ": его негде сделать");
            }
            foreach (var c in b.Crops)
            {
                if (!d.Items.TryGetValue(c.Item, out var it) || it.Kind != ItemKinds.Crop) { p.Add("культура " + c.Item + ": нет товара-культуры"); continue; }
                if (c.GrowSeconds < 120 || c.GrowSeconds > 8 * 3600) p.Add("культура " + c.Item + ": время роста вне 2 мин … 8 ч");
                if (c.Yield < 2) p.Add("культура " + c.Item + ": урожай меньше 2 (посев съест весь урожай)");
            }
            foreach (var t in b.Trees)
                if (!d.Items.ContainsKey(t.Item) || t.Costs.Count == 0 || t.CycleSeconds <= 0 || t.Yield <= 0) p.Add("дерево " + t.Id + ": данные");
            foreach (var a in b.Animals)
            {
                if (!d.AnimalHouses.TryGetValue(a.House, out var h)) { p.Add("животное " + a.Id + ": нет дома " + a.House); continue; }
                if (!d.Items.ContainsKey(a.Feed) || !d.Items.ContainsKey(a.Product)) p.Add("животное " + a.Id + ": корм или продукт");
                if (a.Costs.Count == 0 || a.Costs.Count > h.Capacity) p.Add("животное " + a.Id + ": число цен больше вместимости дома");
                if (a.Level < h.Level) p.Add("животное " + a.Id + ": открывается раньше своего дома");
                if (d.Items.TryGetValue(a.Feed, out var f) && f.Level > a.Level) p.Add("животное " + a.Id + ": корм открывается позже животного");
            }
            var recipeIds = new HashSet<string>();
            foreach (var w in b.Workshops)
            {
                if (!Russian(w.Name)) p.Add("мастерская " + w.Id + ": нет названия");
                if (w.Slots < 1) p.Add("мастерская " + w.Id + ": нет слотов");
                foreach (var r in w.Recipes)
                {
                    if (string.IsNullOrEmpty(r.Id) || !recipeIds.Add(r.Id)) p.Add("рецепт: пустой или повторный id " + r.Id);
                    if (r.Level < w.Level) p.Add("рецепт " + r.Id + ": открывается раньше мастерской");
                    if (r.Seconds <= 0 || r.Count <= 0 || r.Inputs.Count == 0) p.Add("рецепт " + r.Id + ": данные");
                    if (!d.Items.ContainsKey(r.Output) && !Boosters.IsBooster(r.Output)) p.Add("рецепт " + r.Id + ": неизвестный выход " + r.Output);
                    if (d.Items.TryGetValue(r.Output, out var outIt) && outIt.Level > r.Level) p.Add("рецепт " + r.Id + ": товар открыт позже рецепта");
                    foreach (var kv in r.Inputs)
                    {
                        if (!d.Items.TryGetValue(kv.Key, out var inIt)) p.Add("рецепт " + r.Id + ": неизвестный вход " + kv.Key);
                        else if (inIt.Level > r.Level) p.Add("рецепт " + r.Id + ": вход " + kv.Key + " открывается позже рецепта");
                        if (kv.Value <= 0) p.Add("рецепт " + r.Id + ": количество входа");
                    }
                }
            }
            // Every item is makeable at its level from what that level unlocks (no cycles, no gaps).
            foreach (var it in b.Items)
                if (it.Kind != ItemKinds.Match3 && Depth(d, it.Id, 0) < 0) p.Add("товар " + it.Id + ": цепочка производства зациклена или оборвана");

            if (b.Barn.Capacity <= 0) p.Add("амбар: вместимость");
            foreach (var u in b.Barn.Upgrades)
            {
                if (u.Capacity <= 0) p.Add("амбар: улучшение без вместимости");
                foreach (var t in u.Tools.Keys) if (Array.IndexOf(Tools.Barn, t) < 0) p.Add("амбар: улучшение просит " + t);
            }
            foreach (var e in b.Expansions)
            {
                if (!Russian(e.Name)) p.Add("расширение " + e.Id + ": нет названия");
                foreach (var t in e.Tools.Keys) if (Array.IndexOf(Tools.Land, t) < 0) p.Add("расширение " + e.Id + ": просит " + t);
            }
            if (b.Levels.Count < 2 || b.Levels[0].Level != 1 || b.Levels[0].Xp != 0) p.Add("уровни: первый — 1 с опытом 0");
            for (int i = 1; i < b.Levels.Count; i++)
                if (b.Levels[i].Level != i + 1 || b.Levels[i].Xp <= b.Levels[i - 1].Xp) p.Add("уровни: " + (i + 1) + " — порядок или опыт");
            if (b.Orders.Slots != 9) p.Add("заказы: на доске должно быть 9 карточек");
            if (b.Orders.MaxChainMinutes.Count == 0 || b.Orders.MaxKinds.Count == 0 || b.Orders.MaxUnits.Count == 0 || b.Orders.MaxWorkMinutes.Count == 0)
                p.Add("заказы: таблицы по уровням пустые");
            if (b.Lives.Max != 5 || b.Lives.RegenSeconds != 1800) p.Add("жизни: 5 штук, одна за 30 минут (GDD 4.6)");
            if (b.Match3.ExtraMovesPrices.Count == 0 || b.Match3.ExtraMoves != 5) p.Add("+5 ходов: цены");
            foreach (var bo in b.Match3.BoosterRotation) if (!Boosters.IsBooster(bo)) p.Add("подарки бустов: " + bo);
            if (b.Daily.Login.Count != 7) p.Add("подарок входа: 7 дней");
            foreach (var t in b.Daily.Tasks)
            {
                if (Array.IndexOf(DailyKinds.All, t.Kind) < 0) p.Add("задание дня " + t.Id + ": вид " + t.Kind);
                if (!Russian(t.Text) || t.Target <= 0) p.Add("задание дня " + t.Id + ": текст или цель");
            }
            foreach (var w in b.FarmStart.Workshops) if (!d.Workshops.ContainsKey(w)) p.Add("старт фермы: мастерская " + w);
            foreach (var h in b.FarmStart.AnimalHouses) if (!d.AnimalHouses.ContainsKey(h)) p.Add("старт фермы: дом " + h);
            foreach (var a in b.FarmStart.Animals.Keys) if (!d.Animals.ContainsKey(a) || !b.FarmStart.AnimalHouses.Contains(d.Animals[a].House)) p.Add("старт фермы: животное " + a);
            foreach (var t in b.FarmStart.Trees.Keys) if (!d.Trees.ContainsKey(t)) p.Add("старт фермы: дерево " + t);
            foreach (var t in b.FarmStart.Town.Keys) if (!d.Town.ContainsKey(t)) p.Add("старт фермы: здание " + t);

            var rewards = new List<(string, Reward)> { ("веха", b.Match3.MilestoneReward), ("сундук дня", b.Daily.Chest) };
            foreach (var l in b.Levels) rewards.Add(("уровень " + l.Level, l.Reward));
            foreach (var l in b.Daily.Login) rewards.Add(("подарок входа", l));
            foreach (var t in b.Daily.Tasks) rewards.Add(("задание дня " + t.Id, t.Reward));
            foreach (var (what, r) in rewards) CheckReward(d, what, r, p);
        }

        /// <summary>Depth of the production chain (≥0), or -1 if broken or cyclic.</summary>
        static int Depth(MetaData d, string item, int depth)
        {
            if (depth > 10) return -1;
            if (!d.Producer.TryGetValue(item, out var prod)) return -1;
            switch (prod)
            {
                case CropDef _: return 0;
                case TreeDef _: return 0;
                case AnimalDef a: { int f = Depth(d, a.Feed, depth + 1); return f < 0 ? -1 : f + 1; }
                case RecipeDef r:
                {
                    int m = 0;
                    foreach (var kv in r.Inputs)
                    {
                        int x = Depth(d, kv.Key, depth + 1);
                        if (x < 0) return -1;
                        m = Math.Max(m, x);
                    }
                    return m + 1;
                }
            }
            return -1;
        }

        public static void CheckReward(MetaData d, string what, Reward r, List<string> p)
        {
            if (r == null) return;
            foreach (var k in r.Items.Keys) if (!d.Items.ContainsKey(k)) p.Add(what + ": неизвестный товар " + k);
            foreach (var k in r.Tools.Keys) if (!Tools.IsTool(k)) p.Add(what + ": неизвестный инструмент " + k);
            foreach (var k in r.Boosters.Keys) if (!Boosters.IsBooster(k)) p.Add(what + ": неизвестный буст " + k);
            if (r.Coins < 0 || r.Crystals < 0 || r.Stars < 0 || r.Xp < 0) p.Add(what + ": отрицательная награда");
        }

        static void ValidateChapter(MetaData d, List<string> p)
        {
            var c = d.Chapter;
            if (!Russian(c.Title)) p.Add("глава: нет названия");
            if (c.Zones.Count != 8) p.Add("глава: зон должно быть 8 (GDD 5), а их " + c.Zones.Count);
            foreach (var ch in c.Characters) if (!Russian(ch.Name)) p.Add("персонаж " + ch.Id + ": нет имени");
            foreach (var s in c.Scenes)
            {
                if (!Russian(s.Title)) p.Add("сценка " + s.Id + ": нет названия");
                if (s.Lines.Count < 2) p.Add("сценка " + s.Id + ": меньше двух реплик");
                CheckLines(d, "сценка " + s.Id, s.Lines, p);
            }
            void Scene(string id, string what)
            {
                if (id != null && !d.Scenes.ContainsKey(id)) p.Add(what + ": нет сценки " + id);
            }
            Scene(c.IntroScene, "глава");
            Scene(c.FinaleScene, "глава");
            if (c.FinaleScene == null) p.Add("глава: нет финальной сценки");
            CheckReward(d, "финал главы", c.FinaleReward, p);

            var zoneIds = new HashSet<string>();
            foreach (var z in c.Zones)
            {
                if (!zoneIds.Add(z.Id)) p.Add("зона " + z.Id + ": повтор");
                if (!Russian(z.Name)) p.Add("зона " + z.Id + ": нет названия");
                foreach (var r in z.Requires) if (!d.Zones.ContainsKey(r) || r == z.Id) p.Add("зона " + z.Id + ": неверное условие " + r);
                Scene(z.IntroScene, "зона " + z.Id);
                Scene(z.OutroScene, "зона " + z.Id);
                CheckReward(d, "зона " + z.Id, z.Reward, p);
                if (z.Reward.IsEmpty) p.Add("зона " + z.Id + ": нет награды за завершение");
                int n = d.ZoneTasks[z.Id].Count;
                if (n < 8 || n > 20) p.Add("зона " + z.Id + ": заданий " + n + " (нужно 8–20)");
            }
            var taskIds = new HashSet<string>();
            foreach (var t in c.Tasks)
            {
                if (!taskIds.Add(t.Id)) p.Add("задание " + t.Id + ": повтор");
                if (!d.Zones.ContainsKey(t.Zone ?? "")) p.Add("задание " + t.Id + ": нет зоны " + t.Zone);
                if (t.Stars < 1 || t.Stars > 3) p.Add("задание " + t.Id + ": звёзд " + t.Stars + " (нужно 1–3)");
                if (!Russian(t.Title)) p.Add("задание " + t.Id + ": нет названия");
                if (t.Lines.Count == 0) p.Add("задание " + t.Id + ": нет реплики");
                CheckLines(d, "задание " + t.Id, t.Lines, p);
                Scene(t.Scene, "задание " + t.Id);
                CheckReward(d, "задание " + t.Id, t.Reward, p);
                if (t.Choices.Count != 0 && t.Choices.Count != 3) p.Add("задание " + t.Id + ": вариантов вида должно быть 3");
                if (t.Choices.Select(x => x.Id).Distinct().Count() != t.Choices.Count) p.Add("задание " + t.Id + ": повтор вариантов");
                foreach (var ch in t.Choices) if (!Russian(ch.Name)) p.Add("задание " + t.Id + ": вариант без названия");
                foreach (var r in t.Requires)
                {
                    if (!d.Tasks.TryGetValue(r, out var rt)) { p.Add("задание " + t.Id + ": неизвестное условие " + r); continue; }
                    // A prerequisite must be in the same zone or in a zone this one requires (transitively).
                    if (rt.Zone != t.Zone && !ZoneBefore(d, rt.Zone, t.Zone, 0)) p.Add("задание " + t.Id + ": условие " + r + " из недоступной зоны");
                }
            }
            // Everything can be done in some order (no cycles): play the chapter through with infinite stars.
            var done = new HashSet<string>();
            var zonesDone = new HashSet<string>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var t in c.Tasks)
                {
                    if (done.Contains(t.Id)) continue;
                    if (!d.Zones.TryGetValue(t.Zone ?? "", out var z) || !z.Requires.All(zonesDone.Contains)) continue;
                    if (!t.Requires.All(done.Contains)) continue;
                    done.Add(t.Id);
                    progress = true;
                    if (d.ZoneTasks[t.Zone].All(x => done.Contains(x.Id))) zonesDone.Add(t.Zone);
                }
            }
            if (done.Count != c.Tasks.Count)
                p.Add("глава: задания, которые никогда не откроются: " + string.Join(", ", c.Tasks.Where(t => !done.Contains(t.Id)).Select(t => t.Id)));
        }

        static bool ZoneBefore(MetaData d, string earlier, string later, int depth)
        {
            if (depth > 16 || !d.Zones.TryGetValue(later, out var z)) return false;
            foreach (var r in z.Requires)
                if (r == earlier || ZoneBefore(d, earlier, r, depth + 1)) return true;
            return false;
        }

        static void CheckLines(MetaData d, string what, List<DialogueLine> lines, List<string> p)
        {
            foreach (var l in lines)
            {
                if (l.Who == null || !d.Characters.ContainsKey(l.Who)) p.Add(what + ": неизвестный персонаж " + l.Who);
                if (!Russian(l.Text)) p.Add(what + ": реплика не по-русски");
            }
        }
    }
}
