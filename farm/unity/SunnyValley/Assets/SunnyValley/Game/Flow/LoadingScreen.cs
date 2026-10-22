using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Flow
{
    /// <summary>The loading screen: the logo «Солнечная долина», a sun that slowly turns and the progress bar.</summary>
    public sealed class LoadingScreen : GameScreen
    {
        float progress;
        string status = "Загрузка…";

        public LoadingScreen(GameApp app) : base(app)
        {
        }

        protected override UiLayout Layout(CanvasFit fit) => Screens.Loading(fit, progress, status);

        protected override void Bind()
        {
            var logo = View.Rect("loading.logo");
            if (logo != null)
                Tweener.Scale(logo, 0.85f, 1f, 0.6f, Easing.Kind.OutBack);
        }

        public void SetProgress(float value, string text = null)
        {
            progress = Mathf.Clamp01(value);
            if (text != null)
                status = text;
            if (View == null)
                return;
            View.SetBar("loading.bar", progress);
            View.SetText("loading.bar.label", Screens.Percent(progress));
            View.SetText("loading.status", status);
        }

        public override void Tick(float dt)
        {
            var sun = View?.Rect("loading.sun");
            if (sun != null)
                sun.localRotation = Quaternion.Euler(0, 0, -Time.realtimeSinceStartup * 12f);
        }
    }
}
