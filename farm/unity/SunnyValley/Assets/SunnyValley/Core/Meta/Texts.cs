namespace SunnyValley.Core.Meta
{
    /// <summary>Russian texts the core produces itself (errors, durations, plurals).</summary>
    public static class Texts
    {
        public static string Error(MetaError e)
        {
            switch (e)
            {
                case MetaError.None: return "";
                case MetaError.UnknownId: return "Такого нет";
                case MetaError.Locked: return "Откроется на следующих уровнях фермы";
                case MetaError.NotEnoughCoins: return "Не хватает монет";
                case MetaError.NotEnoughStars: return "Не хватает звёзд — пройдите уровень";
                case MetaError.NotEnoughCrystals: return "Не хватает кристаллов";
                case MetaError.NotEnoughItems: return "Не хватает товаров";
                case MetaError.NotEnoughTools: return "Не хватает инструментов";
                case MetaError.NoBooster: return "Нет такого буста";
                case MetaError.NoLives: return "Жизни кончились — скоро восстановятся";
                case MetaError.BarnFull: return "Амбар полон — продайте товары или расширьте амбар";
                case MetaError.AlreadyDone: return "Уже сделано";
                case MetaError.PrerequisitesMissing: return "Сначала нужно сделать другие задания";
                case MetaError.ZoneLocked: return "Эта часть фермы пока закрыта";
                case MetaError.BadChoice: return "Нет такого варианта";
                case MetaError.FarmClosed: return "Хозяйство откроется, когда ферма будет восстановлена";
                case MetaError.Busy: return "Занято";
                case MetaError.NotReady: return "Ещё не готово";
                case MetaError.QueueFull: return "Очередь заполнена";
                case MetaError.LimitReached: return "Больше нельзя";
                case MetaError.NotEnoughPopulation: return "Не хватает жителей — постройте дома";
                case MetaError.NoLevelInProgress: return "Уровень не начат";
                case MetaError.LevelInProgress: return "Уровень уже идёт";
                case MetaError.WrongLevel: return "Этот уровень пока не открыт";
                case MetaError.NothingToCollect: return "Нечего собирать";
                case MetaError.AlreadyClaimed: return "Награда уже получена";
                case MetaError.NeedsFeed: return "Нужен корм";
                case MetaError.NotEnoughSpace: return "Нет места — нужно расширить территорию";
                case MetaError.Empty: return "Пусто";
                default: return e.ToString();
            }
        }

        /// <summary>"1 монета", "3 монеты", "11 монет".</summary>
        public static string Plural(long n, string one, string few, string many)
        {
            long a = System.Math.Abs(n) % 100;
            long b = a % 10;
            string w = a >= 11 && a <= 14 ? many : b == 1 ? one : b >= 2 && b <= 4 ? few : many;
            return n + " " + w;
        }

        /// <summary>"45 с", "12 мин", "2 ч 05 мин", "1 д 3 ч".</summary>
        public static string Duration(long seconds)
        {
            if (seconds < 0)
                seconds = 0;
            if (seconds < 60)
                return seconds + " с";
            long m = seconds / 60;
            if (m < 60)
                return m + " мин";
            long h = m / 60;
            if (h < 24)
                return m % 60 == 0 ? h + " ч" : h + " ч " + (m % 60).ToString("00") + " мин";
            long d = h / 24;
            return h % 24 == 0 ? d + " д" : d + " д " + (h % 24) + " ч";
        }
    }
}
