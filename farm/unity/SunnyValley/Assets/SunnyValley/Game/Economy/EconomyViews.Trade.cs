using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Economy
{
    /// <summary>The order board, the truck and the town (GDD 6).</summary>
    public static partial class EconomyViews
    {
        // ------------------------------------------------------------------ the order board

        /// <summary>
        /// Nine cards like Township: who asks, the goods as «have/need», what it pays; a card waiting for a new order
        /// shows the time left. A tap picks a card; «Отдать» and «Отказаться» at the bottom act on the picked one
        /// (the first that can be handed in, if none is picked). The truck sits in the header once it is open.
        /// </summary>
        static EconomyView OrdersView(MetaGame g, long now, int picked)
        {
            var info = new EconomyInfo { Id = Orders, Title = "Заказы соседей", Cards = true, Empty = "Заказы появятся, когда будет что заказать" };
            var board = g.Board(now);
            if (picked < 0 || picked >= board.Count || board[picked].Order == null)
                picked = DefaultCard(g, now, board);
            var v = new EconomyView(Orders, picked.ToString(), info);
            var data = g.Data;

            if (g.TruckUnlocked)
            {
                var truck = g.Truck(now);
                int loaded = truck.Crates.FindAll(c => c.Loaded).Count;
                info.HeaderText = truck.AwayUntil > now ? "Грузовик вернётся через " + Format.Timer(truck.AwayUntil - now)
                    : truck.Crates.Count == 0 ? "Грузовику пока нечего везти" : "Грузовик: загружено " + loaded + " из " + truck.Crates.Count;
                v.HeaderButton(new RowButton("Грузовик", null, Tone.Blue), EconomyAction.Open(Truck));
            }
            else
            {
                info.HeaderText = "Грузовик в город — с " + data.Balance.Truck.Level + " уровня фермы";
            }

            for (int i = 0; i < board.Count; i++)
            {
                var card = board[i];
                var o = card.Order;
                if (o == null)
                {
                    long left = Math.Max(0, card.AvailableAt - now);
                    v.Row(new EconomyRow { Caption = "Ждём заказ", Icon = Icons.Clock, Title = "Новый заказ", Sub = Format.Timer(left), Dim = true },
                        EconomyAction.Say(left > 0 ? "Новый заказ появится через " + Texts.Duration(left) : "Скоро будет новый заказ"));
                    continue;
                }
                var row = new EconomyRow { Caption = data.NameOf(o.From), Picked = i == picked };
                foreach (var kv in o.Items)
                    row.Chips.Add(Need(g, kv.Key, kv.Value));
                row.Rewards.Add(new Chip(Icons.Coin, "+" + Format.Amount(o.Coins)));
                row.Rewards.Add(new Chip(Icons.Xp, "+" + Format.Amount(o.Xp)));
                v.Row(row, EconomyAction.Pick(i.ToString()));
            }

            var chosen = picked >= 0 ? board[picked].Order : null;
            if (chosen == null)
            {
                v.Footer(new RowButton("Отдать", Icons.Check, Tone.Grey), EconomyAction.Say("Выберите заказ"));
                v.Footer(new RowButton("Отказаться", Icons.Close, Tone.Grey), EconomyAction.Say("Выберите заказ"));
                return v;
            }
            int card0 = picked;
            string extra = chosen.Tools.Count > 0 ? " и " + Texts.Plural(Count(chosen.Tools), "инструмент", "инструмента", "инструментов") : "";
            v.Footer(new RowButton("Отдать", Icons.Check, g.CanFulfil(now, picked) ? Tone.Green : Tone.Grey), EconomyAction.Do(t => g.Fulfil(t, card0)));
            v.Footer(new RowButton("Отказаться", Icons.Close, Tone.Red), EconomyAction.Ask(new ConfirmInfo
            {
                Title = "Отказаться?", Icon = Icons.Orders,
                Text = data.NameOf(chosen.From) + " ждёт " + Texts.Plural(chosen.Coins, "монету", "монеты", "монет") + extra +
                       ". Новый заказ — через " + Texts.Duration(data.Balance.Orders.DiscardCooldownSeconds) + ".",
                Button = new RowButton("Отказаться", Icons.Close, Tone.Red),
            }, t => g.Discard(t, card0)));
            return v;
        }

        static int Count(IDictionary<string, int> d)
        {
            int n = 0;
            foreach (var kv in d)
                n += kv.Value;
            return n;
        }

        /// <summary>The card picked when the board opens: the first order that can be handed in, else the first order.</summary>
        static int DefaultCard(MetaGame g, long now, IReadOnlyList<OrderCard> board)
        {
            int first = -1;
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Order == null)
                    continue;
                if (g.CanFulfil(now, i))
                    return i;
                if (first < 0)
                    first = i;
            }
            return first;
        }

        // ------------------------------------------------------------------ the truck

        /// <summary>The truck to town: its crates (load each from the barn), the pay for the trip, «Отправить»; away, the time it comes back.</summary>
        static EconomyView TruckView(MetaGame g, long now)
        {
            var info = new EconomyInfo { Id = Truck, Title = "Грузовик в город" };
            var v = new EconomyView(Truck, null, info);
            var data = g.Data;
            if (!g.TruckUnlocked)
            {
                info.Empty = "Грузовик откроется на " + data.Balance.Truck.Level + " уровне фермы";
                return v;
            }
            var t = g.Truck(now);
            if (t.AwayUntil > now)
            {
                long left = t.AwayUntil - now;
                info.HeaderText = "Грузовик в пути";
                info.HeaderBar = 1 - (float)left / Math.Max(1, data.Balance.Truck.ReturnSeconds);
                info.HeaderBarText = "Вернётся через " + Format.Timer(left);
                info.Empty = "Грузовик вернётся за новым грузом";
                return v;
            }
            info.Empty = "Пока нечего везти: откройте новые товары";
            if (t.Crates.Count == 0)
                return v;
            info.HeaderText = "Награда за рейс";
            info.HeaderChips.Add(new Chip(Icons.Coin, "+" + Format.Amount(t.Coins)));
            info.HeaderChips.Add(new Chip(Icons.Xp, "+" + Format.Amount(t.Xp)));
            foreach (var kv in t.Tools)
            {
                info.HeaderChips.Add(new Chip(Icons.Tool(kv.Key), "×" + kv.Value));
                break;
            }
            bool all = true;
            for (int i = 0; i < t.Crates.Count; i++)
            {
                var c = t.Crates[i];
                int crate = i;
                string name = data.NameOf(c.Item) + " ×" + c.Count;
                if (c.Loaded)
                {
                    v.Row(new EconomyRow { Icon = IconOf(c.Item), Title = name, Sub = "Загружено", Button = new RowButton("Готово", null, Tone.Grey) },
                        EconomyAction.Say("Этот ящик уже в кузове"));
                    continue;
                }
                all = false;
                int have = g.BarnCount(c.Item);
                v.Row(new EconomyRow
                {
                    Icon = IconOf(c.Item), Title = name, Sub = "В амбаре: " + Math.Min(999, have) + (have < c.Count ? " из " + c.Count : ""),
                    Button = new RowButton("Загрузить", null, have >= c.Count ? Tone.Green : Tone.Grey),
                }, EconomyAction.Do(n => g.LoadCrate(n, crate)));
            }
            v.Footer(new RowButton("Отправить", Icons.Truck, all ? Tone.Yellow : Tone.Grey), EconomyAction.Do(n => g.SendTruck(n)));
            return v;
        }

        // ------------------------------------------------------------------ the town

        /// <summary>
        /// The town: residents (of the most the services allow), lots taken of the lots open, beauty; the next land
        /// expansion (saw, axe and coins); then houses, services and decorations with what each gives and costs —
        /// stars for the special ones, greyed with the reason when it cannot be built now.
        /// </summary>
        static EconomyView TownView(MetaGame g, long now)
        {
            var info = new EconomyInfo { Id = Town, Title = "Городок", HeaderText = "Жители · участки · красота" };
            info.HeaderChips.Add(new Chip(Icons.People, g.Population + "/" + g.PopulationCap, g.Population >= g.PopulationCap));
            info.HeaderChips.Add(new Chip(Icons.Lot, g.LotsUsed + "/" + g.Lots, g.LotsUsed >= g.Lots));
            info.HeaderChips.Add(new Chip(Icons.Beauty, Format.Amount(g.Beauty)));
            var v = new EconomyView(Town, null, info);
            var data = g.Data;

            var e = g.NextExpansion;
            if (e != null)
            {
                string title = e.Name + ": +" + Texts.Plural(e.Lots, "место", "места", "мест");
                if (e.Level > g.Level)
                {
                    v.Row(new EconomyRow { Icon = Icons.Lot, Title = title, Sub = NeedLevel(e.Level), Button = LockedButton(e.Level), Dim = true }, LockedSay(e.Level));
                }
                else
                {
                    var row = new EconomyRow { Icon = Icons.Lot, Title = title, Button = CoinsButton(g, e.Coins) };
                    if (!HasAll(g, e.Tools))
                        row.Button.Tone = Tone.Grey;
                    foreach (var kv in e.Tools)
                        row.Chips.Add(Need(g, kv.Key, kv.Value, true));
                    v.Row(row, EconomyAction.Do(t => g.Expand(t), false, "Земля расчищена: " + e.Name));
                }
            }

            Buildings(v, g, "Дома", data.Balance.Town.Houses, d => "+" + Texts.Plural(d.Population, "житель", "жителя", "жителей"));
            Buildings(v, g, "Для жителей", data.Balance.Town.Services, d => "+" + Texts.Plural(d.Capacity, "место", "места", "мест") + " для жителей");
            Buildings(v, g, "Украшения", data.Balance.Town.Decorations, d => "Красота +" + d.Beauty + (d.Xp > 0 ? " · опыт +" + d.Xp : ""));
            return v;
        }

        static void Buildings(EconomyView v, MetaGame g, string section, List<TownBuildingDef> list, Func<TownBuildingDef, string> gives)
        {
            v.Section(section);
            foreach (var d in list)
            {
                string id = d.Id;
                string icon = d.Kind == TownKinds.Decoration ? Icons.Beauty : Icons.Town;
                int built = g.Built(id);
                if (d.Level > g.Level)
                {
                    v.Row(new EconomyRow { Icon = icon, Title = d.Name, Sub = NeedLevel(d.Level), Button = LockedButton(d.Level), Dim = true }, LockedSay(d.Level));
                    continue;
                }
                string sub = gives(d) + (d.Max > 1 ? " · " + built + " из " + d.Max : d.Max == 0 && built > 0 ? " · есть " + built : "");
                if (d.Max > 0 && built >= d.Max)
                {
                    v.Row(new EconomyRow { Icon = icon, Title = d.Name, Sub = sub, Button = new RowButton("Готово", null, Tone.Grey) },
                        EconomyAction.Say(d.Max == 1 ? d.Name + " уже стоит в городке" : "Больше построить нельзя"));
                    continue;
                }
                bool can = g.CanBuild(id) == MetaError.None;
                var button = d.Stars > 0 ? new RowButton(d.Stars.ToString(), Icons.Star, can ? Tone.Yellow : Tone.Grey) : CoinsButton(g, d.Coins);
                if (!can)
                    button.Tone = Tone.Grey;
                v.Row(new EconomyRow { Icon = icon, Title = d.Name, Sub = sub, Button = button }, EconomyAction.Do(t => g.Build(t, id), false, "Построено: " + d.Name));
            }
        }
    }
}
