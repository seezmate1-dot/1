using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunnyValley.Core.Meta;
using SunnyValley.Game.Economy;
using SunnyValley.Game.UI;
using SunnyValley.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace SunnyValley.Game.Flow
{
    /// <summary>
    /// The main screen: chapter 1's farm to pan and zoom (FarmWorld), currency plates on top bound to MetaGame (lives
    /// with the timer to the next one), tasks and events on the left, settings, shop and orders on the right, the zone
    /// being restored over the big «Играть N». A task (from «Задания», or the picker for one with three looks) plays
    /// like Gardenscapes: the camera flies to the spot, the stars fly from their plate, a puff of dust, the old thing
    /// goes and the new one pops up, then the replicas, the story scene, the zone and chapter celebrations and the
    /// next zone's fog lifting. A tap on a built object with looks offers «Сменить вид».
    /// Once chapter 1 is over (MetaGame.FarmOpen) the farm works like Township (GDD 6): the side buttons open the
    /// fields and orchard, animals, workshops, the barn, the order board and the town (EconomyPopup), and the strip
    /// over «Играть» shows the farm level and its XP.
    /// </summary>
    public sealed class FarmScreen : GameScreen, IFarmState
    {
        float clock;
        FarmWorld world;
        bool busy;

        public FarmScreen(GameApp app) : base(app)
        {
        }

        MetaGame Meta => App.State.Meta;

        FarmHud Hud()
        {
            long now = GameState.Now;
            var hud = App.State.ToFarmHud(now);
            if (Meta.FarmOpen)
            {
                EconomyViews.Hud(hud, Meta, now);
            }
            else if (Meta.ChapterFinished)
            {
                hud.ZoneName = null;
            }
            else
            {
                var zone = Meta.OpenZones().FirstOrDefault();
                if (zone != null)
                {
                    var (done, total) = Meta.ZoneProgress(zone.Id);
                    hud.ZoneName = zone.Name;
                    hud.ZoneDone = done;
                    hud.ZoneTotal = total;
                }
                else
                {
                    hud.ZoneName = null;
                }
            }
            return hud;
        }

        protected override UiLayout Layout(CanvasFit fit)
        {
            FarmWorld.Source = this;
            return Screens.Farm(fit, Hud());
        }

        protected override void Bind()
        {
            View.OnClick("farm.play", () =>
            {
                if (!busy)
                    App.OpenLevelStart();
            });
            if (View.Has("farm.tasks"))
                View.OnClick("farm.tasks", OpenTasks);
            View.OnClick("farm.settings", () => new SettingsPopup(App, false).Open());
            View.OnClick("farm.events", () => Ui.Toast("События недели скоро: «Гонка урожая» и «Тыквенный фестиваль»"));
            View.OnClick("farm.orders", () =>
            {
                if (Meta.FarmOpen)
                    OpenEconomy(EconomyViews.Orders);
                else
                    Ui.Toast("Заказы соседей откроются, когда ферма будет восстановлена");
            });
            View.OnClick("farm.shop", () => Ui.Toast("Магазин скоро откроется"));
            View.OnClick("farm.coins", () => Ui.Toast(Meta.FarmOpen ? "Монеты дают уровни, заказы и продажа из амбара" : "Монеты дают уровни и заказы"));
            View.OnClick("farm.stars", () => Ui.Toast(Meta.ChapterFinished
                ? "Звёзды строят особые украшения городка — их дают уровни"
                : "Звёзды тратятся на задания восстановления фермы"));
            View.OnClick("farm.crystals", () => Ui.Toast(Meta.FarmOpen
                ? "Кристаллы ускоряют урожай и мастерские, покупают места в очереди"
                : "Кристаллы — за уровни-вехи и события"));
            View.OnClick("farm.lives", LivesInfo);
            if (View.Has("farm.zone"))
                View.OnClick("farm.zone", () => world?.Focus(world.ZoneCenter(CurrentZone()), 0.6f, FarmWorld.StartZoom));
            if (View.Has("farm.fields"))
            {
                // chapter 2: the farm economy's windows
                View.OnClick("farm.fields", () => OpenEconomy(EconomyViews.Fields));
                View.OnClick("farm.animals", () => OpenEconomy(EconomyViews.Animals));
                View.OnClick("farm.workshops", () => OpenEconomy(EconomyViews.Workshops));
                View.OnClick("farm.barn", () => OpenEconomy(EconomyViews.Barn));
                View.OnClick("farm.town", () => OpenEconomy(EconomyViews.Town));
                View.OnClick("farm.xp", () => Ui.Toast(EconomyViews.LevelHint(Meta)));
            }

            world = View.Rect("farm.world")?.GetComponent<FarmWorld>();
            if (world != null)
            {
                world.ObjectTapped += OnObjectTapped;
                world.LockedTapped += zone => Ui.Toast("«" + ZoneName(zone) + "» — " + LockText(zone).ToLowerInvariant());
            }

            // the play button breathes a little, so the eye finds it
            var play = View.Rect("farm.play");
            if (play != null)
                Tweener.Scale(play, 0.6f, 1f, 0.45f, Easing.Kind.OutBack, null, 0.15f);
        }

        void OpenEconomy(string window)
        {
            if (!busy && App.Popups.Count == 0)
                new EconomyPopup(App, window).Open();
        }

        void LivesInfo()
        {
            long now = GameState.Now;
            if (Meta.InfiniteLives(now))
                Ui.Toast("Бесконечные жизни ещё " + Format.Timer(Meta.InfiniteLivesLeft(now)));
            else
                Ui.Toast(Meta.Lives(now) >= App.State.MaxLives
                    ? "Жизни полные. Проигрыш забирает одну, победа — нет"
                    : $"Следующая жизнь через {Format.Timer(Meta.NextLifeIn(now))}");
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
            if (View == null)
                return;
            var hud = Hud();
            View.SetText("farm.coins.value", Format.Amount(hud.Coins));
            View.SetText("farm.stars.value", Format.Amount(hud.Stars));
            View.SetText("farm.crystals.value", Format.Amount(hud.Crystals));
            View.SetText("farm.lives.count", hud.Lives.ToString());
            View.SetText("farm.lives.value", hud.Lives >= hud.MaxLives && !hud.InfiniteLives ? "Полные" : Format.Timer(hud.NextLifeSeconds));
            View.SetText("farm.play.label", "Играть " + hud.Level);
            View.SetBadge("farm.tasks.badge", hud.TasksBadge);
            View.SetBadge("farm.events.badge", hud.EventsBadge);
            View.SetBadge("farm.orders.badge", hud.OrdersBadge);
            if (hud.Economy)
            {
                View.SetText("farm.xp.level", Screens.FarmLevel(hud.FarmLevel));
                View.SetBar("farm.xp.bar", Screens.XpValue(hud.XpHave, hud.XpNeed));
                View.SetText("farm.xp.bar.label", Screens.XpText(hud.XpHave, hud.XpNeed));
                View.SetBadge("farm.fields.badge", hud.FieldsBadge);
                View.SetBadge("farm.animals.badge", hud.AnimalsBadge);
                View.SetBadge("farm.workshops.badge", hud.WorkshopsBadge);
                View.SetBadge("farm.barn.badge", hud.BarnBadge);
                View.SetBadge("farm.town.badge", hud.TownBadge);
            }
            else if (hud.ZoneName != null)
            {
                View.SetText("farm.zone.name", hud.ZoneName);
                View.SetBar("farm.zone.bar", hud.ZoneTotal > 0 ? (float)hud.ZoneDone / hud.ZoneTotal : 1);
                View.SetText("farm.zone.bar.label", Screens.ZoneProgress(hud.ZoneDone, hud.ZoneTotal));
            }
        }

        /// <summary>A currency counter bumps when it grows (after a won level).</summary>
        public void Bump(string plate)
        {
            var rt = View?.Rect(plate + ".icon");
            if (rt != null)
                Tweener.Punch(rt, 0.3f, 0.4f);
        }

        // ------------------------------------------------------------------ IFarmState

        public bool Done(string taskId) => Meta.IsTaskDone(taskId);

        public string Choice(string taskId) => Meta.ChoiceOf(taskId);

        public bool ZoneOpen(string zoneId) => Meta.IsZoneOpen(zoneId);

        public string ZoneName(string zoneId) => Meta.Data.Zones.TryGetValue(zoneId, out var z) ? z.Name : zoneId;

        public string LockText(string zoneId)
        {
            if (!Meta.Data.Zones.TryGetValue(zoneId, out var z) || z.Requires.Count == 0)
                return "Скоро";
            return "Откроется после «" + string.Join("», «", z.Requires.Select(ZoneName)) + "»";
        }

        public string CurrentZone() => Meta.OpenZones().FirstOrDefault()?.Id ?? Meta.Data.Chapter.Zones.Last().Id;

        // ------------------------------------------------------------------ the chapter

        void OpenTasks()
        {
            if (busy)
                return;
            if (Meta.ChapterFinished)
                return;
            new TasksPopup(App, Begin).Open();
        }

        /// <summary>A task picked from the list: the look picker first for a task with three looks.</summary>
        void Begin(TaskDef task)
        {
            if (busy)
                return;
            if (Meta.Stars < task.Stars)
            {
                Ui.Toast("Не хватает звёзд — пройдите уровень, каждый даёт звезду");
                return;
            }
            if (task.Choices.Count > 0)
            {
                world?.Focus(world.TaskPoint(task.Id), 0.5f);
                new PickerPopup(App, task, null, false, choice => App.StartCoroutine(PlayTask(task, choice))).Open();
            }
            else
            {
                App.StartCoroutine(PlayTask(task, null));
            }
        }

        void OnObjectTapped(string taskId)
        {
            if (busy || App.Popups.Count > 0 || !Meta.IsTaskDone(taskId))
                return;
            var task = Meta.Data.Tasks[taskId];
            if (task.Choices.Count == 0)
                return;
            new PickerPopup(App, task, Meta.ChoiceOf(taskId), true, choice =>
            {
                if (choice == Meta.ChoiceOf(taskId))
                    return;
                var r = Meta.ChangeLook(taskId, choice);
                if (!r.Ok)
                {
                    Ui.Toast(r.ErrorText);
                    return;
                }
                App.State.Save();
                if (world != null)
                {
                    world.Focus(world.TaskPoint(taskId), 0.4f);
                    world.PlayTask(taskId);
                }
            }).Open();
        }

        IEnumerator PlayTask(TaskDef task, string choice)
        {
            busy = true;
            var o = Meta.CompleteTask(GameState.Now, task.Id, choice);
            if (!o.Ok)
            {
                Ui.Toast(o.ErrorText);
                busy = false;
                yield break;
            }
            App.State.Save();
            Refresh();

            // the camera to the spot, the stars from their plate to it, dust, the new thing
            if (world != null)
            {
                var point = world.TaskPoint(task.Id);
                world.Focus(point, 0.5f);
                yield return new WaitForSecondsRealtime(0.5f);
                FlyStars(task.Stars, world.WorldPoint(point));
                yield return new WaitForSecondsRealtime(0.75f);
                yield return new WaitForSecondsRealtime(world.PlayTask(task.Id));
            }

            bool closed = false;
            if (o.Lines.Count > 0)
            {
                new DialoguePopup(App, o.Lines, null, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
            }
            if (o.Scene != null)
            {
                closed = false;
                Meta.MarkSceneSeen(o.Scene.Id);
                App.State.Save();
                new DialoguePopup(App, o.Scene.Lines, o.Scene.Title, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
            }
            if (o.ZoneCompleted != null)
            {
                closed = false;
                var zr = o.ZoneCompleted.Reward;
                new CelebratePopup(App, new CelebrateInfo
                {
                    Title = "«" + o.ZoneCompleted.Name + "» восстановлен!",
                    Text = o.ChapterCompleted ? "Последняя часть фермы готова!" : "Все задания выполнены. Впереди — новая часть фермы!",
                    Icon = Icons.Star, Rewards = CelebratePopup.Items(zr, Meta.Data),
                }, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
            }
            if (o.ChapterCompleted)
            {
                closed = false;
                new CelebratePopup(App, new CelebrateInfo
                {
                    Title = "Ферма спасена!",
                    Text = "Глава «" + Meta.Data.Chapter.Title + "» пройдена. Открывается хозяйство: поля, животные, мастерские и заказы соседей.",
                    Icon = Icons.Sun, Rewards = CelebratePopup.Items(Meta.Data.Chapter.FinaleReward, Meta.Data), Button = "Ура!",
                }, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
                // the farm works now: the side buttons of chapter 2 and the farm level strip
                Rebuild();
                Ui.Toast("Загляните на поля: грядки ждут семян");
            }
            else if (o.ZoneCompleted != null)
            {
                // the next zone: the fog lifts, the camera flies there, its opening scene
                foreach (var z in Meta.OpenZones())
                {
                    world?.OpenZone(z.Id);
                    if (world != null)
                    {
                        world.Focus(world.ZoneCenter(z.Id), 0.9f, FarmWorld.StartZoom);
                        yield return new WaitForSecondsRealtime(1.2f);
                    }
                }
                yield return PlayPendingScenes();
            }
            App.AnnounceEvents();
            Refresh();
            busy = false;
        }

        /// <summary>Scenes waiting to be seen: the chapter's opening on the first launch, a new zone's opening.</summary>
        public IEnumerator PlayPendingScenes()
        {
            for (int guard = 0; guard < 4; guard++)
            {
                var scene = Meta.PendingScene();
                if (scene == null)
                    yield break;
                bool closed = false;
                Meta.MarkSceneSeen(scene.Id);
                App.State.Save();
                new DialoguePopup(App, scene.Lines, scene.Title, () => closed = true).Open();
                yield return new WaitUntil(() => closed);
            }
        }

        /// <summary>The task's stars leave their plate and fly in an arc to the spot on the farm.</summary>
        void FlyStars(int count, Vector3 target)
        {
            var from = View?.Rect("farm.stars.icon");
            if (from == null)
                return;
            Bump("farm.stars");
            for (int i = 0; i < Mathf.Clamp(count, 1, 5); i++)
            {
                var go = new GameObject("flying star", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(Ui.OverlayLayer, false);
                rt.sizeDelta = new Vector2(110, 110);
                rt.position = from.position;
                var img = go.AddComponent<Image>();
                img.sprite = ShapeSprites.Icon(Icons.Star);
                img.preserveAspect = true;
                img.raycastTarget = false;
                Vector3 a = from.position, b = target;
                Vector3 mid = (a + b) / 2 + new Vector3((i - 1) * 80, Mathf.Abs(b.y - a.y) * 0.25f + 120, 0);
                Tweener.Run(rt, "fly", 0.65f, Easing.Kind.InOutQuad, k =>
                {
                    // a quadratic curve through the arc's top
                    rt.position = (1 - k) * (1 - k) * a + 2 * (1 - k) * k * mid + k * k * b;
                    rt.localScale = Vector3.one * (1 - 0.5f * k);
                    rt.localRotation = Quaternion.Euler(0, 0, 360 * k);
                }, () => Object.Destroy(go), i * 0.09f);
            }
        }
    }
}
