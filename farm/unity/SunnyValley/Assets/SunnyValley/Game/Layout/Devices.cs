using System;
using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The screens the interface is checked on (tests, CI screenshots): common phone shapes from 16:9 to 21:9 with
    /// their cut-outs and gesture bars, and tablets. Insets are in pixels, as Screen.safeArea reports them.
    /// </summary>
    public static class Devices
    {
        public static readonly ScreenSpec[] All =
        {
            ScreenSpec.WithInsets("16:9 HD 720×1280", 720, 1280, 320),
            ScreenSpec.WithInsets("16:9 FHD 1080×1920", 1080, 1920, 420),
            ScreenSpec.WithInsets("19.5:9 1080×2340 punch-hole", 1080, 2340, 400, top: 110, bottom: 63),
            ScreenSpec.WithInsets("20:9 1080×2400 punch-hole", 1080, 2400, 400, top: 100, bottom: 63),
            ScreenSpec.WithInsets("20:9 QHD 1440×3200", 1440, 3200, 515, top: 140, bottom: 84),
            ScreenSpec.WithInsets("4:3 tablet 1536×2048", 1536, 2048, 264, top: 40),
            ScreenSpec.WithInsets("iPhone 1170×2532 notch", 1170, 2532, 460, top: 141, bottom: 102),
            ScreenSpec.WithInsets("21:9 1080×2520 tall", 1080, 2520, 420, top: 96, bottom: 63),
            ScreenSpec.WithInsets("16:10 tablet 1600×2560", 1600, 2560, 320, top: 72, bottom: 48),
            ScreenSpec.WithInsets("1080×2340 at «large» display size (360 dp)", 1080, 2340, 480, top: 110, bottom: 63),
            ScreenSpec.WithInsets("1080×2400 big gesture bar, wide notch", 1080, 2400, 400, top: 160, bottom: 120),
        };

        /// <summary>The screens the task insists on, by size.</summary>
        public static readonly string[] Required =
        {
            "720×1280", "1080×1920", "1080×2340", "1080×2400", "1440×3200", "1536×2048", "1170×2532",
        };

        /// <summary>A short file-friendly name: 1080x2400.</summary>
        public static string Slug(ScreenSpec s) => s.Width + "x" + s.Height + (s.Name.Contains("big gesture") ? "-insets" : "");
    }

    /// <summary>
    /// Every screen and popup built with the content that stresses it most: the longest numbers, the most goals,
    /// timers, picked boosters. Tests and the CI screenshots go through this list, so a new screen added here is
    /// checked everywhere at once.
    /// </summary>
    public static class LayoutCatalog
    {
        /// <summary>The longest replica of chapter 1 (farm/ui-tests also fit every one of chapter1.json).</summary>
        public const string LongestLine =
            "Но закон говорит и другое: если на празднике урожая ферма будет жить — амбар полон, куры несутся, мельница мелет, — её никто не тронет.";

        public static IEnumerable<(string name, Func<CanvasFit, UiLayout> build)> All()
        {
            yield return ("loading", f => Screens.Loading(f, 0.62f));
            yield return ("farm", f => Screens.Farm(f, new FarmHud()));
            yield return ("farm-max", f => Screens.Farm(f, new FarmHud
            {
                Coins = 99_999, Stars = 99_999, Crystals = 99_999, Lives = 2, NextLifeSeconds = 29 * 60 + 59, Level = 9999,
                TasksBadge = 99, EventsBadge = 3, OrdersBadge = 12, ShopBadge = 1,
            }));
            yield return ("farm-long-timer", f => Screens.Farm(f, new FarmHud { Coins = 9_400_000, Lives = 0, NextLifeSeconds = 5 * 3600 }));
            yield return ("level", f => Screens.Level(f, new LevelHud()));
            yield return ("level-max", f => Screens.Level(f, new LevelHud
            {
                Level = 9999, Moves = 99, Boosters = new[] { 99, 0, 12, 7 },
                Goals = new List<Goal> { new Goal(Icons.Pieces[0], 999), new Goal(Icons.Pieces[3], 999), new Goal(Icons.Pieces[5], 999) },
            }));
            yield return ("level-tall-board", f => Screens.Level(f, new LevelHud { Cols = 7, Rows = 9 }));
            yield return ("level-wide-board", f => Screens.Level(f, new LevelHud { Cols = 9, Rows = 6 }));
            yield return ("popup-start", f => Popups.LevelStart(f, new LevelStartInfo
            {
                Level = 9999, Boosters = new[] { 99, 3, 0 }, Picked = new[] { true, false, false },
                Goals = new List<Goal> { new Goal(Icons.Pieces[1], 999), new Goal(Icons.Pieces[2], 999), new Goal(Icons.Pieces[4], 999) },
            }));
            yield return ("popup-win", f => Popups.Win(f, new WinInfo { Level = 9999, Stars = 2, Coins = 99_999 }));
            yield return ("popup-lose", f => Popups.Lose(f, new LoseInfo
            {
                Price = 2900, Left = new List<Goal> { new Goal(Icons.Pieces[0], 999), new Goal(Icons.Pieces[1], 1), new Goal(Icons.Pieces[2], 99) },
            }));
            yield return ("popup-lose-poor", f => Popups.Lose(f, new LoseInfo { CanAfford = false }));
            yield return ("popup-settings", f => Popups.Settings(f, new SettingsInfo { InLevel = true, Version = "0.1.9999" }));
            yield return ("toast", f => Screens.Toast(f, "Нет жизней. Следующая через 29:59 — а пока загляните на ферму"));
            yield return ("popup-tasks", f => Popups.Tasks(f, Popups.DemoTasks()));
            yield return ("popup-tasks-empty", f => Popups.Tasks(f, new TasksInfo { ZoneName = "Ферма спасена", ZoneDone = 71, ZoneTotal = 71, Stars = 0 }));
            yield return ("farm-no-zone", f => Screens.Farm(f, new FarmHud { ZoneName = null, InfiniteLives = true, NextLifeSeconds = 3599 }));
            yield return ("popup-picker", f => Popups.Picker(f, new PickerInfo
            {
                Title = "Выберите вид", Caption = "Подготовить праздник урожая на мельнице у всей долины", Selected = 2,
                Button = "Построить", Stars = 2,
                Cards = new List<LookCard>
                {
                    new LookCard(Icons.Star, "Деревянные решётки"), new LookCard(Icons.Sun, "Увитая виноградом"),
                    new LookCard(Icons.Coin, "С флюгером-петушком"),
                },
            }));
            yield return ("popup-picker-change", f => Popups.Picker(f, new PickerInfo
            {
                Title = "Сменить вид", Caption = "Крыльцо", Selected = 0, Button = "Сменить",
                Cards = new List<LookCard> { new LookCard(Icons.Star, "Деревянное"), new LookCard(Icons.Star, "Каменное"), new LookCard(Icons.Star, "С цветами") },
            }));
            yield return ("dialogue", f => Popups.Dialogue(f, new DialogueInfo
            {
                Name = "Дневник бабушки Дуни", Portrait = "portrait.diary",
                Text = LongestLine,
            }));
            yield return ("dialogue-scene", f => Popups.Dialogue(f, new DialogueInfo
            {
                Name = "Дед Матвей", Portrait = "portrait.matvey", Right = true, CanSkip = true, SceneTitle = "Урок деда Матвея у старой мельницы",
                Text = "Землю, внучка, не обманешь: что посеешь — то и пожнёшь. А бабушка твоя сеяла с душой.",
            }));
            yield return ("popup-zone-done", f => Popups.Celebrate(f, new CelebrateInfo
            {
                Title = "«Мельница» восстановлена!", Text = "Все задания зоны выполнены. Впереди — новая часть фермы!", Icon = Icons.Star,
                Rewards = new List<(string icon, string text)>
                {
                    (Icons.Coin, "+99 999"), (Icons.Crystal, "+999"), (Icons.Heart, "+60 мин"), (Icons.Pitchfork, "+99"), (Icons.Bomb, "+9"),
                    (Icons.Rainbow, "+9"),
                },
            }));
            yield return ("popup-chapter-done", f => Popups.Celebrate(f, new CelebrateInfo
            {
                Title = "Ферма спасена!", Text = "Глава «Восстановление» пройдена. Открывается хозяйство: поля, животные и заказы соседей.",
                Icon = Icons.Sun, Rewards = new List<(string icon, string text)> { (Icons.Coin, "+5 000"), (Icons.Crystal, "+50") }, Button = "Ура!",
            }));
            yield return ("popup-away", f => Popups.Away(f, new AwayInfo
            {
                Away = "Вас не было 1 д 23 ч",
                Lines = new List<string>
                {
                    "Жизни восстановились: +5", "Пшеница ×12 созрела", "Яйца ×3 готовы", "Грузовик вернулся", "Новые заказы: 3",
                    "Сарай сделал бусты: Лопата ×2", "Молоко ×2 готово",
                },
            }));
            yield return ("popup-soon", f => Popups.Soon(f, "Хозяйство", "Поля, животные, мастерские и заказы соседей откроются в следующем обновлении. Скоро!"));
        }
    }
}
