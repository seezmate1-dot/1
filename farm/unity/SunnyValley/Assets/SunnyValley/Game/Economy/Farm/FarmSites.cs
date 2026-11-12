using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Layout;

namespace SunnyValley.Game.Farm
{
    public enum SiteKind
    {
        Plot,
        /// <summary>The next plot for sale: a pegged-out patch with its price.</summary>
        PlotOffer,
        Tree,
        Animal,
        /// <summary>An animal house's sign: buy the next animal, or build the house.</summary>
        Pen,
        Workshop,
        Barn,
        Orders,
        /// <summary>The sign in the orchard: buy the next fruit tree.</summary>
        Nursery,
    }

    /// <summary>
    /// A thing of the working farm that stands on the map and answers a finger: a plot, a tree, an animal, a pen's
    /// sign, a workshop, the barn's doors, the order board. X, Y is its bottom centre on the map (as the chapter's
    /// sprites), W × H its picture, which is also what a finger hits.
    /// </summary>
    public sealed class FarmSite
    {
        public string Id;
        public SiteKind Kind;
        /// <summary>The plot's index; the tree's or animal's uid.</summary>
        public int Index;
        /// <summary>The animal's kind, the house, the workshop, the tree's kind.</summary>
        public string Ref;
        public float X, Y, W, H;
        /// <summary>The site is a tile of the ground plane (a plot): a diamond in its W × H box.</summary>
        public bool Diamond;

        public P2 Centre => new P2(X, Y + H / 2);
        public P2 Top => new P2(X, Y + H);

        public bool Contains(P2 p, float padX = 0, float padY = 0)
        {
            if (Diamond)
                return Math.Abs(p.X - X) / (W / 2 + padX) + Math.Abs(p.Y - (Y + H / 2)) / (H / 2 + padY) <= 1;
            return p.X >= X - W / 2 - padX && p.X <= X + W / 2 + padX && p.Y >= Y - padY && p.Y <= Y + H + padY;
        }

        public override string ToString() => Id;
    }

    public enum SitePhase
    {
        /// <summary>Nothing to show over it (a working pen, the barn).</summary>
        Idle,
        /// <summary>An empty plot: waits for seeds.</summary>
        Empty,
        /// <summary>Growing, digesting, a workshop's job running: Progress and Left count.</summary>
        Growing,
        /// <summary>Ripe, laid, made: a tap takes it.</summary>
        Ready,
        Hungry,
        /// <summary>Can be bought or built now.</summary>
        Offer,
        /// <summary>Opens at a farm level: Level says which.</summary>
        Locked,
    }

    /// <summary>What a site shows right now.</summary>
    public struct SiteStatus
    {
        public SitePhase Phase;
        /// <summary>0..1 while Growing.</summary>
        public float Progress;
        public long Left;
        /// <summary>The good it grows, wants or gives (an item id), or null.</summary>
        public string Item;
        /// <summary>Ready things waiting (a workshop's finished jobs, orders that can be handed in).</summary>
        public int Count;
        public int Level;
        /// <summary>Asks for a finger: bounces, sparkles (ripe; hungry with feed in the barn; an upgrade at hand).</summary>
        public bool Attention;

        /// <summary>A growing plant's size step: 1 a sprout, 2 half grown, 3 nearly ripe; 4 ripe; 0 nothing.</summary>
        public int Stage => Phase == SitePhase.Ready ? 4 : Phase != SitePhase.Growing ? 0 : Progress < 0.34f ? 1 : Progress < 0.75f ? 2 : 3;
    }

    /// <summary>
    /// Where the working farm stands on the chapter's map (docs/FARM_UX.md §4) and what a finger finds there.
    /// Everything lies on the map's ground plane, whose axes run up-right and up-left with the slope of the painted
    /// backgrounds (<see cref="IsoA"/>, <see cref="IsoB"/>): the plots are a diamond field of tiles in the fenced
    /// meadow, the fruit trees stand in rows in the orchard, the hens in the run by the coop; the mill is the mill,
    /// the barn's gate is the barn, the sign by the house is the order board. What chapter 1 has no place for (the
    /// later animal houses and workshops) stands along the road under the farm, each on its own patch of ground.
    /// Built from MetaGame; build it again when something was bought or built (<see cref="Signature"/> changes).
    /// </summary>
    public sealed class FarmSiteMap
    {
        /// <summary>Half a ground tile on the map: a step along a ground axis is (±IsoA, IsoB). The slope of the painted zones.</summary>
        public const float IsoA = 75, IsoB = 44;

        public const int PlotColumns = 5;
        public const int PlotRows = 6;
        /// <summary>A plot is one tile: its diamond's box.</summary>
        public const float PlotW = 2 * IsoA, PlotH = 2 * IsoB;
        public const float TreeW = 150, TreeH = 200;
        public const float AnimalW = 112, AnimalH = 112;
        public const float SignW = 105, SignH = 120;
        public const float WorkshopW = 290, WorkshopH = 270;
        /// <summary>A pen's yard, in tiles along the two ground axes (the pen's picture is drawn to it).</summary>
        public const float PenU = 2.9f, PenV = 2.5f;
        /// <summary>The smallest thing a finger must be able to hit: 44 pt in canvas units (Tokens.MinTouch).</summary>
        public const float MinTouch = 132;

        public readonly List<FarmSite> Sites = new List<FarmSite>();
        readonly Dictionary<string, FarmSite> byId = new Dictionary<string, FarmSite>();
        /// <summary>What was built from: when it differs from the game's, the map is stale.</summary>
        public string Signature { get; private set; }

        public FarmSite Find(string id) => id != null && byId.TryGetValue(id, out var s) ? s : null;

        void Add(FarmSite s)
        {
            Sites.Add(s);
            byId[s.Id] = s;
        }

        static (float x, float y) Zone(FarmLayoutDef layout, string id, float fallbackX, float fallbackY)
        {
            var z = layout?.Zone(id);
            return z != null ? (z.X, z.Y) : (fallbackX, fallbackY);
        }

        public static string SignatureOf(MetaGame g)
        {
            var f = g.State.Farm;
            var sb = new System.Text.StringBuilder();
            sb.Append(f.Open ? 1 : 0).Append('|').Append(f.Plots.Count).Append(g.NextPlotOffer != null ? "+" : "").Append('|');
            foreach (var t in f.Trees)
                sb.Append(t.Uid).Append(',');
            sb.Append('|');
            foreach (var a in f.Animals)
                sb.Append(a.Uid).Append(',');
            sb.Append('|').Append(f.AnimalHouses.Count).Append('|').Append(f.Workshops.Count);
            return sb.ToString();
        }

        /// <summary>The zoom «Поля» flies to: the whole field fits between the HUD's side buttons.</summary>
        public const float FieldZoom = 0.8f;

        /// <summary>A point of the ground plane: u tiles up-right and v tiles up-left of an origin.</summary>
        public static P2 Ground(P2 origin, float u, float v) => new P2(origin.X + (u - v) * IsoA, origin.Y + (u + v) * IsoB);

        /// <summary>The field's nearest corner, in the fenced meadow.</summary>
        public static P2 FieldCorner(FarmLayoutDef layout)
        {
            var (zx, zy) = Zone(layout, "meadow", 3600, 2800);
            return new P2(zx + 715, zy + 250);
        }

        /// <summary>The middle of the whole field (where «Поля» looks).</summary>
        public static P2 FieldCentre(FarmLayoutDef layout) => Ground(FieldCorner(layout), PlotColumns / 2f, PlotRows / 2f);

        // the rows of the field fill from its middle outwards: the first plots lie on open grass, the wild flowers go last
        static readonly int[] RowOrder = { 2, 3, 1, 4, 0, 5 };

        /// <summary>The nearest corner of plot i's tile (its picture's foot): five in a row along the up-right axis.</summary>
        public static P2 PlotPoint(FarmLayoutDef layout, int i)
        {
            int col = i % PlotColumns, row = RowOrder[Math.Min(RowOrder.Length - 1, i / PlotColumns)];
            return Ground(FieldCorner(layout), col, row);
        }

        /// <summary>The street under the farm: pens and workshops chapter 1 has no place for, left to right.</summary>
        static readonly string[] Street =
        {
            "pen:cowshed", "pen:sheepfold", "pen:pigsty", "ws:bakery", "ws:dairy", "ws:toolshed", "ws:jam", "ws:textile", "ws:sugar_mill", "pen:apiary",
        };

        public const float StreetStart = 520, StreetY = 70, PenRoom = 540, WorkshopRoom = 345;

        /// <summary>Where the street ends (for the road and the green along it).</summary>
        public static float StreetEnd => StreetStart + 4 * PenRoom + 6 * WorkshopRoom;

        static P2 StreetPoint(string key)
        {
            // each takes the room it needs: a pen's yard is wider than a workshop
            float x = StreetStart;
            foreach (string k in Street)
            {
                float room = k.StartsWith("pen:") ? PenRoom : WorkshopRoom;
                if (k == key)
                    return new P2(x + room / 2, StreetY);
                x += room;
            }
            // anything the data adds later stands at the end
            return new P2(x + WorkshopRoom / 2, StreetY);
        }

        /// <summary>The nearest corner of a pen's yard (its animals stand on the yard's ground plane).</summary>
        public static P2 PenPoint(FarmLayoutDef layout, string house)
        {
            if (house == "coop")
            {
                var (zx, zy) = Zone(layout, "coop", 3600, 400);
                return new P2(zx + 720, zy + 545);
            }
            var p = StreetPoint("pen:" + house);
            // the yard is wider to the right (PenU > PenV): its corner sits left of the room's middle
            return new P2(p.X - (PenU - PenV) * IsoA / 2, p.Y);
        }

        // where the animals of a house stand in its yard (tiles), nearest first is not needed: the view sorts
        static readonly float[][] YardSpots =
        {
            new[] { 0.5f, 0.4f }, new[] { 1.4f, 0.35f }, new[] { 2.3f, 0.4f }, new[] { 0.4f, 1.3f }, new[] { 1.3f, 1.3f }, new[] { 0.15f, 2.05f },
        };

        // the hens' run by the coop is smaller than a yard
        static readonly float[][] CoopSpots =
        {
            new[] { 0f, 0f }, new[] { 0.95f, 0f }, new[] { 1.9f, 0f }, new[] { 0f, 0.95f }, new[] { 0.95f, 0.95f }, new[] { 1.9f, 0.95f },
        };

        /// <summary>The nearest corner of a workshop's ground (the mill: the foot of chapter 1's mill).</summary>
        public static P2 WorkshopPoint(FarmLayoutDef layout, string id)
        {
            if (id == "mill")
            {
                var (zx, zy) = Zone(layout, "mill", 2000, 2800);
                return new P2(zx + 800, zy + 500);
            }
            return StreetPoint("ws:" + id);
        }

        public static FarmSiteMap Build(MetaGame g, FarmLayoutDef layout)
        {
            var map = new FarmSiteMap { Signature = SignatureOf(g) };
            if (!g.FarmOpen)
                return map;
            var data = g.Data;
            var f = g.State.Farm;

            // plots and the next one for sale: tiles of the field
            int cells = PlotColumns * PlotRows;
            for (int i = 0; i < f.Plots.Count && i < cells; i++)
            {
                var p = PlotPoint(layout, i);
                map.Add(new FarmSite { Id = "plot." + i, Kind = SiteKind.Plot, Index = i, X = p.X, Y = p.Y, W = PlotW, H = PlotH, Diamond = true });
            }
            if (g.NextPlotOffer != null && f.Plots.Count < cells)
            {
                var p = PlotPoint(layout, f.Plots.Count);
                map.Add(new FarmSite { Id = "plot.offer", Kind = SiteKind.PlotOffer, Index = f.Plots.Count, X = p.X, Y = p.Y, W = PlotW, H = PlotH, Diamond = true });
            }

            // the orchard: three rows of fruit trees along the ground plane, on the lawn in front of the old apple trees
            var (ox, oy) = Zone(layout, "orchard", 400, 2800);
            var grove = new P2(ox + 470, oy + 330);
            for (int i = 0; i < f.Trees.Count && i < 18; i++)
            {
                int col = i % 6, row = i / 6;
                var t = f.Trees[i];
                var p = Ground(grove, col * 1.42f, row * 1.25f);
                map.Add(new FarmSite { Id = "tree." + t.Uid, Kind = SiteKind.Tree, Index = t.Uid, Ref = t.Type, X = p.X, Y = p.Y, W = TreeW, H = TreeH });
            }
            map.Add(new FarmSite { Id = "nursery", Kind = SiteKind.Nursery, X = ox + 1290, Y = oy + 330, W = SignW, H = SignH });

            // animal houses: the sign at the yard's near corner, and the animals of a built one in the yard
            foreach (var house in data.Balance.AnimalHouses)
            {
                var c = PenPoint(layout, house.Id);
                bool coop = house.Id == "coop";
                var sign = coop ? new P2(c.X - 215, c.Y - 30) : new P2(c.X - 120, c.Y - 14);
                map.Add(new FarmSite { Id = "pen." + house.Id, Kind = SiteKind.Pen, Ref = house.Id, X = sign.X, Y = sign.Y, W = SignW, H = SignH });
                if (!g.HasAnimalHouse(house.Id))
                    continue;
                var spots = coop ? CoopSpots : YardSpots;
                int k = 0;
                foreach (var a in f.Animals)
                {
                    if (data.Animals[a.Type].House != house.Id || k >= spots.Length)
                        continue;
                    var p = Ground(c, spots[k][0], spots[k][1]);
                    map.Add(new FarmSite { Id = "animal." + a.Uid, Kind = SiteKind.Animal, Index = a.Uid, Ref = a.Type, X = p.X, Y = p.Y, W = AnimalW, H = AnimalH });
                    k++;
                }
            }

            // workshops: the mill is the chapter's mill (its tower's foot; the sails above keep their looks)
            foreach (var w in data.Balance.Workshops)
            {
                var p = WorkshopPoint(layout, w.Id);
                bool mill = w.Id == "mill";
                map.Add(new FarmSite
                {
                    Id = "workshop." + w.Id, Kind = SiteKind.Workshop, Ref = w.Id, X = p.X, Y = mill ? p.Y : p.Y + 8, W = mill ? 380 : WorkshopW, H = mill ? 340 : WorkshopH,
                });
            }

            // the barn's gate (the big doors on the red barn's front) and the order board by the house
            var (bx, by) = Zone(layout, "barn", 3600, 1600);
            map.Add(new FarmSite { Id = "barn", Kind = SiteKind.Barn, X = bx + 915, Y = by + 572, W = 150, H = 150 });
            var (hx, hy) = Zone(layout, "house", 2000, 1600);
            map.Add(new FarmSite { Id = "orders", Kind = SiteKind.Orders, X = hx + 180, Y = hy + 60, W = 280, H = 300 });
            return map;
        }

        // ------------------------------------------------------------------ fingers

        /// <summary>
        /// The site under a map point, or null. A finger needs 44 pt whatever the zoom: small pictures get the
        /// missing room around them, and of several the one whose middle is nearest wins.
        /// </summary>
        public FarmSite At(P2 map, float zoom = 1)
        {
            float need = MinTouch / Math.Max(0.05f, zoom);
            FarmSite best = null;
            float bestScore = float.MaxValue;
            foreach (var s in Sites)
            {
                float padX = Math.Max(0, (need - s.W) / 2), padY = Math.Max(0, (need - s.H) / 2);
                if (!s.Contains(map, padX, padY))
                    continue;
                var c = s.Centre;
                // in the picture itself beats in the padding of a neighbour
                float dx = Math.Abs(map.X - c.X) / (s.W / 2), dy = Math.Abs(map.Y - c.Y) / (s.H / 2);
                float score = s.Diamond ? dx + dy : Math.Max(dx, dy);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>
        /// The sites a finger crossed on its way from one map point to another, in order, each once. A fast swipe
        /// moves far between two frames: the way is walked in short steps so no plot in between is skipped.
        /// </summary>
        public List<FarmSite> Along(P2 from, P2 to, float zoom = 1, float step = 24)
        {
            var hit = new List<FarmSite>();
            float d = P2.Distance(from, to);
            int n = Math.Max(1, (int)Math.Ceiling(d / Math.Max(1, step)));
            // a tool is dragged over the pictures themselves: no padding, so a swipe along a row takes that row only
            for (int i = 0; i <= n; i++)
            {
                var p = from + (to - from) * ((float)i / n);
                FarmSite best = null;
                float bestScore = float.MaxValue;
                foreach (var s in Sites)
                {
                    if (!s.Contains(p, s.Diamond ? 0 : 6, s.Diamond ? 0 : 6))
                        continue;
                    float score = P2.Distance(p, s.Centre);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = s;
                    }
                }
                if (best != null && !hit.Contains(best))
                    hit.Add(best);
            }
            return hit;
        }

        // ------------------------------------------------------------------ what a site shows

        static float Part(long start, long end, long now) => end <= start ? 1 : Math.Max(0, Math.Min(1, (float)(now - start) / (end - start)));

        public static SiteStatus Status(FarmSite s, MetaGame g, long now)
        {
            var data = g.Data;
            var st = new SiteStatus();
            switch (s.Kind)
            {
                case SiteKind.Plot:
                {
                    if (s.Index < 0 || s.Index >= g.Plots.Count)
                        return st;
                    var p = g.Plots[s.Index];
                    if (p.Crop == null)
                    {
                        st.Phase = SitePhase.Empty;
                        return st;
                    }
                    st.Item = p.Crop;
                    if (now >= p.ReadyAt)
                    {
                        st.Phase = SitePhase.Ready;
                        st.Attention = true;
                        st.Count = data.Crops[p.Crop].Yield;
                        return st;
                    }
                    st.Phase = SitePhase.Growing;
                    st.Progress = Part(p.PlantedAt, p.ReadyAt, now);
                    st.Left = p.ReadyAt - now;
                    return st;
                }
                case SiteKind.PlotOffer:
                {
                    var offer = g.NextPlotOffer;
                    if (offer == null)
                        return st;
                    st.Level = offer.Level;
                    st.Count = offer.Coins;
                    st.Phase = offer.Level > g.Level ? SitePhase.Locked : SitePhase.Offer;
                    st.Attention = st.Phase == SitePhase.Offer && g.Coins >= offer.Coins;
                    return st;
                }
                case SiteKind.Tree:
                {
                    var t = g.Tree(s.Index);
                    if (t == null)
                        return st;
                    var def = data.Trees[t.Type];
                    st.Item = def.Item;
                    if (now >= t.ReadyAt)
                    {
                        st.Phase = SitePhase.Ready;
                        st.Attention = true;
                        st.Count = def.Yield;
                        return st;
                    }
                    st.Phase = SitePhase.Growing;
                    st.Progress = Part(t.ReadyAt - def.CycleSeconds, t.ReadyAt, now);
                    st.Left = t.ReadyAt - now;
                    return st;
                }
                case SiteKind.Animal:
                {
                    var a = g.Animal(s.Index);
                    if (a == null)
                        return st;
                    var def = data.Animals[a.Type];
                    if (!a.Fed)
                    {
                        st.Phase = SitePhase.Hungry;
                        st.Item = def.Feed;
                        st.Attention = g.BarnCount(def.Feed) > 0;
                        return st;
                    }
                    st.Item = def.Product;
                    if (now >= a.ReadyAt)
                    {
                        st.Phase = SitePhase.Ready;
                        st.Attention = true;
                        st.Count = 1;
                        return st;
                    }
                    st.Phase = SitePhase.Growing;
                    st.Progress = Part(a.ReadyAt - def.Seconds, a.ReadyAt, now);
                    st.Left = a.ReadyAt - now;
                    return st;
                }
                case SiteKind.Pen:
                {
                    if (!data.AnimalHouses.TryGetValue(s.Ref, out var house))
                        return st;
                    if (g.HasAnimalHouse(s.Ref))
                        return st;
                    st.Level = house.Level;
                    st.Count = house.Coins;
                    st.Phase = house.Level > g.Level ? SitePhase.Locked : SitePhase.Offer;
                    return st;
                }
                case SiteKind.Workshop:
                {
                    if (!data.Workshops.TryGetValue(s.Ref, out var def))
                        return st;
                    if (!g.HasWorkshop(s.Ref))
                    {
                        st.Level = def.Level;
                        st.Count = def.Coins;
                        st.Phase = def.Level > g.Level ? SitePhase.Locked : SitePhase.Offer;
                        return st;
                    }
                    int ready = g.ReadyJobs(now, s.Ref);
                    if (ready > 0)
                    {
                        st.Phase = SitePhase.Ready;
                        st.Attention = true;
                        st.Count = ready;
                        st.Item = data.Recipes[g.Workshop(s.Ref).Jobs[0].Recipe].Output;
                        return st;
                    }
                    var job = g.RunningJob(now, s.Ref);
                    if (job != null)
                    {
                        st.Phase = SitePhase.Growing;
                        st.Item = data.Recipes[job.Recipe].Output;
                        st.Progress = Part(job.StartAt, job.EndAt, now);
                        st.Left = job.EndAt - now;
                    }
                    return st;
                }
                case SiteKind.Barn:
                {
                    var up = g.NextBarnUpgrade;
                    bool can = up != null;
                    if (up != null)
                    {
                        foreach (var kv in up.Tools)
                            can &= g.ToolCount(kv.Key) >= kv.Value;
                    }
                    st.Attention = can;
                    st.Count = g.BarnUsed;
                    return st;
                }
                case SiteKind.Orders:
                {
                    var board = g.Board(now);
                    for (int i = 0; i < board.Count; i++)
                    {
                        if (g.CanFulfil(now, i))
                            st.Count++;
                    }
                    st.Attention = st.Count > 0;
                    if (st.Count > 0)
                        st.Phase = SitePhase.Ready;
                    return st;
                }
            }
            return st;
        }

        /// <summary>The icon of what a site grows, wants or gives (a good, a booster of the tool shed), or null.</summary>
        public static string StatusIcon(SiteStatus st) => st.Item != null ? EconomyViews.IconOf(st.Item) : null;

        /// <summary>The name on a site's sign and over its menu.</summary>
        public static string NameOf(FarmSite s, MetaGame g)
        {
            var data = g.Data;
            switch (s.Kind)
            {
                case SiteKind.Plot: return "Грядка";
                case SiteKind.PlotOffer: return "Новая грядка";
                case SiteKind.Tree: return data.Trees.TryGetValue(s.Ref ?? "", out var t) ? t.Name : "Дерево";
                case SiteKind.Animal: return data.Animals.TryGetValue(s.Ref ?? "", out var a) ? a.Name : "Животное";
                case SiteKind.Pen: return data.AnimalHouses.TryGetValue(s.Ref ?? "", out var h) ? h.Name : "Загон";
                case SiteKind.Workshop: return data.Workshops.TryGetValue(s.Ref ?? "", out var w) ? w.Name : "Мастерская";
                case SiteKind.Barn: return "Амбар";
                case SiteKind.Nursery: return "Саженцы";
                default: return "Заказы";
            }
        }
    }
}
