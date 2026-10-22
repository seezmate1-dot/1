using System;
using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LShape = SunnyValley.Layout.Shape;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// Turns a layout from the plain-C# model (Layout/Screens.cs, Popups.cs) into uGUI objects: every node becomes a
    /// RectTransform with exactly the node's anchors, pivot and corner offsets, so Unity places it where the tested
    /// model says. Shapes get a 9-sliced rounded sprite (radius from the node) with a darker «depth» band under it,
    /// labels a legacy Text in Nunito at the size the model fitted (lines broken by the model too), interactive nodes
    /// a transparent hit area of exactly their rectangle plus a Button with a press animation.
    /// Screens then look elements up by id: OnClick("farm.play", …), SetText("farm.coins.value", …).
    /// </summary>
    public sealed class UiView
    {
        public readonly UiLayout Layout;
        public readonly RectTransform Root;

        readonly Dictionary<string, RectTransform> rects = new Dictionary<string, RectTransform>();
        readonly Dictionary<string, Text> texts = new Dictionary<string, Text>();
        readonly Dictionary<string, Image> faces = new Dictionary<string, Image>();
        readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        readonly Dictionary<string, RectTransform> fills = new Dictionary<string, RectTransform>();

        static Font font;

        /// <summary>The game's font: Nunito ExtraBold (SIL OFL), with Cyrillic.</summary>
        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Color32 ToColor(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

        UiView(UiLayout layout, RectTransform root)
        {
            Layout = layout;
            Root = root;
        }

        /// <summary>Builds the layout's objects under parent (a full-screen layer of the canvas).</summary>
        public static UiView Build(UiLayout layout, RectTransform parent)
        {
            var root = NewRect(layout.Root.Id, parent);
            var view = new UiView(layout, root);
            view.Make(layout.Root, root);
            if (Debug.isDebugBuild)
            {
                // the same rules as the tests, on the real device: log what breaks here
                foreach (string p in LayoutCheck.Problems(layout))
                    Debug.LogWarning("Вёрстка: " + p);
            }
            return view;
        }

        public void Destroy()
        {
            if (Root != null)
                UnityEngine.Object.Destroy(Root.gameObject);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Place(RectTransform rt, UiNode n)
        {
            rt.anchorMin = new Vector2(n.AnchorMin.X, n.AnchorMin.Y);
            rt.anchorMax = new Vector2(n.AnchorMax.X, n.AnchorMax.Y);
            rt.pivot = new Vector2(n.Pivot.X, n.Pivot.Y);
            rt.offsetMin = new Vector2(n.OffsetMin.X, n.OffsetMin.Y);
            rt.offsetMax = new Vector2(n.OffsetMax.X, n.OffsetMax.Y);
            rt.localScale = Vector3.one;
        }

        void Make(UiNode n, RectTransform rt)
        {
            Place(rt, n);
            rects[n.Id] = rt;
            if (n.Has(NodeFlags.Hidden))
                rt.gameObject.SetActive(false);

            Image face = null;
            switch (n.Kind)
            {
                case NodeKind.Dim:
                    face = rt.gameObject.AddComponent<Image>();
                    face.color = ToColor(n.Color);
                    face.raycastTarget = true; // swallows taps on the screen under a popup
                    break;
                case NodeKind.Icon:
                    var icon = rt.gameObject.AddComponent<Image>();
                    icon.sprite = ShapeSprites.Icon(n.Icon);
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    faces[n.Id] = icon;
                    break;
                case NodeKind.Label:
                    texts[n.Id] = MakeText(rt, n);
                    break;
                case NodeKind.World:
                    var world = rt.gameObject.AddComponent<FarmWorld>();
                    world.Build(n);
                    break;
                default:
                    if (n.Shape != LShape.None)
                        face = MakeShape(rt, n);
                    break;
            }
            if (face != null)
                faces[n.Id] = face;

            if (n.Kind == NodeKind.Bar)
                fills[n.Id] = MakeFill(rt, n);

            if (n.Interactive)
                MakeInteractive(rt, n, face);

            foreach (var child in n.Children)
                Make(child, NewRect(child.Id, rt));

            if (n.Kind == NodeKind.Scroll && n.Children.Count > 0)
            {
                var scroll = rt.GetComponent<ScrollRect>();
                scroll.content = rects[n.Children[0].Id];
            }
            if (n.Kind == NodeKind.Board)
                rt.gameObject.AddComponent<BoardView>().Build(n);
        }

        /// <summary>A rounded shape: an optional darker band offset down by Depth, then the face over the whole rectangle.</summary>
        static Image MakeShape(RectTransform rt, UiNode n)
        {
            if (n.Depth > 0)
            {
                var depth = NewRect("depth", rt);
                depth.offsetMin = new Vector2(0, -n.Depth);
                depth.offsetMax = new Vector2(0, -n.Depth);
                var di = depth.gameObject.AddComponent<Image>();
                SetShape(di, n);
                di.color = ToColor(n.DepthColor);
                di.raycastTarget = false;
            }
            var faceRect = NewRect("face", rt);
            var face = faceRect.gameObject.AddComponent<Image>();
            SetShape(face, n);
            face.color = ToColor(n.Color);
            face.raycastTarget = false;
            if (n.Kind == NodeKind.Button || n.Kind == NodeKind.Panel && n.Depth > 0)
            {
                // a soft highlight on the upper half: the glossy cartoon look
                var shine = NewRect("shine", faceRect);
                shine.anchorMin = new Vector2(0, 0.52f);
                shine.offsetMin = new Vector2(10, 0);
                shine.offsetMax = new Vector2(-10, -8);
                var si = shine.gameObject.AddComponent<Image>();
                SetShape(si, n);
                if (n.Shape == LShape.RoundRect)
                    si.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(4, n.Radius - 10);
                si.color = new Color(1, 1, 1, 0.18f);
                si.raycastTarget = false;
            }
            return face;
        }

        static void SetShape(Image img, UiNode n)
        {
            if (n.Shape == LShape.Circle)
            {
                img.sprite = ShapeSprites.Circle;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = ShapeSprites.RoundRect;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                // one sprite serves every radius: the border shows as 64 / multiplier canvas units
                img.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(1, n.Radius);
            }
        }

        static RectTransform MakeFill(RectTransform rt, UiNode n)
        {
            var fill = NewRect("fill", rt);
            fill.SetSiblingIndex(rt.childCount - 1);
            var img = fill.gameObject.AddComponent<Image>();
            img.sprite = ShapeSprites.RoundRect;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(1, n.Radius);
            img.color = ToColor(n.DepthColor);
            img.raycastTarget = false;
            SetFill(fill, n.Value, n.Rect.H, n.Rect.W);
            return fill;
        }

        static void SetFill(RectTransform fill, float value, float h, float w)
        {
            // never thinner than the bar is tall, so the rounded ends stay round
            float min = w > 0 ? Mathf.Min(1, h / w) : 0;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Max(min, Mathf.Clamp01(value)), 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        void MakeInteractive(RectTransform rt, UiNode n, Image face)
        {
            // the hit area is the node's own rectangle: exactly what the layout tests checked
            var hit = rt.gameObject.GetComponent<Image>();
            if (hit == null)
                hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            hit.raycastTarget = true;
            switch (n.Kind)
            {
                case NodeKind.Scroll:
                    rt.gameObject.AddComponent<RectMask2D>();
                    var scroll = rt.gameObject.AddComponent<ScrollRect>();
                    scroll.horizontal = false;
                    scroll.vertical = true;
                    scroll.movementType = ScrollRect.MovementType.Elastic;
                    scroll.scrollSensitivity = 40;
                    scroll.viewport = rt;
                    break;
                case NodeKind.Board:
                    break;
                default:
                    var button = rt.gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    button.targetGraphic = hit;
                    var nav = button.navigation;
                    nav.mode = Navigation.Mode.None;
                    button.navigation = nav;
                    rt.gameObject.AddComponent<PressFx>();
                    buttons[n.Id] = button;
                    break;
            }
        }

        static Text MakeText(RectTransform rt, UiNode n)
        {
            var t = n.Text;
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.color = ToColor(t.Color);
            text.raycastTarget = false;
            text.supportRichText = false;
            text.resizeTextForBestFit = false;
            // the model already chose the size and broke the lines: Unity must not wrap again
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = t.Align == TextAlign.Left ? TextAnchor.MiddleLeft : t.Align == TextAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            if (t.Outline)
            {
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(70, 40, 12, 200);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
            Apply(text, t);
            return text;
        }

        static void Apply(Text text, TextSpec t)
        {
            text.fontSize = Mathf.Max(1, t.FittedSize);
            text.text = string.Join("\n", t.Lines);
        }

        // ------------------------------------------------------------------ for the screens

        public RectTransform Rect(string id) => rects.TryGetValue(id, out var r) ? r : null;

        public Image Face(string id) => faces.TryGetValue(id, out var f) ? f : null;

        public Button Button(string id) => buttons.TryGetValue(id, out var b) ? b : null;

        public UiNode Node(string id) => Layout.Find(id);

        public bool Has(string id) => rects.ContainsKey(id);

        public void OnClick(string id, Action action)
        {
            var b = Button(id);
            if (b == null)
            {
                Debug.LogWarning("Нет кнопки " + id);
                return;
            }
            b.onClick.AddListener(() => action());
        }

        /// <summary>Changes a label and fits it again into its rectangle (the model's rules, same as the tests).</summary>
        public void SetText(string id, string value)
        {
            var node = Layout.Find(id);
            if (node?.Text == null || !texts.TryGetValue(id, out var text))
                return;
            if (node.Text.Value == value)
                return;
            node.Text.Value = value;
            TextFit.Fit(node, Layout.Fit.Scale);
            if (!node.Text.Fits && Debug.isDebugBuild)
                Debug.LogWarning($"Вёрстка: «{value}» не влезает в {id}");
            Apply(text, node.Text);
        }

        public void SetVisible(string id, bool visible)
        {
            var rt = Rect(id);
            if (rt != null && rt.gameObject.activeSelf != visible)
                rt.gameObject.SetActive(visible);
        }

        public void SetBar(string id, float value)
        {
            var node = Layout.Find(id);
            if (node != null && fills.TryGetValue(id, out var fill))
            {
                node.Value = value;
                SetFill(fill, value, node.Rect.H, node.Rect.W);
            }
        }

        /// <summary>Sets an on/off switch: track colour and knob side, with a little slide.</summary>
        public void SetToggle(string id, bool on, bool animate = true)
        {
            var node = Layout.Find(id);
            var track = Face(id + ".track");
            var knob = Rect(id + ".knob");
            if (node == null || track == null || knob == null)
                return;
            node.Value = on ? 1 : 0;
            track.color = ToColor(on ? Tokens.Green : Tokens.TrackOff);
            float travel = 168 - 72 - 16;
            Vector2 offPos = new Vector2(8, 0), onPos = new Vector2(8 + travel, 0);
            knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0, 0.5f);
            Vector2 from = knob.anchoredPosition, to = on ? onPos : offPos;
            if (animate)
                Tweener.Move(knob, from, to, 0.18f, Easing.Kind.OutCubic);
            else
                knob.anchoredPosition = to;
        }

        /// <summary>Shows a red badge with a number, or hides it at 0.</summary>
        public void SetBadge(string id, int count)
        {
            SetVisible(id, count > 0);
            if (count > 0)
                SetText(id + ".label", count > 99 ? "99" : count.ToString());
        }
    }

    /// <summary>The press animation of every button: sinks to 92% on touch, springs back with a small overshoot.</summary>
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            Tweener.Scale(transform, transform.localScale.x, 0.92f, 0.08f, Easing.Kind.OutQuad);
        }

        public void OnPointerUp(PointerEventData e) => Release();

        public void OnPointerExit(PointerEventData e)
        {
            if (down)
                Release();
        }

        void Release()
        {
            down = false;
            Tweener.Scale(transform, transform.localScale.x, 1f, 0.28f, Easing.Kind.OutBack);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }
}
