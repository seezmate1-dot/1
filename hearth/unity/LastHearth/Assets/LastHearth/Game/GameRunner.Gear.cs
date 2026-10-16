using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// GDD 8 «Снаряжение»: the equipment window (six slot frames around the hero's portrait, the item card with
    /// the comparison against what the hero wears, the bag) and the forging tab of the Forge room. The items'
    /// pictures are Resources/Art/Items/&lt;Kind&gt; once Art.json has "itemArt"; until then an item is a plate in
    /// the colour of its rarity with the short name of its kind.
    /// </summary>
    public sealed partial class GameRunner
    {
        /// <summary>The slot chosen on the paper doll, and the item chosen in the bag (0: none).</summary>
        ItemSlot gearSlot = ItemSlot.Weapon;
        int gearItem;
        bool gearAll;
        /// <summary>The Forge room's tab (false: the hall, true: forging) and what is about to be forged.</summary>
        bool forgeTab;
        ItemSlot forgeSlot = ItemSlot.Weapon;
        int forgeKind;
        ItemRarity forgeRarity = ItemRarity.Common;
        int forgeSet;
        GUIStyle plateText, slotText;

        // the paper doll: three slots on each side of the portrait
        static readonly ItemSlot[] LeftSlots = { ItemSlot.Weapon, ItemSlot.Helmet, ItemSlot.Armor };
        static readonly ItemSlot[] RightSlots = { ItemSlot.OffHand, ItemSlot.Amulet, ItemSlot.Boots };

        void GearStyles()
        {
            if (plateText != null)
                return;
            plateText = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            slotText = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        static Color Hex(string hex, float alpha = 1)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7)
                return new Color(0.6f, 0.6f, 0.6f, alpha);
            int v = Convert.ToInt32(hex.Substring(1, 6), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, alpha);
        }

        static void Paint(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        string RarityColor(ItemRarity r) => session.RarityInfo(r).Color;

        /// <summary>"Лук · редкий · ур. 12" in the colour of the rarity.</summary>
        string ItemTitle(GearItem item) => item.Issued
            ? string.Format("<color=#bbb>{0} · казённое кузни · ур. {1}</color>", ItemKinds.Of(item.Kind).Name, item.Level)
            : string.Format("<color={0}><b>{1}</b> · {2} · ур. {3}</color>", RarityColor(item.Rarity), session.ItemName(item),
                session.RarityInfo(item.Rarity).Name, item.Level);

        /// <summary>An item drawn as a frame in its rarity's colour: the picture or the short name, the level, the plus.</summary>
        void DrawItemPlate(Rect r, GearItem item, bool selected)
        {
            GearStyles();
            var rarity = Hex(item == null ? "#555555" : RarityColor(item.Rarity), item == null || item.Issued ? 0.55f : 1f);
            ArtLibrary.DrawSliced(r, "UI/Panel", 30);
            float edge = selected ? 4 : 3;
            Paint(new Rect(r.x, r.y, r.width, edge), selected ? new Color(1f, 0.84f, 0.42f, 1) : rarity);
            Paint(new Rect(r.x, r.yMax - edge, r.width, edge), selected ? new Color(1f, 0.84f, 0.42f, 1) : rarity);
            Paint(new Rect(r.x, r.y, edge, r.height), selected ? new Color(1f, 0.84f, 0.42f, 1) : rarity);
            Paint(new Rect(r.xMax - edge, r.y, edge, r.height), selected ? new Color(1f, 0.84f, 0.42f, 1) : rarity);
            var inner = new Rect(r.x + edge + 2, r.y + edge + 2, r.width - 2 * edge - 4, r.height - 2 * edge - 4);
            Paint(inner, new Color(rarity.r * 0.45f, rarity.g * 0.45f, rarity.b * 0.45f, 0.85f));
            if (item == null)
                return;
            if (ArtLibrary.Data.ItemArt)
                ArtLibrary.Draw(new Rect(inner.x + 4, inner.y + 4, inner.width - 8, inner.height - 8), ArtLibrary.ItemIcon(item.Kind));
            else
                UiSkin.Outlined(new Rect(inner.x, inner.y, inner.width, inner.height - 12), ItemKinds.Of(item.Kind).Short, plateText);
            UiSkin.Outlined(new Rect(inner.x, inner.yMax - 16, inner.width, 16), item.Issued ? "кузня " + item.Level : "ур. " + item.Level, slotText);
            if (item.Plus > 0)
                UiSkin.Outlined(new Rect(inner.x, inner.y, inner.width - 4, 16), "+" + item.Plus, slotText);
        }

        /// <summary>The stats of an item, each with a green ▲ or a red ▼ against another (null: no comparison).</summary>
        string StatLines(GearItem item, GearItem against)
        {
            var mine = session.ItemStats(item);
            var other = against == null ? new List<StatValueData>() : session.ItemStats(against);
            var stats = mine.Select(x => x.Stat).Union(other.Select(x => x.Stat)).OrderBy(x => (int)x);
            var lines = new List<string>();
            foreach (var stat in stats)
            {
                double a = mine.Where(x => x.Stat == stat).Sum(x => x.Value);
                double b = other.Where(x => x.Stat == stat).Sum(x => x.Value);
                string line = string.Format("{0} {1}", ItemKinds.StatName(stat), a > 0 ? ItemKinds.StatValue(stat, a) : "—");
                if (against != null && Math.Abs(a - b) > 1e-6)
                {
                    string diff = ItemKinds.StatValue(stat, Math.Abs(a - b)).TrimStart('+');
                    line += a > b ? " <color=#7e7>▲ " + diff + "</color>" : " <color=#f77>▼ " + diff + "</color>";
                }
                lines.Add(line);
            }
            if (!string.IsNullOrEmpty(item.Set) && session.Balance.Items.Set(item.Set) is ItemSetData set)
                lines.Add(string.Format("<color=#fd7>сет «{0}»: 2 — {1}; 4 — {2}</color>", set.Name, SetText(set.Two), SetText(set.Four)));
            return string.Join("\n", lines);
        }

        static string SetText(List<StatValueData> bonus) =>
            string.Join(", ", bonus.Select(b => ItemKinds.StatName(b.Stat) + " " + ItemKinds.StatValue(b.Stat, b.Value)));

        // ------------------------------------------------------------------ the equipment window

        void DrawGear(long now)
        {
            GearStyles();
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
            {
                heroCard = heroes[(index + heroes.Count - 1) % heroes.Count].Id;
                gearItem = 0;
            }
            GUILayout.Label(string.Format("<b>{0}</b> · {1}, ур. {2} · сила снаряжения <b>{3}</b>", hero.Name, ClassName(hero.HeroClass),
                hero.HeroLevel, session.HeroGearPower(hero)), Rich());
            if (GUILayout.Button("Следующий ▶", GUILayout.Width(150)))
            {
                heroCard = heroes[(index + 1) % heroes.Count].Id;
                gearItem = 0;
            }
            GUILayout.EndHorizontal();

            // the paper doll
            const float plate = 84, pitch = 104, face = 200;
            var doll = GUILayoutUtility.GetRect(Mathf.Max(600, viewWidth), 3 * pitch + 6);
            float cx = doll.x + doll.width / 2;
            var portrait = new Rect(cx - face / 2, doll.y + (3 * pitch - face) / 2, face, face);
            ArtLibrary.DrawSliced(portrait, "UI/Panel", 30);
            ArtLibrary.Draw(new Rect(portrait.x + 10, portrait.y + 10, portrait.width - 20, portrait.height - 20), "Portraits/" + ArtLibrary.HeroSheet(hero.HeroClass));
            for (int side = 0; side < 2; side++)
            {
                var slots = side == 0 ? LeftSlots : RightSlots;
                float x = side == 0 ? cx - face / 2 - 30 - plate : cx + face / 2 + 30;
                for (int i = 0; i < 3; i++)
                {
                    var slot = slots[i];
                    var r = new Rect(x, doll.y + i * pitch, plate, plate);
                    DrawItemPlate(r, session.SlotItem(hero, slot), gearSlot == slot && gearItem == 0);
                    UiSkin.Outlined(new Rect(x - 20, r.yMax, plate + 40, 16), ItemKinds.SlotName(slot), slotText);
                    if (GUI.Button(r, "", GUIStyle.none))
                    {
                        gearSlot = slot;
                        gearItem = 0;
                        sounds?.Play("Click", 0.4f);
                    }
                }
            }
            var sets = session.HeroSets(hero.Id).Where(x => x.count >= 2).ToList();
            if (sets.Count > 0)
                GUILayout.Label("<color=#fd7>Сеты: " + string.Join(", ", sets.Select(x => "«" + x.set.Name + "» " + x.count + " шт.")) + "</color>", Rich());

            // the card: the chosen bag item against what the hero wears, or the worn item of the chosen slot
            var chosen = session.FindItem(gearItem);
            if (chosen != null && chosen.HeroId != 0)
                chosen = null;
            var worn = session.SlotItem(hero, chosen?.Slot ?? gearSlot);
            GUILayout.Space(4);
            if (chosen != null)
                DrawBagCard(hero, chosen, worn, now);
            else
                DrawWornCard(hero, worn);

            // the bag
            GUILayout.Space(6);
            var bag = session.Bag();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<b>Сумка</b> {0}/{1} · обломки {2} · разборка сегодня ещё {3}", bag.Count, session.BagSize(),
                session.Gear.Scrap, session.DismantlesLeft(now)), Rich());
            if (GUILayout.Toggle(gearAll, gearAll ? "Показаны все вещи" : "Только для героя", "Button", GUILayout.Width(200)) != gearAll)
                gearAll = !gearAll;
            var forge = session.State.Rooms.Where(r => r.Type == RoomType.Forge && r.Level > 0).OrderByDescending(r => r.Level).FirstOrDefault();
            if (forge != null && GUILayout.Button("Ковать в кузне", GUILayout.Width(200)))
            {
                selectedRoom = forge.Id;
                forgeTab = true;
                Open(Win.Room);
            }
            GUILayout.EndHorizontal();
            var shown = bag.Where(i => gearAll || session.Fits(i, hero.HeroClass)).OrderBy(i => (int)i.Slot)
                .ThenByDescending(session.ItemPower).ToList();
            if (shown.Count == 0)
            {
                GUILayout.Label(bag.Count == 0 ? "Сумка пуста: вещи приносят Поход, Охота, вылазки и события, их куют в Кузне."
                    : "Для этого героя в сумке ничего нет.");
            }
            else
            {
                const float cell = 76, gap = 8;
                float width = Mathf.Max(600, viewWidth);
                int cols = Math.Max(1, (int)((width + gap) / (cell + gap)));
                int rows = (shown.Count + cols - 1) / cols;
                var grid = GUILayoutUtility.GetRect(width, rows * (cell + gap));
                for (int i = 0; i < shown.Count; i++)
                {
                    var item = shown[i];
                    var r = new Rect(grid.x + (i % cols) * (cell + gap), grid.y + (i / cols) * (cell + gap), cell, cell);
                    DrawItemPlate(r, item, item.Id == gearItem);
                    // a better item for this hero than what is worn: a green arrow in the corner
                    if (session.Fits(item, hero.HeroClass) && session.ItemPower(item) > session.ItemPower(session.SlotItem(hero, item.Slot)))
                    {
                        var old = GUI.color;
                        GUI.color = new Color(0.5f, 0.95f, 0.5f, 1);
                        UiSkin.Outlined(new Rect(r.x + 4, r.y + 2, 20, 18), "▲", slotText);
                        GUI.color = old;
                    }
                    if (GUI.Button(r, "", GUIStyle.none))
                    {
                        gearItem = item.Id;
                        sounds?.Play("Click", 0.4f);
                    }
                }
            }
            foreach (var line in session.Gear.News.AsEnumerable().Reverse().Take(4))
                GUILayout.Label("<size=12><color=#d8c8a8>" + line + "</color></size>", Rich());
        }

        void DrawWornCard(Dweller hero, GearItem worn)
        {
            GUILayout.Label(string.Format("{0}: {1} · сила {2}", ItemKinds.SlotName(worn.Slot), ItemTitle(worn), session.ItemPower(worn)), Rich());
            GUILayout.Label(StatLines(worn, null), Rich());
            if (worn.Issued)
            {
                GUILayout.Label("<size=12><color=#d8c8a8>Пустое место занимает казённая вещь кузни: обычная, уровня очага, без доп. свойств. " +
                    "Любая найденная вещь получше заменит её.</color></size>", Rich());
                return;
            }
            var issued = session.IssuedPiece(hero.HeroClass, worn.Slot);
            if (session.ItemPower(issued) > session.ItemPower(worn))
                GUILayout.Label("<color=#f97>Очаг перерос эту вещь: казённая кузни уже сильнее — снимите её.</color>", Rich());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Снять"))
                Report(session.Unequip(hero.Id, worn.Slot));
            DrawUpgradeButton(worn);
            GUILayout.EndHorizontal();
        }

        void DrawBagCard(Dweller hero, GearItem item, GearItem worn, long now)
        {
            bool fits = session.Fits(item, hero.HeroClass);
            GUILayout.Label(string.Format("{0} · сила {1}{2} · {3}", ItemTitle(item), session.ItemPower(item),
                fits ? string.Format(" (надето: {0})", session.ItemPower(worn)) : "", item.Source), Rich());
            if (!fits)
            {
                var kind = ItemKinds.Of(item.Kind);
                GUILayout.Label("<color=#f97>Не для этого героя: " + string.Join(", ", kind.Classes.Select(ClassName)) + "</color>", Rich());
            }
            GUILayout.Label(StatLines(item, fits ? worn : null), Rich());
            GUILayout.BeginHorizontal();
            GUI.enabled = fits;
            if (GUILayout.Button("Надеть"))
            {
                var fail = session.Equip(hero.Id, item.Id);
                Report(fail, "Done");
                if (fail == Fail.None)
                    gearItem = 0;
            }
            GUI.enabled = true;
            DrawUpgradeButton(item);
            if (GUILayout.Button("Продать: " + ShortNum(session.SellPrice(item)) + " серебра"))
            {
                Report(session.SellItem(item.Id), "Coins");
                gearItem = 0;
            }
            GUI.enabled = session.DismantlesLeft(now) > 0;
            if (GUILayout.Button("Разобрать: +" + session.ScrapOf(item) + " обломков"))
            {
                Report(session.Dismantle(item.Id, now));
                gearItem = 0;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void DrawUpgradeButton(GearItem item)
        {
            var cost = session.UpgradeCost(item);
            if (cost == null || session.ForgeLevel() < 1)
                return;
            if (GUILayout.Button(string.Format("Улучшить до +{0}: {1}", item.Plus + 1, CostText(cost))))
                Report(session.UpgradeItem(item.Id), "Build");
        }

        // ------------------------------------------------------------------ the forge (the Forge room's tab)

        /// <summary>The tabs of the Forge room; true when the forging tab is open (the hall's panel is skipped).</summary>
        bool ForgeTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!forgeTab, "Зал", "Button") && forgeTab)
                forgeTab = false;
            if (GUILayout.Toggle(forgeTab, "Ковка", "Button") && !forgeTab)
                forgeTab = true;
            GUILayout.EndHorizontal();
            return forgeTab;
        }

        void DrawForge(long now)
        {
            var b = session.Balance;
            var gear = session.Gear;
            GUILayout.Label(string.Format("<b>Кузня</b> ур. {0}: куёт вещи {1}-го уровня · обломки {2} · сумка {3}/{4}", session.ForgeLevel(),
                session.CraftLevel, gear.Scrap, session.Bag().Count, session.BagSize()), Rich());
            var job = gear.Craft;
            if (job != null)
            {
                long left = session.CraftRemaining(now);
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Куётся: <color={0}>{1}, {2}</color>, ур. {3} — {4}", RarityColor(job.Rarity), ItemKinds.Of(job.Kind).Name,
                    session.RarityInfo(job.Rarity).Name, job.Level, left > 0 ? "осталось " + Duration(left) : "готово"), Rich());
                long price = session.CraftSpeedupPrice(now);
                if (GUILayout.Button(left <= 0 ? "Забрать" : price == 0 ? "Завершить бесплатно" : "Завершить: " + price + " искр", GUILayout.Width(240)))
                    Report(session.FinishCraftNow(now), "Done");
                GUILayout.EndHorizontal();
            }

            // for whom: the hero of the equipment window decides the kinds
            var heroes = session.State.Dwellers.Where(d => d.IsHero).ToList();
            var hero = heroes.FirstOrDefault(d => d.Id == heroCard) ?? heroes.FirstOrDefault();
            var cls = hero?.HeroClass ?? HeroClass.Guardian;
            GUILayout.BeginHorizontal();
            GUILayout.Label(hero == null ? "Для класса: Страж" : string.Format("Для героя: <b>{0}</b> ({1})", hero.Name, ClassName(cls)), Rich());
            if (heroes.Count > 1 && GUILayout.Button("Другой герой ▶", GUILayout.Width(180)))
            {
                int i = heroes.IndexOf(hero);
                heroCard = heroes[(i + 1) % heroes.Count].Id;
                forgeKind = 0;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (var slot in GameSession.Slots)
            {
                if (GUILayout.Toggle(forgeSlot == slot, ItemKinds.SlotName(slot), "Button") && forgeSlot != slot)
                {
                    forgeSlot = slot;
                    forgeKind = 0;
                }
            }
            GUILayout.EndHorizontal();
            var kinds = ItemKinds.For(cls, forgeSlot);
            forgeKind = Math.Max(0, Math.Min(kinds.Count - 1, forgeKind));
            if (kinds.Count > 1)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < kinds.Count; i++)
                {
                    if (GUILayout.Toggle(forgeKind == i, ItemKinds.Of(kinds[i]).Name, "Button") && forgeKind != i)
                        forgeKind = i;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            foreach (ItemRarity r in Enum.GetValues(typeof(ItemRarity)))
            {
                bool open = session.CraftOpen(r) == Fail.None;
                GUI.enabled = open;
                string label = open ? session.RarityInfo(r).Name
                    : r == ItemRarity.Legendary ? "легендарный (очаг " + b.Activities.Legendary.FromHearth + ")"
                    : session.RarityInfo(r).Name + " (кузня " + session.RarityInfo(r).ForgeLevel + ")";
                if (GUILayout.Toggle(forgeRarity == r, label, "Button") && forgeRarity != r)
                    forgeRarity = r;
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            if (session.CraftOpen(forgeRarity) != Fail.None)
                forgeRarity = ItemRarity.Common;
            string set = "";
            if (forgeRarity == ItemRarity.Legendary && b.Items.Sets.Count > 0)
            {
                forgeSet = Math.Max(0, Math.Min(b.Items.Sets.Count - 1, forgeSet));
                GUILayout.BeginHorizontal();
                for (int i = 0; i < b.Items.Sets.Count; i++)
                {
                    if (GUILayout.Toggle(forgeSet == i, "Сет «" + b.Items.Sets[i].Name + "»", "Button") && forgeSet != i)
                        forgeSet = i;
                }
                GUILayout.EndHorizontal();
                set = b.Items.Sets[forgeSet].Id;
            }
            var rarity = session.RarityInfo(forgeRarity);
            var cost = session.CraftCost(forgeRarity);
            long scrap = session.CraftScrap(forgeRarity);
            GUILayout.Label(string.Format("<color={0}><b>{1}</b>, {2}</color>, ур. {3}: основные свойства ×{4:0.00}, доп. свойств {5} (случайные, от половины до " +
                "полного значения). Цена: {6}{7} · {8}", rarity.Color, ItemKinds.Of(kinds[forgeKind]).Name, rarity.Name, session.CraftLevel, rarity.Mult,
                rarity.Secondaries, CostText(cost), scrap > 0 ? ", обломки " + scrap : "", Duration(session.CraftSeconds(forgeRarity))), Rich());
            GUI.enabled = job == null;
            if (GUILayout.Button("Ковать", GUILayout.Height(40)))
            {
                var fail = session.StartCraft(kinds[forgeKind], forgeRarity, now, set);
                Report(fail, "Build");
                if (fail == Fail.None)
                    message = "Кузня взялась за работу: " + ItemKinds.Of(kinds[forgeKind]).Name;
            }
            GUI.enabled = true;
            GUILayout.Label("<size=12><color=#d8c8a8>Обломки дают разобранные вещи (окно «Снаряжение»). Там же вещи надевают, сравнивают, " +
                "улучшают до +10 железом и эссенцией и продают.</color></size>", Rich());
            if (heroes.Count > 0 && GUILayout.Button("Снаряжение героев"))
                Open(Win.Gear);
        }
    }
}
