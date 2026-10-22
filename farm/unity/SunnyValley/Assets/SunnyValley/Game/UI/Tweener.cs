using System;
using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// A tiny tween system: a value runs from 0 to 1 over a duration along an easing curve (Layout/Easing.cs) and a
    /// callback applies it. Real time (not slowed by Time.timeScale), so popups animate during a pause too.
    /// Tweens belong to an owner (a Transform, a Graphic): starting a new tween of the same owner and channel stops
    /// the old one, and a destroyed owner ends its tweens quietly. Runs on one hidden object made on first use.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        public sealed class Tween
        {
            internal float Start;
            internal float Duration;
            internal Easing.Kind Ease;
            internal Action<float> Step;
            internal Action Done;
            internal UnityEngine.Object Owner;
            internal string Channel;
            internal bool Killed;

            /// <summary>Stops it where it is, without calling Done.</summary>
            public void Kill() => Killed = true;
        }

        static Tweener instance;
        readonly List<Tween> tweens = new List<Tween>();
        readonly List<Tween> adding = new List<Tween>();

        /// <summary>The clock: real seconds. Tests and editor captures can set Manual and drive it by hand.</summary>
        public static float Now => Manual >= 0 ? Manual : Time.realtimeSinceStartup;

        public static float Manual = -1;

        static Tweener Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Tweener") { hideFlags = HideFlags.HideAndDontSave };
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                    instance = go.AddComponent<Tweener>();
                }
                return instance;
            }
        }

        /// <summary>Runs step(eased 0..1) for duration seconds, after delay; then done.</summary>
        public static Tween Run(UnityEngine.Object owner, string channel, float duration, Easing.Kind ease, Action<float> step,
                                Action done = null, float delay = 0)
        {
            var self = Instance;
            Stop(owner, channel);
            var t = new Tween
            {
                Start = Now + delay, Duration = duration, Ease = ease, Step = step, Done = done, Owner = owner, Channel = channel,
            };
            if (duration <= 0 && delay <= 0)
            {
                step(1);
                done?.Invoke();
                return t;
            }
            if (delay <= 0)
                step(0);
            self.adding.Add(t);
            return t;
        }

        /// <summary>Stops the owner's tweens on a channel (all channels when null).</summary>
        public static void Stop(UnityEngine.Object owner, string channel = null)
        {
            if (instance == null || owner == null)
                return;
            foreach (var list in new[] { instance.tweens, instance.adding })
            {
                foreach (var t in list)
                {
                    if (t.Owner == owner && (channel == null || t.Channel == channel))
                        t.Killed = true;
                }
            }
        }

        void Update() => Tick();

        /// <summary>Advances every tween to Now (Update calls it; editor captures call it by hand).</summary>
        public static void Tick()
        {
            if (instance == null)
                return;
            var self = instance;
            self.tweens.AddRange(self.adding);
            self.adding.Clear();
            float now = Now;
            for (int i = self.tweens.Count - 1; i >= 0; i--)
            {
                var t = self.tweens[i];
                // the owner was destroyed with its screen: drop it silently
                bool dead = t.Killed || (object)t.Owner != null && t.Owner == null;
                if (dead)
                {
                    self.tweens.RemoveAt(i);
                    continue;
                }
                if (now < t.Start)
                    continue;
                float p = Mathf.Clamp01((now - t.Start) / t.Duration);
                t.Step(Easing.Apply(t.Ease, p));
                if (p >= 1)
                {
                    self.tweens.RemoveAt(i);
                    t.Done?.Invoke();
                }
            }
        }

        // ------------------------------------------------------------------ ready-made tweens

        public static Tween Scale(Transform target, float from, float to, float duration, Easing.Kind ease, Action done = null, float delay = 0) =>
            Run(target, "scale", duration, ease, k => target.localScale = Vector3.one * Easing.Lerp(from, to, k), done, delay);

        public static Tween Fade(CanvasGroup group, float from, float to, float duration, Easing.Kind ease = Easing.Kind.OutQuad, Action done = null, float delay = 0) =>
            Run(group, "alpha", duration, ease, k => group.alpha = Easing.Lerp(from, to, k), done, delay);

        public static Tween Fade(Graphic g, float from, float to, float duration, Easing.Kind ease = Easing.Kind.OutQuad, Action done = null, float delay = 0)
        {
            var c = g.color;
            return Run(g, "alpha", duration, ease, k => g.color = new Color(c.r, c.g, c.b, Easing.Lerp(from, to, k)), done, delay);
        }

        public static Tween Move(RectTransform target, Vector2 from, Vector2 to, float duration, Easing.Kind ease, Action done = null, float delay = 0) =>
            Run(target, "move", duration, ease, k => target.anchoredPosition = Vector2.LerpUnclamped(from, to, k), done, delay);

        /// <summary>A quick bump: grows a little and settles back (a counter that changed).</summary>
        public static Tween Punch(Transform target, float amount = 0.18f, float duration = 0.3f) =>
            Run(target, "scale", duration, Easing.Kind.Linear, k => target.localScale = Vector3.one * (1 + amount * Mathf.Sin(k * Mathf.PI)));
    }
}
