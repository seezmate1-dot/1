using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// MODES.md, 4: the Sparks shop (4th builder, resource packs, speed-ups) and Brik's shop
    /// (8 goods for silver, new at 00:00 and 12:00). Essence and Cores are never sold for Sparks.
    /// </summary>
    public sealed partial class GameSession
    {
        public long IncomePerHour() => Balance.Activities.Routes.IncomeByLevel[State.HearthLevel - 1];

        public Fail BuyFourthBuilder()
        {
            var shop = Balance.Activities.Shop;
            if (State.FourthBuilder)
                return Fail.AlreadyQueued;
            if (State.HearthLevel < shop.FourthBuilderFrom)
                return Fail.Locked;
            if (State.Wallet[ResourceType.Sparks] < shop.FourthBuilder)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= shop.FourthBuilder;
            State.FourthBuilder = true;
            State.Queue.EnsureBuilders(BuilderCount());
            return Fail.None;
        }

        public int PacksLeft(long now)
        {
            long week = LocalDay(now) / 7;
            if (State.PacksWeek != week)
            {
                State.PacksWeek = week;
                State.PacksBought = 0;
            }
            return Balance.Activities.Shop.PacksPerWeek - State.PacksBought;
        }

        /// <summary>One hour of the full material income for 30 Sparks, 14 a week (ECONOMY.md, 8).</summary>
        public Fail BuyPack(long now)
        {
            Advance(now);
            if (PacksLeft(now) <= 0)
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < Balance.Activities.Shop.PackPrice)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= Balance.Activities.Shop.PackPrice;
            State.PacksBought++;
            State.Rewards.Add(ResourceBag.Materials(IncomePerHour(), Balance.SilverShare));
            ClaimRewards();
            return Fail.None;
        }

        // ------------------------------------------------------------------ Brik's shop

        long ShopKeyOf(long now) => LocalDay(now) * 2 + (LocalSecondOfDay(now) >= 12 * 3600 ? 1 : 0);

        public List<ShopItem> BrikShop(long now)
        {
            if (State.HearthLevel < Balance.Room(RoomType.TradingPost).Unlock)
                return new List<ShopItem>();
            long key = ShopKeyOf(now);
            if (State.ShopKey != key)
            {
                State.ShopKey = key;
                FillShop((int)(key % int.MaxValue));
            }
            return State.Shop;
        }

        void FillShop(int seed)
        {
            var rng = new Rng(seed);
            long income = IncomePerHour();
            int L = State.HearthLevel;
            long essenceDay = Balance.Activities.EssenceByLevel[L - 1];
            var offers = new List<ShopItem>
            {
                new ShopItem { Goods = ShopGoods.Essence, Amount = essenceDay / 4, PayWith = ResourceType.Silver, Price = 2 * income },
                new ShopItem { Goods = ShopGoods.Essence, Amount = essenceDay / 4, PayWith = ResourceType.Silver, Price = 3 * income },
                new ShopItem { Goods = ShopGoods.Coal, Amount = (long)(CoalBurnPerHour() * 12), PayWith = ResourceType.Silver, Price = income },
                new ShopItem { Goods = ShopGoods.Food, Amount = Formulas.DwellerCap(Balance, L) * 12, PayWith = ResourceType.Silver, Price = income },
                new ShopItem { Goods = ShopGoods.Hourglass, Amount = 15 * 60, PayWith = ResourceType.Silver, Price = 3 * income / 2 },
                new ShopItem { Goods = ShopGoods.RecruitSeal, Amount = 1, PayWith = ResourceType.Silver, Price = 3 * income },
                new ShopItem { Goods = ShopGoods.Coal, Amount = (long)(CoalBurnPerHour() * 24), PayWith = ResourceType.Silver, Price = 2 * income },
                new ShopItem { Goods = ShopGoods.Food, Amount = Formulas.DwellerCap(Balance, L) * 24, PayWith = ResourceType.Silver, Price = 2 * income },
            };
            // shuffle deterministically and keep 7, then the "deal of the day" for Sparks
            var picked = offers.OrderBy(_ => rng.Next()).Take(Balance.Activities.Shop.Slots - 1).ToList();
            picked.Add(new ShopItem { Goods = ShopGoods.HourglassDeal, Amount = 2 * 3600, PayWith = ResourceType.Sparks, Price = 30 });
            State.Shop = picked;
        }

        public Fail BuyShopItem(int index, long now)
        {
            var items = BrikShop(now);
            if (index < 0 || index >= items.Count)
                return Fail.NotFound;
            var item = items[index];
            if (item.Sold)
                return Fail.AlreadyQueued;
            if (State.Wallet[item.PayWith] < item.Price)
                return Fail.NotEnough;
            if (item.Goods == ShopGoods.RecruitSeal && State.Dwellers.Count >= Formulas.DwellerCap(Balance, State.HearthLevel))
                return Fail.LimitReached;
            State.Wallet[item.PayWith] -= item.Price;
            item.Sold = true;
            switch (item.Goods)
            {
                case ShopGoods.Essence: State.Wallet[ResourceType.Essence] += item.Amount; break;
                case ShopGoods.Coal: State.Wallet[ResourceType.Coal] += item.Amount; break;
                case ShopGoods.Food: State.Wallet[ResourceType.Food] += item.Amount; break;
                case ShopGoods.RecruitSeal: RefugeesArrive(1); break;
                default: State.HourglassSeconds += item.Amount; break;
            }
            return Fail.None;
        }

        public long RefreshPrice(long now)
        {
            if (State.ShopRefreshDay != LocalDay(now))
            {
                State.ShopRefreshDay = LocalDay(now);
                State.ShopRefreshes = 0;
            }
            var prices = Balance.Activities.Shop.Refresh;
            return State.ShopRefreshes < prices.Length ? prices[State.ShopRefreshes] : -1;
        }

        public Fail RefreshShop(long now)
        {
            long price = RefreshPrice(now);
            if (price < 0)
                return Fail.LimitReached;
            if (State.Wallet[ResourceType.Sparks] < price)
                return Fail.NotEnough;
            State.Wallet[ResourceType.Sparks] -= price;
            State.ShopRefreshes++;
            BrikShop(now);
            FillShop(State.NextBattleSeed++);
            return Fail.None;
        }
    }
}
