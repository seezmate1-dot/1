using SunnyValley.Game.UI;
using SunnyValley.Layout;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// A full screen: builds its layout from the model for the current screen, binds the buttons, ticks every frame.
    /// When the screen or its safe area changes, it is simply built again (Rebuild) from the same data.
    /// </summary>
    public abstract class GameScreen
    {
        protected readonly GameApp App;
        public UiView View { get; private set; }

        protected GameScreen(GameApp app) => App = app;

        protected UiRoot Ui => App.Ui;

        protected abstract UiLayout Layout(CanvasFit fit);

        protected abstract void Bind();

        public virtual void Tick(float dt)
        {
        }

        public void Show()
        {
            View = Ui.ShowScreen(Layout(Ui.Fit));
            Bind();
        }

        public void Rebuild()
        {
            Hide();
            Show();
        }

        public void Hide()
        {
            View?.Destroy();
            View = null;
        }
    }

    /// <summary>A popup: the same, on the popup layer, opened and closed with an animation.</summary>
    public abstract class GamePopup
    {
        protected readonly GameApp App;
        public UiView View { get; private set; }

        protected GamePopup(GameApp app) => App = app;

        protected UiRoot Ui => App.Ui;

        protected abstract UiLayout Layout(CanvasFit fit);

        protected abstract void Bind();

        public void Open(bool animate = true)
        {
            View = Ui.ShowPopup(Layout(Ui.Fit), animate);
            Bind();
            App.Popups.Add(this);
        }

        public void Close(System.Action done = null)
        {
            App.Popups.Remove(this);
            var view = View;
            View = null;
            Ui.ClosePopup(view, done);
        }

        /// <summary>Built again after a screen change, without the opening animation.</summary>
        public void Rebuild()
        {
            Ui.RemovePopupNow(View);
            View = Ui.ShowPopup(Layout(Ui.Fit), false);
            Bind();
        }
    }
}
