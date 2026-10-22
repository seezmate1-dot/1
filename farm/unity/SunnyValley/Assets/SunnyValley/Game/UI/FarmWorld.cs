using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The farm under the interface: a big map moved with one finger and zoomed with two (or the mouse wheel),
    /// never showing past its edges. For now a placeholder drawn from shapes: grass, the house, the barn, beds,
    /// the pond, trees and a fence, with the zones still locked. The isometric art (GDD §10) replaces the content later;
    /// the panning and zooming stay.
    /// </summary>
    public sealed class FarmWorld : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        public const float MapSize = 2800;
        public const float MinZoom = 0.6f;
        public const float MaxZoom = 1.8f;

        RectTransform content;
        RectTransform view;
        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        float zoom = 1;

        public void Build(UiNode node)
        {
            view = (RectTransform)transform;
            var bg = gameObject.AddComponent<Image>();
            bg.color = UiView.ToColor(node.Color);
            bg.raycastTarget = true; // drags on empty farm pan it; buttons above get their taps first
            gameObject.AddComponent<RectMask2D>();

            content = Rect("map", view, 0, 0, MapSize, MapSize);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            Paint(content);
            content.anchoredPosition = new Vector2(0, -250); // the house a little above the middle
            Apply();
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Shape(string name, Transform parent, float x, float y, float w, float h, uint color, bool circle = false, float radius = 24, float rotate = 0)
        {
            var rt = Rect(name, parent, x, y, w, h);
            rt.localRotation = Quaternion.Euler(0, 0, rotate);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = UiView.ToColor(color);
            if (circle)
            {
                img.sprite = ShapeSprites.Circle;
            }
            else
            {
                img.sprite = ShapeSprites.RoundRect;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = ShapeSprites.SliceRadius / Mathf.Max(1, radius);
            }
            return img;
        }

        static void Sign(Transform parent, float x, float y, string text, bool locked)
        {
            var plate = Shape("sign " + text, parent, x, y, 300, 84, Tokens.Wood, radius: 32);
            var label = Rect("label", plate.transform, locked ? 24 : 0, 0, 220, 70);
            var t = label.gameObject.AddComponent<Text>();
            t.font = UiView.Font;
            t.fontSize = 40;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = UiView.ToColor(Tokens.TextDark);
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (locked)
            {
                var icon = Rect("lock", plate.transform, -105, 0, 64, 64).gameObject.AddComponent<Image>();
                icon.sprite = ShapeSprites.Icon(Icons.Lock);
                icon.raycastTarget = false;
            }
        }

        static void Paint(RectTransform map)
        {
            Shape("grass", map, 0, 0, MapSize, MapSize, Tokens.Grass, radius: 0);
            for (int i = 0; i < 7; i++)
                Shape("stripe", map, 0, -1200 + i * 400, MapSize, 140, 0x7ABD50FF, radius: 0);
            // the path
            Shape("path", map, 0, -500, 180, 1800, 0xE8D3A0FF, radius: 90);
            Shape("path2", map, 0, 150, 1500, 160, 0xE8D3A0FF, radius: 80);
            // the house
            Shape("house shadow", map, 20, 470, 560, 420, 0x00000030, radius: 30);
            Shape("house", map, 0, 480, 520, 380, 0xF3E3C3FF, radius: 20);
            Shape("roof", map, 0, 700, 400, 400, 0xD9573AFF, radius: 30, rotate: 45);
            Shape("door", map, 0, 370, 110, 170, 0x8A5528FF, radius: 18);
            Shape("window1", map, -160, 520, 110, 110, 0x9ED9F6FF, radius: 14);
            Shape("window2", map, 160, 520, 110, 110, 0x9ED9F6FF, radius: 14);
            Sign(map, 0, 230, "Дом", false);
            // the barn
            Shape("barn", map, 820, 520, 420, 340, 0xC8453AFF, radius: 20);
            Shape("barn roof", map, 820, 700, 300, 300, 0x8E2E24FF, radius: 24, rotate: 45);
            Shape("barn door", map, 820, 450, 180, 200, 0xF6E3BCFF, radius: 10);
            Sign(map, 820, 290, "Амбар", true);
            // the vegetable beds
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                Shape("bed", map, -900 + c * 200, -160 - r * 200, 180, 180, Tokens.Soil, radius: 24);
                Shape("sprout", map, -900 + c * 200, -160 - r * 200, 60, 60, Tokens.Green, circle: true);
            }
            Sign(map, -700, 60, "Огород", false);
            // the pond
            Shape("pond", map, 760, -480, 620, 400, 0x5CB8E8FF, circle: true);
            Shape("pond shine", map, 680, -420, 200, 90, 0xFFFFFF40, circle: true);
            Sign(map, 760, -760, "Пруд", true);
            // trees around
            float[] trees = { -1200, 1100, -1050, 1250, 1150, 1150, 1250, 950, -1250, -900, -1100, -1150, 300, -1200, -300, -1150, 1200, -1100 };
            for (int i = 0; i < trees.Length; i += 2)
            {
                Shape("trunk", map, trees[i], trees[i + 1] - 70, 50, 120, 0x7A4A1EFF, radius: 16);
                Shape("crown", map, trees[i], trees[i + 1] + 20, 220, 220, 0x4E9A34FF, circle: true);
                Shape("crown shine", map, trees[i] - 40, trees[i + 1] + 60, 80, 80, 0x6CC04AFF, circle: true);
            }
            // the broken fence
            for (int i = 0; i < 14; i++)
            {
                if (i % 5 == 3)
                    continue;
                Shape("post", map, -1300 + i * 200, 1000, 36, 140, 0xB98C5AFF, radius: 12);
            }
            Shape("rail", map, 0, 1020, 2640, 26, 0xB98C5AFF, radius: 12);
        }

        // ------------------------------------------------------------------ fingers

        public void OnPointerDown(PointerEventData e) => pointers[e.pointerId] = e.position;

        public void OnPointerUp(PointerEventData e) => pointers.Remove(e.pointerId);

        public void OnDrag(PointerEventData e)
        {
            if (content == null)
                return;
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
            float minZoom = Mathf.Max(size.x / MapSize, size.y / MapSize);
            zoom = Mathf.Clamp(zoom, Mathf.Max(MinZoom, minZoom), MaxZoom);
            content.localScale = Vector3.one * zoom;
            float mx = Mathf.Max(0, (MapSize * zoom - size.x) / 2), my = Mathf.Max(0, (MapSize * zoom - size.y) / 2);
            var p = content.anchoredPosition;
            content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
        }

        void OnRectTransformDimensionsChange()
        {
            if (content != null)
                Apply();
        }
    }
}
