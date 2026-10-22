using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// The main screen: the farm to pan and zoom, currency plates on top (lives with the timer to the next one),
    /// tasks and events on the left, settings, shop and orders on the right, the big «Играть N» at the bottom.
    /// </summary>
    public sealed class FarmScreen : GameScreen
    {
        float clock;

        public FarmScreen(GameApp app) : base(app)
        {
        }

        protected override UiLayout Layout(CanvasFit fit) => Screens.Farm(fit, App.State.ToFarmHud(GameState.Now));

        protected override void Bind()
        {
            View.OnClick("farm.play", App.OpenLevelStart);
            View.OnClick("farm.tasks", () => new TasksPopup(App).Open());
            View.OnClick("farm.settings", () => new SettingsPopup(App, false).Open());
            View.OnClick("farm.events", () => Ui.Toast("События недели скоро: «Гонка урожая» и «Тыквенный фестиваль»"));
            View.OnClick("farm.orders", () => Ui.Toast("Заказы соседей откроются, когда ферма будет восстановлена"));
            View.OnClick("farm.shop", () => Ui.Toast("Магазин скоро откроется"));
            View.OnClick("farm.coins", () => Ui.Toast("Монеты дают уровни и заказы"));
            View.OnClick("farm.stars", () => Ui.Toast("Звёзды тратятся на задания восстановления фермы"));
            View.OnClick("farm.crystals", () => Ui.Toast("Кристаллы — за уровни-вехи и события"));
            View.OnClick("farm.lives", LivesInfo);

            // the play button breathes a little, so the eye finds it
            var play = View.Rect("farm.play");
            if (play != null)
                Tweener.Scale(play, 0.6f, 1f, 0.45f, Easing.Kind.OutBack, null, 0.15f);
        }

        void LivesInfo()
        {
            var s = App.State;
            Ui.Toast(s.lives >= GameState.MaxLives
                ? "Жизни полные. Проигрыш забирает одну, победа — нет"
                : $"Следующая жизнь через {Format.Timer(s.NextLifeIn(GameState.Now))}");
        }

        /// <summary>Updates the numbers: the lives timer every half second, the rest when they change.</summary>
        public override void Tick(float dt)
        {
            if (View == null)
                return;
            clock += dt;
            if (clock < 0.5f)
                return;
            clock = 0;
            Refresh();
        }

        public void Refresh()
        {
            var hud = App.State.ToFarmHud(GameState.Now);
            View.SetText("farm.coins.value", Format.Amount(hud.Coins));
            View.SetText("farm.stars.value", Format.Amount(hud.Stars));
            View.SetText("farm.crystals.value", Format.Amount(hud.Crystals));
            View.SetText("farm.lives.count", hud.Lives.ToString());
            View.SetText("farm.lives.value", hud.Lives >= hud.MaxLives ? "Полные" : Format.Timer(hud.NextLifeSeconds));
            View.SetText("farm.play.label", "Играть " + hud.Level);
            View.SetBadge("farm.tasks.badge", hud.TasksBadge);
            View.SetBadge("farm.events.badge", hud.EventsBadge);
        }

        /// <summary>A currency counter bumps when it grows (after a won level).</summary>
        public void Bump(string plate)
        {
            var rt = View?.Rect(plate + ".icon");
            if (rt != null)
                Tweener.Punch(rt, 0.3f, 0.4f);
        }
    }
}
