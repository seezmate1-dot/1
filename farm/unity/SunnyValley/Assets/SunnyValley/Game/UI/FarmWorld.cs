using System;
using System.Collections.Generic;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Farm;
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
    /// chapter1_layout.json) moved with one finger and zoomed with two (or the mouse wheel). The fingers are read by
    /// TouchGestures and the map is moved by FarmCamera (Game/Economy/Farm, plain C#, docs/FARM_UX.md §3): a tap is
    /// told from a drag by a 10 dp slop, a flick glides, the edges are rubber, the zoom is clamped, a double tap
    /// zooms in and back. A tap goes first to the working farm's sites (plots, animals, workshops: Sites), then to
    /// the chapter's objects with looks and the fog.
    /// Each zone is its background picture with the objects standing on it in draw order: what the finished tasks
    /// built (the chosen look), the broken things and junk of the tasks not done yet. Closed zones lie under fog and
    /// overgrowth with a sign saying what opens them. FarmScreen drives the show of a task: the camera flies to the
    /// spot, a puff of dust, the old thing goes and the new one pops up; a tap on an object with looks offers
    /// «Сменить вид». Pictures come from FarmArt: painted art when it exists, placeholders until then.
    /// </summary>
    public sealed class FarmWorld : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler,
        IEndDragHandler, IScrollHandler
    {
        public const float MinZoom = FarmCamera.MinZoom;
        public const float MaxZoom = FarmCamera.MaxZoom;
        public const float StartZoom = FarmCamera.StartZoom;

        /// <summary>Set by the farm screen before the layout is built.</summary>
        public static IFarmState Source;

        /// <summary>A tap on an object with looks: its task.</summary>
        public event Action<string> ObjectTapped;

        /// <summary>A tap on a closed zone's fog.</summary>
        public event Action<string> LockedTapped;

        /// <summary>The working farm's sites (chapter 2), or null: a finger finds them before anything else.</summary>
        public FarmSiteMap Sites;

        /// <summary>A tap on a site.</summary>
        public event Action<FarmSite> SiteTapped;

        /// <summary>A finger went down on a site (it shows itself pressed) and left it without a tap or with one.</summary>
        public event Action<FarmSite> SitePressed;
        public event Action<FarmSite> SiteReleased;

        /// <summary>A tap on nothing, or the map was grabbed: an open menu closes.</summary>
        public event Action GroundTouched;

        /// <summary>Art of the chapter that the working farm replaces with live things (the painted hens).</summary>
        public static readonly HashSet<string> HiddenArt = new HashSet<string>();

        RectTransform content;
        RectTransform view;
        Vector2 mapSize;
        FarmCamera cam;
        readonly TouchGestures gestures = new TouchGestures();
        FarmTap pressedTap;
        FarmSite pressedSite;
        float zoom = StartZoom;

        /// <summary>The map itself: what stands on it is its child, in map units from its lower left corner.</summary>
        public RectTransform Content => content;
        public Vector2 MapSize => mapSize;
        public FarmCamera Cam => cam;
        public float Zoom => zoom;

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
            // the grass of the map goes on past its edges (the camera's insets let the map out from under the HUD)
            bg.color = FarmArt.Color(Layout.Map.Ground);
            bg.raycastTarget = true; // drags on empty farm pan it; buttons above get their taps first
            gameObject.AddComponent<RectMask2D>();

            var map = Layout.Map;
            mapSize = new Vector2(map.W, map.H);
            cam = new FarmCamera(map.W, map.H);
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

            var size = view.rect.size;
            cam.ViewW = size.x;
            cam.ViewH = size.y;
            if (hasCamera)
            {
                cam.Zoom = savedZoom;
                cam.Position = new P2(savedPosition.x, savedPosition.y);
            }
            else
            {
                cam.Zoom = StartZoom;
                cam.Position = cam.Target(ToP(ZoneCenter(Source?.CurrentZone())), StartZoom);
            }
            Apply();
        }

        static P2 ToP(Vector2 v) => new P2(v.x, v.y);

        static Vector2 ToV(P2 p) => new Vector2(p.X, p.Y);

        /// <summary>
        /// The parts of the view the HUD covers (canvas units from each edge): the map may slide out from under
        /// them, and a camera flight ends in the middle of what is left.
        /// </summary>
        public void SetInsets(float left, float right, float top, float bottom)
        {
            cam.InsetLeft = Mathf.Max(0, left);
            cam.InsetRight = Mathf.Max(0, right);
            cam.InsetTop = Mathf.Max(0, top);
            cam.InsetBottom = Mathf.Max(0, bottom);
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

        bool Visible(SpriteDef s) => (s.Art == null || !HiddenArt.Contains(s.Art)) && (Source == null ? !s.IsBefore : s.Visible(Source.Done));

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

        /// <summary>Flies the camera to a map point (and a zoom), then calls done. The point ends in the middle of what the HUD leaves free.</summary>
        public void Focus(Vector2 map, float duration = 0.5f, float targetZoom = -1, Action done = null)
        {
            float z1 = cam.ClampZoom(targetZoom > 0 ? targetZoom : Mathf.Max(cam.Zoom, 0.75f));
            FlyTo(cam.Target(ToP(map), z1), z1, duration, done);
        }

        void FlyTo(P2 to, float z1, float duration, Action done = null)
        {
            cam.Grab();
            float z0 = cam.Zoom;
            P2 from = cam.Position;
            // the map point in the middle of the free part travels in a straight line while the zoom changes
            P2 centre = cam.FreeCentre;
            P2 m0 = cam.ViewToMap(centre);
            cam.Position = to;
            cam.Zoom = z1;
            P2 m1 = cam.ViewToMap(centre);
            cam.Position = from;
            cam.Zoom = z0;
            Tweener.Run(content, "focus", duration, Easing.Kind.OutCubic, k =>
            {
                cam.Zoom = Mathf.Lerp(z0, z1, k);
                cam.Position = cam.PositionFor(m0 + (m1 - m0) * k, cam.Zoom, centre);
                if (k >= 1)
                    cam.Position = to;
                Apply();
            }, done);
        }

        /// <summary>
        /// Brings a map point into the comfortable middle of the view if it is near an edge or under the HUD (a
        /// tapped thing whose menu would not fit); does nothing when it already is. The zoom stays.
        /// </summary>
        public void Reveal(Vector2 map, float margin = 260, float duration = 0.35f)
        {
            P2 v = cam.MapToView(ToP(map));
            float l = -cam.ViewW / 2 + cam.InsetLeft + margin, r = cam.ViewW / 2 - cam.InsetRight - margin;
            float b = -cam.ViewH / 2 + cam.InsetBottom + margin, t = cam.ViewH / 2 - cam.InsetTop - margin * 1.6f;
            float dx = v.X < l ? l - v.X : v.X > r ? r - v.X : 0, dy = v.Y < b ? b - v.Y : v.Y > t ? t - v.Y : 0;
            if (Mathf.Abs(dx) < 1 && Mathf.Abs(dy) < 1)
                return;
            FlyTo(cam.ClampPosition(cam.Position + new P2(dx, dy), cam.Zoom), cam.Zoom, duration);
        }

        /// <summary>A screen point on the map (map units from its lower left corner).</summary>
        public Vector2 ScreenToMap(Vector2 screen) => ToV(cam.ViewToMap(ViewPoint(screen)));

        /// <summary>A map point in the view (canvas units from the view's centre).</summary>
        public Vector2 MapToView(Vector2 map) => ToV(cam.MapToView(ToP(map)));

        /// <summary>A screen point in the view (canvas units from the view's centre).</summary>
        public Vector2 ScreenToView(Vector2 screen) => ToV(ViewPoint(screen));

        /// <summary>Slides the map by a view delta inside its limits (a dragged tool near the edge).</summary>
        public void Slide(Vector2 delta)
        {
            cam.Slide(ToP(delta));
            Apply();
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

        /// <summary>A finger on the view, in canvas units from the view's centre (what TouchGestures and FarmCamera count in).</summary>
        P2 ViewPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, Camera(), out var local);
            local -= view.rect.center;
            return new P2(local.x, local.y);
        }

        static bool Counts(PointerEventData e) => e.pointerId >= 0 || e.button == PointerEventData.InputButton.Left;

        public void OnPointerDown(PointerEventData e) => Down(e, null);

        /// <summary>A press that landed on one of the farm's own tap targets (FarmTap hands it over).</summary>
        public void Down(PointerEventData e, FarmTap target)
        {
            if (content == null || !Counts(e))
                return;
            if (gestures.Fingers == 0)
                pressedTap = target;
            gestures.UnitsPerDp = UnitsPerDp();
            Handle(gestures.Down(e.pointerId, ViewPoint(e.position), Time.unscaledTime));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (content != null && Counts(e))
                Handle(gestures.Up(e.pointerId, ViewPoint(e.position), Time.unscaledTime));
        }

        /// <summary>The slop is ours (10 dp, TouchGestures), not the event system's few pixels.</summary>
        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnDrag(PointerEventData e)
        {
            if (content != null && Counts(e))
                Handle(gestures.Move(e.pointerId, ViewPoint(e.position), Time.unscaledTime));
        }

        public void OnEndDrag(PointerEventData e)
        {
            // normally the release came first and this finds nothing; a drag ended any other way lifts the finger here
            if (content != null && Counts(e))
                Handle(gestures.Up(e.pointerId, ViewPoint(e.position), Time.unscaledTime));
        }

        public void OnScroll(PointerEventData e)
        {
            if (content == null)
                return;
            Tweener.Stop(content, "focus");
            cam.ZoomAbout(ViewPoint(e.position), 1 + e.scrollDelta.y * 0.1f, false);
            Apply();
        }

        /// <summary>Canvas units in a dp: the slop is a physical distance, the same on every screen.</summary>
        float UnitsPerDp()
        {
            float dpi = Screen.dpi > 1 ? Screen.dpi : 480;
            return Mathf.Clamp(dpi / 160f / Mathf.Max(0.01f, CanvasScale()), 1.5f, 6f);
        }

        void Handle(IReadOnlyList<Gesture> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var g = events[i];
                switch (g.Kind)
                {
                    case GestureKind.Press:
                        Tweener.Stop(content, "focus");
                        cam.Grab();
                        pressedSite = Sites?.At(cam.ViewToMap(g.Point), cam.Zoom);
                        if (pressedSite != null)
                            SitePressed?.Invoke(pressedSite);
                        break;
                    case GestureKind.PressEnd:
                        Unpress();
                        break;
                    case GestureKind.Tap:
                        // a map caught on its way back from past an edge goes on home
                        cam.Release(new P2(0, 0));
                        Tap(g.Point);
                        break;
                    case GestureKind.LongPress:
                        cam.Release(new P2(0, 0));
                        break;
                    case GestureKind.DoubleTap:
                    {
                        Unpress();
                        cam.DoubleTapTarget(g.Point, out var to, out float z);
                        FlyTo(to, z, 0.3f);
                        break;
                    }
                    case GestureKind.PanStart:
                    case GestureKind.PinchStart:
                        Tweener.Stop(content, "focus");
                        cam.Grab();
                        GroundTouched?.Invoke();
                        break;
                    case GestureKind.Pan:
                        cam.DragBy(g.Delta);
                        Apply(false);
                        break;
                    case GestureKind.Pinch:
                        cam.DragBy(g.Delta);
                        cam.ZoomAbout(g.Point, g.Scale);
                        Apply(false);
                        break;
                    case GestureKind.PanEnd:
                        cam.Release(g.Delta);
                        break;
                    case GestureKind.PinchEnd:
                        cam.Release(new P2(0, 0));
                        break;
                }
            }
        }

        void Unpress()
        {
            if (pressedSite != null)
                SiteReleased?.Invoke(pressedSite);
            pressedSite = null;
        }

        void Tap(P2 at)
        {
            var site = Sites?.At(cam.ViewToMap(at), cam.Zoom);
            var tap = pressedTap;
            pressedTap = null;
            Unpress();
            if (site != null)
            {
                // the tap did something: the next one is not the second half of a double tap
                gestures.ForgetTap();
                SiteTapped?.Invoke(site);
            }
            else if (tap != null && tap.Tapped != null)
            {
                gestures.ForgetTap();
                GroundTouched?.Invoke();
                tap.Tapped();
            }
            else
            {
                GroundTouched?.Invoke();
            }
        }

        void Update()
        {
            if (content == null)
                return;
            Handle(gestures.Tick(Time.unscaledTime));
            if (gestures.Fingers == 0 && cam.Moving)
            {
                cam.Step(Mathf.Min(0.05f, Time.unscaledDeltaTime));
                Apply(false);
            }
        }

        void OnDisable()
        {
            gestures.Reset();
            pressedTap = null;
            pressedSite = null;
        }

        void OnApplicationFocus(bool focus)
        {
            // the system took the fingers away (a call, the notification shade): nothing is tapped, nothing glides
            if (!focus)
            {
                gestures.Reset();
                Unpress();
            }
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

        /// <summary>
        /// Shows the camera: the map's place and size. With clamp, inside the hard limits (a flight, a rebuild); a
        /// finger or a glide may hold the map a little past an edge.
        /// </summary>
        void Apply(bool clamp = true)
        {
            var size = view.rect.size;
            cam.ViewW = size.x;
            cam.ViewH = size.y;
            if (clamp)
                cam.Clamp();
            zoom = cam.Zoom;
            content.localScale = Vector3.one * zoom;
            content.anchoredPosition = new Vector2(cam.Position.X, cam.Position.Y);
            hasCamera = true;
            savedPosition = ToV(cam.ClampPosition(cam.Position, cam.ClampZoom(cam.Zoom)));
            savedZoom = cam.ClampZoom(cam.Zoom);
        }

        void OnRectTransformDimensionsChange()
        {
            if (content != null)
                Apply();
        }
    }

    /// <summary>
    /// A tap on something on the farm (an object with looks, a closed zone). It takes the press itself and hands the
    /// press and release to the farm, which tells a tap from a drag (TouchGestures): a drag that starts on it pans
    /// the map, a tap comes back as Tapped — unless a site of the working farm stands under the finger.
    /// </summary>
    public sealed class FarmTap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public FarmWorld World;
        public Action Tapped;

        public void OnPointerDown(PointerEventData e) => World?.Down(e, this);

        public void OnPointerUp(PointerEventData e) => World?.OnPointerUp(e);
    }
}
