using System;
using System.Collections.Generic;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunnyValley.Game.UI
{
    /// <summary>
    /// The one canvas of the game: CanvasScaler with the 1080×1920 reference (match width on phones taller than 16:9,
    /// height on tablets — CanvasFit), three layers (screens, popups, a fade veil) and the event system.
    /// Watches the screen size and the safe area every frame and raises Changed when either moves (rotation is off,
    /// but split screen, foldables and a cut-out reported late all happen), so screens rebuild their layout.
    /// For editor captures it renders into a camera at a given fake screen instead (Editor/Screenshots.cs).
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform PopupLayer { get; private set; }
        public RectTransform OverlayLayer { get; private set; }
        Image veil;

        /// <summary>The screen as the layout sees it, and the canvas on it.</summary>
        public ScreenSpec Spec { get; private set; }
        public CanvasFit Fit { get; private set; }

        /// <summary>The screen or its safe area changed: rebuild.</summary>
        public event Action Changed;

        ScreenSpec fixedSpec;
        int lastW, lastH;
        Rect lastSafe;

        readonly List<UiView> popups = new List<UiView>();

        /// <summary>The game's canvas on the device screen.</summary>
        public static UiRoot Create()
        {
            var go = new GameObject("UI");
            DontDestroyOnLoad(go);
            var root = go.AddComponent<UiRoot>();
            root.Setup(null, null);
            EnsureEventSystem();
            return root;
        }

        /// <summary>A canvas drawn by a camera at a fixed fake screen (captures in the editor).</summary>
        public static UiRoot CreateForCapture(ScreenSpec spec, Camera camera)
        {
            var go = new GameObject("UI capture");
            var root = go.AddComponent<UiRoot>();
            root.Setup(spec, camera);
            return root;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        void Setup(ScreenSpec spec, Camera camera)
        {
            fixedSpec = spec;
            gameObject.layer = 5; // UI
            Canvas = gameObject.AddComponent<Canvas>();
            Scaler = gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();
            Scaler.referenceResolution = new Vector2(CanvasFit.ReferenceWidth, CanvasFit.ReferenceHeight);
            Scaler.referencePixelsPerUnit = 100;
            if (camera != null)
            {
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = camera;
                Canvas.planeDistance = 10;
                // the camera draws into a texture of the fake screen's size: scale exactly as the model says
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            }
            else
            {
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            }
            Canvas.pixelPerfect = false;

            ScreenLayer = Layer("Screens");
            PopupLayer = Layer("Popups");
            OverlayLayer = Layer("Overlay");
            veil = OverlayLayer.gameObject.AddComponent<Image>();
            veil.color = new Color(1, 0.97f, 0.88f, 0);
            veil.raycastTarget = false;
            Measure(true);
        }

        RectTransform Layer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>The current screen as a ScreenSpec: size, density and Screen.safeArea.</summary>
        public static ScreenSpec DeviceSpec()
        {
            var sa = Screen.safeArea;
            return new ScreenSpec("device", Screen.width, Screen.height, Screen.dpi, new Box(sa.x, sa.y, sa.width, sa.height));
        }

        bool Measure(bool force)
        {
            if (fixedSpec == null && !force && Screen.width == lastW && Screen.height == lastH && Screen.safeArea == lastSafe)
                return false;
            if (fixedSpec != null && !force)
                return false;
            Spec = fixedSpec ?? DeviceSpec();
            lastW = Screen.width;
            lastH = Screen.height;
            lastSafe = Screen.safeArea;
            Fit = new CanvasFit(Spec);
            if (fixedSpec != null)
                Scaler.scaleFactor = Fit.Scale;
            else
                Scaler.matchWidthOrHeight = Fit.Match;
            return true;
        }

        void Update()
        {
            if (Measure(false))
                Changed?.Invoke();
        }

        // ------------------------------------------------------------------ screens and popups

        /// <summary>Builds a full screen under the popups.</summary>
        public UiView ShowScreen(UiLayout layout) => UiView.Build(layout, ScreenLayer);

        /// <summary>Opens a popup: the veil fades in, the panel pops from 70% with a small overshoot.</summary>
        public UiView ShowPopup(UiLayout layout, bool animate = true)
        {
            var view = UiView.Build(layout, PopupLayer);
            popups.Add(view);
            if (animate)
                AnimateIn(view);
            return view;
        }

        public bool HasPopup => popups.Count > 0;

        public UiView TopPopup => popups.Count > 0 ? popups[popups.Count - 1] : null;

        static void AnimateIn(UiView view)
        {
            var dim = view.Face(view.Layout.Name + ".dim");
            if (dim != null)
                Tweener.Fade(dim, 0, dim.color.a, 0.22f);
            var panel = view.Rect(view.Layout.Name + ".panel");
            if (panel != null)
            {
                var group = panel.gameObject.AddComponent<CanvasGroup>();
                Tweener.Fade(group, 0, 1, 0.16f);
                Tweener.Scale(panel, 0.7f, 1, 0.34f, Easing.Kind.OutBack);
            }
        }

        /// <summary>Closes a popup: the panel shrinks back and fades with the veil, then it is destroyed.</summary>
        public void ClosePopup(UiView view, Action done = null)
        {
            if (view == null || !popups.Remove(view))
                return;
            var panel = view.Rect(view.Layout.Name + ".panel");
            var dim = view.Face(view.Layout.Name + ".dim");
            var group = panel != null ? panel.GetComponent<CanvasGroup>() : null;
            if (group != null)
                group.blocksRaycasts = false;
            if (dim != null)
                Tweener.Fade(dim, dim.color.a, 0, 0.2f);
            if (group != null)
                Tweener.Fade(group, 1, 0, 0.2f, Easing.Kind.InQuad);
            if (panel != null)
            {
                Tweener.Scale(panel, 1, 0.8f, 0.2f, Easing.Kind.InBack, () =>
                {
                    view.Destroy();
                    done?.Invoke();
                });
            }
            else
            {
                view.Destroy();
                done?.Invoke();
            }
        }

        public void CloseAllPopups()
        {
            foreach (var p in popups.ToArray())
            {
                popups.Remove(p);
                p.Destroy();
            }
        }

        /// <summary>Fades to a warm white, runs middle (swap the screen), fades back.</summary>
        public void FadeThrough(Action middle)
        {
            veil.raycastTarget = true;
            Tweener.Fade(veil, veil.color.a, 1, 0.22f, Easing.Kind.InQuad, () =>
            {
                middle();
                Tweener.Fade(veil, 1, 0, 0.3f, Easing.Kind.OutQuad, () => veil.raycastTarget = false, 0.05f);
            });
        }

        // ------------------------------------------------------------------ toast

        UiView toast;

        /// <summary>A short message under the top bar that fades away by itself.</summary>
        public void Toast(string text)
        {
            toast?.Destroy();
            var view = UiView.Build(Screens.Toast(Fit, text), OverlayLayer);
            toast = view;
            var pill = view.Rect("toast.pill");
            var group = pill.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tweener.Fade(group, 0, 1, 0.18f);
            Tweener.Scale(pill, 0.85f, 1, 0.3f, Easing.Kind.OutBack);
            Tweener.Run(pill, "fade-out", 0.35f, Easing.Kind.InQuad, k => group.alpha = 1 - k, () =>
            {
                if (toast == view)
                    toast = null;
                view.Destroy();
            }, 2.2f);
        }
    }
}
