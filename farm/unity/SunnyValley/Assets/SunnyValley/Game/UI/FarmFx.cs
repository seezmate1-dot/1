using System;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// A hook for a short tick of the phone's motor on a farm action (docs/FARM_UX.md §6). The game has the
    /// «Вибрация» setting but no light haptic of its own yet: a platform layer sets <see cref="Light"/>.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;
        public static Action Light;

        public static void Tap()
        {
            if (Enabled)
                Light?.Invoke();
        }
    }

    /// <summary>
    /// The small effects of the farm map (docs/FARM_UX.md §6): a thing flying in an arc to the HUD, a «+2» floating
    /// up, sparkles, a shake of a thing that cannot do what was asked. Each is a few objects on a layer that remove
    /// themselves; a tween belongs to its own object, so it ends by itself whatever happens to the screen.
    /// </summary>
    public static class FarmFx
    {
        static RectTransform Make(string name, RectTransform layer, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = layer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        public static Image Picture(RectTransform rt, Sprite sprite)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static Text Words(RectTransform rt, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UiView.Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color;
            t.text = text ?? "";
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.12f, 0.04f, 0.85f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            return t;
        }

        /// <summary>
        /// A picture leaves a world point, hops up, and flies in an arc to another (a harvest to the barn's button):
        /// it starts with a little pop and shrinks into the target; arrived is called as it lands.
        /// </summary>
        public static void Fly(RectTransform layer, Sprite sprite, Vector3 from, Vector3 to, float size, float delay, Action arrived = null, float sideways = 0)
        {
            if (layer == null || sprite == null)
            {
                arrived?.Invoke();
                return;
            }
            var rt = Make("fly", layer, size);
            Picture(rt, sprite);
            rt.position = from;
            rt.localScale = Vector3.zero;
            float scale = layer.lossyScale.x;
            Vector3 mid = (from + to) / 2 + new Vector3(sideways * scale, (Mathf.Abs(to.y - from.y) * 0.2f + 160 * scale), 0);
            var go = rt.gameObject;
            Tweener.Run(rt, "fly", 0.62f, Easing.Kind.InOutQuad, k =>
            {
                rt.position = (1 - k) * (1 - k) * from + 2 * (1 - k) * k * mid + k * k * to;
                // pops out, then shrinks into the counter
                float s = k < 0.18f ? Mathf.Lerp(0.3f, 1.15f, k / 0.18f) : Mathf.Lerp(1.15f, 0.55f, (k - 0.18f) / 0.82f);
                rt.localScale = Vector3.one * s;
            }, () =>
            {
                UnityEngine.Object.Destroy(go);
                arrived?.Invoke();
            }, delay);
        }

        /// <summary>«+2» with a small picture floats up from a world point and fades.</summary>
        public static void FloatText(RectTransform layer, string text, Vector3 world, Color color, Sprite icon = null, float delay = 0)
        {
            if (layer == null)
                return;
            var rt = Make("float", layer, 10);
            rt.position = world;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0;
            var label = Make("text", rt, 10);
            var t = Words(label, text, 54, color, icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
            if (icon != null)
            {
                var pic = Make("icon", rt, 64);
                Picture(pic, icon);
                pic.anchoredPosition = new Vector2(-40, 0);
                label.anchoredPosition = new Vector2(2, 0);
            }
            Vector2 start = rt.anchoredPosition;
            var go = rt.gameObject;
            Tweener.Run(rt, "float", 0.95f, Easing.Kind.OutCubic, k =>
            {
                rt.anchoredPosition = start + new Vector2(0, 130 * k);
                rt.localScale = Vector3.one * (k < 0.2f ? Mathf.Lerp(0.5f, 1.12f, k / 0.2f) : Mathf.Lerp(1.12f, 1f, (k - 0.2f) / 0.8f));
                group.alpha = k < 0.1f ? k * 10 : k > 0.7f ? (1 - k) / 0.3f : 1;
            }, () => UnityEngine.Object.Destroy(go), delay);
        }

        /// <summary>A few sparkles burst from a world point.</summary>
        public static void Sparks(RectTransform layer, Vector3 world, int count = 6, float radius = 120)
        {
            var sprite = FarmArt.Painted("Farm/Spark");
            if (layer == null || sprite == null)
                return;
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.37f * (i % 2)) * Mathf.PI * 2 / count;
                var rt = Make("spark", layer, 56);
                var img = Picture(rt, sprite);
                img.color = new Color(1, 0.96f, 0.6f, 1);
                rt.position = world;
                Vector2 from = rt.anchoredPosition, to = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.25f) * radius;
                var go = rt.gameObject;
                Tweener.Run(rt, "spark", 0.5f, Easing.Kind.OutCubic, k =>
                {
                    rt.anchoredPosition = Vector2.Lerp(from, to, k);
                    rt.localScale = Vector3.one * Mathf.Sin(Mathf.Clamp01(k * 1.1f) * Mathf.PI);
                    rt.localRotation = Quaternion.Euler(0, 0, 120 * k);
                }, () => UnityEngine.Object.Destroy(go), i * 0.015f);
            }
        }

        /// <summary>«No»: a quick shake left and right that dies out.</summary>
        public static void Shake(RectTransform rt, float amount = 14)
        {
            if (rt == null)
                return;
            Tweener.Run(rt, "shake", 0.36f, Easing.Kind.Linear, k =>
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 5) * amount * 0.5f * (1 - k)), () => rt.localRotation = Quaternion.identity);
        }

        /// <summary>A bump with a squash: wider and lower, then taller, then itself (a counter that took something in).</summary>
        public static void Squash(RectTransform rt, float amount = 0.16f, float duration = 0.34f)
        {
            if (rt == null)
                return;
            Tweener.Run(rt, "scale", duration, Easing.Kind.Linear, k =>
            {
                float c = Mathf.Sin(k * Mathf.PI * 2) * (1 - k) * amount;
                float grow = Mathf.Sin(k * Mathf.PI) * amount * 0.6f;
                rt.localScale = new Vector3(1 + grow + c, 1 + grow - c, 1);
            }, () => rt.localScale = Vector3.one);
        }
    }
}
