using System;
using System.Text;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// A window of the farm economy (chapter 2): fields, the crop picker, animals, workshops, a workshop, the barn,
    /// the order board, the truck, the town. What it shows and what its buttons do come from Game/Economy
    /// (EconomyViews, tested without Unity); this runs the actions through MetaGame, saves after each one, tells
    /// what happened, and reads the view again every half second: timers and bars change in place, anything else
    /// (a plot ripened, a job done) builds the window again where the list was scrolled to.
    /// </summary>
    public sealed class EconomyPopup : GamePopup
    {
        readonly string window;
        string arg;
        EconomyView view;
        string shape;
        float clock;

        public EconomyPopup(GameApp app, string window, string arg = null) : base(app)
        {
            this.window = window;
            this.arg = arg;
        }

        MetaGame Meta => App.State.Meta;

        protected override UiLayout Layout(CanvasFit fit)
        {
            view = EconomyViews.Build(window, arg, Meta, GameState.Now);
            arg = view.Arg;
            shape = Shape(view.Info);
            return Popups.Economy(fit, view.Info);
        }

        protected override void Bind()
        {
            View.OnClick(view.Info.Id + ".panel.close", () => Close());
            foreach (var kv in view.Actions)
            {
                var action = kv.Value;
                View.OnClick(kv.Key, () => Perform(action));
            }
        }

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

        /// <summary>Reads the window again: in place when only timers moved, else built again at the same scroll.</summary>
        public void Refresh()
        {
            if (View == null)
                return;
            var next = EconomyViews.Build(window, arg, Meta, GameState.Now);
            if (Shape(next.Info) != shape)
            {
                Rebuild(keepScroll: true);
                return;
            }
            view = next;
            var info = next.Info;
            string id = info.Id;
            if (info.HeaderText != null)
            {
                View.SetText(id + ".head.text", info.HeaderText);
                if (info.HeaderBar >= 0)
                {
                    View.SetBar(id + ".head.bar", info.HeaderBar);
                    View.SetText(id + ".head.bar.label", info.HeaderBarText ?? "");
                }
            }
            if (info.Queue != null)
            {
                for (int i = 0; i < info.Queue.Count; i++)
                    View.SetText(id + ".slot" + i + ".label", info.Queue[i].Text ?? "");
            }
            for (int i = 0; i < info.Rows.Count; i++)
            {
                var r = info.Rows[i];
                if (info.Cards)
                {
                    View.SetText(id + ".card" + i + ".sub", r.Sub ?? "");
                    continue;
                }
                if (r.Progress >= 0)
                {
                    View.SetBar(id + ".row" + i + ".bar", r.Progress);
                    View.SetText(id + ".row" + i + ".bar.label", r.ProgressText ?? "");
                }
                else if (r.Sub != null)
                {
                    View.SetText(id + ".row" + i + ".sub", r.Sub);
                }
            }
        }

        void Rebuild(bool keepScroll)
        {
            var scroll = View?.Rect(view.Info.Id + ".list")?.GetComponent<ScrollRect>();
            float pos = scroll != null ? scroll.verticalNormalizedPosition : 1;
            Rebuild();
            if (!keepScroll)
                return;
            var again = View?.Rect(view.Info.Id + ".list")?.GetComponent<ScrollRect>();
            if (again != null)
            {
                Canvas.ForceUpdateCanvases();
                again.verticalNormalizedPosition = pos;
            }
        }

        /// <summary>
        /// Everything of a window but its timers: two views of the same shape differ only in what Refresh changes in
        /// place (bars, timer texts, the line under a title).
        /// </summary>
        static string Shape(EconomyInfo info)
        {
            var sb = new StringBuilder();
            sb.Append(info.Id).Append('|').Append(info.Title).Append('|').Append(info.HeaderText != null).Append(info.HeaderBar >= 0);
            foreach (var c in info.HeaderChips)
                sb.Append(c.Icon).Append(c.Text).Append(c.Missing);
            Button(sb, info.HeaderButton);
            if (info.Queue != null)
            {
                foreach (var q in info.Queue)
                    sb.Append('[').Append(q.State).Append(q.Icon).Append(']');
            }
            foreach (var r in info.Rows)
            {
                sb.Append('{').Append(r.Kind).Append(r.Icon).Append(r.Title).Append(r.Dim).Append(r.Picked).Append(r.Caption)
                    .Append(r.Progress >= 0).Append(r.Sub != null);
                foreach (var c in r.Chips)
                    sb.Append(c.Icon).Append(c.Text).Append(c.Missing);
                foreach (var c in r.Rewards)
                    sb.Append(c.Icon).Append(c.Text);
                Button(sb, r.Button);
                Button(sb, r.Alt);
                sb.Append('}');
            }
            foreach (var b in info.Footer)
                Button(sb, b);
            return sb.ToString();
        }

        static void Button(StringBuilder sb, RowButton b)
        {
            if (b != null)
                sb.Append('<').Append(b.Text).Append(b.Icon).Append(b.Tone).Append('>');
        }

        // ------------------------------------------------------------------ the buttons

        void Perform(EconomyAction a)
        {
            switch (a.Kind)
            {
                case ActionKind.Toast:
                    Ui.Toast(a.Text);
                    break;
                case ActionKind.Open:
                    new EconomyPopup(App, a.Window, a.Arg).Open();
                    break;
                case ActionKind.Select:
                    arg = a.Arg;
                    Rebuild(keepScroll: true);
                    break;
                case ActionKind.Run:
                    if (a.Confirm != null)
                        new ConfirmPopup(App, a.Confirm, () => Execute(a)).Open();
                    else
                        Execute(a);
                    break;
            }
        }

        void Execute(EconomyAction a)
        {
            var r = a.Run(GameState.Now);
            if (!r.Ok)
            {
                Ui.Toast(r.ErrorText);
                return;
            }
            App.State.Save();
            string text = a.Text ?? (r.Reward.IsEmpty ? null : "Получено: " + r.Reward.Describe(Meta.Data));
            if (text != null)
                Ui.Toast(text);
            App.AnnounceEvents();
            (App.Current as FarmScreen)?.Refresh();
            // the crop picker goes back to the fields; the windows still open show what changed
            if (a.Close)
                Close();
            foreach (var p in App.Popups.ToArray())
                (p as EconomyPopup)?.Refresh();
        }
    }

    /// <summary>A question before crystals are spent or an order is thrown away: a picture, a line, the button that does it.</summary>
    public sealed class ConfirmPopup : GamePopup
    {
        readonly ConfirmInfo info;
        readonly Action yes;

        public ConfirmPopup(GameApp app, ConfirmInfo info, Action yes) : base(app)
        {
            this.info = info;
            this.yes = yes;
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Confirm(fit, info);

        protected override void Bind()
        {
            View.OnClick("confirm.panel.close", () => Close());
            View.OnClick("confirm.ok", () => Close(yes));
        }
    }
}
