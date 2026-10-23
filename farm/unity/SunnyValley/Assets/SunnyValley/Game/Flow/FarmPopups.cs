using System;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Meta;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Flow
{
    /// <summary>The restoration tasks open now (GDD 5), with the zone's progress; a row's button starts the task.</summary>
    public sealed class TasksPopup : GamePopup
    {
        readonly Action<TaskDef> pick;
        List<TaskDef> tasks;

        public TasksPopup(GameApp app, Action<TaskDef> pick) : base(app) => this.pick = pick;

        protected override UiLayout Layout(CanvasFit fit)
        {
            var meta = App.State.Meta;
            tasks = meta.AvailableTasks();
            var zone = meta.OpenZones().FirstOrDefault();
            var info = new TasksInfo { Stars = meta.Stars };
            if (zone != null)
            {
                var (done, total) = meta.ZoneProgress(zone.Id);
                info.ZoneName = zone.Name;
                info.ZoneDone = done;
                info.ZoneTotal = total;
            }
            else
            {
                info.ZoneName = "Ферма восстановлена";
                info.ZoneDone = info.ZoneTotal = 1;
            }
            foreach (var t in tasks)
            {
                info.Tasks.Add(new TaskItem(t.Title, t.Stars, meta.Data.Zones[t.Zone].Name, t.Stars <= meta.Stars, t.Choices.Count > 0, t.Id));
            }
            return Popups.Tasks(fit, info);
        }

        protected override void Bind()
        {
            View.OnClick("tasks.panel.close", () => Close());
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                View.OnClick("tasks.row" + i + ".do", () =>
                {
                    if (App.State.Meta.Stars < task.Stars)
                    {
                        Ui.Toast("Не хватает звёзд — пройдите уровень, каждый даёт звезду");
                        return;
                    }
                    Close(() => pick(task));
                });
            }
        }
    }

    /// <summary>Three looks of a task: tap a card to pick it, then the button builds it (or changes the look).</summary>
    public sealed class PickerPopup : GamePopup
    {
        readonly PickerInfo info;
        readonly List<string> choiceIds;
        readonly Action<string> confirm;

        public PickerPopup(GameApp app, TaskDef task, string current, bool change, Action<string> confirm) : base(app)
        {
            this.confirm = confirm;
            choiceIds = task.Choices.Select(c => c.Id).ToList();
            var sprite = FarmArt.Layout.SpritesOf(task.Id).Select(s => s.sprite).FirstOrDefault(s => s.Looks != null);
            info = new PickerInfo
            {
                Title = change ? "Сменить вид" : "Выберите вид",
                Caption = task.Title,
                Selected = Math.Max(0, current != null ? choiceIds.IndexOf(current) : 0),
                Button = change ? "Сменить" : "Построить",
                Stars = change ? 0 : task.Stars,
            };
            foreach (var c in task.Choices)
            {
                string art = sprite != null ? sprite.ArtFor(c.Id) : null;
                info.Cards.Add(new LookCard(art != null ? "farm/" + art : Icons.Star, c.Name));
            }
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Picker(fit, info);

        protected override void Bind()
        {
            View.OnClick("picker.panel.close", () => Close());
            for (int i = 0; i < info.Cards.Count; i++)
            {
                int k = i;
                View.OnClick("picker.card" + i, () => Select(k));
            }
            View.OnClick("picker.ok", () =>
            {
                string id = choiceIds[Mathf.Clamp(info.Selected, 0, choiceIds.Count - 1)];
                Close(() => confirm(id));
            });
        }

        void Select(int k)
        {
            info.Selected = k;
            for (int i = 0; i < info.Cards.Count; i++)
            {
                var face = View.Face("picker.card" + i);
                if (face != null)
                    face.color = UiView.ToColor(i == k ? Popups.CardPicked : Popups.CardIdle);
                View.SetVisible("picker.card" + i + ".tick", i == k);
            }
            var card = View.Rect("picker.card" + k + ".icon");
            if (card != null)
                Tweener.Punch(card, 0.12f, 0.3f);
        }
    }

    /// <summary>
    /// A replica or a story scene: the speaker's portrait, name and text; a tap anywhere shows the next line, the
    /// last one closes it. A scene can be skipped. The window is laid out again for every line (the text is fitted
    /// to the panel by the same rules the tests check).
    /// </summary>
    public sealed class DialoguePopup : GamePopup
    {
        readonly List<DialogueLine> lines;
        readonly string title;
        readonly Action done;
        int index;
        bool closing;

        public DialoguePopup(GameApp app, IEnumerable<DialogueLine> lines, string sceneTitle, Action done) : base(app)
        {
            this.lines = lines?.Where(l => l != null).ToList() ?? new List<DialogueLine>();
            title = sceneTitle;
            this.done = done;
        }

        DialogueInfo Info()
        {
            var line = lines[Mathf.Clamp(index, 0, lines.Count - 1)];
            var data = App.State.Data;
            string name = line.Who != null && data.Characters.TryGetValue(line.Who, out var c) ? c.Name : line.Who ?? "";
            return new DialogueInfo
            {
                Name = name, Portrait = "portrait." + line.Who, Text = line.Text, SceneTitle = title,
                CanSkip = title != null && lines.Count > 1, Right = line.Who != "anya" && line.Who != "diary",
            };
        }

        public new void Open(bool animate = true)
        {
            if (lines.Count == 0)
            {
                done?.Invoke();
                return;
            }
            base.Open(animate);
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Dialogue(fit, Info());

        protected override void Bind()
        {
            View.OnClick("dialogue.next", Next);
            if (View.Has("dialogue.skip"))
                View.OnClick("dialogue.skip", Finish);
            var portrait = View.Rect("dialogue.portrait");
            if (portrait != null)
                Tweener.Scale(portrait, 0.85f, 1, 0.3f, Easing.Kind.OutBack);
        }

        void Next()
        {
            if (closing)
                return;
            index++;
            if (index >= lines.Count)
            {
                Finish();
                return;
            }
            Rebuild();
            var text = View.Rect("dialogue.text");
            if (text != null)
                Tweener.Scale(text, 0.96f, 1, 0.2f, Easing.Kind.OutQuad);
        }

        void Finish()
        {
            if (closing)
                return;
            closing = true;
            Close(done);
        }
    }

    /// <summary>A zone or the chapter done: the picture turns on its rays, the rewards, «Дальше».</summary>
    public sealed class CelebratePopup : GamePopup
    {
        readonly CelebrateInfo info;
        readonly Action done;

        public CelebratePopup(GameApp app, CelebrateInfo info, Action done) : base(app)
        {
            this.info = info;
            this.done = done;
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Celebrate(fit, info);

        protected override void Bind()
        {
            var rays = View.Rect("celebrate.rays");
            if (rays != null)
                Tweener.Run(rays, "spin", 60f, Easing.Kind.Linear, k => rays.localRotation = Quaternion.Euler(0, 0, -k * 1800));
            var icon = View.Rect("celebrate.icon");
            if (icon != null)
                Tweener.Scale(icon, 0, 1, 0.5f, Easing.Kind.OutBack, null, 0.2f);
            for (int i = 0; i < 6; i++)
            {
                var item = View.Rect("celebrate.rewards.item" + i);
                if (item != null)
                    Tweener.Scale(item, 0, 1, 0.35f, Easing.Kind.OutBack, null, 0.45f + i * 0.1f);
            }
            View.OnClick("celebrate.next", () => Close(done));
        }

        /// <summary>A reward as the celebration lists it: an icon and «+N».</summary>
        public static List<(string icon, string text)> Items(Reward r, MetaData data)
        {
            var list = new List<(string, string)>();
            if (r == null)
                return list;
            if (r.Coins > 0) list.Add((Icons.Coin, "+" + Format.Amount(r.Coins)));
            if (r.Crystals > 0) list.Add((Icons.Crystal, "+" + r.Crystals));
            if (r.Stars > 0) list.Add((Icons.Star, "+" + r.Stars));
            if (r.Lives > 0) list.Add((Icons.Heart, "+" + r.Lives));
            if (r.InfiniteLivesMinutes > 0) list.Add((Icons.Heart, r.InfiniteLivesMinutes + " мин"));
            foreach (var kv in r.Boosters)
                list.Add((BoosterIcon(kv.Key), "+" + kv.Value));
            return list;
        }

        public static string BoosterIcon(string id)
        {
            int pre = Array.IndexOf(Boosters.PreLevel, id);
            if (pre >= 0)
                return Popups.PreBoosterIcons[pre];
            int inLevel = Array.IndexOf(Boosters.InLevel, id);
            return inLevel >= 0 ? Screens.BoosterIcons[inLevel] : Icons.Star;
        }
    }

    /// <summary>
    /// «Пока вас не было»: what grew, came back and got made while the game was closed (GDD 8). On the working farm,
    /// with something ripe, «Собрать всё» collects every field, tree, animal and workshop at once (MetaGame.CollectAll).
    /// </summary>
    public sealed class AwayPopup : GamePopup
    {
        readonly AwayInfo info;
        readonly Action done;

        public AwayPopup(GameApp app, AwaySummary summary, Action done) : base(app)
        {
            info = new AwayInfo
            {
                Away = "Вас не было " + Texts.Duration(summary.AwaySeconds), Lines = new List<string>(summary.Lines),
                CollectAll = app.State.Meta.FarmOpen && (summary.Ready.Count > 0 || summary.Boosters.Count > 0),
            };
            this.done = done;
        }

        protected override UiLayout Layout(CanvasFit fit) => Popups.Away(fit, info);

        protected override void Bind()
        {
            View.OnClick("away.ok", () => Close(done));
            if (info.CollectAll)
                View.OnClick("away.collect", Collect);
        }

        void Collect()
        {
            var meta = App.State.Meta;
            var r = meta.CollectAll(GameState.Now);
            App.State.Save();
            Ui.Toast(r.IsEmpty ? Texts.Error(MetaError.BarnFull) : "Собрано: " + r.Describe(meta.Data));
            Close(done);
        }
    }
}
