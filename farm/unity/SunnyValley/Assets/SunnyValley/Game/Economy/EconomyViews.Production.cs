using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Economy
{
    /// <summary>The workshops and their queues, the barn (GDD 6).</summary>
    public static partial class EconomyViews
    {
        // ------------------------------------------------------------------ the workshops

        /// <summary>Every workshop of the data: a built one with its queue's state («Открыть»), the others to build or locked.</summary>
        static EconomyView WorkshopsView(MetaGame g, long now)
        {
            var v = new EconomyView(Workshops, null, new EconomyInfo { Id = Workshops, Title = "Мастерские" });
            int ready = 0;
            foreach (var def in g.Data.Balance.Workshops)
            {
                string id = def.Id;
                var w = g.Workshop(id);
                if (w == null)
                {
                    if (def.Level > g.Level)
                        v.Row(new EconomyRow { Icon = Icons.Workshop, Title = def.Name, Sub = NeedLevel(def.Level), Button = LockedButton(def.Level), Dim = true },
                            LockedSay(def.Level));
                    else
                        v.Row(new EconomyRow
                        {
                            Icon = Icons.Workshop, Title = def.Name,
                            Sub = def.Population > 0 ? "Жителей: " + g.Population + " из " + def.Population : "Очередь на " + def.Slots + " места",
                            Button = CoinsButton(g, def.Coins),
                        }, EconomyAction.Do(t => g.BuildWorkshop(t, id), false, "Построено: " + def.Name));
                    continue;
                }
                int done = g.ReadyJobs(now, id);
                ready += done;
                var job = g.RunningJob(now, id);
                var open = new RowButton("Открыть", null, done > 0 ? Tone.Yellow : Tone.Green);
                if (job != null)
                {
                    v.Row(new EconomyRow
                    {
                        Icon = IconOf(g.Data.Recipes[job.Recipe].Output), Title = def.Name, Progress = Progress(job.StartAt, job.EndAt, now),
                        ProgressText = g.Data.NameOf(job.Recipe) + " · " + Format.Timer(job.EndAt - now), Button = open,
                    }, EconomyAction.Open(Workshop, id));
                }
                else
                {
                    string sub = done > 0 ? "Готово: " + done : w.Jobs.Count == 0 ? "Очередь пуста" : "В очереди " + w.Jobs.Count + " из " + w.Slots;
                    v.Row(new EconomyRow { Icon = Icons.Workshop, Title = def.Name, Sub = sub, Button = open }, EconomyAction.Open(Workshop, id));
                }
            }
            v.Footer(new RowButton("Собрать всё", Icons.Workshop, ready > 0 ? Tone.Yellow : Tone.Grey), EconomyAction.Do(t => CollectWorkshops(g, t)));
            return v;
        }

        static ActionResult CollectWorkshops(MetaGame g, long now)
        {
            var steps = new List<Func<ActionResult>>();
            foreach (var w in g.State.Farm.Workshops.ToArray())
            {
                string id = w.Id;
                if (g.ReadyJobs(now, id) > 0)
                    steps.Add(() => g.Collect(now, id));
            }
            return Each(steps, MetaError.NothingToCollect);
        }

        // ------------------------------------------------------------------ one workshop

        /// <summary>
        /// A workshop: its queue on top (done: tap to collect; running: tap to speed up; free; one more slot for
        /// crystals), then its recipes with their inputs as «have/need» (red when short) and «Начать»; locked recipes
        /// greyed with their level. «Собрать» and «Ускорить» at the bottom.
        /// </summary>
        static EconomyView WorkshopView(MetaGame g, long now, string id)
        {
            var data = g.Data;
            data.Workshops.TryGetValue(id, out var def);
            var info = new EconomyInfo { Id = Workshop, Title = def?.Name ?? "Мастерская", Empty = "Мастерская ещё не построена" };
            var v = new EconomyView(Workshop, id, info);
            var w = g.Workshop(id);
            if (def == null || w == null)
                return v;

            var running = g.RunningJob(now, id);
            for (int i = 0; i < w.Slots; i++)
            {
                if (i >= w.Jobs.Count)
                {
                    v.Slot(new QueueSlot(SlotState.Free, null, "Пусто"), EconomyAction.Say("Свободное место: выберите, что сделать"));
                    continue;
                }
                var j = w.Jobs[i];
                var rc = data.Recipes[j.Recipe];
                string icon = IconOf(rc.Output);
                if (j.EndAt <= now)
                    v.Slot(new QueueSlot(SlotState.Ready, icon, "Готово"), EconomyAction.Do(t => g.Collect(t, id)));
                else if (j == running)
                    v.Slot(new QueueSlot(SlotState.Running, icon, Format.Timer(j.EndAt - now)),
                        EconomyAction.Ask(SpeedUpQuestion(g, icon, data.NameOf(rc.Output), j.EndAt - now), t => g.SpeedUpWorkshop(t, id)));
                else
                    v.Slot(new QueueSlot(SlotState.Waiting, icon, Format.Timer(j.EndAt - now)),
                        EconomyAction.Say(data.NameOf(rc.Output) + " ждёт своей очереди: готово через " + Texts.Duration(j.EndAt - now)));
            }
            int slotPrice = g.NextSlotPrice(id);
            if (slotPrice >= 0)
            {
                v.Slot(new QueueSlot(SlotState.Buy, Icons.Crystal, "+" + slotPrice), EconomyAction.Ask(new ConfirmInfo
                {
                    Title = "Ещё место?", Icon = Icons.Workshop,
                    Text = "Ещё одно место в очереди «" + def.Name + "» за " + Crystals(slotPrice) + ".",
                    Button = new RowButton("Купить " + slotPrice, Icons.Crystal, Tone.Blue),
                }, t => g.BuySlot(t, id)));
            }

            bool full = w.Jobs.Count >= w.Slots;
            foreach (var rc in def.Recipes)
            {
                string recipe = rc.Id;
                string name = data.NameOf(rc.Output) + (rc.Count > 1 ? " ×" + rc.Count : "");
                if (rc.Level > g.Level)
                {
                    v.Row(new EconomyRow { Icon = IconOf(rc.Output), Title = name, Sub = NeedLevel(rc.Level), Button = LockedButton(rc.Level), Dim = true },
                        LockedSay(rc.Level));
                    continue;
                }
                var row = new EconomyRow
                {
                    Icon = IconOf(rc.Output), Title = name + " · " + Texts.Duration(rc.Seconds),
                    Button = new RowButton("Начать", null, !full && g.CanAfford(rc) ? Tone.Green : Tone.Grey),
                };
                foreach (var kv in rc.Inputs)
                    row.Chips.Add(Need(g, kv.Key, kv.Value));
                v.Row(row, EconomyAction.Do(t => g.Enqueue(t, id, recipe)));
            }

            int ready = g.ReadyJobs(now, id);
            v.Footer(new RowButton("Собрать", Icons.Check, ready > 0 ? Tone.Yellow : Tone.Grey), EconomyAction.Do(t => g.Collect(t, id)));
            if (running != null)
            {
                long left = running.EndAt - now;
                var rc = data.Recipes[running.Recipe];
                v.Footer(new RowButton("Ускорить " + g.SpeedUpPrice(left), Icons.Crystal, Tone.Blue),
                    EconomyAction.Ask(SpeedUpQuestion(g, IconOf(rc.Output), data.NameOf(rc.Output), left), t => g.SpeedUpWorkshop(t, id)));
            }
            return v;
        }

        // ------------------------------------------------------------------ the barn

        /// <summary>
        /// The barn: how full it is, the next upgrade with its tools («have/need»), then the goods (one sells with the
        /// main button, all of a kind with the round one after a question) and the tools kept.
        /// </summary>
        static EconomyView BarnView(MetaGame g, long now)
        {
            int used = g.BarnUsed, cap = g.BarnCapacity;
            var info = new EconomyInfo
            {
                Id = Barn, Title = "Амбар", HeaderText = "Занято " + used + " из " + cap,
                HeaderBar = cap > 0 ? Math.Min(1f, (float)used / cap) : 1, HeaderBarText = used >= cap ? "Амбар полон" : "Свободно " + (cap - used),
            };
            var v = new EconomyView(Barn, null, info);
            var data = g.Data;

            var up = g.NextBarnUpgrade;
            if (up != null)
            {
                var row = new EconomyRow
                {
                    Icon = Icons.Barn, Title = "Улучшить: +" + Texts.Plural(up.Capacity, "место", "места", "мест"),
                    Button = new RowButton("Улучшить", null, HasAll(g, up.Tools) ? Tone.Green : Tone.Grey),
                };
                foreach (var kv in up.Tools)
                    row.Chips.Add(Need(g, kv.Key, kv.Value, true));
                v.Row(row, EconomyAction.Do(t => g.UpgradeBarn(t), false, "Амбар стал больше: " + g.BarnCapacity + " → " + (g.BarnCapacity + up.Capacity)));
            }
            else
            {
                v.Row(new EconomyRow { Icon = Icons.Barn, Title = "Амбар улучшен до предела", Sub = "Вмещает " + cap }, null);
            }

            v.Section(used > 0 ? "Товары" : "Товары: пока пусто");
            foreach (var item in data.Balance.Items)
            {
                int n = g.BarnCount(item.Id);
                if (n <= 0)
                    continue;
                string id = item.Id;
                v.Row(new EconomyRow
                {
                    Icon = IconOf(id), Title = item.Name, Sub = "В амбаре " + Math.Min(999, n) + " · по " + item.Sell,
                    Button = new RowButton("+" + Format.Amount(item.Sell), Icons.Coin, Tone.Yellow), Alt = n > 1 ? new RowButton(null, Icons.Coin, Tone.Red) : null,
                }, EconomyAction.Do(t => g.Sell(t, id, 1)), EconomyAction.Ask(new ConfirmInfo
                {
                    Title = "Продать всё?", Icon = IconOf(id), Text = item.Name + " ×" + n + " за " + Texts.Plural((long)item.Sell * n, "монету", "монеты", "монет") + ".",
                    Button = new RowButton("Продать", Icons.Coin, Tone.Yellow),
                }, t => g.Sell(t, id, g.BarnCount(id))));
            }

            bool tools = false;
            foreach (var t in data.Balance.Tools)
            {
                int n = g.ToolCount(t.Id);
                if (n <= 0)
                    continue;
                if (!tools)
                    v.Section("Инструменты");
                tools = true;
                v.Row(new EconomyRow
                {
                    Icon = Icons.Tool(t.Id), Title = t.Name + " ×" + n, Sub = t.Kind == "land" ? "Расчищает землю для городка" : "Для улучшения амбара",
                }, null);
            }
            return v;
        }
    }
}
