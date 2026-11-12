using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Game.Farm;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The working farm on the map (docs/FARM_UX.md §4, §6): every site of FarmSiteMap as a living thing, drawn in the
    /// map's own projection (Resources/Art/Farm, farm/tools/art/farm_sites_art.py). A plot is a tilled bed with its
    /// crop's own plants in four steps (young, half grown, nearly ripe, ripe), swaying; ripe, its crop bobs over it.
    /// A fruit tree stands in leaf, in bloom, then with fruit. Animals breathe and hop; a hungry one shows its feed
    /// in a bubble, one with a product stands by it and bounces it. Every workshop is its own building; a pen is a
    /// fenced yard with its house. What can be bought or built wears its price, what is closed its level and stands
    /// faded. Growing things have a thin bar under them. The street under the farm gets its road and greenery here. A pressed site sinks a little and springs back, the selected one stands in a ring.
    /// One Update moves everything (no tween per object); the layer is its own canvas, so the swaying does not
    /// rebuild the HUD. What a site shows comes from FarmSiteMap.Status; Sync reads it again.
    /// </summary>
    public sealed class FarmSitesView : MonoBehaviour
    {
        sealed class Visual
        {
            public FarmSite Site;
            public RectTransform Root;
            public RectTransform Body;
            public Image BodyImage;
            public Image Crop;
            public RectTransform Bubble;
            public Image BubbleBack;
            public Image BubbleIcon;
            public RectTransform Bar;
            public Image BarFill;
            public RectTransform Tag;
            public Text TagText;
            public Image TagIcon;
            public SiteStatus Status;
            public bool Known;
            public float Phase;
            public float Scale = 1, ScaleTarget = 1;
            public float PopAt = -10;
            public float BodyX, BodyY;
            public string Shown;
        }

        FarmWorld world;
        MetaGame game;
        RectTransform layer;
        RectTransform ring;
        readonly List<Visual> visuals = new List<Visual>();
        readonly Dictionary<string, Visual> byId = new Dictionary<string, Visual>();
        Visual selected;

        public FarmSiteMap Map { get; private set; }

        // how each picture sits on its site: the canvas in map units and where the site's foot (X, Y) is on it
        // (from the canvas' left and bottom) — the numbers of farm/tools/art/farm_art
        const float PlotW = 170, PlotH = 122, CropH = 224, PlotFoot = 14;
        const float TreeW = 195.5f, TreeH = 248.2f, TreeFootX = 91, TreeFoot = 25.5f;
        const float AnimalSize = 112.5f, AnimalFootX = 49.5f, AnimalFoot = 9;
        const float SignW = 105, SignH = 120, SignFoot = 9;
        const float ShopW = 344, ShopH = 320, ShopFootX = 154, ShopFoot = 22;
        const float PenW = 500, PenH = 330, PenFootX = 201.5f, PenFoot = 12;

        /// <summary>Puts the working farm on the world's map and tells the world where a finger finds it.</summary>
        public static FarmSitesView Create(FarmWorld world, MetaGame game)
        {
            var go = new GameObject("sites", typeof(RectTransform));
            go.layer = world.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(world.Content, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            // its own canvas: the idle motion re-batches the farm's things, not the whole screen
            go.AddComponent<Canvas>();
            var v = go.AddComponent<FarmSitesView>();
            v.world = world;
            v.game = game;
            v.layer = rt;
            v.Rebuild();
            return v;
        }

        // ------------------------------------------------------------------ building

        static RectTransform Child(string name, Transform parent, float x, float y, float w, float h, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Picture(RectTransform rt, Sprite sprite, bool keepAspect = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = keepAspect;
            img.raycastTarget = false;
            img.enabled = sprite != null;
            return img;
        }

        static void Set(Image img, Sprite sprite)
        {
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        static Sprite Art(string name) => FarmArt.Painted("Farm/" + name);

        static readonly Vector2 Foot = new Vector2(0.5f, 0);
        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// The street under the farm, outside the painted zones: a strip of painted ground along the map's lower
        /// edge (meadow grass, a dirt road in front, a rail fence at the back that covers the seam with the zones),
        /// and bushes between the yards — so the pens and workshops stand in a place, not on a flat green strip.
        /// </summary>
        void Street()
        {
            const float tileW = 512, tileH = 440;
            var ground = Art("Street");
            if (ground != null)
            {
                // the same meadow goes on below the map's edge: the camera may lift the street out from under «Играть»
                var grass = Art("Grass");
                for (float x = -tileW; x < world.MapSize.x + tileW; x += tileW)
                {
                    if (grass != null)
                        Picture(Child("grass", layer, x, -tileH + 1, tileW + 1, tileH, Vector2.zero), grass);
                    Picture(Child("street", layer, x, 0, tileW + 1, tileH, Vector2.zero), ground);
                }
            }
            float x0 = FarmSiteMap.StreetStart - 420, x1 = FarmSiteMap.StreetEnd + 420;
            var rng = new System.Random(20261112);
            string[] green = { "Bush1", "Bush2", "Bush3", "Tuft1", "Tuft2" };
            for (int i = 0; i < 70; i++)
            {
                float x = x0 + (float)rng.NextDouble() * (x1 - x0), y = 110 + (float)rng.NextDouble() * 250;
                string name = green[rng.Next(green.Length)];
                bool bush = name.StartsWith("Bush");
                // not on a yard, a building or an animal
                bool free = true;
                foreach (var s in Map.Sites)
                {
                    if (s.Y < 460 && Mathf.Abs(x - s.X) < s.W / 2 + 200 && y > s.Y - 70 && y < s.Y + 340)
                    {
                        free = false;
                        break;
                    }
                }
                if (free)
                    Picture(Child("green", layer, x, y, bush ? 120 : 60, bush ? 96 : 44, Foot), Art(name));
            }
        }

        /// <summary>Builds every site again (something was bought or built).</summary>
        public void Rebuild()
        {
            string was = selected?.Site.Id;
            selected = null;
            for (int i = layer.childCount - 1; i >= 0; i--)
                Destroy(layer.GetChild(i).gameObject);
            visuals.Clear();
            byId.Clear();
            ring = null;

            Map = FarmSiteMap.Build(game, FarmArt.Layout);
            world.Sites = Map;

            Street();

            // the yards of the animal houses chapter 1 has no place for, under their animals; one not built yet is faded
            var houses = new List<AnimalHouseDef>(game.Data.Balance.AnimalHouses);
            houses.Sort((a, b) => FarmSiteMap.PenPoint(FarmArt.Layout, b.Id).Y.CompareTo(FarmSiteMap.PenPoint(FarmArt.Layout, a.Id).Y));
            foreach (var house in houses)
            {
                var yard = Art("Pen_" + house.Id);
                if (yard == null)
                    continue;
                var c = FarmSiteMap.PenPoint(FarmArt.Layout, house.Id);
                var pad = Picture(Child("pen " + house.Id, layer, c.X - PenFootX, c.Y - PenFoot, PenW, PenH, Vector2.zero), yard);
                pad.color = game.HasAnimalHouse(house.Id) ? Color.white : new Color(1, 1, 1, 0.5f);
            }

            // far things first: the near ones stand in front
            var order = new List<FarmSite>(Map.Sites);
            order.Sort((a, b) => b.Y.CompareTo(a.Y));
            foreach (var s in order)
                Make(s);

            var r = Child("ring", layer, 0, 0, 100, 60, Middle);
            Picture(r, Art("Ring"));
            r.gameObject.SetActive(false);
            ring = r;

            Sync(GameState.Now);
            if (was != null && byId.TryGetValue(was, out var again))
                Select(again.Site);
        }

        void Make(FarmSite s)
        {
            var v = new Visual { Site = s, Phase = (s.X * 0.013f + s.Y * 0.029f) % 6.28f };
            v.Root = Child(s.Id, layer, s.X, s.Y, s.W, s.H, Foot);
            float mid = s.W / 2;
            var data = game.Data;
            switch (s.Kind)
            {
                case SiteKind.Plot:
                    v.Body = Child("body", v.Root, mid, -PlotFoot, PlotW, PlotH, Foot);
                    v.BodyImage = Picture(v.Body, Art("Plot"));
                    v.Crop = Picture(Child("crop", v.Root, mid, -PlotFoot, PlotW, CropH, Foot), null);
                    break;
                case SiteKind.PlotOffer:
                    v.Body = Child("body", v.Root, mid, -PlotFoot, PlotW, PlotH, Foot);
                    v.BodyImage = Picture(v.Body, Art("PlotOffer"));
                    Picture(Child("plus", v.Body, PlotW / 2, PlotFoot + s.H * 0.62f, 56, 56, Middle), ShapeSprites.Icon(Icons.Plus), true);
                    break;
                case SiteKind.Tree:
                    v.Body = Child("body", v.Root, mid - TreeFootX + TreeW / 2, -TreeFoot, TreeW, TreeH, Foot);
                    v.BodyImage = Picture(v.Body, null);
                    break;
                case SiteKind.Animal:
                    v.Body = Child("body", v.Root, mid - AnimalFootX + AnimalSize / 2, -AnimalFoot, AnimalSize, AnimalSize, Foot);
                    v.BodyImage = Picture(v.Body, Art("Animal_" + s.Ref) ?? ShapeSprites.Icon(EconomyViews.AnimalIcon(s.Ref)), true);
                    break;
                case SiteKind.Pen:
                case SiteKind.Nursery:
                {
                    v.Body = Child("body", v.Root, mid, -SignFoot, SignW, SignH, Foot);
                    v.BodyImage = Picture(v.Body, Art("Sign"));
                    string icon = Icons.Item("apple");
                    if (s.Kind == SiteKind.Pen)
                    {
                        var kind = data.Balance.Animals.Find(a => a.House == s.Ref);
                        icon = kind != null ? EconomyViews.AnimalIcon(kind.Id) : Icons.Animals;
                    }
                    Picture(Child("icon", v.Body, SignW / 2 - 1, SignH * 0.69f, 46, 46, Middle), ShapeSprites.Icon(icon), true);
                    break;
                }
                case SiteKind.Workshop:
                    // the mill is the chapter's own picture; the others are their own buildings
                    v.Body = Child("body", v.Root, mid - ShopFootX + ShopW / 2, -ShopFoot, ShopW, ShopH, Foot);
                    if (s.Ref != "mill")
                        v.BodyImage = Picture(v.Body, Art("Workshop_" + s.Ref));
                    break;
                default:
                    v.Body = Child("body", v.Root, mid, 0, s.W, s.H, Foot);
                    break;
            }
            v.BodyX = v.Body.anchoredPosition.x;
            v.BodyY = v.Body.anchoredPosition.y;

            // over it: a bubble (what it wants or gives), a tag (a price, a level); under it: a bar while it grows
            bool plain = s.Kind == SiteKind.Plot || s.Kind == SiteKind.Tree;
            v.Bubble = Child("bubble", v.Root, s.W / 2, BubbleY(s), plain ? 76 : 96, plain ? 76 : 110, Foot);
            if (!plain)
                v.BubbleBack = Picture(v.Bubble, Art("Bubble"));
            var icon2 = Child("icon", v.Bubble, plain ? 38 : 48, plain ? 38 : 63, plain ? 72 : 60, plain ? 72 : 60, Middle);
            v.BubbleIcon = Picture(icon2, null, true);
            v.Bubble.gameObject.SetActive(false);

            v.Bar = Child("bar", v.Root, s.W / 2, s.Kind == SiteKind.Plot ? -4 : -10, Mathf.Min(120, s.W * 0.7f), 18, Middle);
            Picture(v.Bar, Art("BarBack"));
            var fill = Child("fill", v.Bar, 0, 0, 10, 10, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            v.BarFill = Picture(fill, Art("BarFill"));
            v.BarFill.type = Image.Type.Filled;
            v.BarFill.fillMethod = Image.FillMethod.Horizontal;
            v.BarFill.color = UiView.ToColor(Tokens.Green);
            v.Bar.gameObject.SetActive(false);

            v.Tag = Child("tag", v.Root, s.W / 2, s.Kind == SiteKind.PlotOffer ? s.H * 0.3f : s.Kind == SiteKind.Workshop ? 10 : -22, 160, 52, Middle);
            var plate = Picture(v.Tag, ShapeSprites.RoundRect);
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / 28f;
            plate.color = UiView.ToColor(Tokens.Plate);
            v.TagIcon = Picture(Child("icon", v.Tag, 28, 26, 40, 40, Middle), null, true);
            var words = Child("text", v.Tag, 96, 26, 110, 46, Middle);
            v.TagText = FarmFx.Words(words, "", 34, Color.white);
            v.Tag.gameObject.SetActive(false);

            visuals.Add(v);
            byId[s.Id] = v;
        }

        // ------------------------------------------------------------------ state

        /// <summary>Where the bubble (or a ripe crop's icon) hangs over a site, from its foot.</summary>
        static float BubbleY(FarmSite s) =>
            s.Kind == SiteKind.Tree ? s.H * 0.5f : s.Kind == SiteKind.Plot ? s.H * 0.9f : s.Kind == SiteKind.Workshop ? s.H * 0.86f : s.H + 2;

        /// <summary>The picture of a growing thing as it stands: the crop's own plants by step, the tree by season.</summary>
        static string Growth(FarmSite s, SiteStatus st)
        {
            switch (s.Kind)
            {
                case SiteKind.Plot:
                    if (st.Stage <= 0 || st.Item == null)
                        return null;
                    // just sown: the first leaves, the same for every crop
                    if (st.Phase == SitePhase.Growing && st.Progress < 0.08f)
                        return "Seeded";
                    return "Crop_" + st.Item + "_" + st.Stage;
                case SiteKind.Tree:
                    return "Tree_" + s.Ref + (st.Phase == SitePhase.Ready ? "_ripe" : st.Progress >= 0.5f ? "_bloom" : "_leaf");
                case SiteKind.Animal:
                    return "Animal_" + s.Ref + (st.Phase == SitePhase.Ready ? "_ready" : "");
                default:
                    return null;
            }
        }

        /// <summary>Reads every site's status again; rebuilds when the farm itself changed (a plot bought, a house built).</summary>
        public void Sync(long now)
        {
            if (Map == null || Map.Signature != FarmSiteMap.SignatureOf(game))
            {
                Rebuild();
                return;
            }
            foreach (var v in visuals)
            {
                var st = FarmSiteMap.Status(v.Site, game, now);
                string look = Growth(v.Site, st);
                bool changed = !v.Known || look != v.Shown || st.Phase != v.Status.Phase || st.Stage != v.Status.Stage || st.Item != v.Status.Item || st.Count != v.Status.Count
                               || st.Attention != v.Status.Attention || st.Level != v.Status.Level;
                bool ripened = v.Known && st.Phase == SitePhase.Ready && v.Status.Phase == SitePhase.Growing;
                v.Status = st;
                if (st.Phase == SitePhase.Growing && v.BarFill != null)
                    v.BarFill.fillAmount = Mathf.Clamp01(st.Progress);
                if (!changed)
                    continue;
                v.Known = true;
                Show(v);
                if (ripened)
                    v.PopAt = Time.unscaledTime;
            }
        }

        void Show(Visual v)
        {
            var st = v.Status;
            var s = v.Site;
            bool growing = st.Phase == SitePhase.Growing;
            v.Bar.gameObject.SetActive(growing);

            v.Shown = Growth(s, st);
            if (s.Kind == SiteKind.Plot)
                Set(v.Crop, v.Shown != null ? Art(v.Shown) ?? Art("Seeded") : null);
            else if (v.Shown != null && v.BodyImage != null)
            {
                var look = Art(v.Shown);
                if (look != null)
                    Set(v.BodyImage, look);
                else if (s.Kind == SiteKind.Tree)
                    Set(v.BodyImage, FarmArt.For("Ch1/Objects/AppleTree", "tree", null, s.W, s.H));
            }

            // the bubble: a ripe crop, a product, the feed a hungry animal wants, what a workshop finished
            string icon = null;
            bool dim = false;
            switch (s.Kind)
            {
                case SiteKind.Plot:
                case SiteKind.Tree:
                    icon = st.Phase == SitePhase.Ready ? FarmSiteMap.StatusIcon(st) : null;
                    break;
                case SiteKind.Animal:
                    icon = st.Phase == SitePhase.Ready || st.Phase == SitePhase.Hungry ? FarmSiteMap.StatusIcon(st) : null;
                    dim = st.Phase == SitePhase.Hungry && !st.Attention;
                    break;
                case SiteKind.Workshop:
                    icon = st.Phase == SitePhase.Ready ? FarmSiteMap.StatusIcon(st) : null;
                    break;
                case SiteKind.Barn:
                    icon = st.Attention ? Icons.Tool("hammer") : null;
                    break;
                case SiteKind.Orders:
                    icon = st.Attention ? Icons.Orders : null;
                    break;
            }
            v.Bubble.gameObject.SetActive(icon != null);
            if (icon != null)
            {
                v.BubbleIcon.sprite = ShapeSprites.Icon(icon);
                v.BubbleIcon.color = dim ? new Color(1, 1, 1, 0.55f) : Color.white;
                if (v.BubbleBack != null)
                    v.BubbleBack.color = dim ? new Color(0.85f, 0.85f, 0.85f, 0.9f) : Color.white;
            }
            v.Bubble.localScale = Vector3.one;
            v.Bubble.localRotation = Quaternion.identity;

            // the tag: a price to buy or build, a level for what is closed
            bool offer = st.Phase == SitePhase.Offer, locked = st.Phase == SitePhase.Locked;
            v.Tag.gameObject.SetActive(offer || locked);
            if (offer || locked)
            {
                v.TagIcon.sprite = ShapeSprites.Icon(locked ? Icons.Lock : Icons.Coin);
                v.TagText.text = locked ? "Ур. " + st.Level : st.Count <= 0 ? "Даром" : Format.Amount(st.Count);
            }
            if (v.BodyImage != null && (s.Kind == SiteKind.Workshop || s.Kind == SiteKind.PlotOffer))
                v.BodyImage.color = new Color(1, 1, 1, locked ? 0.45f : offer && s.Kind == SiteKind.Workshop ? 0.7f : 1);
        }

        // ------------------------------------------------------------------ what the fingers do to a site

        Visual Find(FarmSite s) => s != null && byId.TryGetValue(s.Id, out var v) ? v : null;

        /// <summary>A finger is on it: it sinks a little.</summary>
        public void Press(FarmSite s)
        {
            var v = Find(s);
            if (v != null)
                v.ScaleTarget = 0.93f;
        }

        /// <summary>The finger left: it springs back.</summary>
        public void Release(FarmSite s)
        {
            var v = Find(s);
            if (v != null)
                v.ScaleTarget = 1;
        }

        /// <summary>Something was done to it: a squash and a stretch.</summary>
        public void Pop(FarmSite s)
        {
            var v = Find(s);
            if (v != null)
                v.PopAt = Time.unscaledTime;
        }

        /// <summary>It cannot do what was asked.</summary>
        public void Shake(FarmSite s)
        {
            var v = Find(s);
            if (v != null)
                FarmFx.Shake(v.Body, 10);
        }

        /// <summary>The site whose menu is open stands in a pulsing ring; null takes the ring away.</summary>
        public void Select(FarmSite s)
        {
            selected = Find(s);
            if (ring == null)
                return;
            ring.gameObject.SetActive(selected != null);
            if (selected == null)
                return;
            // the ring lies on the ground plane: as wide as the thing, with the plane's slope
            float w = Mathf.Max(190, s.W * 1.25f);
            ring.sizeDelta = new Vector2(w, w * 140f / 220f);
            ring.anchoredPosition = new Vector2(s.X, s.Y + (s.Diamond ? s.H / 2 : w * 0.12f));
            // under the site itself, over what stands behind it
            ring.SetSiblingIndex(Mathf.Max(0, selected.Root.GetSiblingIndex() - (ring.GetSiblingIndex() < selected.Root.GetSiblingIndex() ? 1 : 0)));
        }

        /// <summary>The middle of a site in world space (where a harvest flies from).</summary>
        public Vector3 WorldPoint(FarmSite s) => world.WorldPoint(new Vector2(s.X, s.Y + s.H * 0.55f));

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float t = Time.unscaledTime, dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            for (int i = 0; i < visuals.Count; i++)
            {
                var v = visuals[i];
                var st = v.Status;

                // press and pop: a quick sink, a springy return, a squash and stretch when something happened
                v.Scale += (v.ScaleTarget - v.Scale) * Mathf.Min(1, dt * (v.ScaleTarget < v.Scale ? 30 : 14));
                float sx = v.Scale, sy = v.Scale;
                float k = (t - v.PopAt) / 0.38f;
                if (k >= 0 && k < 1)
                {
                    float c = Mathf.Sin(k * Mathf.PI * 2) * (1 - k) * 0.2f;
                    sx *= 1 + c;
                    sy *= 1 - c;
                }
                // animals breathe; now and then one hops
                float lift = 0;
                if (v.Site.Kind == SiteKind.Animal)
                {
                    sy *= 1 + 0.025f * Mathf.Sin(t * 2.2f + v.Phase);
                    float hop = Mathf.Sin(t * 0.7f + v.Phase * 3);
                    if (hop > 0.93f)
                        lift = (hop - 0.93f) / 0.07f * 14;
                }
                v.Body.localScale = new Vector3(sx, sy, 1);
                v.Body.anchoredPosition = new Vector2(v.BodyX, v.BodyY + lift);
                if (v.Crop != null)
                    v.Crop.rectTransform.localScale = new Vector3(sx, sy, 1);

                // plants and trees sway in the wind
                if (v.Crop != null && st.Stage > 0)
                    v.Crop.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f + v.Phase) * (0.5f + st.Stage * 0.35f));
                else if (v.Site.Kind == SiteKind.Tree)
                    v.Body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.9f + v.Phase) * 0.7f);

                // what is ready bounces
                if (v.Bubble.gameObject.activeSelf)
                {
                    float b = st.Attention ? Mathf.Abs(Mathf.Sin(t * 3.2f + v.Phase)) : 0;
                    var p = v.Bubble.anchoredPosition;
                    v.Bubble.anchoredPosition = new Vector2(p.x, BubbleY(v.Site) + b * 14);
                    v.Bubble.localScale = new Vector3(1 + (1 - b) * 0.06f * (st.Attention ? 1 : 0), 1 - (1 - b) * 0.06f * (st.Attention ? 1 : 0), 1);
                }
            }
            if (ring != null && selected != null)
            {
                float pulse = 1 + 0.05f * Mathf.Sin(t * 5);
                ring.localScale = new Vector3(pulse, pulse, 1);
            }
        }
    }
}
