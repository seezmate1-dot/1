using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Layout;

namespace SunnyValley.Game.Farm
{
    public enum ToolKind
    {
        /// <summary>Seeds of a crop (Arg): dragged over empty plots.</summary>
        Seed,
        /// <summary>The sickle: over ripe plots and trees.</summary>
        Sickle,
        /// <summary>A feed (Arg, an item): over the hungry animals that eat it.</summary>
        Feed,
        /// <summary>The basket: over animals with a product ready.</summary>
        Basket,
    }

    /// <summary>Something picked from a site's menu and dragged across the farm.</summary>
    public sealed class FarmTool
    {
        public ToolKind Kind;
        public string Arg;
        public string Icon;

        public static FarmTool Seed(string crop) => new FarmTool { Kind = ToolKind.Seed, Arg = crop, Icon = Icons.Item(crop) };
        public static FarmTool Sickle() => new FarmTool { Kind = ToolKind.Sickle, Icon = FarmMenus.SickleIcon };
        public static FarmTool Feed(string feed) => new FarmTool { Kind = ToolKind.Feed, Arg = feed, Icon = Icons.Item(feed) };
        public static FarmTool Basket() => new FarmTool { Kind = ToolKind.Basket, Icon = FarmMenus.BasketIcon };
    }

    /// <summary>
    /// One round button of a site's menu. A tool is dragged from it over the farm (a tap uses it on the site the
    /// menu belongs to); a button does its action (asked first when it spends crystals). A disabled one stays in
    /// its place, greyed, and says why when tapped.
    /// </summary>
    public sealed class MenuItem
    {
        public string Id;
        public string Icon;
        /// <summary>Under the icon: a price, a count, a time, «Ур. 5».</summary>
        public string Label;
        /// <summary>A small icon before the label (a coin, a crystal), or null.</summary>
        public string LabelIcon;
        public FarmTool Tool;
        public EconomyAction Action;
        public bool Enabled = true;
        public bool Locked;
        /// <summary>Why it is disabled, for a toast.</summary>
        public string Reason;
        /// <summary>The name for a toast and the tests.</summary>
        public string Name;
    }

    public sealed class MenuSlot
    {
        public string Icon;
        public string Text;
        public SlotState State;
    }

    /// <summary>The small menu a tapped site opens next to itself (docs/FARM_UX.md §5).</summary>
    public sealed class SiteMenu
    {
        public string Title;
        /// <summary>A line under the title: the timer, what is missing.</summary>
        public string Sub;
        /// <summary>0..1 with a bar, or -1.</summary>
        public float Progress = -1;
        /// <summary>A hint the first times: «Проведите семенами по грядкам».</summary>
        public string Hint;
        public readonly List<MenuItem> Items = new List<MenuItem>();
        /// <summary>A workshop's queue, shown over the recipes.</summary>
        public List<MenuSlot> Queue;

        public MenuItem Add(MenuItem item)
        {
            item.Id ??= "item" + Items.Count;
            Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// What a tap on a site does (docs/FARM_UX.md §5), read from MetaGame at a moment. A tap first does the obvious
    /// thing at once — takes what is ready, feeds a hungry animal when there is feed, opens the barn or the orders —
    /// (<see cref="Primary"/>); otherwise the site opens its menu (<see cref="For"/>): seeds for an empty plot, a
    /// timer with a speed-up for something growing, recipes for a workshop, what can be bought or built. The same
    /// MetaGame calls as the windows, the same questions before crystals. Plain C#, pressed by the tests.
    /// </summary>
    public static class FarmMenus
    {
        public const string SickleIcon = "tool.sickle";
        public const string BasketIcon = "basket";
        /// <summary>At most this many round buttons: more do not fit around a finger.</summary>
        public const int MaxItems = 8;

        static string Coins(long n) => Format.Amount(n);

        /// <summary>What a tap does at once, or null when the site opens its menu instead.</summary>
        public static EconomyAction Primary(FarmSite s, MetaGame g, long now)
        {
            var st = FarmSiteMap.Status(s, g, now);
            int i = s.Index;
            string r = s.Ref;
            switch (s.Kind)
            {
                case SiteKind.Plot:
                    return st.Phase == SitePhase.Ready ? EconomyAction.Do(t => g.Harvest(t, i)) : null;
                case SiteKind.Tree:
                    return st.Phase == SitePhase.Ready ? EconomyAction.Do(t => g.HarvestTree(t, i)) : null;
                case SiteKind.Animal:
                    if (st.Phase == SitePhase.Ready)
                        return EconomyAction.Do(t => g.CollectAnimal(t, i));
                    if (st.Phase == SitePhase.Hungry && st.Attention)
                        return EconomyAction.Do(t => g.Feed(t, i));
                    return null;
                case SiteKind.Workshop:
                    return st.Phase == SitePhase.Ready ? EconomyAction.Do(t => g.Collect(t, r)) : null;
                case SiteKind.Barn:
                    return EconomyAction.Open(EconomyViews.Barn);
                case SiteKind.Orders:
                    return EconomyAction.Open(EconomyViews.Orders);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The tool that goes on with what a tap just did, when more of the same waits: the sickle after a harvest,
        /// the feed after a feeding, the basket after an egg. Null when nothing else needs it.
        /// </summary>
        public static FarmTool FollowUp(FarmSite s, SiteStatus before, FarmSiteMap map, MetaGame g, long now)
        {
            FarmTool tool = null;
            if ((s.Kind == SiteKind.Plot || s.Kind == SiteKind.Tree) && before.Phase == SitePhase.Ready)
                tool = FarmTool.Sickle();
            else if (s.Kind == SiteKind.Animal && before.Phase == SitePhase.Ready)
                tool = FarmTool.Basket();
            else if (s.Kind == SiteKind.Animal && before.Phase == SitePhase.Hungry)
                tool = FarmTool.Feed(before.Item);
            if (tool == null)
                return null;
            foreach (var other in map.Sites)
            {
                if (other != s && ToolStroke.Fits(tool, other, g, now))
                    return tool;
            }
            return null;
        }

        /// <summary>A menu of one tool (after a tap that already did the first one).</summary>
        public static SiteMenu OfTool(FarmTool tool, MetaGame g)
        {
            var m = new SiteMenu();
            switch (tool.Kind)
            {
                case ToolKind.Sickle:
                    m.Title = "Урожай";
                    m.Hint = "Проведите серпом по спелому";
                    m.Add(new MenuItem { Icon = tool.Icon, Tool = tool, Name = "Серп" });
                    break;
                case ToolKind.Basket:
                    m.Title = "Готово";
                    m.Hint = "Проведите корзиной по животным";
                    m.Add(new MenuItem { Icon = tool.Icon, Tool = tool, Name = "Корзина" });
                    break;
                case ToolKind.Feed:
                    m.Title = g.Data.NameOf(tool.Arg);
                    m.Hint = "Проведите кормом по голодным";
                    m.Add(FeedItem(g, tool.Arg));
                    break;
                default:
                    m.Title = g.Data.NameOf(tool.Arg);
                    m.Add(new MenuItem { Icon = tool.Icon, Tool = tool, Name = m.Title });
                    break;
            }
            return m;
        }

        static MenuItem FeedItem(MetaGame g, string feed)
        {
            int have = g.BarnCount(feed);
            string name = g.Data.NameOf(feed);
            return new MenuItem
            {
                Icon = Icons.Item(feed), Label = "×" + Math.Min(999, have), Tool = FarmTool.Feed(feed), Enabled = have > 0, Name = name,
                Reason = "Нет корма: " + name.ToLowerInvariant(),
            };
        }

        static MenuItem SpeedUp(MetaGame g, string icon, string what, long left, Func<long, ActionResult> run)
        {
            int price = g.SpeedUpPrice(left);
            return new MenuItem
            {
                Icon = Icons.Crystal, Label = price.ToString(), Name = "Ускорить", Enabled = g.Crystals >= price,
                Reason = "Не хватает кристаллов: нужно " + price,
                Action = EconomyAction.Ask(EconomyViews.SpeedUpAsk(g, icon, what, left), run),
            };
        }

        static MenuItem LockedItem(string icon, string name, int level) => new MenuItem
        {
            Icon = icon, Label = "Ур. " + level, Name = name, Enabled = false, Locked = true, Reason = name + ": " + EconomyViews.LockedText(level).ToLowerInvariant(),
        };

        static MenuItem Buy(MetaGame g, string icon, string name, long price, Func<long, ActionResult> run, string done) => new MenuItem
        {
            Icon = icon, Label = price <= 0 ? "Даром" : Coins(price), LabelIcon = price <= 0 ? null : Icons.Coin, Name = name, Enabled = g.Coins >= price,
            Reason = "Не хватает монет: нужно " + Coins(price), Action = EconomyAction.Do(run, true, done),
        };

        /// <summary>The menu of a site as it stands now.</summary>
        public static SiteMenu For(FarmSite s, MetaGame g, long now)
        {
            var data = g.Data;
            var st = FarmSiteMap.Status(s, g, now);
            var m = new SiteMenu { Title = FarmSiteMap.NameOf(s, g) };
            int i = s.Index;
            string r = s.Ref;
            switch (s.Kind)
            {
                case SiteKind.Plot:
                    if (st.Phase == SitePhase.Empty)
                    {
                        m.Title = "Что посадить?";
                        m.Hint = "Проведите семенами по грядкам";
                        bool teaser = false;
                        foreach (var c in data.Balance.Crops)
                        {
                            var item = data.Items[c.Item];
                            if (m.Items.Count >= MaxItems)
                                break;
                            if (item.Level > g.Level)
                            {
                                // one closed crop as a promise, not the whole list
                                if (!teaser)
                                    m.Add(LockedItem(Icons.Item(c.Item), item.Name, item.Level));
                                teaser = true;
                                continue;
                            }
                            int own = g.BarnCount(c.Item);
                            bool coins = g.Coins >= c.SeedCoins;
                            m.Add(new MenuItem
                            {
                                Icon = Icons.Item(c.Item), Name = item.Name, Tool = FarmTool.Seed(c.Item),
                                Label = coins || own <= 0 ? Coins(c.SeedCoins) : "×" + Math.Min(999, own), LabelIcon = coins || own <= 0 ? Icons.Coin : Icons.Barn,
                                Enabled = coins || own > 0, Reason = "Не хватает монет на семена: " + item.Name.ToLowerInvariant(),
                            });
                        }
                    }
                    else if (st.Phase == SitePhase.Growing)
                    {
                        m.Title = data.NameOf(st.Item);
                        m.Sub = Format.Timer(st.Left);
                        m.Progress = st.Progress;
                        m.Add(SpeedUp(g, Icons.Item(st.Item), m.Title, st.Left, t => g.SpeedUpPlot(t, i)));
                    }
                    else
                    {
                        m.Title = data.NameOf(st.Item);
                        m.Sub = "Созрело · урожай ×" + st.Count;
                        m.Hint = "Проведите серпом по спелому";
                        m.Add(new MenuItem { Icon = SickleIcon, Tool = FarmTool.Sickle(), Name = "Серп" });
                    }
                    break;

                case SiteKind.PlotOffer:
                    if (st.Phase == SitePhase.Locked)
                        m.Add(LockedItem(Icons.Soil, "Новая грядка", st.Level));
                    else if (st.Phase == SitePhase.Offer)
                        m.Add(Buy(g, Icons.Soil, "Новая грядка", st.Count, t => g.BuyPlot(t), "Новая грядка готова"));
                    break;

                case SiteKind.Tree:
                    if (st.Phase == SitePhase.Growing)
                    {
                        m.Sub = Format.Timer(st.Left);
                        m.Progress = st.Progress;
                        m.Add(SpeedUp(g, Icons.Item(st.Item), m.Title, st.Left, t => g.SpeedUpTree(t, i)));
                    }
                    else
                    {
                        m.Sub = "Поспело · " + data.NameOf(st.Item).ToLowerInvariant() + " ×" + st.Count;
                        m.Add(new MenuItem { Icon = SickleIcon, Tool = FarmTool.Sickle(), Name = "Серп" });
                    }
                    break;

                case SiteKind.Nursery:
                    foreach (var def in data.Balance.Trees)
                    {
                        string type = def.Id;
                        int price = g.NextTreePrice(type);
                        if (price < 0)
                            continue;
                        if (def.Level > g.Level)
                            m.Add(LockedItem(Icons.Item(def.Item), def.Name, def.Level));
                        else
                            m.Add(Buy(g, Icons.Item(def.Item), def.Name, price, t => g.BuyTree(t, type), def.Name + " посажена в саду"));
                    }
                    m.Sub = m.Items.Count == 0 ? "Сад полон" : "Посадить дерево";
                    break;

                case SiteKind.Animal:
                    if (st.Phase == SitePhase.Hungry)
                    {
                        m.Sub = "Нужен корм: " + data.NameOf(st.Item).ToLowerInvariant();
                        m.Add(FeedItem(g, st.Item));
                        if (g.BarnCount(st.Item) <= 0)
                        {
                            var (button, way) = EconomyViews.FeedWay(g, st.Item);
                            m.Add(new MenuItem
                            {
                                Icon = way.Kind == ActionKind.Open && way.Window == EconomyViews.Fields ? Icons.Fields : Icons.Workshop, Label = button.Text,
                                Name = button.Text, Action = way,
                            });
                        }
                    }
                    else if (st.Phase == SitePhase.Growing)
                    {
                        m.Sub = Format.Timer(st.Left);
                        m.Progress = st.Progress;
                        m.Add(SpeedUp(g, EconomyViews.AnimalIcon(r), m.Title, st.Left, t => g.SpeedUpAnimal(t, i)));
                    }
                    else
                    {
                        m.Sub = "Готово: " + data.NameOf(st.Item).ToLowerInvariant();
                        m.Add(new MenuItem { Icon = BasketIcon, Tool = FarmTool.Basket(), Name = "Корзина" });
                    }
                    break;

                case SiteKind.Pen:
                {
                    var house = data.AnimalHouses[r];
                    if (st.Phase == SitePhase.Locked)
                    {
                        m.Add(LockedItem(Icons.Barn, house.Name, house.Level));
                        break;
                    }
                    if (st.Phase == SitePhase.Offer)
                    {
                        var build = m.Add(Buy(g, Icons.Barn, house.Name, house.Coins, t => g.BuildAnimalHouse(t, r), "Построено: " + house.Name));
                        if (g.Population < house.Population)
                        {
                            build.Enabled = false;
                            build.Reason = "Нужно жителей: " + house.Population + ", есть " + g.Population;
                        }
                        m.Sub = "Место для " + house.Capacity + " животных";
                        break;
                    }
                    int count = 0;
                    foreach (var kind in data.Balance.Animals)
                    {
                        if (kind.House != r)
                            continue;
                        count += g.AnimalCount(kind.Id);
                        string type = kind.Id;
                        int price = g.NextAnimalPrice(type);
                        if (price < 0)
                            continue;
                        if (kind.Level > g.Level)
                            m.Add(LockedItem(EconomyViews.AnimalIcon(type), kind.Name, kind.Level));
                        else
                            m.Add(Buy(g, EconomyViews.AnimalIcon(type), kind.Name, price, t => g.BuyAnimal(t, type), kind.Name + " уже на ферме"));
                    }
                    m.Sub = count + " из " + house.Capacity + (m.Items.Count == 0 ? " · мест нет" : "");
                    break;
                }

                case SiteKind.Workshop:
                {
                    var def = data.Workshops[r];
                    if (st.Phase == SitePhase.Locked)
                    {
                        m.Add(LockedItem(Icons.Workshop, def.Name, def.Level));
                        break;
                    }
                    var w = g.Workshop(r);
                    if (w == null)
                    {
                        var build = m.Add(Buy(g, Icons.Workshop, def.Name, def.Coins, t => g.BuildWorkshop(t, r), "Построено: " + def.Name));
                        if (g.Population < def.Population)
                        {
                            build.Enabled = false;
                            build.Reason = "Нужно жителей: " + def.Population + ", есть " + g.Population;
                        }
                        m.Sub = "Очередь на " + def.Slots + " места";
                        break;
                    }
                    // the queue on top, as in the window
                    m.Queue = new List<MenuSlot>();
                    var running = g.RunningJob(now, r);
                    for (int k = 0; k < w.Slots; k++)
                    {
                        if (k >= w.Jobs.Count)
                        {
                            m.Queue.Add(new MenuSlot { State = SlotState.Free });
                            continue;
                        }
                        var j = w.Jobs[k];
                        string icon = EconomyViews.IconOf(data.Recipes[j.Recipe].Output);
                        m.Queue.Add(new MenuSlot
                        {
                            Icon = icon, State = j.EndAt <= now ? SlotState.Ready : j == running ? SlotState.Running : SlotState.Waiting,
                            Text = j.EndAt <= now ? "Готово" : Format.Timer(j.EndAt - now),
                        });
                    }
                    bool full = w.Jobs.Count >= w.Slots;
                    m.Sub = running != null ? data.NameOf(data.Recipes[running.Recipe].Output) + " · " + Format.Timer(running.EndAt - now)
                        : full ? "Очередь занята" : "Что сделать?";
                    bool teaser = false;
                    foreach (var rc in def.Recipes)
                    {
                        // the last place is the way to the whole window
                        if (m.Items.Count >= MaxItems - 1)
                            break;
                        string name = data.NameOf(rc.Output) + (rc.Count > 1 ? " ×" + rc.Count : "");
                        if (rc.Level > g.Level)
                        {
                            if (!teaser)
                                m.Add(LockedItem(EconomyViews.IconOf(rc.Output), name, rc.Level));
                            teaser = true;
                            continue;
                        }
                        string recipe = rc.Id;
                        string missing = null;
                        foreach (var kv in rc.Inputs)
                        {
                            int have = g.BarnCount(kv.Key);
                            if (have < kv.Value)
                                missing = (missing == null ? "" : missing + ", ") + data.NameOf(kv.Key).ToLowerInvariant() + " " + have + "/" + kv.Value;
                        }
                        m.Add(new MenuItem
                        {
                            Icon = EconomyViews.IconOf(rc.Output), Label = Texts.Duration(rc.Seconds), Name = name, Enabled = !full && missing == null,
                            Reason = full ? "Очередь занята — дождитесь или заберите готовое" : name + " — не хватает: " + missing,
                            Action = EconomyAction.Do(t => g.Enqueue(t, r, recipe)),
                        });
                    }
                    m.Add(new MenuItem { Icon = Icons.Workshop, Label = "Ещё", Name = "Вся мастерская", Action = EconomyAction.Open(EconomyViews.Workshop, r) });
                    break;
                }
            }
            return m;
        }
    }

    /// <summary>What a dragged tool did to one site.</summary>
    public sealed class StrokeHit
    {
        public FarmSite Site;
        public bool Ok;
        public MetaError Error;
        public Reward Reward = new Reward();
        /// <summary>Coins the seed cost (0 when it came from the barn).</summary>
        public int Coins;
        /// <summary>What went into the site: the crop planted, the feed given.</summary>
        public string Item;
    }

    /// <summary>
    /// One drag of a tool across the farm (docs/FARM_UX.md §5): every site it passes that the tool fits gets the
    /// tool's action once — seeds planted, ripe things cut, the hungry fed, products picked up — through the same
    /// MetaGame calls as the windows. It ends itself when it cannot go on (no coins and no seeds of one's own, the
    /// barn full, the feed gone): the reason is told once, not for every plot after.
    /// </summary>
    public sealed class ToolStroke
    {
        public readonly FarmTool Tool;
        readonly MetaGame g;
        readonly FarmSiteMap map;
        readonly HashSet<string> seen = new HashSet<string>();

        public bool Stopped { get; private set; }
        public MetaError StopError { get; private set; }
        public int Done { get; private set; }
        public int CoinsSpent { get; private set; }
        public readonly Reward Total = new Reward();

        public ToolStroke(MetaGame game, FarmSiteMap sites, FarmTool tool)
        {
            g = game;
            map = sites;
            Tool = tool;
        }

        /// <summary>Whether the tool does anything to the site as it stands.</summary>
        public static bool Fits(FarmTool tool, FarmSite s, MetaGame g, long now)
        {
            var st = FarmSiteMap.Status(s, g, now);
            switch (tool.Kind)
            {
                case ToolKind.Seed: return s.Kind == SiteKind.Plot && st.Phase == SitePhase.Empty;
                case ToolKind.Sickle: return (s.Kind == SiteKind.Plot || s.Kind == SiteKind.Tree) && st.Phase == SitePhase.Ready;
                case ToolKind.Feed: return s.Kind == SiteKind.Animal && st.Phase == SitePhase.Hungry && st.Item == tool.Arg;
                case ToolKind.Basket: return s.Kind == SiteKind.Animal && st.Phase == SitePhase.Ready;
                default: return false;
            }
        }

        static bool Blocks(MetaError e) =>
            e == MetaError.NotEnoughCoins || e == MetaError.BarnFull || e == MetaError.NeedsFeed || e == MetaError.NotEnoughItems || e == MetaError.FarmClosed;

        /// <summary>The tool over a site: what happened, or null (already done in this drag, does not fit, the drag is over).</summary>
        public StrokeHit Touch(FarmSite s, long now)
        {
            if (Stopped || s == null || seen.Contains(s.Id) || !Fits(Tool, s, g, now))
                return null;
            seen.Add(s.Id);
            var hit = new StrokeHit { Site = s, Item = Tool.Arg };
            ActionResult r;
            switch (Tool.Kind)
            {
                case ToolKind.Seed:
                {
                    int price = g.Data.Crops[Tool.Arg].SeedCoins;
                    // seeds for coins; with no coins, one's own from the barn
                    bool own = g.Coins < price && g.BarnCount(Tool.Arg) > 0;
                    r = g.Plant(now, s.Index, Tool.Arg, own);
                    if (r.Ok && !own)
                        hit.Coins = price;
                    break;
                }
                case ToolKind.Sickle:
                    r = s.Kind == SiteKind.Tree ? g.HarvestTree(now, s.Index) : g.Harvest(now, s.Index);
                    break;
                case ToolKind.Feed:
                    r = g.Feed(now, s.Index);
                    break;
                default:
                    r = g.CollectAnimal(now, s.Index);
                    break;
            }
            hit.Ok = r.Ok;
            hit.Error = r.Error;
            if (r.Ok)
            {
                Done++;
                CoinsSpent += hit.Coins;
                hit.Reward = r.Reward;
                Total.Add(r.Reward);
            }
            else if (Blocks(r.Error))
            {
                Stopped = true;
                StopError = r.Error;
            }
            return hit;
        }

        /// <summary>The tool moved from one map point to another: everything on the way, in order.</summary>
        public List<StrokeHit> Sweep(P2 from, P2 to, long now, float zoom = 1)
        {
            var hits = new List<StrokeHit>();
            foreach (var s in map.Along(from, to, zoom))
            {
                var h = Touch(s, now);
                if (h != null)
                    hits.Add(h);
            }
            return hits;
        }
    }

    /// <summary>
    /// Where a menu's round buttons go: a fan over the tapped thing, on one arc while they fit and on two when
    /// there are many, moved as a whole so every button stays inside the free part of the view; with no room above
    /// (the thing is under the top plates) the fan opens downwards. View coordinates, origin anywhere, y up.
    /// </summary>
    public static class RadialLayout
    {
        /// <summary>A round button: 144 units — 48 pt, over the 44 pt minimum.</summary>
        public const float Item = 144;
        public const float Gap = 18;
        /// <summary>Room over the buttons for the menu's title plate.</summary>
        public const float TitleRoom = 150;

        public struct Result
        {
            public P2[] Points;
            /// <summary>The fan opens downwards.</summary>
            public bool Below;
            /// <summary>Where the title plate's middle goes.</summary>
            public P2 Title;
        }

        public static Result Place(int n, P2 anchor, float anchorRadius, float left, float bottom, float right, float top)
        {
            var pts = new P2[Math.Max(0, n)];
            float half = Item / 2, pitch = Item + Gap;
            // how many fit on the first arc: at most a half circle and a bit
            float r1 = Math.Max(anchorRadius + half + Gap, 190);
            int first = Math.Min(n, Math.Max(1, (int)Math.Floor(Math.PI * 1.05 * r1 / pitch) + 1));
            float r2 = r1 + pitch;
            int k = 0;
            float highest = 0;
            for (int ring = 0; k < n; ring++)
            {
                float r = ring == 0 ? r1 : r2 + (ring - 1) * pitch;
                int count = ring == 0 ? first : Math.Min(n - k, Math.Max(1, (int)Math.Floor(Math.PI * 1.05 * r / pitch) + 1));
                float stepAngle = 2 * (float)Math.Asin(Math.Min(1, pitch / (2 * r)));
                for (int j = 0; j < count; j++, k++)
                {
                    float a = (float)Math.PI / 2 + (j - (count - 1) / 2f) * stepAngle;
                    pts[k] = new P2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
                    highest = Math.Max(highest, pts[k].Y);
                }
            }

            bool below = anchor.Y + highest + half + TitleRoom > top && anchor.Y - highest - half - TitleRoom >= bottom;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                pts[i] = new P2(anchor.X + pts[i].X, anchor.Y + (below ? -pts[i].Y : pts[i].Y));
                minX = Math.Min(minX, pts[i].X - half);
                maxX = Math.Max(maxX, pts[i].X + half);
                minY = Math.Min(minY, pts[i].Y - half);
                maxY = Math.Max(maxY, pts[i].Y + half);
            }
            if (n == 0)
            {
                minX = maxX = anchor.X;
                minY = maxY = anchor.Y;
            }
            // the title sits beyond the fan, away from the thing
            if (below)
                minY -= TitleRoom;
            else
                maxY += TitleRoom;
            float dx = minX < left ? left - minX : maxX > right ? right - maxX : 0;
            float dy = minY < bottom ? bottom - minY : maxY > top ? top - maxY : 0;
            for (int i = 0; i < n; i++)
                pts[i] = new P2(pts[i].X + dx, pts[i].Y + dy);
            return new Result
            {
                Points = pts, Below = below,
                Title = new P2(Math.Max(left + 260, Math.Min(right - 260, anchor.X + dx)), below ? minY + dy + TitleRoom / 2 : maxY + dy - TitleRoom / 2),
            };
        }
    }
}
