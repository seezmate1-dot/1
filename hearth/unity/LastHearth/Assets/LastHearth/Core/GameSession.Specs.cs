using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    /// <summary>What a room specialization changes. The first six act on the room's own type, the rest on the fortress.</summary>
    public enum SpecEffect
    {
        None,
        /// <summary>+share of the output of the rooms of this type.</summary>
        Output,
        /// <summary>+hours the rooms of this type keep their output (the offline cap).</summary>
        StockHours,
        /// <summary>The rooms of this type never freeze.</summary>
        NoFreeze,
        /// <summary>+worker places in every room of this type.</summary>
        WorkerSlot,
        /// <summary>+share of the Surge strength of the workers of this type's rooms.</summary>
        Defense,
        /// <summary>+share of the whole fortress's Surge defense (DefenseMult).</summary>
        SurgeAll,
        /// <summary>+share of the attack of every hero in every battle.</summary>
        SquadAttack,
        /// <summary>+share of the health of every hero in every battle.</summary>
        SquadHp,
        /// <summary>+share of the chance that a raid line holds.</summary>
        RaidDefense,
        /// <summary>+goods in Brick's shop.</summary>
        ShopSlot,
        /// <summary>+minutes of hourglass for the daily tasks.</summary>
        TaskHourglass,
        /// <summary>−share of the food the dwellers eat.</summary>
        FoodUse,
        /// <summary>−share of the coal the hearth burns.</summary>
        CoalBurn,
        /// <summary>+share of the Cores of a hunt.</summary>
        HuntCores,
        /// <summary>The forge forges faster (+share of speed).</summary>
        CraftSpeed,
        /// <summary>Injuries and the illness heal faster (+share of speed).</summary>
        HealSpeed,
        /// <summary>−share of the chance that a route brings the illness from the fog.</summary>
        IllnessGuard,
        /// <summary>The library researches faster.</summary>
        ResearchSpeed,
        /// <summary>The archive reads faster.</summary>
        ReadSpeed,
        /// <summary>The training hall trains faster.</summary>
        TrainingSpeed,
        /// <summary>+luck of the tavern's board.</summary>
        RecruitLuck,
        /// <summary>+places on the tavern's board.</summary>
        BoardSlot,
        /// <summary>+share of the chapel's gift of the day.</summary>
        GiftMult,
        /// <summary>Routes take a share less time.</summary>
        RouteSpeed,
        /// <summary>The checks of route points need a share less.</summary>
        RouteCheck,
        /// <summary>+chance to find a note about the world on a route.</summary>
        NoteChance,
        /// <summary>+chance to find a piece of a map on a route.</summary>
        FragmentChance,
        /// <summary>+chance that a route brings a refugee.</summary>
        RefugeeChance,
    }

    public sealed class SpecOption
    {
        public string Name = "";
        public string Text = "";
        public SpecEffect Effect;
        public double Value;
    }

    /// <summary>
    /// ACTIVITIES.md 4, MODES.md 6: specializations of the halls for Blueprints. Each of the 15 halls (all but the
    /// hearth and the living quarters) has its own five tiers, opened at hall level 5, 10, 15, 20 and 25, and on each
    /// tier a choice of two. The production halls choose between more output, longer stock, warm walls and extra
    /// hands; the others make their own work faster or help the squad, the Surge, the raids and the routes.
    /// The choice belongs to the hall type (both coal mines share it). Ranks of mastery from hearth 25 add +50% and +100%.
    /// </summary>
    public sealed partial class GameSession
    {
        public const int SpecTierCount = 5;

        /// <summary>Which version of the table the saved choices follow: 0 the old generic one, 2 the per-hall one.</summary>
        const int SpecTableVersion = 2;

        static SpecOption O(string name, string text, SpecEffect effect, double value) =>
            new SpecOption { Name = name, Text = text, Effect = effect, Value = value };

        static SpecOption[] Tier(SpecOption a, SpecOption b) => new[] { a, b };

        /// <summary>The halls with specializations, in the order of the «Развитие» window.</summary>
        public static readonly RoomType[] SpecRooms =
        {
            RoomType.CoalMine, RoomType.Kitchen, RoomType.Mine, RoomType.TradingPost, RoomType.Greenhouse,
            RoomType.Barracks, RoomType.Warehouse, RoomType.Tavern, RoomType.Forge, RoomType.Infirmary,
            RoomType.Library, RoomType.TrainingHall, RoomType.Watchtower, RoomType.Chapel, RoomType.Archive,
        };

        /// <summary>Five tiers of two options for every hall (MODES.md, 6 lists them).</summary>
        public static readonly Dictionary<RoomType, SpecOption[][]> RoomSpecs = new Dictionary<RoomType, SpecOption[][]>
        {
            [RoomType.CoalMine] = new[]
            {
                Tier(O("Глубокие жилы", "+15% угля", SpecEffect.Output, 0.15), O("Крепкие своды", "шахта не промерзает", SpecEffect.NoFreeze, 1)),
                Tier(O("Вагонетки", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Угольный склад", "+3 часа запаса", SpecEffect.StockHours, 3)),
                Tier(O("Жаркие пласты", "+20% угля", SpecEffect.Output, 0.20), O("Ровный жар", "очаг жжёт на 5% меньше угля", SpecEffect.CoalBurn, 0.05)),
                Tier(O("Дубовая крепь", "+20% силы шахтёров в Наплыве", SpecEffect.Defense, 0.20), O("Бездонный забой", "+4 часа запаса", SpecEffect.StockHours, 4)),
                Tier(O("Сердце горы", "+25% угля", SpecEffect.Output, 0.25), O("Угольная артель", "+1 место работника", SpecEffect.WorkerSlot, 1)),
            },
            [RoomType.Kitchen] = new[]
            {
                Tier(O("Похлёбка", "жители едят на 10% меньше", SpecEffect.FoodUse, 0.10), O("Запасы", "еда копится 14 часов", SpecEffect.StockHours, 4)),
                Tier(O("Печь на углях", "+15% еды", SpecEffect.Output, 0.15), O("Тёплый очаг кухни", "кухня не промерзает", SpecEffect.NoFreeze, 1)),
                Tier(O("Поварята", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Коптильня", "+3 часа запаса", SpecEffect.StockHours, 3)),
                Tier(O("Пряности", "+20% еды", SpecEffect.Output, 0.20), O("Сытный ужин", "+5% здоровья отряда", SpecEffect.SquadHp, 0.05)),
                Tier(O("Пир очага", "+25% еды", SpecEffect.Output, 0.25), O("Котёл на всех", "жители едят на 15% меньше", SpecEffect.FoodUse, 0.15)),
            },
            [RoomType.Mine] = new[]
            {
                Tier(O("Богатая жила", "+10% железа", SpecEffect.Output, 0.10), O("Подпорки", "рудник не промерзает", SpecEffect.NoFreeze, 1)),
                Tier(O("Рудные тележки", "+2 часа запаса", SpecEffect.StockHours, 2), O("Закалённые кирки", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Чистая руда", "+15% железа", SpecEffect.Output, 0.15), O("Горняцкий дозор", "+15% силы рудокопов в Наплыве", SpecEffect.Defense, 0.15)),
                Tier(O("Глубокий штрек", "+3 часа запаса", SpecEffect.StockHours, 3), O("Кузнечная руда", "кузня куёт на 10% быстрее", SpecEffect.CraftSpeed, 0.10)),
                Tier(O("Железное сердце", "+25% железа", SpecEffect.Output, 0.25), O("Вторая смена", "+1 место работника", SpecEffect.WorkerSlot, 1)),
            },
            [RoomType.TradingPost] = new[]
            {
                Tier(O("Связи", "+1 товар в Лавке Брика", SpecEffect.ShopSlot, 1), O("Скупка", "+10% серебра", SpecEffect.Output, 0.10)),
                Tier(O("Тёплая лавка", "лавка не промерзает", SpecEffect.NoFreeze, 1), O("Счетовод", "+2 часа запаса", SpecEffect.StockHours, 2)),
                Tier(O("Караванные пути", "+15% серебра", SpecEffect.Output, 0.15), O("Зазывала", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Сундук купца", "+3 часа запаса", SpecEffect.StockHours, 3), O("Охрана обоза", "+10% обороны в Набегах", SpecEffect.RaidDefense, 0.10)),
                Tier(O("Торговый дом", "+25% серебра", SpecEffect.Output, 0.25), O("Старые долги", "+1 товар в Лавке Брика", SpecEffect.ShopSlot, 1)),
            },
            [RoomType.Greenhouse] = new[]
            {
                Tier(O("Грибные грядки", "+10% еды", SpecEffect.Output, 0.10), O("Двойные рамы", "теплица не промерзает", SpecEffect.NoFreeze, 1)),
                Tier(O("Полив по желобам", "+2 часа запаса", SpecEffect.StockHours, 2), O("Садовники", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Солнечный мох", "+15% еды", SpecEffect.Output, 0.15), O("Целебные травы", "раны и хворь лечатся на 10% быстрее", SpecEffect.HealSpeed, 0.10)),
                Tier(O("Погреб", "+3 часа запаса", SpecEffect.StockHours, 3), O("Семенной фонд", "жители едят на 10% меньше", SpecEffect.FoodUse, 0.10)),
                Tier(O("Зелёный зал", "+25% еды", SpecEffect.Output, 0.25), O("Травники", "раны и хворь лечатся на 20% быстрее", SpecEffect.HealSpeed, 0.20)),
            },
            [RoomType.Barracks] = new[]
            {
                Tier(O("Строевая", "+5% атаки отряда", SpecEffect.SquadAttack, 0.05), O("Дозор", "+15% силы в Наплыве", SpecEffect.SurgeAll, 0.15)),
                Tier(O("Крепкие доспехи", "+5% здоровья отряда", SpecEffect.SquadHp, 0.05), O("Ночная стража", "+15% силы бойцов казармы в Наплыве", SpecEffect.Defense, 0.15)),
                Tier(O("Ветераны", "+8% атаки отряда", SpecEffect.SquadAttack, 0.08), O("Гарнизон", "+20% силы в Наплыве", SpecEffect.SurgeAll, 0.20)),
                Tier(O("Закалка", "+8% здоровья отряда", SpecEffect.SquadHp, 0.08), O("Учебный плац", "+1 место в казарме", SpecEffect.WorkerSlot, 1)),
                Tier(O("Клятва Стражей", "+10% атаки отряда", SpecEffect.SquadAttack, 0.10), O("Бастион", "+25% силы в Наплыве", SpecEffect.SurgeAll, 0.25)),
            },
            [RoomType.Warehouse] = new[]
            {
                Tier(O("Охрана", "+20% обороны в Набегах", SpecEffect.RaidDefense, 0.20), O("Учёт", "+15 минут часов за поручения", SpecEffect.TaskHourglass, 15)),
                Tier(O("Засовы", "+15% обороны в Набегах", SpecEffect.RaidDefense, 0.15), O("Опись", "+10 минут часов за поручения", SpecEffect.TaskHourglass, 10)),
                Tier(O("Ловчие ямы", "+20% обороны в Набегах", SpecEffect.RaidDefense, 0.20), O("Сверка книг", "+15 минут часов за поручения", SpecEffect.TaskHourglass, 15)),
                Tier(O("Окованные двери", "+25% обороны в Набегах", SpecEffect.RaidDefense, 0.25), O("Старший кладовщик", "+15 минут часов за поручения", SpecEffect.TaskHourglass, 15)),
                Tier(O("Неприступный склад", "+30% обороны в Набегах", SpecEffect.RaidDefense, 0.30), O("Летопись запасов", "+20 минут часов за поручения", SpecEffect.TaskHourglass, 20)),
            },
            [RoomType.Tavern] = new[]
            {
                Tier(O("Доска объявлений", "+1 место на доске найма", SpecEffect.BoardSlot, 1), O("Добрый эль", "+5% удачи найма", SpecEffect.RecruitLuck, 0.05)),
                Tier(O("Рассказчики", "+15% к шансу найти записку в вылазке", SpecEffect.NoteChance, 0.15), O("Постоялый двор", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Вербовщик", "+8% удачи найма", SpecEffect.RecruitLuck, 0.08), O("Ночлег", "+10% к шансу привести беженца", SpecEffect.RefugeeChance, 0.10)),
                Tier(O("Знатные гости", "+10% удачи найма", SpecEffect.RecruitLuck, 0.10), O("Второй зал", "+1 место на доске найма", SpecEffect.BoardSlot, 1)),
                Tier(O("Легенды таверны", "+15% удачи найма", SpecEffect.RecruitLuck, 0.15), O("Слава очага", "+15% к шансу привести беженца", SpecEffect.RefugeeChance, 0.15)),
            },
            [RoomType.Forge] = new[]
            {
                Tier(O("Мехи", "ковка на 10% быстрее", SpecEffect.CraftSpeed, 0.10), O("Точильщик", "+3% атаки отряда", SpecEffect.SquadAttack, 0.03)),
                Tier(O("Жаркий горн", "ковка на 15% быстрее", SpecEffect.CraftSpeed, 0.15), O("Подмастерья", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Клёпаная броня", "+4% здоровья отряда", SpecEffect.SquadHp, 0.04), O("Мастер-горн", "ковка на 20% быстрее", SpecEffect.CraftSpeed, 0.20)),
                Tier(O("Закалка клинков", "+5% атаки отряда", SpecEffect.SquadAttack, 0.05), O("Кузнечный дозор", "+15% силы кузнецов в Наплыве", SpecEffect.Defense, 0.15)),
                Tier(O("Наковальня Стражей", "ковка на 25% быстрее", SpecEffect.CraftSpeed, 0.25), O("Броня Стражей", "+6% здоровья отряда", SpecEffect.SquadHp, 0.06)),
            },
            [RoomType.Infirmary] = new[]
            {
                Tier(O("Чистые бинты", "лечение на 10% быстрее", SpecEffect.HealSpeed, 0.10), O("Травяной отвар", "хворь из тумана цепляется на 25% реже", SpecEffect.IllnessGuard, 0.25)),
                Tier(O("Лишние койки", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Окуривание", "хворь цепляется на 20% реже", SpecEffect.IllnessGuard, 0.20)),
                Tier(O("Лекарь", "лечение на 15% быстрее", SpecEffect.HealSpeed, 0.15), O("Полевые сумки", "+4% здоровья отряда", SpecEffect.SquadHp, 0.04)),
                Tier(O("Сиделки", "лечение на 20% быстрее", SpecEffect.HealSpeed, 0.20), O("Обереги от тумана", "хворь цепляется на 30% реже", SpecEffect.IllnessGuard, 0.30)),
                Tier(O("Дом исцеления", "лечение на 25% быстрее", SpecEffect.HealSpeed, 0.25), O("Закалка тела", "+6% здоровья отряда", SpecEffect.SquadHp, 0.06)),
            },
            [RoomType.Library] = new[]
            {
                Tier(O("Свечи", "исследования на 10% быстрее", SpecEffect.ResearchSpeed, 0.10), O("Переписчики", "архив читает на 10% быстрее", SpecEffect.ReadSpeed, 0.10)),
                Tier(O("Читальня", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Карты туманов", "вылазки на 10% короче", SpecEffect.RouteSpeed, 0.10)),
                Tier(O("Каталог", "исследования на 15% быстрее", SpecEffect.ResearchSpeed, 0.15), O("Летописец", "+15% к шансу найти записку", SpecEffect.NoteChance, 0.15)),
                Tier(O("Учёные", "исследования на 20% быстрее", SpecEffect.ResearchSpeed, 0.20), O("Бестиарий", "+5% сердцевин на Охоте", SpecEffect.HuntCores, 0.05)),
                Tier(O("Великая библиотека", "исследования на 25% быстрее", SpecEffect.ResearchSpeed, 0.25), O("Знание чудищ", "+10% сердцевин на Охоте", SpecEffect.HuntCores, 0.10)),
            },
            [RoomType.TrainingHall] = new[]
            {
                Tier(O("Чучела", "тренировка на 10% быстрее", SpecEffect.TrainingSpeed, 0.10), O("Разминка", "+3% здоровья отряда", SpecEffect.SquadHp, 0.03)),
                Tier(O("Тяжёлые гири", "тренировка на 15% быстрее", SpecEffect.TrainingSpeed, 0.15), O("Второй круг", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Наставник", "тренировка на 15% быстрее", SpecEffect.TrainingSpeed, 0.15), O("Спарринг", "+4% атаки отряда", SpecEffect.SquadAttack, 0.04)),
                Tier(O("Полоса препятствий", "тренировка на 20% быстрее", SpecEffect.TrainingSpeed, 0.20), O("Выносливость", "+5% здоровья отряда", SpecEffect.SquadHp, 0.05)),
                Tier(O("Школа Стражей", "тренировка на 25% быстрее", SpecEffect.TrainingSpeed, 0.25), O("Мастер клинка", "+6% атаки отряда", SpecEffect.SquadAttack, 0.06)),
            },
            [RoomType.Watchtower] = new[]
            {
                Tier(O("Следопыты", "вылазки на 10% короче", SpecEffect.RouteSpeed, 0.10), O("Сигнальные огни", "+10% силы в Наплыве", SpecEffect.SurgeAll, 0.10)),
                Tier(O("Подзорная труба", "проверки в вылазках на 10% легче", SpecEffect.RouteCheck, 0.10), O("Дозорные", "+1 место работника", SpecEffect.WorkerSlot, 1)),
                Tier(O("Карты дорог", "+15% к шансу найти обломок карты", SpecEffect.FragmentChance, 0.15), O("Дальний дозор", "+15% силы в Наплыве", SpecEffect.SurgeAll, 0.15)),
                Tier(O("Быстрые ноги", "вылазки на 15% короче", SpecEffect.RouteSpeed, 0.15), O("Ловушки на тропах", "+15% обороны в Набегах", SpecEffect.RaidDefense, 0.15)),
                Tier(O("Глаза гор", "проверки в вылазках на 15% легче", SpecEffect.RouteCheck, 0.15), O("Сторожевой рог", "+20% силы в Наплыве", SpecEffect.SurgeAll, 0.20)),
            },
            [RoomType.Chapel] = new[]
            {
                Tier(O("Благословение", "дар дня на 15% больше", SpecEffect.GiftMult, 0.15), O("Тихая молитва", "лечение на 10% быстрее", SpecEffect.HealSpeed, 0.10)),
                Tier(O("Свечи у алтаря", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Пламя веры", "+4% здоровья отряда", SpecEffect.SquadHp, 0.04)),
                Tier(O("Щедрый дар", "дар дня на 20% больше", SpecEffect.GiftMult, 0.20), O("Паломники", "+10% к шансу привести беженца", SpecEffect.RefugeeChance, 0.10)),
                Tier(O("Обет Угля", "+5% атаки отряда", SpecEffect.SquadAttack, 0.05), O("Огонь хранит", "+15% силы в Наплыве", SpecEffect.SurgeAll, 0.15)),
                Tier(O("Святыня очага", "дар дня на 30% больше", SpecEffect.GiftMult, 0.30), O("Свет во мгле", "хворь цепляется на 30% реже", SpecEffect.IllnessGuard, 0.30)),
            },
            [RoomType.Archive] = new[]
            {
                Tier(O("Опись свитков", "чтение на 15% быстрее", SpecEffect.ReadSpeed, 0.15), O("Летописцы", "+15% к шансу найти записку", SpecEffect.NoteChance, 0.15)),
                Tier(O("Хранители", "+1 место работника", SpecEffect.WorkerSlot, 1), O("Старые карты", "+15% к шансу найти обломок карты", SpecEffect.FragmentChance, 0.15)),
                Tier(O("Мудрость предков", "исследования на 10% быстрее", SpecEffect.ResearchSpeed, 0.10), O("Тайные тропы", "вылазки на 10% короче", SpecEffect.RouteSpeed, 0.10)),
                Tier(O("Читальный зал", "чтение на 20% быстрее", SpecEffect.ReadSpeed, 0.20), O("Карта Стражей", "+20% к шансу найти обломок карты", SpecEffect.FragmentChance, 0.20)),
                Tier(O("Память Стражей", "чтение на 25% быстрее", SpecEffect.ReadSpeed, 0.25), O("Тайны чудищ", "+10% сердцевин на Охоте", SpecEffect.HuntCores, 0.10)),
            },
        };

        /// <summary>The old generic table (before version 2): its choices move to the option of the same kind.</summary>
        static readonly SpecEffect[][] LegacySpecEffects =
        {
            new[] { SpecEffect.Output, SpecEffect.StockHours },
            new[] { SpecEffect.NoFreeze, SpecEffect.WorkerSlot },
            new[] { SpecEffect.Output, SpecEffect.Defense },
            new[] { SpecEffect.StockHours, SpecEffect.WorkerSlot },
            new[] { SpecEffect.Output, SpecEffect.Defense },
        };

        /// <summary>Effects that are a count (a place, a slot, a flag): ranks of mastery do not multiply them.</summary>
        static bool SpecScales(SpecEffect e) =>
            e != SpecEffect.NoFreeze && e != SpecEffect.WorkerSlot && e != SpecEffect.ShopSlot && e != SpecEffect.BoardSlot;

        public static bool HasSpecs(RoomType type) => RoomSpecs.ContainsKey(type);

        /// <summary>An option of a hall (tier 0..4, option 1 or 2), or null.</summary>
        public static SpecOption SpecOptionOf(RoomType type, int tier, int option) =>
            RoomSpecs.TryGetValue(type, out var t) && tier >= 0 && tier < t.Length && option >= 1 && option <= t[tier].Length ? t[tier][option - 1] : null;

        int[] Choices(RoomType type)
        {
            UpgradeSpecs();
            if (!State.SpecChoice.TryGetValue(type, out var c))
                State.SpecChoice[type] = c = new int[SpecTierCount];
            return c;
        }

        int[] Ranks(RoomType type)
        {
            if (!State.SpecRank.TryGetValue(type, out var r))
                State.SpecRank[type] = r = new int[SpecTierCount];
            return r;
        }

        /// <summary>
        /// Saves of the generic table: each choice moves to the option of the hall that does the same kind of thing
        /// (more output stays more output), otherwise it keeps its place; the living quarters (and the hearth) have no
        /// specializations any more, so their Blueprints and ranks come back to the wallet.
        /// </summary>
        void UpgradeSpecs()
        {
            if (State.SpecTable >= SpecTableVersion)
                return;
            State.SpecTable = SpecTableVersion;
            foreach (var type in State.SpecChoice.Keys.ToList())
            {
                var choices = State.SpecChoice[type];
                if (!HasSpecs(type))
                {
                    for (int t = 0; t < choices.Length && t < SpecTierCount; t++)
                    {
                        if (choices[t] != 0)
                            State.Wallet[ResourceType.Blueprints] += SpecPaid(type, t);
                    }
                    State.SpecChoice.Remove(type);
                    State.SpecRank.Remove(type);
                    continue;
                }
                if (choices.Length != SpecTierCount)
                    Array.Resize(ref choices, SpecTierCount);
                State.SpecChoice[type] = choices;
                for (int t = 0; t < SpecTierCount; t++)
                {
                    int o = choices[t];
                    if (o < 1 || o > 2)
                    {
                        choices[t] = 0;
                        continue;
                    }
                    var old = LegacySpecEffects[t][o - 1];
                    var opts = RoomSpecs[type][t];
                    int same = Array.FindIndex(opts, x => x.Effect == old);
                    if (same >= 0)
                        choices[t] = same + 1;
                }
                if (State.SpecRank.TryGetValue(type, out var ranks) && ranks.Length != SpecTierCount)
                {
                    Array.Resize(ref ranks, SpecTierCount);
                    State.SpecRank[type] = ranks;
                }
            }
        }

        public int SpecChosen(RoomType type, int tier) => Choices(type)[tier];

        public int SpecRankOf(RoomType type, int tier) => Ranks(type)[tier];

        /// <summary>The option chosen on a tier, or null.</summary>
        public SpecOption SpecChosenOption(RoomType type, int tier) => SpecOptionOf(type, tier, SpecChosen(type, tier));

        /// <summary>Sum of an effect over the chosen options of a hall type; ranks scale the numbers by 1, 1.5, 2.</summary>
        public double TypeSpecValue(RoomType type, SpecEffect effect)
        {
            UpgradeSpecs();
            if (!RoomSpecs.TryGetValue(type, out var table) || !State.SpecChoice.TryGetValue(type, out var choices))
                return 0;
            State.SpecRank.TryGetValue(type, out var ranks);
            double total = 0;
            for (int t = 0; t < choices.Length && t < table.Length; t++)
            {
                if (choices[t] < 1 || choices[t] > table[t].Length)
                    continue;
                var opt = table[t][choices[t] - 1];
                if (opt.Effect != effect)
                    continue;
                int rank = ranks != null && t < ranks.Length ? Math.Max(1, ranks[t]) : 1;
                total += SpecScales(effect) ? opt.Value * (1 + 0.5 * (rank - 1)) : opt.Value;
            }
            return total;
        }

        /// <summary>An effect of the room's own hall type.</summary>
        public double SpecValue(Room room, SpecEffect effect) => TypeSpecValue(room.Type, effect);

        public bool Specialized(Room room, SpecEffect effect) => SpecValue(room, effect) > 0;

        /// <summary>An effect over every hall (the fortress-wide ones: Surge, squad, raids, routes, the halls' work).</summary>
        public double SpecSum(SpecEffect effect)
        {
            UpgradeSpecs();
            double total = 0;
            foreach (var type in State.SpecChoice.Keys)
                total += TypeSpecValue(type, effect);
            return total;
        }

        public int SpecTierLevel(int tier) => Balance.Activities.Specializations.Tiers[tier][0];

        public long SpecCost(int tier) => Balance.Activities.Specializations.Tiers[tier][1];

        /// <summary>Blueprints paid into a tier: its price and every rank of mastery (×2, ×3).</summary>
        long SpecPaid(RoomType type, int tier)
        {
            int rank = State.SpecRank.TryGetValue(type, out var r) && tier < r.Length ? Math.Max(1, r[tier]) : 1;
            return SpecCost(tier) * rank * (rank + 1) / 2;
        }

        public Fail BuySpec(RoomType type, int tier, int option)
        {
            if (SpecOptionOf(type, tier, option) == null)
                return Fail.NotFound;
            if (MaxRoomLevel(type) < SpecTierLevel(tier))
                return Fail.Locked;
            if (Choices(type)[tier] != 0)
                return Fail.AlreadyQueued;
            long cost = SpecCost(tier);
            if (State.Wallet[ResourceType.Blueprints] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Blueprints] -= cost;
            Choices(type)[tier] = option;
            Ranks(type)[tier] = 1;
            return Fail.None;
        }

        /// <summary>Mastery from hearth 25: rank 2 costs x2 and rank 3 x3 of the tier price.</summary>
        public Fail RankUpSpec(RoomType type, int tier)
        {
            var sp = Balance.Activities.Specializations;
            if (!HasSpecs(type) || tier < 0 || tier >= SpecTierCount || Choices(type)[tier] == 0)
                return Fail.NotFound;
            if (State.HearthLevel < sp.RanksFromHearth)
                return Fail.Locked;
            int rank = Ranks(type)[tier];
            if (rank >= sp.Ranks)
                return Fail.LimitReached;
            long cost = SpecCost(tier) * (rank + 1);
            if (State.Wallet[ResourceType.Blueprints] < cost)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Blueprints] -= cost;
            Ranks(type)[tier] = rank + 1;
            return Fail.None;
        }

        /// <summary>Undoes a tier's choice and gives back everything paid for it (the developer's menu); returns the Blueprints.</summary>
        public long AdminResetSpec(RoomType type, int tier)
        {
            if (!HasSpecs(type) || tier < 0 || tier >= SpecTierCount || Choices(type)[tier] == 0)
                return 0;
            long paid = SpecPaid(type, tier);
            State.Wallet[ResourceType.Blueprints] += paid;
            Choices(type)[tier] = 0;
            Ranks(type)[tier] = 0;
            return paid;
        }

        /// <summary>Every chosen tier of every hall undone and paid back; returns the Blueprints.</summary>
        public long AdminResetAllSpecs()
        {
            long total = 0;
            foreach (var type in SpecRooms)
            {
                for (int t = 0; t < SpecTierCount; t++)
                    total += AdminResetSpec(type, t);
            }
            return total;
        }
    }
}
