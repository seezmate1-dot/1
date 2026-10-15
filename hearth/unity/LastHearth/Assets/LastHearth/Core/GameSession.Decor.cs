using System;
using System.Collections.Generic;
using System.Linq;

namespace LastHearth.Core
{
    public sealed class DecorData
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Premium decorations are sold for Sparks; plain ones for silver (or Glory).</summary>
        public bool Premium;
        /// <summary>Plain: hours of the fortress income in silver; premium: Sparks; Glory: Glory.</summary>
        public long Price;
        public ResourceType Currency;
    }

    /// <summary>
    /// Decorations for the rooms, like in Hustle Castle: purely cosmetic (no bonus for money, the
    /// "Honesty" pillar of the GDD). One stands in a room; an owned one can be moved or put away.
    /// </summary>
    public sealed partial class GameSession
    {
        public static readonly DecorData[] DecorCatalog =
        {
            new DecorData { Id = "banner_red", Name = "Алое знамя", Price = 2, Currency = ResourceType.Silver },
            new DecorData { Id = "banner_blue", Name = "Синее знамя", Price = 2, Currency = ResourceType.Silver },
            new DecorData { Id = "banner_green", Name = "Зелёное знамя", Price = 2, Currency = ResourceType.Silver },
            new DecorData { Id = "candles", Name = "Подсвечник", Price = 3, Currency = ResourceType.Silver },
            new DecorData { Id = "barrels", Name = "Бочки", Price = 3, Currency = ResourceType.Silver },
            new DecorData { Id = "plant", Name = "Кадка с зеленью", Price = 4, Currency = ResourceType.Silver },
            new DecorData { Id = "rug", Name = "Гобелен Стражей", Price = 5, Currency = ResourceType.Silver },
            new DecorData { Id = "shelf", Name = "Полка с бутылями", Price = 5, Currency = ResourceType.Silver },
            new DecorData { Id = "keg", Name = "Бочка яблок", Price = 6, Currency = ResourceType.Silver },
            new DecorData { Id = "shield", Name = "Щит и мечи", Price = 300, Currency = ResourceType.Glory },
            new DecorData { Id = "gold_shield", Name = "Золотой герб", Premium = true, Price = 80, Currency = ResourceType.Sparks },
            new DecorData { Id = "chest_gold", Name = "Сундук мастера", Premium = true, Price = 100, Currency = ResourceType.Sparks },
            new DecorData { Id = "pumpkin", Name = "Тыква-фонарь", Premium = true, Price = 120, Currency = ResourceType.Sparks },
            new DecorData { Id = "shrine", Name = "Святилище свечей", Premium = true, Price = 150, Currency = ResourceType.Sparks },
            new DecorData { Id = "crystal", Name = "Кристалл угля", Premium = true, Price = 200, Currency = ResourceType.Sparks },
            new DecorData { Id = "treasure", Name = "Сокровищница", Premium = true, Price = 250, Currency = ResourceType.Sparks },
            new DecorData { Id = "throne", Name = "Трон старейшины", Premium = true, Price = 300, Currency = ResourceType.Sparks },
            new DecorData { Id = "armor", Name = "Доспехи Стража", Premium = true, Price = 350, Currency = ResourceType.Sparks },
        };

        public DecorData Decor(string id) => DecorCatalog.FirstOrDefault(d => d.Id == id);

        /// <summary>The price now: plain decorations cost hours of the current income, so they matter at any level.</summary>
        public long DecorPrice(DecorData d) => d.Currency == ResourceType.Silver ? d.Price * IncomePerHour() : d.Price;

        public int DecorOwned(string id) => State.DecorOwned.TryGetValue(id, out int n) ? n : 0;

        public int DecorPlaced(string id) => State.RoomDecor.Values.Count(v => v == id);

        public Fail BuyDecor(string id, long now)
        {
            Advance(now);
            var d = Decor(id);
            if (d == null)
                return Fail.NotFound;
            long price = DecorPrice(d);
            if (State.Wallet[d.Currency] < price)
                return Fail.NotEnough;
            State.Wallet[d.Currency] -= price;
            State.DecorOwned[id] = DecorOwned(id) + 1;
            return Fail.None;
        }

        /// <summary>Puts an owned decoration into a room (the one there before goes back to storage).</summary>
        public Fail PlaceDecor(int roomId, string id)
        {
            if (FindRoom(roomId) == null || Decor(id) == null)
                return Fail.NotFound;
            State.RoomDecor.Remove(roomId);
            if (DecorPlaced(id) >= DecorOwned(id))
                return Fail.NotEnough;
            State.RoomDecor[roomId] = id;
            return Fail.None;
        }

        public void RemoveDecor(int roomId) => State.RoomDecor.Remove(roomId);

        public string RoomDecorId(int roomId) => State.RoomDecor.TryGetValue(roomId, out var id) ? id : null;
    }
}
