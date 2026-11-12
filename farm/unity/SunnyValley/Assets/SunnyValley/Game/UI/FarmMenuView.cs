using System;
using System.Collections.Generic;
using SunnyValley.Game.Farm;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// A round button of a site's menu under a finger: it sinks when pressed and springs back; a tap is a tap, and
    /// a drag that starts on it carries its tool away over the farm (FarmInteraction does the rest).
    /// </summary>
    public sealed class FarmMenuButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler
    {
        public int Index;
        public Action<int> Clicked;
        public Action<int, PointerEventData> DragBegan, Dragged, DragEnded;
        public RectTransform Face;
        bool dragging;

        public void OnPointerDown(PointerEventData e) => Tweener.Scale(Face, Face.localScale.x, 0.9f, 0.07f, Easing.Kind.OutQuad);

        public void OnPointerUp(PointerEventData e) => Tweener.Scale(Face, Face.localScale.x, 1, 0.26f, Easing.Kind.OutBack);

        public void OnPointerClick(PointerEventData e)
        {
            if (!e.dragging && !dragging)
                Clicked?.Invoke(Index);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            dragging = true;
            DragBegan?.Invoke(Index, e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (dragging)
                Dragged?.Invoke(Index, e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging)
                return;
            dragging = false;
            DragEnded?.Invoke(Index, e);
        }
    }

    /// <summary>
    /// The menu a tapped site opens next to itself (docs/FARM_UX.md §5): a fan of round buttons around the site
    /// (RadialLayout keeps it inside the free part of the screen) and a small plate with the site's name, its timer
    /// with a bar, a workshop's queue, a hint. It follows the site while the map moves. Drawn over the HUD and
    /// removed with the screen. What the buttons are comes from FarmMenus; what they do, from FarmInteraction.
    /// </summary>
    public sealed class FarmMenuView : MonoBehaviour
    {
        sealed class ItemView
        {
            public RectTransform Root, Face;
            public Image Disc, Icon;
            public Text Label;
            public Image LabelIcon;
            public FarmMenuButton Button;
        }

        FarmWorld world;
        RectTransform root;
        CanvasGroup group;
        readonly List<ItemView> items = new List<ItemView>();
        RectTransform plate;
        Text title, sub, hint;
        RectTransform bar;
        Image barFill;
        RectTransform queue;
        string shape;

        public FarmSite Site { get; private set; }
        public SiteMenu Menu { get; private set; }
        public bool Open => Site != null;

        public Action<int> Clicked;
        public Action<int, PointerEventData> DragBegan, Dragged, DragEnded;

        static readonly Color32 Rim = new Color32(126, 84, 44, 255);
        static readonly Color32 Cream = new Color32(255, 246, 222, 255);
        static readonly Color32 Grey = new Color32(196, 190, 178, 255);

        /// <summary>The menu's layer: over the whole screen and its HUD, a child of the screen so it goes with it.</summary>
        public static FarmMenuView Create(FarmWorld world)
        {
            var go = new GameObject("farm menu", typeof(RectTransform));
            go.layer = world.gameObject.layer;
            var rt = (RectTransform)go.transform;
            var w = (RectTransform)world.transform;
            rt.SetParent(w.parent, false);
            rt.anchorMin = w.anchorMin;
            rt.anchorMax = w.anchorMax;
            rt.offsetMin = w.offsetMin;
            rt.offsetMax = w.offsetMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.SetAsLastSibling();
            var v = go.AddComponent<FarmMenuView>();
            v.world = world;
            v.root = rt;
            v.group = go.AddComponent<CanvasGroup>();
            go.SetActive(false);
            return v;
        }

        static RectTransform Child(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static string ShapeOf(SiteMenu m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(m.Progress >= 0).Append(m.Hint).Append('|');
            foreach (var i in m.Items)
                sb.Append(i.Icon).Append(i.Enabled).Append(i.Locked).Append(i.LabelIcon).Append(';');
            if (m.Queue != null)
            {
                foreach (var q in m.Queue)
                    sb.Append(q.Icon).Append(q.State).Append(',');
            }
            return sb.ToString();
        }

        /// <summary>Opens the menu on a site: the buttons pop out one after another.</summary>
        public void Show(FarmSite site, SiteMenu menu)
        {
            Site = site;
            gameObject.SetActive(true);
            group.alpha = 1;
            group.blocksRaycasts = true;
            Build(menu, true);
            Place();
        }

        /// <summary>The same site half a second later: the timer and the counts change in place; anything else builds it again.</summary>
        public void Refresh(SiteMenu menu)
        {
            if (!Open)
                return;
            if (ShapeOf(menu) != shape)
            {
                Build(menu, false);
                Place();
                return;
            }
            Menu = menu;
            Fill();
        }

        public void Hide()
        {
            if (!Open)
                return;
            Site = null;
            Menu = null;
            Clear();
            gameObject.SetActive(false);
        }

        /// <summary>While a tool is dragged the menu steps back: only a faint trace of it stays.</summary>
        public void Dim(bool dim)
        {
            Tweener.Fade(group, group.alpha, dim ? 0.2f : 1, 0.15f);
        }

        public RectTransform ItemRect(int i) => i >= 0 && i < items.Count ? items[i].Root : null;

        void Clear()
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
            items.Clear();
            plate = null;
        }

        void Build(SiteMenu menu, bool animate)
        {
            Clear();
            Menu = menu;
            shape = ShapeOf(menu);

            // the plate: name, timer line, bar, queue, hint
            bool hasBar = menu.Progress >= 0, hasQueue = menu.Queue != null && menu.Queue.Count > 0;
            float h = 76 + (menu.Sub != null ? 44 : 0) + (hasBar ? 30 : 0) + (hasQueue ? 84 : 0);
            plate = Child("plate", root, 520, h);
            var back = plate.gameObject.AddComponent<Image>();
            back.sprite = ShapeSprites.RoundRect;
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / 30f;
            back.color = UiView.ToColor(Tokens.Plate);
            back.raycastTarget = false;
            float y = h / 2 - 38;
            title = Line(plate, y, 44, Color.white);
            y -= 44;
            sub = null;
            if (menu.Sub != null)
            {
                sub = Line(plate, y, 32, new Color(1, 0.93f, 0.7f, 1));
                y -= 40;
            }
            bar = null;
            barFill = null;
            if (hasBar)
            {
                bar = Child("bar", plate, 400, 22);
                bar.anchoredPosition = new Vector2(0, y + 4);
                var bb = FarmFx.Picture(bar, FarmArt.Painted("Farm/BarBack"));
                bb.preserveAspect = false;
                var fill = Child("fill", bar, 400, 22);
                barFill = FarmFx.Picture(fill, FarmArt.Painted("Farm/BarFill"));
                barFill.preserveAspect = false;
                barFill.type = Image.Type.Filled;
                barFill.fillMethod = Image.FillMethod.Horizontal;
                barFill.color = UiView.ToColor(Tokens.Green);
                y -= 30;
            }
            queue = null;
            if (hasQueue)
            {
                queue = Child("queue", plate, 500, 76);
                queue.anchoredPosition = new Vector2(0, y - 22);
                int n = menu.Queue.Count;
                for (int i = 0; i < n; i++)
                {
                    var q = menu.Queue[i];
                    var slot = Child("slot" + i, queue, 72, 72);
                    slot.anchoredPosition = new Vector2((i - (n - 1) / 2f) * 84, 0);
                    var disc = FarmFx.Picture(slot, ShapeSprites.Circle);
                    disc.color = q.State == SlotState.Ready ? UiView.ToColor(Tokens.Yellow) : q.State == SlotState.Running ? UiView.ToColor(Tokens.Green)
                        : new Color(1, 1, 1, q.State == SlotState.Free ? 0.18f : 0.4f);
                    if (q.Icon != null)
                        FarmFx.Picture(Child("icon", slot, 56, 56), ShapeSprites.Icon(q.Icon));
                }
            }
            hint = null;
            if (menu.Hint != null)
            {
                var hr = Child("hint", root, 700, 44);
                hint = FarmFx.Words(hr, menu.Hint, 32, new Color(1, 1, 1, 0.95f));
            }

            for (int i = 0; i < menu.Items.Count; i++)
                items.Add(MakeItem(i, menu.Items[i], animate));
            Fill();
            if (animate)
            {
                plate.localScale = Vector3.zero;
                Tweener.Scale(plate, 0.6f, 1, 0.24f, Easing.Kind.OutBack, null, 0.05f);
            }
        }

        static Text Line(RectTransform parent, float y, int size, Color color)
        {
            var rt = Child("line", parent, 480, size + 8);
            rt.anchoredPosition = new Vector2(0, y);
            var t = FarmFx.Words(rt, "", size, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 24;
            t.resizeTextMaxSize = size;
            return t;
        }

        ItemView MakeItem(int index, MenuItem item, bool animate)
        {
            var v = new ItemView();
            v.Root = Child("item" + index, root, RadialLayout.Item, RadialLayout.Item);
            // the touch target is the whole button; what moves under the finger is its face
            var hit = v.Root.gameObject.AddComponent<Image>();
            hit.sprite = ShapeSprites.Circle;
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            v.Face = Child("face", v.Root, RadialLayout.Item, RadialLayout.Item);
            var shadow = FarmFx.Picture(Child("shadow", v.Face, RadialLayout.Item, RadialLayout.Item), ShapeSprites.Circle);
            shadow.color = new Color(0.16f, 0.1f, 0.03f, 0.35f);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -8);
            FarmFx.Picture(Child("rim", v.Face, RadialLayout.Item, RadialLayout.Item), ShapeSprites.Circle).color = Rim;
            v.Disc = FarmFx.Picture(Child("disc", v.Face, RadialLayout.Item - 14, RadialLayout.Item - 14), ShapeSprites.Circle);
            v.Disc.color = item.Enabled ? Cream : Grey;
            var icon = Child("icon", v.Face, 92, 92);
            icon.anchoredPosition = new Vector2(0, item.Label != null ? 12 : 0);
            v.Icon = FarmFx.Picture(icon, ShapeSprites.Icon(item.Icon));
            v.Icon.color = item.Enabled ? Color.white : new Color(1, 1, 1, 0.55f);
            if (item.Label != null)
            {
                var lr = Child("label", v.Face, 130, 40);
                lr.anchoredPosition = new Vector2(item.LabelIcon != null ? 16 : 0, -46);
                v.Label = FarmFx.Words(lr, item.Label, 32, Color.white);
                if (item.LabelIcon != null)
                {
                    var li = Child("label icon", v.Face, 38, 38);
                    li.anchoredPosition = new Vector2(-40, -46);
                    v.LabelIcon = FarmFx.Picture(li, ShapeSprites.Icon(item.LabelIcon));
                }
            }
            if (item.Locked)
            {
                var lk = Child("lock", v.Face, 54, 54);
                lk.anchoredPosition = new Vector2(48, 48);
                FarmFx.Picture(lk, ShapeSprites.Icon(Icons.Lock));
            }
            v.Button = v.Root.gameObject.AddComponent<FarmMenuButton>();
            v.Button.Index = index;
            v.Button.Face = v.Face;
            v.Button.Clicked = i => Clicked?.Invoke(i);
            v.Button.DragBegan = (i, e) => DragBegan?.Invoke(i, e);
            v.Button.Dragged = (i, e) => Dragged?.Invoke(i, e);
            v.Button.DragEnded = (i, e) => DragEnded?.Invoke(i, e);
            if (animate)
            {
                v.Root.localScale = Vector3.zero;
                Tweener.Scale(v.Root, 0, 1, 0.26f, Easing.Kind.OutBack, null, index * 0.035f);
            }
            return v;
        }

        /// <summary>The texts that change while the menu is open.</summary>
        void Fill()
        {
            var m = Menu;
            if (title != null)
                title.text = m.Title ?? "";
            if (sub != null)
                sub.text = m.Sub ?? "";
            if (barFill != null)
                barFill.fillAmount = Mathf.Clamp01(m.Progress);
            for (int i = 0; i < items.Count && i < m.Items.Count; i++)
            {
                if (items[i].Label != null)
                    items[i].Label.text = m.Items[i].Label ?? "";
            }
        }

        void LateUpdate()
        {
            if (Open)
                Place();
        }

        /// <summary>Puts the fan and the plate around the site where it is on the screen right now.</summary>
        void Place()
        {
            if (Site == null || Menu == null)
                return;
            var cam = world.Cam;
            float zoom = world.Zoom;
            Vector2 anchor = world.MapToView(new Vector2(Site.X, Site.Y + Site.H / 2));
            float radius = Mathf.Clamp(Mathf.Max(Site.W, Site.H) * zoom * 0.5f, 60, 170);
            float l = -cam.ViewW / 2 + 20, r = cam.ViewW / 2 - 20, b = -cam.ViewH / 2 + cam.InsetBottom + 12, t = cam.ViewH / 2 - cam.InsetTop - 12;
            float plateH = plate != null ? plate.sizeDelta.y : RadialLayout.TitleRoom;
            var fan = RadialLayout.Place(items.Count, new P2(anchor.x, anchor.y), radius, l, b, r, t);
            for (int i = 0; i < items.Count; i++)
                items[i].Root.anchoredPosition = new Vector2(fan.Points[i].X, fan.Points[i].Y);
            if (plate != null)
            {
                // the plate grows away from the fan when it is taller than the room kept for it
                float extra = Mathf.Max(0, plateH - RadialLayout.TitleRoom + 24) / 2;
                float py = fan.Title.Y + (fan.Below ? -extra : extra);
                py = Mathf.Clamp(py, b + plateH / 2, t - plateH / 2);
                plate.anchoredPosition = new Vector2(Mathf.Clamp(fan.Title.X, l + 260, r - 260), py);
                if (hint != null)
                    hint.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(anchor.x, l + 350, r - 350),
                        Mathf.Clamp(fan.Below ? anchor.y + radius + 40 : anchor.y - radius - 40, b + 30, t - 30));
            }
        }
    }
}
