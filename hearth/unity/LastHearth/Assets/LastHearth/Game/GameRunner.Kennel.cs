using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The "Питомцы" and "Герой" windows and the shops with their tabs (GDD 8.1-8.3, 13.1): the merchant
    /// for silver, the deals of the day, the exchange at the trading post and the Sparks shop.
    /// </summary>
    public sealed partial class GameRunner
    {
        /// <summary>The hero shown on the hero screen (a dweller id; -1: the first hero).</summary>
        int heroCard = -1;
        int shopTab;
        static readonly string[] ShopTabs = { "Торговец", "Предложения дня", "Обмен", "Искры" };

        /// <summary>A pet or a companion found by play gets a message over the fortress.</summary>
        void KennelNews()
        {
            var found = session.CheckKennel();
            if (found.Count > 0)
                message = "Новый друг в крепости: " + string.Join(", ", found) + " — окно «Питомцы»";
        }

        /// <summary>Something can be fed or raised now: the red dot on the "Питомцы" button.</summary>
        bool KennelWaits()
        {
            var pet = session.ActivePet();
            long cost = pet == null ? -1 : session.PetFeedCost(pet.Id);
            return cost > 0 && session.Treats >= cost && session.PetLevel(pet.Id) < session.State.HearthLevel;
        }

        static string ResName(ResourceType r)
        {
            string[] names = { "уголь", "еда", "железо", "серебро", "эссенция", "искры", "жар", "сердцевины", "чертежи", "слава" };
            return names[(int)r];
        }

        // ------------------------------------------------------------------ pets

        void DrawPets(long now)
        {
            var pets = session.Balance.Pets;
            var active = session.ActivePet();
            GUILayout.Label(string.Format("<b>Лакомства: {0}</b> · растят питомцев и узы компаньонов. Активный питомец один: он помогает всей " +
                "крепости и отряду, остальные отдыхают у очага. Сменить активного можно в любой момент.", session.Treats), Rich());
            GUILayout.Label(active == null ? "Активного питомца пока нет — первый придёт после победы на Охоте."
                : string.Format("Сейчас помогает: <b>{0}</b>, ур.{1} — {2}", active.Name, session.PetLevel(active.Id),
                    session.PetBonusText(active, session.PetLevel(active.Id))), Rich());
            GUILayout.Space(6);
            foreach (var kind in pets.Kinds)
            {
                int level = session.PetLevel(kind.Id);
                GUILayout.BeginHorizontal();
                if (level > 0)
                {
                    bool isActive = active != null && active.Id == kind.Id;
                    string next = level < pets.MaxLevel ? " · на ур." + (level + 1) + ": " + session.PetBonusText(kind, level + 1) : " · высший уровень";
                    GUILayout.Label(string.Format("<b>{0}</b> ур.{1}/{2}{3}\n<size=12><color=#d8c8a8>{4}{5}</color></size>", kind.Name, level, pets.MaxLevel,
                        isActive ? " <color=#9e6>· активный</color>" : "", session.PetBonusText(kind, level), next), Rich());
                    if (!isActive && GUILayout.Button("Сделать активным", GUILayout.Width(170)))
                        Report(session.SetActivePet(kind.Id));
                    long cost = session.PetFeedCost(kind.Id);
                    if (cost > 0 && GUILayout.Button("Кормить: " + cost + " лакомств", GUILayout.Width(200)))
                        Report(session.FeedPet(kind.Id));
                }
                else
                {
                    GUILayout.Label(string.Format("<color=#a99>{0}</color> · {1}\n<size=12><color=#b8a888>Как получить: {2}{3}</color></size>", kind.Name,
                        session.PetBonusText(kind, 1), kind.Text, ProgressText(kind.Source, kind.Condition)), Rich());
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            var comps = session.Balance.Companions;
            GUILayout.Label(string.Format("<b>Компаньоны героев</b> · у каждого героя один, он бьётся рядом. Уровень уз растёт лакомствами, " +
                "не выше 1 + уровень героя / {0}, и остаётся у героя, если сменить компаньона.", comps.HeroLevelsPerLevel), Rich());
            foreach (var kind in comps.Kinds)
            {
                bool found = session.CompanionUnlocked(kind.Id);
                int walking = session.State.Dwellers.Count(d => d.IsHero && session.CompanionOf(d.Id)?.Kind == kind.Id);
                GUILayout.Label(found
                    ? string.Format("<b>{0}</b> · {1}{2}", kind.Name, session.CompanionSkillText(kind, 1), walking > 0 ? " · с героями: " + walking : "")
                    : string.Format("<color=#a99>{0}</color> · {1} · <color=#b8a888>как получить: {2}{3}</color>", kind.Name,
                        session.CompanionSkillText(kind, 1), kind.Text, ProgressText(kind.Source, kind.Condition)), Rich());
            }
            if (session.State.Dwellers.Any(d => d.IsHero) && GUILayout.Button("Герои и их компаньоны", GUILayout.Height(34)))
                Open(Win.Hero);
        }

        string ProgressText(UnlockSource source, int condition)
        {
            if (source == UnlockSource.Shop || source == UnlockSource.Start)
                return "";
            return string.Format(" ({0}/{1})", Math.Min(condition, session.UnlockProgress(source)), condition);
        }

        // ------------------------------------------------------------------ the hero screen

        void DrawHero(long now)
        {
            var heroes = session.State.Dwellers.Where(d => d.IsHero).ToList();
            if (heroes.Count == 0)
            {
                GUILayout.Label("Героев пока нет: сделайте жителя героем в окне «Жители» (нужна казарма).");
                return;
            }
            int index = Math.Max(0, heroes.FindIndex(d => d.Id == heroCard));
            var hero = heroes[index];
            heroCard = hero.Id;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Предыдущий", GUILayout.Width(150)))
                heroCard = heroes[(index + heroes.Count - 1) % heroes.Count].Id;
            GUILayout.Label(string.Format("<b>{0}</b> · герой {1} из {2}{3}", hero.Name, index + 1, heroes.Count,
                session.Squad().Contains(hero) ? " · в отряде" : " · в запасе"), Rich());
            if (GUILayout.Button("Следующий ▶", GUILayout.Width(150)))
                heroCard = heroes[(index + 1) % heroes.Count].Id;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var face = GUILayoutUtility.GetRect(150, 150, GUILayout.Width(150));
            ArtLibrary.DrawSliced(face, "UI/Panel", 30);
            ArtLibrary.Draw(new Rect(face.x + 8, face.y + 8, face.width - 16, face.height - 16), "Portraits/" + ArtLibrary.HeroSheet(hero.HeroClass));
            GUILayout.BeginVertical();
            GUILayout.Label(string.Format("<b>{0}</b> · {1}, уровень {2} · {3} {4} из {5}", hero.Name, ClassName(hero.HeroClass), hero.HeroLevel,
                SkillName(hero.HeroClass), hero.SkillLevel, session.SkillCap(hero)), Rich());
            GUILayout.Label("Характеристики: " + string.Join("  ", Enum.GetValues(typeof(Stat)).Cast<Stat>().Select(s => StatName(s) + " " + hero.StatOf(s))));
            var f = session.HeroPreview(hero.Id);
            if (f != null)
                GUILayout.Label(string.Format("В бою: атака {0:0}, здоровье {1:0}, броня {2:0}, крит {3:0}%, удар раз в {4:0.0} с",
                    f.Attack, f.MaxHp, f.Armor, f.Crit * 100, f.Interval * (session.Combat?.TickSeconds ?? 0.1)));
            GUILayout.Label(string.Format("Снаряжение кузни: ступень {0} (растёт с очагом) — атака и здоровье ×{1:0.00}", session.State.HearthLevel, session.GearMult()));
            var pet = session.ActivePet();
            GUILayout.Label(pet == null ? "Питомец: нет активного" : string.Format("Питомец: {0}, ур.{1} — {2}", pet.Name, session.PetLevel(pet.Id),
                session.PetBonusText(pet, session.PetLevel(pet.Id))));
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Уровень +1 (" + Formulas.HeroLevelCost(session.Balance, hero.HeroLevel) + " эсс.)"))
                Report(session.LevelUpHero(hero.Id));
            if (hero.SkillLevel < session.Balance.Activities.Skills.Max && GUILayout.Button("Умение +1 (" + session.SkillCost(hero) + " серд.)"))
                Report(session.UpgradeSkill(hero.Id));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            var comp = session.CompanionOf(hero.Id);
            int bond = session.BondLevel(hero.Id), cap = session.BondCap(hero);
            var kindNow = comp == null ? null : session.CompanionKind(comp.Kind);
            GUILayout.Label(kindNow == null
                ? string.Format("<b>Компаньон</b>: нет · узы ур.{0} (предел {1})", bond, cap)
                : string.Format("<b>Компаньон</b>: {0} · узы ур.{1} (предел {2}) — {3}", kindNow.Name, bond, cap,
                    session.CompanionSkillText(kindNow, Math.Min(bond, cap))), Rich());
            long bondCost = session.BondCost(hero.Id);
            if (kindNow != null && bondCost > 0 &&
                GUILayout.Button(string.Format("Узы +1: {0} лакомств (есть {1})", bondCost, session.Treats)))
                Report(session.RaiseBond(hero.Id));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(kindNow == null, "Без компаньона", "Button") && kindNow != null)
                Report(session.SetCompanion(hero.Id, ""));
            foreach (var kind in session.Balance.Companions.Kinds.Where(k => session.CompanionUnlocked(k.Id)))
            {
                bool on = kindNow != null && kindNow.Id == kind.Id;
                if (GUILayout.Toggle(on, kind.Name, "Button") && !on)
                    Report(session.SetCompanion(hero.Id, kind.Id));
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ the shops

        void DrawShops(long now)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                if (GUILayout.Toggle(shopTab == i, ShopTabs[i], "Button") && shopTab != i)
                {
                    shopTab = i;
                    windowScroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(string.Format("Серебро {0} · искры {1} · лакомства {2}", ShortNum(session.State.Wallet[ResourceType.Silver]),
                ShortNum(session.State.Wallet[ResourceType.Sparks]), session.Treats));
            GUILayout.Space(4);
            switch (shopTab)
            {
                case 0:
                    DrawMerchant(now);
                    break;
                case 1:
                    DrawDeals(now);
                    break;
                case 2:
                    DrawExchange(now);
                    break;
                default:
                    DrawSparksShop(now);
                    break;
            }
        }

        bool ClosedWithoutTradingPost()
        {
            if (session.MerchantOpen)
                return false;
            GUILayout.Label(string.Format("Торговец придёт с Торговой лавкой (очаг {0})", session.Balance.Room(RoomType.TradingPost).Unlock));
            return true;
        }

        void DrawOffers(List<Offer> offers, long now)
        {
            foreach (var o in offers)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("<b>{0}</b>\n<size=12><color=#d8c8a8>{1} · {2}</color></size>", o.Name, o.Text, o.Limit), Rich());
                GUI.enabled = o.Left > 0;
                string pay = o.PayWith == ResourceType.Sparks ? "искр" : "серебра";
                if (GUILayout.Button(o.Left > 0 ? string.Format("Купить: {0} {1}", ShortNum(o.Price), pay) : "Нет в наличии", GUILayout.Width(210)))
                {
                    var fail = session.BuyOffer(o.Id, now);
                    Report(fail, "Coins");
                    if (fail == Fail.None)
                        message = "Куплено: " + o.Name;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        void DrawMerchant(long now)
        {
            if (ClosedWithoutTradingPost())
                return;
            GUILayout.Label("<b>Торговец</b> · за серебро, цены — в часах дохода крепости", Rich());
            DrawOffers(session.MerchantOffers(now), now);

            var items = session.BrikShop(now);
            GUILayout.Space(6);
            GUILayout.Label("<b>Лавка Брика</b> · новый товар в 00:00 и 12:00", Rich());
            GUILayout.BeginHorizontal();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                GUI.enabled = !it.Sold;
                string pay = it.PayWith == ResourceType.Sparks ? "искр" : "серебра";
                if (GUILayout.Button(string.Format("{0}\n{1} {2}{3}", GoodsName(it), it.Price, pay, it.Sold ? " · продано" : "")))
                    Report(session.BuyShopItem(i, now));
                if (i % 4 == 3)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            long price = session.RefreshPrice(now);
            if (price > 0 && GUILayout.Button("Обновить лавку за " + price + " искр"))
                Report(session.RefreshShop(now));
        }

        void DrawDeals(long now)
        {
            if (ClosedWithoutTradingPost())
                return;
            GUILayout.Label("<b>Предложения дня</b> · три товара дешевле обычного, новый набор в полночь, каждый — один раз", Rich());
            DrawOffers(session.DailyDeals(now), now);
        }

        void DrawExchange(long now)
        {
            if (ClosedWithoutTradingPost())
                return;
            long left = session.ExchangeLeft(now);
            GUILayout.Label(string.Format("<b>Обмен у торговой лавки</b> · ×{0:0.0#} ценности · сегодня ещё на {1} серебра (Ярмарка Пепла меняет выгоднее)",
                session.Balance.Market.ExchangeFee, ShortNum(left)), Rich());
            foreach (var (from, to) in GameSession.ExchangePairs)
            {
                double value = GameSession.FairValue[Array.IndexOf(GameSession.FairGoods, from)];
                long hour = Math.Max(1, (long)(session.IncomePerHour() / value / 4)); // a quarter of an hour of income
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("{0} → {1} · есть {2}", ResName(from), ResName(to), ShortNum(session.State.Wallet[from])), GUILayout.Width(300));
                foreach (long amount in new[] { hour, hour * 4 })
                {
                    GUI.enabled = session.State.Wallet[from] >= amount && amount * value <= left;
                    if (GUILayout.Button(string.Format("Обменять {0} → {1}", ShortNum(amount), ShortNum(session.ExchangeQuote(from, to, amount)))))
                    {
                        var fail = session.Exchange(from, to, amount, now, out long got);
                        Report(fail, "Coins");
                        if (fail == Fail.None)
                            message = string.Format("Обмен: {0} {1} → {2} {3}", amount, ResName(from), got, ResName(to));
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        void DrawSparksShop(long now)
        {
            var shop = session.Balance.Activities.Shop;
            var st = session.State;
            GUILayout.Label("<b>Лавка Искр</b> · цены известны заранее, никаких лотерей; всё это можно добыть и игрой", Rich());
            GUILayout.BeginHorizontal();
            if (!st.FourthBuilder && GUILayout.Button(string.Format("Четвёртый строитель — {0} искр (с очага {1})", shop.FourthBuilder, shop.FourthBuilderFrom)))
                Report(session.BuyFourthBuilder());
            if (GUILayout.Button(string.Format("Набор материалов: {0} за {1} искр · осталось {2} на неделе",
                    session.IncomePerHour(), shop.PackPrice, session.PacksLeft(now))))
                Report(session.BuyPack(now));
            GUILayout.EndHorizontal();
            DrawOffers(session.PremiumOffers(now), now);
            GUILayout.Label("<size=12><color=#d8c8a8>Украшения за искры ставятся в залах: откройте зал → «Украсить».</color></size>", Rich());
        }
    }
}
