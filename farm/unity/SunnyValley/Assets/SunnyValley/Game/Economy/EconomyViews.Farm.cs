using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Economy
{
    /// <summary>Fields and the orchard, the crop picker, the animals (GDD 6).</summary>
    public static partial class EconomyViews
    {
        // ------------------------------------------------------------------ fields and the orchard

        /// <summary>
        /// Every plot (empty: «Посадить» opens the crop picker; growing: the timer and a speed-up for crystals; ripe:
        /// «Собрать»), then the trees the same way and the trees to buy; «Собрать всё» and the next plot for sale.
        /// </summary>
        static EconomyView FieldsView(MetaGame g, long now)
        {
            var v = new EconomyView(Fields, null, new EconomyInfo { Id = Fields, Title = "Поля и сад" });
            var data = g.Data;
            v.Section("Грядки: " + g.Plots.Count);
            for (int i = 0; i < g.Plots.Count; i++)
            {
                var p = g.Plots[i];
                int plot = i;
                if (p.Crop == null)
                {
                    v.Row(new EconomyRow { Icon = Icons.Soil, Title = "Грядка " + (i + 1), Sub = "Пусто", Button = new RowButton("Посадить") },
                        EconomyAction.Open(Crops, i.ToString()));
                    continue;
                }
                string name = data.NameOf(p.Crop);
                if (now < p.ReadyAt)
                {
                    long left = p.ReadyAt - now;
                    v.Row(new EconomyRow
                    {
                        Icon = Icons.Item(p.Crop), Title = name, Progress = Progress(p.PlantedAt, p.ReadyAt, now), ProgressText = Format.Timer(left),
                        Button = CrystalsButton(g, g.SpeedUpPrice(left)),
                    }, EconomyAction.Ask(SpeedUpQuestion(g, Icons.Item(p.Crop), name, left), t => g.SpeedUpPlot(t, plot)));
                }
                else
                {
                    v.Row(new EconomyRow
                    {
                        Icon = Icons.Item(p.Crop), Title = name, Sub = "Созрело · урожай ×" + data.Crops[p.Crop].Yield,
                        Button = new RowButton("Собрать", null, Tone.Yellow),
                    }, EconomyAction.Do(t => g.Harvest(t, plot)));
                }
            }

            v.Section("Сад");
            foreach (var tree in g.State.Farm.Trees)
            {
                var def = data.Trees[tree.Type];
                int uid = tree.Uid;
                if (now < tree.ReadyAt)
                {
                    long left = tree.ReadyAt - now;
                    v.Row(new EconomyRow
                    {
                        Icon = Icons.Item(def.Item), Title = def.Name, Progress = Progress(tree.ReadyAt - def.CycleSeconds, tree.ReadyAt, now),
                        ProgressText = Format.Timer(left), Button = CrystalsButton(g, g.SpeedUpPrice(left)),
                    }, EconomyAction.Ask(SpeedUpQuestion(g, Icons.Item(def.Item), def.Name, left), t => g.SpeedUpTree(t, uid)));
                }
                else
                {
                    v.Row(new EconomyRow
                    {
                        Icon = Icons.Item(def.Item), Title = def.Name, Sub = "Поспело · " + data.NameOf(def.Item).ToLowerInvariant() + " ×" + def.Yield,
                        Button = new RowButton("Собрать", null, Tone.Yellow),
                    }, EconomyAction.Do(t => g.HarvestTree(t, uid)));
                }
            }
            foreach (var def in g.Data.Balance.Trees)
            {
                int price = g.NextTreePrice(def.Id);
                if (price < 0)
                    continue;
                string type = def.Id;
                if (def.Level > g.Level)
                {
                    v.Row(new EconomyRow { Icon = Icons.Item(def.Item), Title = "Купить: " + def.Name, Sub = NeedLevel(def.Level), Button = LockedButton(def.Level), Dim = true },
                        LockedSay(def.Level));
                    continue;
                }
                v.Row(new EconomyRow
                {
                    Icon = Icons.Item(def.Item), Title = "Купить: " + def.Name,
                    Sub = g.TreeCount(def.Id) + " из " + def.Costs.Count + " · ×" + def.Yield + " за " + Texts.Duration(def.CycleSeconds),
                    Button = CoinsButton(g, price),
                }, EconomyAction.Do(t => g.BuyTree(t, type), false, def.Name + " посажена в саду"));
            }

            int ready = ReadyFields(g, now);
            v.Footer(new RowButton("Собрать всё", Icons.Fields, ready > 0 ? Tone.Yellow : Tone.Grey), EconomyAction.Do(t => HarvestAll(g, t)));
            var offer = g.NextPlotOffer;
            if (offer != null)
            {
                if (offer.Level > g.Level)
                    v.Footer(new RowButton("Грядка: ур. " + offer.Level, null, Tone.Grey), EconomyAction.Say("Новая грядка — на " + offer.Level + " уровне фермы"));
                else
                    v.Footer(new RowButton("Грядка за " + Format.Amount(offer.Coins), null, g.Coins >= offer.Coins ? Tone.Green : Tone.Grey),
                        EconomyAction.Do(t => g.BuyPlot(t), false, "Новая грядка готова"));
            }
            return v;
        }

        /// <summary>Harvests every ripe plot and tree while the barn has room.</summary>
        static ActionResult HarvestAll(MetaGame g, long now)
        {
            var steps = new List<Func<ActionResult>>();
            for (int i = 0; i < g.Plots.Count; i++)
            {
                int plot = i;
                if (g.Plots[i].Crop != null && now >= g.Plots[i].ReadyAt)
                    steps.Add(() => g.Harvest(now, plot));
            }
            foreach (var tree in g.State.Farm.Trees.ToArray())
            {
                int uid = tree.Uid;
                if (now >= tree.ReadyAt)
                    steps.Add(() => g.HarvestTree(now, uid));
            }
            return Each(steps, MetaError.NothingToCollect);
        }

        // ------------------------------------------------------------------ the crop picker

        /// <summary>
        /// What to plant on a plot: every crop with its time and yield; seeds for coins (the main button) or one unit
        /// from the barn (the round button with the barn); crops of higher levels greyed with their level.
        /// </summary>
        static EconomyView CropsView(MetaGame g, long now, int plot)
        {
            var info = new EconomyInfo
            {
                Id = Crops, Title = "Что посадить?", HeaderText = "Грядка " + (plot + 1) + ": семена за монеты или своё из амбара",
                Empty = "Грядка уже занята",
            };
            var v = new EconomyView(Crops, plot.ToString(), info);
            if (plot < 0 || plot >= g.Plots.Count || g.Plots[plot].Crop != null)
                return v;
            foreach (var c in g.Data.Balance.Crops)
            {
                var item = g.Data.Items[c.Item];
                string crop = c.Item;
                if (item.Level > g.Level)
                {
                    v.Row(new EconomyRow { Icon = Icons.Item(crop), Title = item.Name, Sub = NeedLevel(item.Level), Button = LockedButton(item.Level), Dim = true },
                        LockedSay(item.Level));
                    continue;
                }
                int own = g.BarnCount(crop);
                var row = new EconomyRow
                {
                    Icon = Icons.Item(crop), Title = item.Name, Sub = Texts.Duration(c.GrowSeconds) + (own > 0 ? " · своих " + Math.Min(999, own) : " · урожай ×" + c.Yield),
                    Button = CoinsButton(g, c.SeedCoins), Alt = own > 0 ? new RowButton(null, Icons.Barn, Tone.Yellow) : null,
                };
                v.Row(row, EconomyAction.Do(t => g.Plant(t, plot, crop), true), EconomyAction.Do(t => g.Plant(t, plot, crop, true), true));
            }
            return v;
        }

        // ------------------------------------------------------------------ animals

        /// <summary>
        /// The animal houses in order: a built one lists its animals (hungry: «Покормить» with the feed from the barn,
        /// or the way to the mill when there is none; fed: the timer and a speed-up; ready: «Собрать») and the next
        /// animal to buy; a house not built yet offers itself. «Собрать всё» and «Покормить всех» at the bottom.
        /// </summary>
        static EconomyView AnimalsView(MetaGame g, long now)
        {
            var v = new EconomyView(Animals, null, new EconomyInfo { Id = Animals, Title = "Животные" });
            var data = g.Data;
            foreach (var house in data.Balance.AnimalHouses)
            {
                if (!g.HasAnimalHouse(house.Id))
                {
                    v.Section(house.Name);
                    string id = house.Id;
                    if (house.Level > g.Level)
                    {
                        v.Row(new EconomyRow { Icon = Icons.Barn, Title = house.Name, Sub = NeedLevel(house.Level), Button = LockedButton(house.Level), Dim = true },
                            LockedSay(house.Level));
                    }
                    else
                    {
                        string sub = house.Population > 0 ? "Нужно жителей: " + house.Population + " · есть " + g.Population : "Место для " + house.Capacity + " животных";
                        v.Row(new EconomyRow { Icon = Icons.Barn, Title = house.Name, Sub = sub, Button = CoinsButton(g, house.Coins) },
                            EconomyAction.Do(t => g.BuildAnimalHouse(t, id), false, "Построено: " + house.Name));
                    }
                    continue;
                }
                var kinds = data.Balance.Animals.FindAll(a => a.House == house.Id);
                int count = 0;
                foreach (var k in kinds)
                    count += g.AnimalCount(k.Id);
                v.Section(house.Name + ": " + count + " из " + house.Capacity);
                foreach (var kind in kinds)
                {
                    foreach (var a in g.State.Farm.Animals)
                    {
                        if (a.Type == kind.Id)
                            AnimalRow(v, g, now, a, kind);
                    }
                    int price = g.NextAnimalPrice(kind.Id);
                    if (price < 0)
                        continue;
                    string type = kind.Id;
                    if (kind.Level > g.Level)
                        v.Row(new EconomyRow { Icon = AnimalIcon(kind.Id), Title = "Купить: " + kind.Name, Sub = NeedLevel(kind.Level), Button = LockedButton(kind.Level), Dim = true },
                            LockedSay(kind.Level));
                    else
                        v.Row(new EconomyRow
                        {
                            Icon = AnimalIcon(kind.Id), Title = "Купить: " + kind.Name, Sub = g.AnimalCount(kind.Id) + " из " + Math.Min(kind.Costs.Count, house.Capacity),
                            Button = CoinsButton(g, price),
                        }, EconomyAction.Do(t => g.BuyAnimal(t, type), false, kind.Name + " уже на ферме"));
                }
            }
            int ready = ReadyAnimals(g, now), hungry = HungryWithFeed(g);
            v.Footer(new RowButton("Собрать всё", Icons.Animals, ready > 0 ? Tone.Yellow : Tone.Grey), EconomyAction.Do(t => CollectAnimals(g, t)));
            v.Footer(new RowButton("Покормить всех", null, hungry > 0 ? Tone.Green : Tone.Grey), EconomyAction.Do(t => FeedAll(g, t)));
            return v;
        }

        static void AnimalRow(EconomyView v, MetaGame g, long now, AnimalState a, AnimalDef def)
        {
            int uid = a.Uid;
            string icon = AnimalIcon(a.Type);
            if (!a.Fed)
            {
                int feed = g.BarnCount(def.Feed);
                string feedName = g.Data.NameOf(def.Feed);
                if (feed > 0)
                {
                    v.Row(new EconomyRow { Icon = icon, Title = def.Name, Sub = feedName + ": " + Math.Min(999, feed), Button = new RowButton("Кормить") },
                        EconomyAction.Do(t => g.Feed(t, uid)));
                    return;
                }
                // no feed: show where it comes from (the mill grinds it; bees want sunflowers from the fields)
                var (button, way) = FeedSource(g, def.Feed);
                v.Row(new EconomyRow { Icon = icon, Title = def.Name, Sub = "Нужен корм: " + feedName.ToLowerInvariant(), Button = button }, way);
                return;
            }
            if (now < a.ReadyAt)
            {
                long left = a.ReadyAt - now;
                v.Row(new EconomyRow
                {
                    Icon = icon, Title = def.Name, Progress = Progress(a.ReadyAt - def.Seconds, a.ReadyAt, now), ProgressText = Format.Timer(left),
                    Button = CrystalsButton(g, g.SpeedUpPrice(left)),
                }, EconomyAction.Ask(SpeedUpQuestion(g, icon, def.Name, left), t => g.SpeedUpAnimal(t, uid)));
                return;
            }
            v.Row(new EconomyRow
            {
                Icon = icon, Title = def.Name, Sub = "Готово: " + g.Data.NameOf(def.Product).ToLowerInvariant(), Button = new RowButton("Собрать", null, Tone.Yellow),
            }, EconomyAction.Do(t => g.CollectAnimal(t, uid)));
        }

        /// <summary>Where a feed comes from: the workshop that makes it (the mill), or the fields for a crop.</summary>
        static (RowButton, EconomyAction) FeedSource(MetaGame g, string feed)
        {
            if (g.Data.Producer.TryGetValue(feed, out var p) && p is RecipeDef r)
            {
                var w = g.Data.RecipeWorkshop[r.Id];
                if (g.HasWorkshop(w.Id))
                    return (new RowButton(w.Name, null, Tone.Blue), EconomyAction.Open(Workshop, w.Id));
                return (new RowButton(w.Name, null, Tone.Grey), EconomyAction.Say(g.Data.NameOf(feed) + " делает " + w.Name.ToLowerInvariant() + " — её нужно построить"));
            }
            return (new RowButton("Поля", null, Tone.Blue), EconomyAction.Open(Fields));
        }

        static ActionResult CollectAnimals(MetaGame g, long now)
        {
            var steps = new List<Func<ActionResult>>();
            foreach (var a in g.State.Farm.Animals.ToArray())
            {
                int uid = a.Uid;
                if (a.Fed && now >= a.ReadyAt)
                    steps.Add(() => g.CollectAnimal(now, uid));
            }
            return Each(steps, MetaError.NothingToCollect);
        }

        static ActionResult FeedAll(MetaGame g, long now)
        {
            var steps = new List<Func<ActionResult>>();
            foreach (var a in g.State.Farm.Animals.ToArray())
            {
                int uid = a.Uid;
                if (!a.Fed)
                    steps.Add(() => g.Feed(now, uid));
            }
            // everyone already fed: nothing to do
            return Each(steps, MetaError.AlreadyDone);
        }
    }
}
