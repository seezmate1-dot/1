using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 13.1: the shops, like in Hustle Castle but honest. The merchant (silver: treats, the
    /// Coal cat, the Falcon; Brik's 8 goods in GameSession.Shop.cs), deals of the day, the exchange at
    /// the trading post, and the Sparks shop (hourglasses at the speed-up price, a veteran dweller
    /// with stats shown before buying, two pets and a companion that are also found by play, treats).
    /// No lottery: every offer says exactly what it gives.
    /// </summary>
    public sealed partial class GameSession
    {
        MarketState Market => State.Market ?? (State.Market = new MarketState());

        void MarketTick(long now)
        {
            long day = LocalDay(now), week = day / 7;
            if (Market.Day != day)
            {
                Market.Day = day;
                Market.Today.Clear();
                Market.Exchanged = 0;
            }
            if (Market.Week != week)
            {
                Market.Week = week;
                Market.ThisWeek.Clear();
            }
        }

        int Bought(Dictionary<string, int> counts, string id) => counts.TryGetValue(id, out int n) ? n : 0;

        public bool MerchantOpen => State.HearthLevel >= Balance.Room(RoomType.TradingPost).Unlock;

        long SilverHours(double hours) => Math.Max(1, (long)Math.Round(hours * IncomePerHour()));

        // ------------------------------------------------------------------ the shelves

        /// <summary>The merchant's own goods for silver (Brik's rotating goods are BrikShop).</summary>
        public List<Offer> MerchantOffers(long now)
        {
            MarketTick(now);
            var list = new List<Offer>();
            if (!MerchantOpen)
                return list;
            var m = Balance.Market;
            list.Add(new Offer
            {
                Id = "m:Treats", Name = "Лакомства ×" + m.MerchantTreats, Text = "растят питомцев и узы компаньонов",
                PayWith = ResourceType.Silver, Price = SilverHours(1), Left = 1 - Bought(Market.Today, "m:Treats"), Limit = "раз в день",
            });
            foreach (var pet in Balance.Pets.Kinds.Where(k => k.SilverHours > 0))
            {
                list.Add(new Offer
                {
                    Id = "pet:" + pet.Id, Name = "Питомец: " + pet.Name, Text = PetBonusText(pet, 1),
                    PayWith = ResourceType.Silver, Price = SilverHours(pet.SilverHours), Left = HasPet(pet.Id) ? 0 : 1, Limit = "один раз",
                });
            }
            foreach (var comp in Balance.Companions.Kinds.Where(k => k.SilverHours > 0))
            {
                list.Add(new Offer
                {
                    Id = "comp:" + comp.Id, Name = "Компаньон: " + comp.Name, Text = CompanionSkillText(comp, 1),
                    PayWith = ResourceType.Silver, Price = SilverHours(comp.SilverHours), Left = CompanionUnlocked(comp.Id) ? 0 : 1, Limit = "один раз",
                });
            }
            return list;
        }

        /// <summary>The Sparks shop (the 4th builder and resource packs stay in GameSession.Shop.cs).</summary>
        public List<Offer> PremiumOffers(long now)
        {
            MarketTick(now);
            var m = Balance.Market;
            var list = new List<Offer>();
            if (m.PremiumHourglass.Length >= 3)
            {
                list.Add(new Offer
                {
                    Id = "p:Hourglass", Name = "Песочные часы " + m.PremiumHourglass[0] + " мин", Text = "по цене ускорения: не дешевле, просто удобно",
                    PayWith = ResourceType.Sparks, Price = m.PremiumHourglass[1],
                    Left = (int)m.PremiumHourglass[2] - Bought(Market.Today, "p:Hourglass"), Limit = m.PremiumHourglass[2] + " в день",
                });
            }
            if (m.PremiumDweller.Length >= 2)
            {
                var (name, stats) = VeteranPreview(now);
                list.Add(new Offer
                {
                    Id = "p:Veteran", Name = "Житель: " + name, Text = "характеристики " + string.Join(" / ", stats),
                    PayWith = ResourceType.Sparks, Price = m.PremiumDweller[0],
                    Left = State.Dwellers.Count >= Formulas.DwellerCap(Balance, State.HearthLevel) ? 0
                        : (int)m.PremiumDweller[1] - Bought(Market.ThisWeek, "p:Veteran"),
                    Limit = m.PremiumDweller[1] + " в неделю, нужно место",
                });
            }
            foreach (var pet in Balance.Pets.Kinds.Where(k => k.Sparks > 0))
            {
                list.Add(new Offer
                {
                    Id = "pet:" + pet.Id, Name = "Питомец: " + pet.Name, Text = PetBonusText(pet, 1) + " · или за игру: " + pet.Text,
                    PayWith = ResourceType.Sparks, Price = pet.Sparks, Left = HasPet(pet.Id) ? 0 : 1, Limit = "один раз",
                });
            }
            foreach (var comp in Balance.Companions.Kinds.Where(k => k.Sparks > 0))
            {
                list.Add(new Offer
                {
                    Id = "comp:" + comp.Id, Name = "Компаньон: " + comp.Name, Text = CompanionSkillText(comp, 1) + " · или за игру: " + comp.Text,
                    PayWith = ResourceType.Sparks, Price = comp.Sparks, Left = CompanionUnlocked(comp.Id) ? 0 : 1, Limit = "один раз",
                });
            }
            if (m.PremiumTreats.Length >= 3)
            {
                list.Add(new Offer
                {
                    Id = "p:Treats", Name = "Лакомства ×" + m.PremiumTreats[0], Text = "растят питомцев и узы компаньонов",
                    PayWith = ResourceType.Sparks, Price = m.PremiumTreats[1],
                    Left = (int)m.PremiumTreats[2] - Bought(Market.ThisWeek, "p:Treats"), Limit = m.PremiumTreats[2] + " в неделю",
                });
            }
            return list;
        }

        /// <summary>Three deals a day (a new set at local midnight), each once, paid in silver.</summary>
        public List<Offer> DailyDeals(long now)
        {
            MarketTick(now);
            var list = new List<Offer>();
            if (!MerchantOpen)
                return list;
            var rng = new Rng((int)(LocalDay(now) % 100000) + 7);
            foreach (var deal in Balance.Market.Deals.OrderBy(_ => rng.Next()).Take(Balance.Market.DealsShown))
            {
                string id = "d:" + deal.Id;
                list.Add(new Offer
                {
                    Id = id, Name = DealName(deal), Text = "предложение дня",
                    PayWith = ResourceType.Silver, Price = SilverHours(deal.Hours), Left = 1 - Bought(Market.Today, id), Limit = "раз в день",
                });
            }
            return list;
        }

        long DealAmount(DealData deal)
        {
            switch (deal.Goods)
            {
                case "Coal": return (long)Math.Round(CoalBurnPerHour() * deal.Amount);
                case "Food": return (long)Math.Round(Formulas.DwellerCap(Balance, State.HearthLevel) * Balance.FoodPerDwellerHour * deal.Amount);
                case "Hourglass": return (long)Math.Round(deal.Amount * 60);
                case "Essence": return (long)Math.Round(Balance.Activities.EssenceByLevel[State.HearthLevel - 1] * deal.Amount);
                default: return (long)Math.Round(deal.Amount);
            }
        }

        string DealName(DealData deal)
        {
            long n = DealAmount(deal);
            switch (deal.Goods)
            {
                case "Coal": return "Уголь " + n;
                case "Food": return "Еда " + n;
                case "Hourglass": return "Песочные часы " + n / 60 + " мин";
                case "Essence": return "Эссенция " + n;
                default: return "Лакомства ×" + n;
            }
        }

        /// <summary>The veteran of this week: the name and the stats are known before buying.</summary>
        public (string name, int[] stats) VeteranPreview(long now)
        {
            string[] names = { "Ветеран Гордей", "Ветеран Ольха", "Ветеран Стоян", "Ветеран Рада", "Ветеран Яромир", "Ветеран Злата" };
            long week = LocalDay(now) / 7;
            int lead = (int)(week % 5);
            var stats = new int[5];
            for (int k = 0; k < 5; k++)
                stats[k] = 5 + 2 * State.HearthLevel / 3 + Balance.Market.PremiumDwellerBonus;
            stats[lead] += 2 * Balance.Market.PremiumDwellerBonus;
            return (names[week % names.Length], stats);
        }

        // ------------------------------------------------------------------ buying

        /// <summary>Buys an offer of any shelf by its id. Never takes more than the wallet holds.</summary>
        public Fail BuyOffer(string id, long now)
        {
            Advance(now);
            var offer = MerchantOffers(now).Concat(PremiumOffers(now)).Concat(DailyDeals(now)).FirstOrDefault(o => o.Id == id);
            if (offer == null)
                return MerchantOpen ? Fail.NotFound : Fail.Locked;
            if (offer.Left <= 0)
                return Fail.LimitReached;
            if (offer.Price <= 0 || State.Wallet[offer.PayWith] < offer.Price)
                return Fail.NotEnough;
            if (id == "p:Veteran" && State.Dwellers.Count >= Formulas.DwellerCap(Balance, State.HearthLevel))
                return Fail.LimitReached;
            State.Wallet[offer.PayWith] -= offer.Price;
            Deliver(id, now);
            Market.Today[id] = Bought(Market.Today, id) + 1;
            Market.ThisWeek[id] = Bought(Market.ThisWeek, id) + 1;
            return Fail.None;
        }

        void Deliver(string id, long now)
        {
            if (id.StartsWith("pet:"))
            {
                AddPet(id.Substring(4));
                return;
            }
            if (id.StartsWith("comp:"))
            {
                string kind = id.Substring(5);
                if (!Companions.Unlocked.Contains(kind))
                    Companions.Unlocked.Add(kind);
                return;
            }
            if (id.StartsWith("d:"))
            {
                var deal = Balance.Market.Deals.First(d => "d:" + d.Id == id);
                long n = DealAmount(deal);
                switch (deal.Goods)
                {
                    case "Coal": State.Wallet[ResourceType.Coal] += n; break;
                    case "Food": State.Wallet[ResourceType.Food] += n; break;
                    case "Hourglass": State.HourglassSeconds += n; break;
                    case "Essence": State.Wallet[ResourceType.Essence] += n; break;
                    default: GrantTreats(n); break;
                }
                return;
            }
            var m = Balance.Market;
            switch (id)
            {
                case "m:Treats": GrantTreats(m.MerchantTreats); break;
                case "p:Treats": GrantTreats(m.PremiumTreats[0]); break;
                case "p:Hourglass": State.HourglassSeconds += m.PremiumHourglass[0] * 60; break;
                case "p:Veteran":
                    var (name, stats) = VeteranPreview(now);
                    AddDweller(name, stats);
                    break;
            }
        }

        // ------------------------------------------------------------------ the exchange at the trading post

        public static readonly (ResourceType from, ResourceType to)[] ExchangePairs =
        {
            (ResourceType.Iron, ResourceType.Silver), (ResourceType.Silver, ResourceType.Iron),
            (ResourceType.Coal, ResourceType.Food), (ResourceType.Food, ResourceType.Coal),
        };

        static double ExchangeValue(ResourceType t) => FairValue[Array.IndexOf(FairGoods, t)];

        /// <summary>Value (in silver) that can still be exchanged today.</summary>
        public long ExchangeLeft(long now)
        {
            MarketTick(now);
            return Math.Max(0, (long)(Balance.Market.ExchangeHours * IncomePerHour()) - Market.Exchanged);
        }

        /// <summary>What an exchange of this amount would give (×0.8 of the value; the Ash Fair gives more).</summary>
        public long ExchangeQuote(ResourceType from, ResourceType to, long amount) =>
            (long)Math.Floor(amount * ExchangeValue(from) * Balance.Market.ExchangeFee / ExchangeValue(to));

        public Fail Exchange(ResourceType from, ResourceType to, long amount, long now, out long got)
        {
            got = 0;
            Advance(now);
            if (!MerchantOpen)
                return Fail.Locked;
            if (!ExchangePairs.Contains((from, to)) || amount <= 0)
                return Fail.NotFound;
            if (State.Wallet[from] < amount)
                return Fail.NotEnough;
            long value = (long)Math.Ceiling(amount * ExchangeValue(from));
            if (value > ExchangeLeft(now))
                return Fail.LimitReached;
            got = ExchangeQuote(from, to, amount);
            if (got <= 0)
                return Fail.NotEnough;
            State.Wallet[from] -= amount;
            Market.Exchanged += value;
            long taken = Deposit(to, got);
            State.Rewards[to] += got - taken; // what the warehouse cannot hold waits in the rewards
            return Fail.None;
        }
    }
}
