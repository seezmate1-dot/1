using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;

namespace SunnyValley.Game.Economy
{
    public enum ActionKind
    {
        /// <summary>A MetaGame action (plant, feed, enqueue, fulfil…), asked first when it has a Confirm.</summary>
        Run,
        /// <summary>Opens another window over this one (the crop picker for a plot, a workshop).</summary>
        Open,
        /// <summary>Picks a card on the order board: the window is shown again with it.</summary>
        Select,
        /// <summary>Only says something (a locked thing: when it opens).</summary>
        Toast,
    }

    /// <summary>What a button of an economy window does. Plain data with the core call in it, so the tests can press it.</summary>
    public sealed class EconomyAction
    {
        public ActionKind Kind;
        public Func<long, ActionResult> Run;
        /// <summary>Asked before running: crystals to spend, an order to throw away.</summary>
        public ConfirmInfo Confirm;
        public string Window;
        public string Arg;
        /// <summary>Toast: the text. Run: the text after it worked (null: what was received, if anything).</summary>
        public string Text;
        /// <summary>Closes the window after it worked (the crop picker returns to the fields).</summary>
        public bool Close;

        public static EconomyAction Do(Func<long, ActionResult> run, bool close = false, string text = null) =>
            new EconomyAction { Kind = ActionKind.Run, Run = run, Close = close, Text = text };

        public static EconomyAction Ask(ConfirmInfo confirm, Func<long, ActionResult> run) =>
            new EconomyAction { Kind = ActionKind.Run, Run = run, Confirm = confirm };

        public static EconomyAction Open(string window, string arg = null) => new EconomyAction { Kind = ActionKind.Open, Window = window, Arg = arg };

        public static EconomyAction Pick(string arg) => new EconomyAction { Kind = ActionKind.Select, Arg = arg };

        public static EconomyAction Say(string text) => new EconomyAction { Kind = ActionKind.Toast, Text = text };
    }

    /// <summary>A window ready to lay out (Popups.Economy) with what each of its buttons does, by button id.</summary>
    public sealed class EconomyView
    {
        public readonly string Window;
        public readonly string Arg;
        public readonly EconomyInfo Info;
        public readonly Dictionary<string, EconomyAction> Actions = new Dictionary<string, EconomyAction>();

        public EconomyView(string window, string arg, EconomyInfo info)
        {
            Window = window;
            Arg = arg;
            Info = info;
        }

        public EconomyAction Action(string id) => Actions.TryGetValue(id, out var a) ? a : null;

        /// <summary>Adds a row and what its main and round buttons do.</summary>
        public EconomyRow Row(EconomyRow row, EconomyAction main, EconomyAction alt = null)
        {
            int i = Info.Rows.Count;
            Info.Rows.Add(row);
            string id = Info.Id + (Info.Cards ? ".card" : ".row") + i;
            if (Info.Cards)
            {
                if (main != null)
                    Actions[id] = main;
                return row;
            }
            if (row.Button != null && main != null)
                Actions[id + ".do"] = main;
            if (row.Alt != null && alt != null)
                Actions[id + ".alt"] = alt;
            return row;
        }

        public void Section(string title) => Info.Rows.Add(EconomyRow.Section(title));

        public void Footer(RowButton button, EconomyAction action)
        {
            if (Info.Footer.Count >= 2)
                return;
            Actions[Info.Id + ".foot" + Info.Footer.Count] = action;
            Info.Footer.Add(button);
        }

        public void Slot(QueueSlot slot, EconomyAction action)
        {
            Info.Queue ??= new List<QueueSlot>();
            Actions[Info.Id + ".slot" + Info.Queue.Count] = action;
            Info.Queue.Add(slot);
        }

        public void HeaderButton(RowButton button, EconomyAction action)
        {
            Info.HeaderButton = button;
            Actions[Info.Id + ".head.do"] = action;
        }
    }

    /// <summary>
    /// The farm economy of chapter 2 (GDD 6) as windows: what each one shows and what its buttons do, read from
    /// MetaGame at a moment <c>now</c>. Plain C# with no Unity, so farm/ui-tests lay every window out with the real
    /// data on every screen and press every button. The Unity popup (Flow/EconomyPopup) shows a view, runs its
    /// actions, saves, and builds the view again every half second for the timers.
    /// </summary>
    public static partial class EconomyViews
    {
        public const string Fields = "fields";
        public const string Crops = "crops";
        public const string Animals = "animals";
        public const string Workshops = "workshops";
        public const string Workshop = "workshop";
        public const string Barn = "barn";
        public const string Orders = "orders";
        public const string Truck = "truck";
        public const string Town = "town";

        public static readonly string[] All = { Fields, Crops, Animals, Workshops, Workshop, Barn, Orders, Truck, Town };

        /// <summary>The window by name; arg is the plot of the crop picker, the workshop, the picked order card.</summary>
        public static EconomyView Build(string window, string arg, MetaGame g, long now)
        {
            switch (window)
            {
                case Fields: return FieldsView(g, now);
                case Crops: return CropsView(g, now, ParseInt(arg, 0));
                case Animals: return AnimalsView(g, now);
                case Workshops: return WorkshopsView(g, now);
                case Workshop: return WorkshopView(g, now, arg ?? "mill");
                case Barn: return BarnView(g, now);
                case Orders: return OrdersView(g, now, ParseInt(arg, -1));
                case Truck: return TruckView(g, now);
                case Town: return TownView(g, now);
                default: throw new ArgumentException("Нет такого окна: " + window);
            }
        }

        static int ParseInt(string s, int fallback) => int.TryParse(s, out int v) ? v : fallback;

        // ------------------------------------------------------------------ the farm screen

        /// <summary>Fills the farm screen's economy part: the farm level bar and the red dots of the side buttons.</summary>
        public static FarmHud Hud(FarmHud hud, MetaGame g, long now)
        {
            hud.Economy = true;
            hud.ZoneName = null;
            hud.FarmLevel = g.Level;
            (hud.XpHave, hud.XpNeed) = g.XpProgress;
            hud.TasksBadge = 0;
            hud.FieldsBadge = ReadyFields(g, now);
            hud.AnimalsBadge = ReadyAnimals(g, now) + HungryWithFeed(g);
            int jobs = 0;
            foreach (var w in g.State.Farm.Workshops)
                jobs += g.ReadyJobs(now, w.Id);
            hud.WorkshopsBadge = jobs;
            int orders = 0;
            var board = g.Board(now);
            for (int i = 0; i < board.Count; i++)
            {
                if (g.CanFulfil(now, i))
                    orders++;
            }
            hud.OrdersBadge = orders;
            var up = g.NextBarnUpgrade;
            hud.BarnBadge = up != null && HasAll(g, up.Tools) ? 1 : 0;
            var e = g.NextExpansion;
            hud.TownBadge = e != null && e.Level <= g.Level && HasAll(g, e.Tools) && g.Coins >= e.Coins ? 1 : 0;
            return hud;
        }

        /// <summary>«До 5 уровня: 120 опыта. Откроются: Подсолнух, Дом с мансардой…» for a tap on the level strip.</summary>
        public static string LevelHint(MetaGame g)
        {
            var (have, need) = g.XpProgress;
            if (need <= 0)
                return "Ферма на высшем уровне — " + g.Level;
            int next = g.Level + 1;
            string s = "До " + next + " уровня: " + Format.Amount(need - have) + " опыта";
            var opens = g.Data.UnlocksAt(next);
            if (opens.Count == 0)
                return s + ". Опыт дают заказы, урожай и постройки";
            var names = new List<string>();
            for (int i = 0; i < opens.Count && i < 3; i++)
                names.Add(opens[i] == "truck" ? "грузовик" : g.Data.NameOf(opens[i]));
            return s + ". Откроется: " + string.Join(", ", names) + (opens.Count > 3 ? "…" : "");
        }

        static int ReadyFields(MetaGame g, long now)
        {
            int n = 0;
            foreach (var p in g.Plots)
            {
                if (p.Crop != null && now >= p.ReadyAt)
                    n++;
            }
            foreach (var t in g.State.Farm.Trees)
            {
                if (now >= t.ReadyAt)
                    n++;
            }
            return n;
        }

        static int ReadyAnimals(MetaGame g, long now)
        {
            int n = 0;
            foreach (var a in g.State.Farm.Animals)
            {
                if (a.Fed && now >= a.ReadyAt)
                    n++;
            }
            return n;
        }

        static int HungryWithFeed(MetaGame g)
        {
            int n = 0;
            var feed = new Dictionary<string, int>();
            foreach (var a in g.State.Farm.Animals)
            {
                if (a.Fed)
                    continue;
                string f = g.Data.Animals[a.Type].Feed;
                feed.TryGetValue(f, out int used);
                if (g.BarnCount(f) > used)
                {
                    feed[f] = used + 1;
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>The icon of any id: a good, a tool or a booster.</summary>
        public static string IconOf(string id)
        {
            if (Boosters.IsBooster(id))
                return BoosterIcon(id);
            if (Tools.IsTool(id))
                return Icons.Tool(id);
            return Icons.Item(id);
        }

        public static string BoosterIcon(string id)
        {
            switch (id)
            {
                case Boosters.Line: return Icons.Pitchfork;
                case Boosters.Bomb: return Icons.Bomb;
                case Boosters.Rainbow: return Icons.Rainbow;
                case Boosters.Shovel: return Icons.Shovel;
                case Boosters.Rake: return Icons.Rake;
                case Boosters.WateringCan: return Icons.Can;
                case Boosters.Shuffle: return Icons.Shuffle;
                default: return Icons.Star;
            }
        }

        public static string AnimalIcon(string type) => "animal." + type;

        static RowButton CoinsButton(MetaGame g, long price) =>
            price <= 0 ? new RowButton("Даром", null, Tone.Green) : new RowButton(Format.Amount(price), Icons.Coin, g.Coins >= price ? Tone.Green : Tone.Grey);

        static RowButton CrystalsButton(MetaGame g, int price) => new RowButton(price.ToString(), Icons.Crystal, g.Crystals >= price ? Tone.Blue : Tone.Grey);

        static RowButton LockedButton(int level) => new RowButton("Ур. " + level, null, Tone.Grey);

        static EconomyAction LockedSay(int level) => EconomyAction.Say("Откроется на " + level + " уровне фермы");

        static string NeedLevel(int level) => "Нужен " + level + " уровень";

        static float Progress(long start, long end, long now) => end <= start ? 1 : Math.Max(0, Math.Min(1, (float)(now - start) / (end - start)));

        /// <summary>«1/2»: have of need (have capped at 999 so the chip stays short).</summary>
        static Chip Need(MetaGame g, string id, int need, bool tool = false)
        {
            int have = tool ? g.ToolCount(id) : g.BarnCount(id);
            return new Chip(IconOf(id), Math.Min(999, have) + "/" + need, have < need);
        }

        static bool HasAll(MetaGame g, IDictionary<string, int> tools)
        {
            foreach (var kv in tools)
            {
                if (g.ToolCount(kv.Key) < kv.Value)
                    return false;
            }
            return true;
        }

        static string Crystals(int n) => Texts.Plural(n, "кристалл", "кристалла", "кристаллов");

        /// <summary>The question before a speed-up: what, how long is left, the price.</summary>
        static ConfirmInfo SpeedUpQuestion(MetaGame g, string icon, string what, long left)
        {
            int price = g.SpeedUpPrice(left);
            return new ConfirmInfo
            {
                Title = "Ускорить?", Icon = icon,
                Text = what + " — ещё " + Texts.Duration(left) + ". Ускорить за " + Crystals(price) + "?",
                Button = new RowButton("Ускорить " + price, Icons.Crystal, Tone.Blue),
            };
        }

        /// <summary>Runs a group action (collect all, feed all): the rewards summed, the first error if nothing worked.</summary>
        static ActionResult Each(IEnumerable<Func<ActionResult>> steps, MetaError none)
        {
            var total = new Reward();
            int ok = 0;
            MetaError first = MetaError.None;
            foreach (var step in steps)
            {
                var r = step();
                if (r.Ok)
                {
                    ok++;
                    total.Add(r.Reward);
                }
                else if (first == MetaError.None)
                {
                    first = r.Error;
                }
            }
            if (ok > 0)
                return ActionResult.Success(total);
            return ActionResult.Fail(first != MetaError.None ? first : none);
        }
    }
}
