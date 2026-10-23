using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>What the farm scene needs to know about the player's chapter 1 (FarmScreen answers from MetaGame).</summary>
    public interface IFarmState
    {
        bool Done(string taskId);
        string Choice(string taskId);
        bool ZoneOpen(string zoneId);
        /// <summary>«Двор»; and for a closed zone, what opens it («После «Дома»»).</summary>
        string ZoneName(string zoneId);
        string LockText(string zoneId);
        /// <summary>The zone the camera starts on: the one being restored.</summary>
        string CurrentZone();
    }

    /// <summary>
    /// The chapter 1 farm under the interface, Gardenscapes-style: a big map of the eight zones (Resources/Meta/
    /// chapter1_layout.json) moved with one finger and zoomed with two (or the mouse wheel), never past its edges.
    /// Each zone is its background picture with the objects standing on it in draw order: what the finished tasks
    /// built (the chosen look), the broken things and junk of the tasks not done yet. Closed zones lie under fog and
    /// overgrowth with a sign saying what opens them. FarmScreen drives the show of a task: the camera flies to the
    /// spot, a puff of dust, the old thing goes and the new one pops up; a tap on an object with looks offers
    /// «Сменить вид». Pictures come from FarmArt: painted art when it exists, placeholders until then.
    /// </summary>
    public sealed class FarmWorld : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public const float MinZoom = 0.35f;
        public const float MaxZoom = 1.6f;
        public const float StartZoom = 0.8f;

        /// <summary>Set by the farm screen before the layout is built.</summary>
        public static IFarmState Source;

        /// <summary>A tap on an object with looks: its task.</summary>
        public event Action<string> ObjectTapped;

        /// <summary>A tap on a closed zone's fog.</summary>
        public event Action<string> LockedTapped;

        RectTransform content;
        RectTransform view;
        Vector2 mapSize;
        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        float zoom = StartZoom;

        sealed class Placed
        {
            public SpriteDef Def;
            public ZoneLayoutDef Zone;
            public Image Image;
            public string ArtShown;
        }

        readonly List<Placed> placed = new List<Placed>();
        readonly Dictionary<string, RectTransform> zoneRects = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, RectTransform> fogs = new Dictionary<string, RectTransform>();

        // the camera outlives a rebuild of the screen (rotation, safe area, coming back from a level)
        static bool hasCamera;
        static Vector2 savedPosition;
        static float savedZoom = StartZoom;

        static FarmLayoutDef Layout => FarmArt.Layout;

        public void Build(UiNode node)
        {
            view = (RectTransform)transform;
            var bg = gameObject.AddComponent<Image>();
            bg.color = UiView.ToColor(node.Color);
            bg.raycastTarget = true; // drags on empty farm pan it; buttons above get their taps first
            gameObject.AddComponent<RectMask2D>();

            var map = Layout.Map;
            mapSize = new Vector2(map.W, map.H);
            content = Rect("map", view, 0, 0, map.W, map.H);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            var ground = content.gameObject.AddComponent<Image>();
            ground.color = FarmArt.Color(map.Ground);
            ground.raycastTarget = false;

            // far zones first: a tall building of a near zone may stand over the one behind it
            var zones = new List<ZoneLayoutDef>(Layout.Zones);
            zones.Sort((a, b) => b.Y.CompareTo(a.Y));
            foreach (var z in zones)
                BuildZone(z);
            foreach (var z in zones)
                BuildFog(z);
            Refresh();

            if (hasCamera)
            {
                zoom = savedZoom;
                content.anchoredPosition = savedPosition;
            }
            else
            {
                zoom = StartZoom;
                content.anchoredPosition = CameraFor(ZoneCenter(Source?.CurrentZone()), zoom);
            }
            Apply();
        }

        // ------------------------------------------------------------------ building

        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>A rectangle in its parent's lower-left coordinates: (x, y) is its pivot point.</summary>
        static RectTransform Placed01(string name, Transform parent, float x, float y, float w, float h, Vector2 pivot)
        {
            var rt = Rect(name, parent, 0, 0, w, h);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = pivot;
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        void BuildZone(ZoneLayoutDef z)
        {
            var zr = Placed01("zone " + z.Id, content, z.X, z.Y, z.W, z.H, Vector2.zero);
            zoneRects[z.Id] = zr;
            var painted = FarmArt.Painted(z.Background);
            if (painted != null)
            {
                var img = zr.gameObject.AddComponent<Image>();
                img.sprite = painted;
                img.raycastTarget = false;
            }
            else
            {
                foreach (var s in z.Ph)
                {
                    var rt = Placed01("ground", zr, s.X, s.Y, s.W, s.H, new Vector2(0.5f, 0.5f));
                    var img = rt.gameObject.AddComponent<Image>();
                    img.color = FarmArt.Color(s.Color);
                    img.raycastTarget = false;
                    if (s.Shape == "ellipse")
                        img.sprite = ShapeSprites.Circle;
                }
            }
            var sprites = new List<SpriteDef>(z.Sprites);
            sprites.Sort((a, b) => a.Z.CompareTo(b.Z));
            foreach (var s in sprites)
            {
                var rt = Placed01(s.Id, zr, s.X, s.Y, s.W, s.H, new Vector2(0.5f, 0));
                var img = rt.gameObject.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = s.Tap;
                var p = new Placed { Def = s, Zone = z, Image = img };
                if (s.Tap)
                {
                    var tap = rt.gameObject.AddComponent<FarmTap>();
                    tap.World = this;
                    string task = s.Task;
                    tap.Tapped = () => ObjectTapped?.Invoke(task);
                }
                placed.Add(p);
            }
        }

        void BuildFog(ZoneLayoutDef z)
        {
            if (Source == null || Source.ZoneOpen(z.Id) || !IsChapterZone(z.Id))
                return;
            var zr = zoneRects[z.Id];
            // over the zone and a little past its edges, drawn over everything nearer as well
            var fog = Placed01("fog " + z.Id, content, z.X - 60, z.Y - 60, z.W + 120, z.H + 120, Vector2.zero);
            fogs[z.Id] = fog;
            var veil = fog.gameObject.AddComponent<Image>();
            veil.sprite = ShapeSprites.RoundRect;
            veil.type = Image.Type.Sliced;
            veil.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / 160f;
            veil.color = new Color32(46, 74, 42, 170);
            veil.raycastTarget = true;
            var tap = fog.gameObject.AddComponent<FarmTap>();
            tap.World = this;
            string id = z.Id;
            tap.Tapped = () => LockedTapped?.Invoke(id);
            var rng = new System.Random(z.Id.GetHashCode());
            for (int i = 0; i < 9; i++)
            {
                float w = 520 + (float)rng.NextDouble() * 260, h = w * 0.62f;
                var c = Placed01("cloud", fog, 60 + (float)rng.NextDouble() * z.W, 60 + (float)rng.NextDouble() * z.H, w, h, new Vector2(0.5f, 0.5f));
                var img = c.gameObject.AddComponent<Image>();
                img.sprite = FarmArt.Cloud(i);
                img.color = new Color(1, 1, 1, 0.82f);
                img.raycastTarget = false;
            }
            Sign(fog, (z.W + 120) / 2, (z.H + 120) / 2, Source.ZoneName(z.Id), Source.LockText(z.Id));
        }

        static bool IsChapterZone(string id) => id != "meadow";

        static void Sign(Transform parent, float x, float y, string title, string hint)
        {
            var plate = Placed01("sign", parent, x, y, 680, 220, new Vector2(0.5f, 0.5f));
            var img = plate.gameObject.AddComponent<Image>();
            img.sprite = ShapeSprites.RoundRect;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / 44f;
            img.color = UiView.ToColor(Tokens.Wood);
            img.raycastTarget = false;
            var icon = Placed01("lock", plate, 90, 110, 120, 120, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<Image>();
            icon.sprite = ShapeSprites.Icon(Icons.Lock);
            icon.raycastTarget = false;
            Label(plate, 170, 120, 490, 80, title, 64, Tokens.TextDark);
            Label(plate, 170, 50, 490, 60, hint, 38, Tokens.TextMuted);
        }

        static void Label(Transform parent, float x, float y, float w, float h, string text, int size, uint color)
        {
            var rt = Placed01("label", parent, x, y, w, h, new Vector2(0, 0.5f));
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiView.Font;
            t.fontSize = size;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 20;
            t.resizeTextMaxSize = size;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = UiView.ToColor(color);
            t.text = text ?? "";
            t.raycastTarget = false;
        }

        // ------------------------------------------------------------------ state

        bool Visible(SpriteDef s) => Source == null ? !s.IsBefore : s.Visible(Source.Done);

        string ArtOf(SpriteDef s) => s.ArtFor(Source?.Choice(s.Task));

        void Show(Placed p, string art)
        {
            if (p.ArtShown == art)
                return;
            p.ArtShown = art;
            p.Image.sprite = FarmArt.For(art, p.Def.Ph, p.Def.Color, p.Def.W, p.Def.H);
        }

        /// <summary>Every object as the chapter stands now, without animation.</summary>
        public void Refresh()
        {
            foreach (var p in placed)
            {
                bool on = Visible(p.Def);
                if (on)
                    Show(p, ArtOf(p.Def));
                p.Image.gameObject.SetActive(on);
                p.Image.rectTransform.localScale = Vector3.one;
            }
        }

        /// <summary>The middle of what a task changes, on the map.</summary>
        public Vector2 TaskPoint(string taskId)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            foreach (var p in placed)
            {
                if (p.Def.Task != taskId)
                    continue;
                sum += new Vector2(p.Zone.X + p.Def.X, p.Zone.Y + p.Def.Y + p.Def.H / 2);
                n++;
            }
            return n > 0 ? sum / n : ZoneCenter(null);
        }

        public Vector2 ZoneCenter(string zoneId)
        {
            var z = zoneId != null ? Layout.Zone(zoneId) : null;
            if (z == null)
                return mapSize / 2;
            var (x, y) = z.CameraPoint;
            return new Vector2(x, y);
        }

        /// <summary>A map point in world space (where a flying star aims).</summary>
        public Vector3 WorldPoint(Vector2 map) => content.TransformPoint(map - mapSize / 2);

        Vector2 CameraFor(Vector2 map, float z) => -(map - mapSize / 2) * z;

        /// <summary>Flies the camera to a map point (and a zoom), then calls done.</summary>
        public void Focus(Vector2 map, float duration = 0.5f, float targetZoom = -1, Action done = null)
        {
            float z0 = zoom, z1 = targetZoom > 0 ? targetZoom : Mathf.Max(zoom, 0.75f);
            Vector2 from = content.anchoredPosition;
            Tweener.Run(content, "focus", duration, Easing.Kind.OutCubic, k =>
            {
                zoom = Mathf.Lerp(z0, z1, k);
                content.anchoredPosition = Vector2.Lerp(from, CameraFor(map, zoom), k);
                Apply();
            }, done);
        }

        /// <summary>The show of a finished task: dust puffs where its objects stand, the old ones shrink away, the new pop up.</summary>
        public float PlayTask(string taskId)
        {
            int puffs = 0;
            foreach (var p in placed)
            {
                if (p.Def.Task != taskId && p.Def.Until != taskId)
                    continue;
                bool on = Visible(p.Def);
                bool was = p.Image.gameObject.activeSelf;
                if (on == was && (!on || p.ArtShown == ArtOf(p.Def)))
                    continue;
                if (puffs < 6)
                {
                    Puff(new Vector2(p.Zone.X + p.Def.X, p.Zone.Y + p.Def.Y + p.Def.H * 0.4f), Mathf.Clamp(Mathf.Max(p.Def.W, p.Def.H) * 0.8f, 220, 700), puffs * 0.06f);
                    puffs++;
                }
                var rt = p.Image.rectTransform;
                if (on)
                {
                    Show(p, ArtOf(p.Def));
                    rt.localScale = Vector3.zero;
                    p.Image.gameObject.SetActive(true);
                    Tweener.Scale(rt, 0, 1, 0.45f, Easing.Kind.OutBack, null, 0.3f);
                }
                else
                {
                    var img = p.Image;
                    Tweener.Scale(rt, 1, 0.4f, 0.3f, Easing.Kind.InBack, () =>
                    {
                        if (img != null)
                        {
                            img.gameObject.SetActive(false);
                            img.rectTransform.localScale = Vector3.one;
                        }
                    }, 0.15f);
                }
            }
            return 0.9f;
        }

        /// <summary>A puff of dust at a map point: a few clouds swell and fade.</summary>
        public void Puff(Vector2 map, float size, float delay = 0)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72 * Mathf.Deg2Rad;
                var rt = Rect("dust", content, 0, 0, size * 0.55f, size * 0.55f);
                rt.anchoredPosition = map - mapSize / 2 + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * size * 0.22f;
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = FarmArt.Dust();
                img.raycastTarget = false;
                img.color = new Color(1, 1, 1, 0);
                Vector2 from = rt.anchoredPosition, to = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f + 0.3f) * size * 0.25f;
                var go = rt.gameObject;
                Tweener.Run(rt, "puff", 0.85f, Easing.Kind.OutQuad, k =>
                {
                    rt.anchoredPosition = Vector2.Lerp(from, to, k);
                    rt.localScale = Vector3.one * (0.4f + 0.9f * k);
                    img.color = new Color(1, 1, 1, k < 0.25f ? k * 4 : (1 - k) / 0.75f);
                }, () => Destroy(go), delay);
            }
        }

        /// <summary>A newly opened zone: the fog's clouds drift apart and fade.</summary>
        public void OpenZone(string zoneId)
        {
            if (!fogs.TryGetValue(zoneId, out var fog) || fog == null)
                return;
            fogs.Remove(zoneId);
            var center = fog.rect.size / 2;
            foreach (Transform child in fog)
            {
                var rt = (RectTransform)child;
                var img = rt.GetComponent<Image>();
                Vector2 from = rt.anchoredPosition, dir = (from - center).normalized;
                Tweener.Run(rt, "clear", 1.2f, Easing.Kind.InQuad, k =>
                {
                    rt.anchoredPosition = from + dir * 900 * k;
                    if (img != null)
                        img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Min(img.color.a, 1 - k));
                });
            }
            var veil = fog.GetComponent<Image>();
            veil.raycastTarget = false;
            var go = fog.gameObject;
            Tweener.Fade(veil, veil.color.a, 0, 1.2f, Easing.Kind.InQuad, () => Destroy(go));
        }

        // ------------------------------------------------------------------ fingers

        public void OnPointerDown(PointerEventData e) => pointers[e.pointerId] = e.position;

        public void OnPointerUp(PointerEventData e) => pointers.Remove(e.pointerId);

        public void OnEndDrag(PointerEventData e) => pointers.Remove(e.pointerId);

        public void OnDrag(PointerEventData e)
        {
            if (content == null)
                return;
            Tweener.Stop(content, "focus");
            float scale = CanvasScale();
            if (pointers.Count >= 2 && pointers.ContainsKey(e.pointerId))
            {
                // pinch: the distance between two fingers zooms around their midpoint
                Vector2 other = Vector2.zero;
                foreach (var kv in pointers)
                {
                    if (kv.Key != e.pointerId)
                    {
                        other = kv.Value;
                        break;
                    }
                }
                Vector2 before = pointers[e.pointerId];
                float d0 = Vector2.Distance(before, other), d1 = Vector2.Distance(e.position, other);
                pointers[e.pointerId] = e.position;
                if (d0 > 10)
                    ZoomAt((e.position + other) / 2, d1 / d0);
                return;
            }
            pointers[e.pointerId] = e.position;
            content.anchoredPosition += e.delta / scale;
            Apply();
        }

        public void OnScroll(PointerEventData e) => ZoomAt(e.position, 1 + e.scrollDelta.y * 0.1f);

        void ZoomAt(Vector2 screenPoint, float factor)
        {
            float next = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(next, zoom))
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screenPoint, Camera(), out var local);
            // keep the point under the fingers where it is
            Vector2 p = content.anchoredPosition;
            content.anchoredPosition = local + (p - local) * (next / zoom);
            zoom = next;
            Apply();
        }

        float CanvasScale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.scaleFactor : 1;
        }

        Camera Camera()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        /// <summary>Applies the zoom and keeps the map covering the whole view.</summary>
        void Apply()
        {
            var size = view.rect.size;
            float minZoom = Mathf.Max(size.x / mapSize.x, size.y / mapSize.y);
            zoom = Mathf.Clamp(zoom, Mathf.Max(MinZoom, minZoom), MaxZoom);
            content.localScale = Vector3.one * zoom;
            float mx = Mathf.Max(0, (mapSize.x * zoom - size.x) / 2), my = Mathf.Max(0, (mapSize.y * zoom - size.y) / 2);
            var p = content.anchoredPosition;
            content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
            hasCamera = true;
            savedPosition = content.anchoredPosition;
            savedZoom = zoom;
        }

        void OnRectTransformDimensionsChange()
        {
            if (content != null)
                Apply();
        }
    }

    /// <summary>
    /// A tap on something on the farm (an object with looks, a closed zone). It takes the press itself, so a click
    /// reaches it on every uGUI version, and hands the press and release to the farm, so a drag that starts on it
    /// still pans the map (a drag cancels the click).
    /// </summary>
    public sealed class FarmTap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public FarmWorld World;
        public Action Tapped;

        public void OnPointerDown(PointerEventData e) => World?.OnPointerDown(e);

        public void OnPointerUp(PointerEventData e) => World?.OnPointerUp(e);

        public void OnPointerClick(PointerEventData e)
        {
            if (!e.dragging)
                Tapped?.Invoke();
        }
    }
}
