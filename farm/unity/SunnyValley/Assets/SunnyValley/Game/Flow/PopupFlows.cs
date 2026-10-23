using System.Collections.Generic;
using System.Linq;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;

namespace SunnyValley.Game.Flow
{
    /// <summary>Before a level: its goals, the boosters to take along (tap to pick), «Играть».</summary>
    public sealed class LevelStartPopup : GamePopup
    {
        readonly LevelStartInfo info;

        public LevelStartPopup(GameApp app, LevelStartInfo info) : base(app) => this.info = info;

        protected override UiLayout Layout(CanvasFit fit) => Popups.LevelStart(fit, info);

        protected override void Bind()
        {
            View.OnClick("start.panel.close", () => Close());
            View.OnClick("start.play", () => Close(() => App.StartLevel(info)));
            for (int i = 0; i < Popups.PreBoosterIcons.Length; i++)
            {
                int b = i;
                View.OnClick("start.booster" + i, () => Toggle(b));
            }
        }

        void Toggle(int b)
        {
            if (!info.Picked[b] && info.Boosters[b] <= 0)
            {
                Ui.Toast($"«{Popups.PreBoosterNames[b]}» закончились");
                return;
            }
            info.Picked[b] = !info.Picked[b];
            string tick = "start.booster" + b + ".picked";
            View.SetVisible(tick, info.Picked[b]);
            if (info.Picked[b])
                Tweener.Scale(View.Rect(tick), 0.3f, 1, 0.3f, Easing.Kind.OutBack);
        }
    }

    /// <summary>Won: the stars pop in one by one, the rewards, «Дальше».</summary>
    public sealed class WinPopup : GamePopup
    {
        readonly WinInfo info;

        public WinPopup(GameApp app, WinInfo info) : base(app) => this.info = info;

        protected override UiLayout Layout(CanvasFit fit) => Popups.Win(fit, info);

        protected override void Bind()
        {
            for (int i = 0; i < 3; i++)
            {
                var star = View.Rect("win.star" + i);
                if (star != null)
                    Tweener.Scale(star, 0, 1, 0.42f, Easing.Kind.OutBack, null, 0.25f + i * 0.18f);
            }
            View.OnClick("win.next", () => Close(App.BackToFarm));
        }
    }

    /// <summary>Out of moves: what is left, «+5 ходов за N» (if there are coins), «Сдаться».</summary>
    public sealed class LosePopup : GamePopup
    {
        readonly LevelScreen level;
        readonly LoseInfo info;

        public LosePopup(GameApp app, LevelScreen level) : base(app)
        {
            this.level = level;
            int price = app.State.Meta.ExtraMovesPrice;
            info = new LoseInfo
            {
                Level = level.Session.Level, Price = price, CanAfford = app.State.Meta.Coins >= price,
                Left = level.Session.Goals.Where(g => g.Count > 0).Select(g => new Goal(g.Icon, g.Count)).ToList(),
            };
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Lose(fit, info);

        protected override void Bind()
        {
            View.OnClick("lose.more", () =>
            {
                var r = App.State.Meta.BuyExtraMoves(GameState.Now);
                if (!r.Ok)
                {
                    Ui.Toast(r.Error == Core.Meta.MetaError.NotEnoughCoins ? "Не хватает монет. Их приносят уровни и заказы" : r.ErrorText);
                    return;
                }
                App.State.Save();
                Close(() => level.Continue(App.State.Meta.ExtraMovesCount));
            });
            View.OnClick("lose.give_up", () => Close(App.LevelLost));
        }
    }

    /// <summary>Settings: music, sounds, vibration; support; leaving a level from the pause.</summary>
    public sealed class SettingsPopup : GamePopup
    {
        readonly bool inLevel;

        public SettingsPopup(GameApp app, bool inLevel) : base(app) => this.inLevel = inLevel;

        protected override UiLayout Layout(CanvasFit fit)
        {
            var s = App.State;
            return Popups.Settings(fit, new SettingsInfo
            {
                Music = s.music, Sounds = s.sounds, Vibration = s.vibration, InLevel = inLevel, Version = Application.version,
            });
        }

        protected override void Bind()
        {
            View.OnClick("settings.panel.close", () => Close());
            View.OnClick("settings.toggle0", () => Flip(0));
            View.OnClick("settings.toggle1", () => Flip(1));
            View.OnClick("settings.toggle2", () => Flip(2));
            View.OnClick("settings.support", () => Ui.Toast("Поддержка: напишите нам из меню магазина приложений"));
            if (inLevel)
                View.OnClick("settings.quit", () => Close(App.LevelLost));
        }

        void Flip(int i)
        {
            var s = App.State;
            bool on = i == 0 ? s.music = !s.music : i == 1 ? s.sounds = !s.sounds : s.vibration = !s.vibration;
            s.Save();
            View.SetToggle("settings.toggle" + i, on);
        }
    }
}
