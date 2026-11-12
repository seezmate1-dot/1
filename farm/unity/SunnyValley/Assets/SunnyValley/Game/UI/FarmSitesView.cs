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
    /// The working farm on the map (docs/FARM_UX.md §4, §6): every site of FarmSiteMap as a living thing. A plot is
    /// soil with a plant that grows in three steps and sways; ripe, its crop bobs over it. Animals breathe and hop;
    /// a hungry one shows its feed in a bubble, one with a product bounces it. A workshop shows what is ready or a
    /// bar while it works; what can be bought or built wears its price, what is closed its level. Growing things
    /// have a thin bar under them. A pressed site sinks a little and springs back, the selected one stands in a ring.
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
        }

        FarmWorld world;
        MetaGame game;
        RectTransform layer;
        RectTransform ring;
        readonly List<Visual> visuals = new List<Visual>();
        readonly Dictionary<string, Visual> byId = new Dictionary<string, Visual>();
        Visual selected;

        public FarmSiteMap Map { get; private set; }

        static readonly Dictionary<string, Color32> WorkshopTint = new Dictionary<string, Color32>
        {
            { "bakery", new Color32(250, 200, 130, 255) }, { "dairy", new Color32(190, 226, 248, 255) }, { "toolshed", new Color32(214, 176, 132, 255) },
            { "jam", new Color32(248, 164, 184, 255) }, { "textile", new Color32(204, 186, 244, 255) }, { "sugar_mill", new Color32(250, 244, 196, 255) },
        };

        static readonly Dictionary<string, Color32> TreeTint = new Dictionary<string, Color32>
        {
            { "pear_tree", new Color32(232, 244, 170, 255) }, { "cherry_tree", new Color32(255, 214, 224, 255) },
        };

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

        static Image Picture(RectTransform rt, Sprite sprite, bool keepAspect = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = keepAspect;
            img.raycastTarget = false;
            return img;
        }

        static readonly Vector2 Foot = new Vector2(0.5f, 0);
        static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

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

            // the ground of the pens chapter 1 has no place for, under everything
            foreach (var house in game.Data.Balance.AnimalHouses)
            {
                if (house.Id == "coop")
                    continue;
                var c = FarmSiteMap.PenPoint(FarmArt.Layout, house.Id);
                var pad = Picture(Child("pad " + house.Id, layer, c.X + 40, c.Y - 30, 520, 310, Foot), FarmArt.Painted("Farm/PenPad"));
                pad.color = game.HasAnimalHouse(house.Id) ? Color.white : new Color(1, 1, 1, 0.45f);
            }

            // far things first: the near ones stand in front
            var order = new List<FarmSite>(Map.Sites);
            order.Sort((a, b) => b.Y.CompareTo(a.Y));
            foreach (var s in order)
                Make(s);

            var r = Child("ring", layer, 0, 0, 100, 60, Middle);
            Picture(r, FarmArt.Painted("Farm/Ring"), false);
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
            v.Body = Child("body", v.Root, s.W / 2, 0, s.W, s.H, Foot);
            var data = game.Data;
            switch (s.Kind)
            {
                case SiteKind.Plot:
                    v.BodyImage = Picture(v.Body, FarmArt.Painted("Farm/Soil"), false);
                    v.Crop = Picture(Child("crop", v.Body, s.W / 2, s.H * 0.22f, s.W * 0.92f, s.H * 1.05f, Foot), null);
                    v.Crop.gameObject.SetActive(false);
                    break;
                case SiteKind.PlotOffer:
                    v.BodyImage = Picture(v.Body, FarmArt.Painted("Farm/SoilOffer"), false);
                    Picture(Child("plus", v.Body, s.W / 2, s.H / 2, 64, 64, Middle), ShapeSprites.Icon(Icons.Plus));
                    break;
                case SiteKind.Tree:
                    v.BodyImage = Picture(v.Body, FarmArt.For("Ch1/Objects/AppleTree", "tree", null, s.W, s.H));
                    if (TreeTint.TryGetValue(s.Ref ?? "", out var tint))
                        v.BodyImage.color = tint;
                    break;
                case SiteKind.Animal:
                    v.BodyImage = Picture(v.Body, ShapeSprites.Icon(EconomyViews.AnimalIcon(s.Ref)));
                    break;
                case SiteKind.Pen:
                case SiteKind.Nursery:
                {
                    v.BodyImage = Picture(v.Body, FarmArt.Painted("Farm/PenSign"));
                    string icon = Icons.Item("apple");
                    if (s.Kind == SiteKind.Pen)
                    {
                        var kind = data.Balance.Animals.Find(a => a.House == s.Ref);
                        icon = kind != null ? EconomyViews.AnimalIcon(kind.Id) : Icons.Animals;
                    }
                    Picture(Child("icon", v.Body, s.W / 2, s.H * 0.66f, s.W * 0.5f, s.W * 0.5f, Middle), ShapeSprites.Icon(icon));
                    break;
                }
                case SiteKind.Workshop:
                    if (s.Ref != "mill")
                    {
                        v.BodyImage = Picture(v.Body, FarmArt.Painted("Farm/Workshop"));
                        if (WorkshopTint.TryGetValue(s.Ref ?? "", out var wt))
                            v.BodyImage.color = wt;
                        // what it makes, over the door
                        if (data.Workshops.TryGetValue(s.Ref ?? "", out var def) && def.Recipes.Count > 0)
                            Picture(Child("makes", v.Body, s.W / 2, s.H * 0.5f, 84, 84, Middle), ShapeSprites.Icon(EconomyViews.IconOf(def.Recipes[0].Output)));
                    }
                    break;
            }

            // over it: a bubble (what it wants or gives), a tag (a price, a level); under it: a bar while it grows
            bool plain = s.Kind == SiteKind.Plot || s.Kind == SiteKind.Tree;
            float top = s.Kind == SiteKind.Tree ? s.H * 0.62f : s.Kind == SiteKind.Plot ? s.H * 0.95f : s.H + 6;
            v.Bubble = Child("bubble", v.Root, s.W / 2, top, plain ? 92 : 104, plain ? 92 : 118, Foot);
            if (!plain)
                v.BubbleBack = Picture(v.Bubble, FarmArt.Painted("Farm/Bubble"));
            var icon2 = Child("icon", v.Bubble, plain ? 46 : 52, plain ? 46 : 68, plain ? 88 : 66, plain ? 88 : 66, Middle);
            v.BubbleIcon = Picture(icon2, null);
            v.Bubble.gameObject.SetActive(false);

            v.Bar = Child("bar", v.Root, s.W / 2, s.Kind == SiteKind.Plot ? 4 : -8, Mathf.Min(150, s.W * 0.8f), 20, Middle);
            Picture(v.Bar, FarmArt.Painted("Farm/BarBack"), false);
            var fill = Child("fill", v.Bar, 0, 0, 10, 10, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            v.BarFill = Picture(fill, FarmArt.Painted("Farm/BarFill"), false);
            v.BarFill.type = Image.Type.Filled;
            v.BarFill.fillMethod = Image.FillMethod.Horizontal;
            v.BarFill.color = UiView.ToColor(Tokens.Green);
            v.Bar.gameObject.SetActive(false);

            v.Tag = Child("tag", v.Root, s.W / 2, s.Kind == SiteKind.PlotOffer ? s.H * 0.2f : s.H * 0.14f, 170, 56, Middle);
            var plate = Picture(v.Tag, ShapeSprites.RoundRect, false);
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / 28f;
            plate.color = UiView.ToColor(Tokens.Plate);
            v.TagIcon = Picture(Child("icon", v.Tag, 30, 28, 44, 44, Middle), null);
            var words = Child("text", v.Tag, 100, 28, 120, 50, Middle);
            v.TagText = FarmFx.Words(words, "", 34, Color.white);
            v.Tag.gameObject.SetActive(false);

            visuals.Add(v);
            byId[s.Id] = v;
        }

        // ------------------------------------------------------------------ state

        static Sprite PlantSprite(int stage) => FarmArt.Painted(stage <= 1 ? "Farm/Plant1" : stage == 2 ? "Farm/Plant2" : "Farm/Plant3");

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
                bool changed = !v.Known || st.Phase != v.Status.Phase || st.Stage != v.Status.Stage || st.Item != v.Status.Item || st.Count != v.Status.Count
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

            if (v.Crop != null)
            {
                v.Crop.gameObject.SetActive(st.Stage > 0);
                if (st.Stage > 0)
                    v.Crop.sprite = PlantSprite(st.Stage);
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
            {
                var c = v.BodyImage.color;
                v.BodyImage.color = new Color(c.r, c.g, c.b, locked ? 0.5f : offer && s.Kind == SiteKind.Workshop ? 0.75f : 1);
            }
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
            float w = Mathf.Max(170, s.W * 1.3f);
            ring.sizeDelta = new Vector2(w, Mathf.Max(110, Mathf.Min(s.H, w * 0.62f)));
            ring.anchoredPosition = new Vector2(s.X, s.Y + ring.sizeDelta.y * 0.36f);
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
                v.Body.anchoredPosition = new Vector2(v.Site.W / 2, lift);

                // plants and trees sway in the wind
                if (v.Crop != null && st.Stage > 0)
                    v.Crop.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f + v.Phase) * (1.2f + st.Stage * 0.5f));
                else if (v.Site.Kind == SiteKind.Tree)
                    v.Body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.9f + v.Phase) * 0.8f);

                // what is ready bounces
                if (v.Bubble.gameObject.activeSelf)
                {
                    float b = st.Attention ? Mathf.Abs(Mathf.Sin(t * 3.2f + v.Phase)) : 0;
                    var p = v.Bubble.anchoredPosition;
                    float top = v.Site.Kind == SiteKind.Tree ? v.Site.H * 0.62f : v.Site.Kind == SiteKind.Plot ? v.Site.H * 0.95f : v.Site.H + 6;
                    v.Bubble.anchoredPosition = new Vector2(p.x, top + b * 16);
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
