using System.Collections.Generic;

namespace SunnyValley.Layout
{
    /// <summary>
    /// The windows of the farm economy with the content that stresses them most (the longest names of balance.json,
    /// the biggest numbers, every kind of row), for LayoutCatalog: the tests and the wireframes. The real windows are
    /// filled by Game/Economy from MetaGame and checked with the real data by farm/ui-tests too.
    /// </summary>
    public static class EconomyDemo
    {
        static EconomyRow Row(string icon, string title, string sub, RowButton button, RowButton alt = null, bool dim = false) =>
            new EconomyRow { Icon = icon, Title = title, Sub = sub, Button = button, Alt = alt, Dim = dim };

        static EconomyRow Timer(string icon, string title, float progress, string text, RowButton button) =>
            new EconomyRow { Icon = icon, Title = title, Progress = progress, ProgressText = text, Button = button };

        static RowButton Crystals(int n) => new RowButton(n.ToString(), Icons.Crystal, Tone.Blue);

        static RowButton Coins(long n, bool can = true) => new RowButton(Format.Amount(n), Icons.Coin, can ? Tone.Green : Tone.Grey);

        static RowButton Locked(int level) => new RowButton("Ур. " + level, null, Tone.Grey);

        public static FarmHud Hud() => new FarmHud
        {
            Economy = true, Coins = 99_999, Stars = 99_999, Crystals = 99_999, Lives = 2, NextLifeSeconds = 29 * 60 + 59, Level = 9999,
            FarmLevel = 30, XpHave = 99_999, XpNeed = 99_999, FieldsBadge = 30, AnimalsBadge = 99, WorkshopsBadge = 12, OrdersBadge = 9, BarnBadge = 1,
            ShopBadge = 1, EventsBadge = 3, TownBadge = 2,
        };

        public static EconomyInfo Fields() => new EconomyInfo
        {
            Id = "fields", Title = "Поля и сад",
            Rows = new List<EconomyRow>
            {
                EconomyRow.Section("Грядки: 30 из 30"),
                Row(Icons.Soil, "Грядка 1", "Пусто", new RowButton("Посадить")),
                Timer(Icons.Item("pumpkin"), "Тыква", 0.05f, "7:59:59", Crystals(48)),
                Row(Icons.Item("strawberry"), "Клубника", "Созрела: урожай ×2", new RowButton("Собрать", null, Tone.Yellow)),
                EconomyRow.Section("Сад"),
                Timer(Icons.Item("cherry"), "Вишня", 0.5f, "1:05:00", Crystals(7)),
                Row(Icons.Item("pear"), "Груша", "Поспело: урожай ×3", new RowButton("Собрать", null, Tone.Yellow)),
                Row(Icons.Item("cherry"), "Ещё вишня", "6 из 6 · откроется на ур. 14", Locked(14), null, true),
                Row(Icons.Item("pear"), "Ещё груша", "2 из 6 деревьев", Coins(99_999, false)),
            },
            Footer = new List<RowButton> { new RowButton("Собрать всё", Icons.Fields, Tone.Yellow), new RowButton("Грядка за 4 500") },
        };

        public static EconomyInfo Crops() => new EconomyInfo
        {
            Id = "crops", Title = "Что посадить?",
            HeaderText = "Грядка 30: семена за монеты или своё из амбара",
            Rows = new List<EconomyRow>
            {
                Row(Icons.Item("wheat"), "Пшеница", "2 мин · урожай ×2", Coins(1), new RowButton(null, Icons.Barn, Tone.Yellow)),
                Row(Icons.Item("sunflower"), "Подсолнух", "20 мин · урожай ×2", Coins(2, false)),
                Row(Icons.Item("pumpkin"), "Тыква", "8 ч · урожай ×2", Coins(26), new RowButton(null, Icons.Barn, Tone.Yellow)),
                Row(Icons.Item("potato"), "Картофель", "Нужен 13 уровень", Locked(13), null, true),
            },
        };

        public static EconomyInfo Animals() => new EconomyInfo
        {
            Id = "animals", Title = "Животные",
            Rows = new List<EconomyRow>
            {
                EconomyRow.Section("Курятник: 6 из 6"),
                Row(Icons.Item("egg"), "Курица", "Голодна · корм для кур: 99", new RowButton("Кормить", null, Tone.Green)),
                Timer(Icons.Item("egg"), "Курица", 0.3f, "19:59", Crystals(2)),
                Row(Icons.Item("egg"), "Курица", "Яйцо готово", new RowButton("Собрать", null, Tone.Yellow)),
                Row(Icons.Item("chicken_feed"), "Курица", "Нужен корм: 0", new RowButton("Мельница", null, Tone.Blue)),
                Row(Icons.Animals, "Ещё курица", "6 из 6", Coins(99_999)),
                EconomyRow.Section("Свинарник"),
                Row(Icons.Barn, "Свинарник", "Ур. 15 · жителей: 200", Coins(5_000, false), null, true),
            },
            Footer = new List<RowButton> { new RowButton("Собрать всё", Icons.Animals, Tone.Yellow), new RowButton("Покормить всех") },
        };

        public static EconomyInfo Workshops() => new EconomyInfo
        {
            Id = "workshops", Title = "Мастерские",
            Rows = new List<EconomyRow>
            {
                Row(Icons.Workshop, "Сарай инструментов", "Готово: 6 · в очереди 6 из 6", new RowButton("Открыть", null, Tone.Yellow)),
                Timer(Icons.Workshop, "Мельница", 0.66f, "Корм для свиней · 1:59:59", new RowButton("Открыть")),
                Row(Icons.Workshop, "Пекарня", "Очередь пуста", new RowButton("Открыть")),
                Row(Icons.Workshop, "Сахарная", "Ур. 10 · жителей: 110", Coins(3_500, false), null, true),
                Row(Icons.Workshop, "Ткацкая", "Жителей: 79 из 80", Coins(2_500)),
            },
        };

        public static EconomyInfo Workshop() => new EconomyInfo
        {
            Id = "workshop", Title = "Сарай инструментов",
            Queue = new List<QueueSlot>
            {
                new QueueSlot(SlotState.Ready, Icons.Item("pumpkin_pie"), "Готово"), new QueueSlot(SlotState.Running, Icons.Rainbow, "1:59:59"),
                new QueueSlot(SlotState.Waiting, Icons.Bomb, "1:05:00"), new QueueSlot(SlotState.Waiting, Icons.Item("cherry_jam"), "4:00"),
                new QueueSlot(SlotState.Free, null, "Пусто"), new QueueSlot(SlotState.Buy, null, "25"),
            },
            Rows = new List<EconomyRow>
            {
                new EconomyRow
                {
                    Icon = Icons.Item("honey_cake"), Title = "Медовик · 1 ч 30 мин", Button = new RowButton("Начать"),
                    Chips = new List<Chip> { new Chip(Icons.Item("cream"), "99/1"), new Chip(Icons.Item("flour"), "0/1", true), new Chip(Icons.Item("honey"), "12/10") },
                },
                new EconomyRow
                {
                    Icon = Icons.Item("strawberry_jam"), Title = "Клубничное варенье", Button = new RowButton("Начать", null, Tone.Grey),
                    Chips = new List<Chip> { new Chip(Icons.Item("strawberry"), "2/3", true) },
                },
                Row(Icons.Item("pumpkin_pie"), "Тыквенный пирог", "Нужен 17 уровень", Locked(17), null, true),
            },
            Footer = new List<RowButton> { new RowButton("Собрать", Icons.Check, Tone.Yellow), new RowButton("Ускорить 99", Icons.Crystal, Tone.Blue) },
        };

        public static EconomyInfo Barn() => new EconomyInfo
        {
            Id = "barn", Title = "Амбар",
            HeaderText = "Занято 695 из 695", HeaderBar = 1, HeaderBarText = "Амбар полон",
            Rows = new List<EconomyRow>
            {
                new EconomyRow
                {
                    Icon = Icons.Barn, Title = "Улучшить: +25 мест", Button = new RowButton("Улучшить", null, Tone.Grey),
                    Chips = new List<Chip>
                    {
                        new Chip(Icons.Tool("hammer"), "99/12"), new Chip(Icons.Tool("nails"), "0/12", true), new Chip(Icons.Tool("paint"), "12/12"),
                    },
                },
                EconomyRow.Section("Товары"),
                Row(Icons.Item("strawberry_jam"), "Клубничное варенье", "В амбаре 999 · по 999", new RowButton("+999", Icons.Coin, Tone.Yellow),
                    new RowButton(null, Icons.Coin, Tone.Red)),
                Row(Icons.Item("chicken_feed"), "Корм для кур", "В амбаре 1 · по 1", new RowButton("+1", Icons.Coin, Tone.Yellow)),
                EconomyRow.Section("Инструменты"),
                Row(Icons.Tool("saw"), "Пила ×99", "Расчищает землю для городка", null),
            },
        };

        public static EconomyInfo Orders()
        {
            var info = new EconomyInfo
            {
                Id = "orders", Title = "Заказы соседей", Cards = true,
                HeaderText = "Грузовик: загружено 2 из 3", HeaderButton = new RowButton("Грузовик", null, Tone.Blue),
                Footer = new List<RowButton> { new RowButton("Отдать", Icons.Check), new RowButton("Отказаться", Icons.Close, Tone.Red) },
            };
            string[] who = { "Дед Матвей", "Тётя Вера", "Паша", "Лида", "Федот", "Нина" };
            for (int i = 0; i < 9; i++)
            {
                if (i == 4 || i == 8)
                {
                    info.Rows.Add(new EconomyRow { Caption = "Ждём заказ", Icon = Icons.Clock, Title = "Новый заказ", Sub = "14:59", Dim = true });
                    continue;
                }
                info.Rows.Add(new EconomyRow
                {
                    Caption = who[i % who.Length], Picked = i == 0,
                    Chips = new List<Chip>
                    {
                        new Chip(Icons.Item("strawberry_jam"), "99/11"), new Chip(Icons.Item("corn_bread"), "0/11", true), new Chip(Icons.Item("pumpkin_pie"), "9/9"),
                    },
                    Rewards = new List<Chip> { new Chip(Icons.Coin, "9 999"), new Chip(Icons.Xp, "999") },
                });
            }
            return info;
        }

        public static EconomyInfo Truck() => new EconomyInfo
        {
            Id = "truck", Title = "Грузовик в город",
            HeaderText = "Награда за рейс", HeaderChips = new List<Chip> { new Chip(Icons.Coin, "99 999"), new Chip(Icons.Xp, "9 999"), new Chip(Icons.Tool("axe"), "×1") },
            Rows = new List<EconomyRow>
            {
                Row(Icons.Item("strawberry_jam"), "Клубничное варенье ×16", "В амбаре: 999", new RowButton("Загрузить")),
                Row(Icons.Item("sweater"), "Свитер ×16", "В амбаре: 0 из 16", new RowButton("Загрузить", null, Tone.Grey)),
                Row(Icons.Item("cheese"), "Сыр ×16", "Загружено", new RowButton("Готово", null, Tone.Grey)),
            },
            Footer = new List<RowButton> { new RowButton("Отправить", Icons.Truck, Tone.Yellow) },
        };

        public static EconomyInfo TruckAway() => new EconomyInfo
        {
            Id = "truck", Title = "Грузовик в город",
            HeaderText = "Грузовик в пути", HeaderBar = 0.25f, HeaderBarText = "Вернётся через 59:59",
            Empty = "Грузовик вернётся с новым заказом",
        };

        public static EconomyInfo Town() => new EconomyInfo
        {
            Id = "town", Title = "Городок",
            HeaderText = "Жители, участки и красота",
            HeaderChips = new List<Chip> { new Chip(Icons.People, "999/999"), new Chip(Icons.Lot, "99/99"), new Chip(Icons.Beauty, "9 999") },
            Rows = new List<EconomyRow>
            {
                new EconomyRow
                {
                    Icon = Icons.Lot, Title = "Участок 12: +5 мест", Button = Coins(12_500, false),
                    Chips = new List<Chip> { new Chip(Icons.Tool("saw"), "0/12", true), new Chip(Icons.Tool("axe"), "99/12") },
                },
                EconomyRow.Section("Дома"),
                Row(Icons.Town, "Двухэтажный дом", "+40 жителей · 12 из 12", new RowButton("Готово", null, Tone.Grey)),
                Row(Icons.Town, "Терем", "Нужен 14 уровень", Locked(14), null, true),
                EconomyRow.Section("Для жителей"),
                Row(Icons.Town, "Библиотека", "+120 мест для жителей", Coins(3_200)),
                EconomyRow.Section("Украшения"),
                Row(Icons.Beauty, "Памятник бабушке Дуне", "Красота +15 · опыт +99", new RowButton("15", Icons.Star, Tone.Yellow)),
            },
            Footer = new List<RowButton> { new RowButton("Расширить", Icons.Lot) },
        };

        public static ConfirmInfo SpeedUp() => new ConfirmInfo
        {
            Title = "Ускорить?", Icon = Icons.Item("pumpkin"), Text = "Тыква созреет через 7 ч 59 мин. Ускорить за 48 кристаллов?",
            Button = new RowButton("Ускорить 48", Icons.Crystal, Tone.Blue),
        };
    }
}
