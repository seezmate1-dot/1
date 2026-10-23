using System.Collections.Generic;
using SunnyValley.Core.Match3;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Match3View
{
    /// <summary>How an effect sprite moves.</summary>
    public enum FxMotion : byte
    {
        /// <summary>Stays at P0 (rings, flashes, glows).</summary>
        Still,
        /// <summary>Flies with a velocity under gravity, spinning (sparkles, debris).</summary>
        Ballistic,
        /// <summary>Goes P0 → P1 along an arc of height Arc (ghosts, bees, sparks).</summary>
        Path,
        /// <summary>A streak from P0 whose head runs to P1: stretched between them, then fades from the tail.</summary>
        Streak,
    }

    /// <summary>One effect sprite of the pool; plain fields, advanced by <see cref="FxPool.Tick"/>.</summary>
    public sealed class FxItem
    {
        public RectTransform Rt;
        public Image Img;
        public bool Live;
        public FxMotion Motion;
        public Vector2 P0, P1, Vel;
        public float Gravity, Spin, Rot, S0, S1, A0, A1, T0, Dur, Arc, Width;
        public Easing.Kind Ease;
        public Color Color;
        /// <summary>Faces along its path (bees, pitchfork heads).</summary>
        public bool Orient;
        /// <summary>Base rotation added to Orient (art that points up: -90).</summary>
        public float OrientOffset;
        /// <summary>Called back when it ends (goal ghosts): what it carries.</summary>
        public StepEvent Payload;
        public int Tag;
    }

    /// <summary>
    /// A pool of effect sprites under one parent: sparkles, debris, rings, streaks, beams, bees, goal ghosts. Grows
    /// to the busiest moment and then reuses; nothing is allocated per frame.
    /// </summary>
    public sealed class FxPool
    {
        readonly RectTransform parent;
        readonly List<FxItem> all = new List<FxItem>();
        readonly Stack<FxItem> free = new Stack<FxItem>();
        readonly List<FxItem> live = new List<FxItem>();

        /// <summary>Called when an item with a payload or tag ends.</summary>
        public System.Action<FxItem> Ended;

        public FxPool(RectTransform parent) => this.parent = parent;

        public int LiveCount => live.Count;

        public FxItem Get(Sprite sprite, Vector2 at, float size, Color color, float t0, float duration)
        {
            FxItem f = free.Count > 0 ? free.Pop() : Make();
            f.Live = true;
            f.Img.sprite = sprite;
            f.Img.color = color;
            f.Color = color;
            f.Motion = FxMotion.Still;
            f.P0 = f.P1 = at;
            f.Vel = Vector2.zero;
            f.Gravity = f.Spin = f.Rot = f.Arc = 0;
            f.S0 = f.S1 = 1;
            f.A0 = color.a;
            f.A1 = 0;
            f.T0 = t0;
            f.Dur = Mathf.Max(0.0001f, duration);
            f.Width = size;
            f.Ease = Easing.Kind.Linear;
            f.Orient = false;
            f.OrientOffset = 0;
            f.Payload = null;
            f.Tag = 0;
            f.Rt.sizeDelta = new Vector2(size, size);
            f.Rt.anchoredPosition = at;
            f.Rt.localScale = Vector3.zero; // hidden until it starts
            f.Rt.localRotation = Quaternion.identity;
            f.Rt.SetAsLastSibling();
            f.Rt.gameObject.SetActive(true);
            live.Add(f);
            return f;
        }

        FxItem Make()
        {
            var go = new GameObject("fx", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var f = new FxItem { Rt = rt, Img = img };
            all.Add(f);
            return f;
        }

        public void Tick(float now)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                if (now < f.T0)
                    continue;
                float t = now - f.T0;
                float k = Mathf.Clamp01(t / f.Dur);
                float e = Easing.Apply(f.Ease, k);
                float scale = Mathf.LerpUnclamped(f.S0, f.S1, e);
                float alpha = Mathf.LerpUnclamped(f.A0, f.A1, k);
                switch (f.Motion)
                {
                    case FxMotion.Ballistic:
                    {
                        var p = f.P0 + f.Vel * t + new Vector2(0, -0.5f * f.Gravity * t * t);
                        f.Rt.anchoredPosition = p;
                        f.Rt.localRotation = Quaternion.Euler(0, 0, f.Rot + f.Spin * t);
                        f.Rt.localScale = new Vector3(scale, scale, 1);
                        break;
                    }
                    case FxMotion.Path:
                    {
                        var p = Vector2.LerpUnclamped(f.P0, f.P1, e);
                        var n = new Vector2(-(f.P1.y - f.P0.y), f.P1.x - f.P0.x);
                        float len = n.magnitude;
                        float lift = f.Arc * 4 * e * (1 - e);
                        if (len > 0.001f)
                            p += n / len * lift * (f.P1.x >= f.P0.x ? 1 : -1);
                        f.Rt.anchoredPosition = p;
                        if (f.Orient)
                        {
                            // direction of travel: derivative of the arc
                            var d = (f.P1 - f.P0) + (len > 0.001f ? n / len * f.Arc * 4 * (1 - 2 * e) * (f.P1.x >= f.P0.x ? 1 : -1) : Vector2.zero);
                            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + f.OrientOffset;
                            f.Rt.localRotation = Quaternion.Euler(0, 0, ang);
                        }
                        else
                        {
                            f.Rt.localRotation = Quaternion.Euler(0, 0, f.Rot + f.Spin * t);
                        }
                        f.Rt.localScale = new Vector3(scale, scale, 1);
                        break;
                    }
                    case FxMotion.Streak:
                    {
                        // the head runs out in the first 70%, the tail follows and the streak fades
                        float head = Mathf.Clamp01(k / 0.7f);
                        float tail = Mathf.Clamp01((k - 0.25f) / 0.75f);
                        var a = Vector2.LerpUnclamped(f.P0, f.P1, tail * tail);
                        var b = Vector2.LerpUnclamped(f.P0, f.P1, Easing.Apply(Easing.Kind.OutQuad, head));
                        var d = b - a;
                        float len = d.magnitude;
                        f.Rt.anchoredPosition = (a + b) * 0.5f;
                        f.Rt.sizeDelta = new Vector2(Mathf.Max(len, f.Width * 0.5f), f.Width);
                        f.Rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                        f.Rt.localScale = Vector3.one;
                        break;
                    }
                    default:
                        f.Rt.localRotation = Quaternion.Euler(0, 0, f.Rot + f.Spin * t);
                        f.Rt.localScale = new Vector3(scale, scale, 1);
                        break;
                }
                var c = f.Color;
                c.a = alpha;
                f.Img.color = c;
                if (k >= 1)
                {
                    live.RemoveAt(i);
                    Release(f);
                    if ((f.Payload != null || f.Tag != 0) && Ended != null)
                        Ended(f);
                }
            }
        }

        void Release(FxItem f)
        {
            f.Live = false;
            f.Rt.gameObject.SetActive(false);
            free.Push(f);
        }

        /// <summary>Ends everything at once (a rebuild): items with payloads still report.</summary>
        public void Clear(bool report)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var f = live[i];
                live.RemoveAt(i);
                Release(f);
                if (report && (f.Payload != null || f.Tag != 0) && Ended != null)
                    Ended(f);
            }
        }

        // ------------------------------------------------------------------ ready-made effects

        /// <summary>A burst of sparkles and dots in a colour.</summary>
        public void Burst(Vector2 at, Color color, float cell, float t0, int count, float speed = 1, Sprite sprite = null)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (i + 0.37f * (i % 3)) / count * Mathf.PI * 2;
                float v = cell * (2.2f + 1.6f * Frac(i * 0.618f)) * speed;
                bool sparkle = sprite == null && i % 2 == 0;
                var f = Get(sprite ?? (sparkle ? BoardSprites.Spark : BoardSprites.Dot), at, cell * (sparkle ? 0.34f : 0.16f + 0.08f * Frac(i * 0.37f)),
                    sparkle ? Color.Lerp(color, Color.white, 0.55f) : color, t0, 0.42f + 0.12f * Frac(i * 0.29f));
                f.Motion = FxMotion.Ballistic;
                f.Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + 0.6f) * v;
                f.Gravity = cell * 9f;
                f.Spin = sparkle ? 0 : 360 * (Frac(i * 0.71f) - 0.5f);
                f.Rot = sprite != null ? 360 * Frac(i * 0.43f) : 0;
                f.S0 = 1;
                f.S1 = 0.2f;
                f.A0 = 1;
                f.A1 = 0;
            }
        }

        /// <summary>An expanding ring (shockwave, pop flash).</summary>
        public FxItem RingOut(Vector2 at, Color color, float fromSize, float toSize, float t0, float duration)
        {
            var f = Get(BoardSprites.Ring, at, toSize, color, t0, duration);
            f.S0 = fromSize / toSize;
            f.S1 = 1;
            f.Ease = Easing.Kind.OutCubic;
            f.A0 = color.a;
            f.A1 = 0;
            return f;
        }

        /// <summary>A soft flash of light.</summary>
        public FxItem Flash(Vector2 at, Color color, float size, float t0, float duration)
        {
            var f = Get(BoardSprites.Glow, at, size, color, t0, duration);
            f.S0 = 0.5f;
            f.S1 = 1.2f;
            f.Ease = Easing.Kind.OutQuad;
            return f;
        }

        public FxItem StreakLine(Vector2 from, Vector2 to, Color color, float width, float t0, float duration)
        {
            var f = Get(BoardSprites.Streak, from, width, color, t0, duration);
            f.Motion = FxMotion.Streak;
            f.P0 = from;
            f.P1 = to;
            f.A0 = color.a;
            f.A1 = 0.2f;
            return f;
        }

        public FxItem Fly(Sprite sprite, Vector2 from, Vector2 to, float size, float arc, float t0, float duration, Easing.Kind ease)
        {
            var f = Get(sprite, from, size, Color.white, t0, duration);
            f.Motion = FxMotion.Path;
            f.P0 = from;
            f.P1 = to;
            f.Arc = arc;
            f.Ease = ease;
            f.A0 = 1;
            f.A1 = 1;
            return f;
        }

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
