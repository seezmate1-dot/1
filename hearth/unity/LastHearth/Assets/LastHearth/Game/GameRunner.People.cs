using System;
using System.Linq;
using LastHearth.Core;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// GDD 7-8 on the screen: the spirit of the fortress as a small plate in the top bar (a tap opens its breakdown
    /// in the «Жители» window), the level, experience, rarity and traits of every dweller, the deserters and the
    /// developer's page «Жители». The rules are the core's (GameSession.People.cs).
    /// </summary>
    public sealed partial class GameRunner
    {
        const float MoralePlateWidth = 78;

        /// <summary>The breakdown of the spirit is open in the «Жители» window.</summary>
        bool moraleOpen;
        GameSession peopleEventsOf;
        int adminPeopleDweller = -1;
        Trait adminPeopleTrait;

        /// <summary>The session's news of the dwellers, told once (a desertion is worth a toast; levels are seen in the list).</summary>
        void WatchPeople()
        {
            if (peopleEventsOf == session || session == null)
                return;
            peopleEventsOf = session;
            session.DwellerDeserted += d =>
            {
                message = d.Name + " ушёл в туман: дух крепости в отчаянии";
                sounds?.Play("Click");
            };
        }

        static Color MoraleColor(double morale) =>
            morale < 20 ? new Color(1f, 0.42f, 0.38f, 1) : morale < 40 ? new Color(1f, 0.72f, 0.4f, 1) : morale < 80 ? new Color(0.92f, 0.88f, 0.78f, 1) : new Color(0.6f, 0.95f, 0.5f, 1);

        static string MoraleHex(double morale) => morale < 20 ? "#ff6b61" : morale < 40 ? "#ffb866" : morale < 80 ? "#ebe0c7" : "#99f280";

        /// <summary>The top bar's plate: «Дух 68» over a thin bar; red and pulsing in despair. A tap opens the breakdown.</summary>
        void DrawMoralePlate(Rect r)
        {
            WatchPeople();
            double morale = session.Morale;
            bool despair = session.InDespair;
            var old = GUI.color;
            if (despair)
                GUI.color = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin(Time.time * 5));
            ArtLibrary.DrawSliced(r, "UI/Pill", 30);
            GUI.color = MoraleColor(morale);
            UiSkin.Outlined(new Rect(r.x + 8, r.y, r.width - 12, r.height - 3), "Дух " + Math.Round(morale), hudText);
            var track = new Rect(r.x + 10, r.yMax - 7, r.width - 20, 3);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = MoraleColor(morale);
            float fill = Mathf.Clamp((float)(morale / 100), 0, 1);
            if (track.width * fill >= 0.5f)
                GUI.DrawTexture(new Rect(track.x, track.y, track.width * fill, track.height), Texture2D.whiteTexture);
            GUI.color = old;
            if (window == Win.None && GUI.Button(r, "", GUIStyle.none))
            {
                moraleOpen = true;
                Open(Win.Dwellers);
            }
        }

        static string Percent(double x) => x > 0 ? "+" + Math.Round(x * 100) + "%" : x < 0 ? "−" + Math.Round(-x * 100) + "%" : "без изменений";

        static string Points(double v) => v > 0 ? "+" + v.ToString("0.#") : "−" + (-v).ToString("0.#");

        /// <summary>The head of the «Жители» window: the spirit, its band, what makes it, the warning and the deserters.</summary>
        void DrawMoralePanel(long now)
        {
            WatchPeople();
            double morale = session.Morale, target = session.MoraleTarget(now);
            var band = session.MoraleBand();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<b>Дух крепости: <color={0}>{1:0}</color> — {2}</b> · выработка {3} · идёт к {4:0}{5}",
                MoraleHex(morale), morale, band.Name, Percent(band.Output), target, target > morale + 0.5 ? " ↑" : target < morale - 0.5 ? " ↓" : ""), Rich());
            if (GUILayout.Button(moraleOpen ? "Скрыть" : "Из чего он", GUILayout.Width(130)))
                moraleOpen = !moraleOpen;
            GUILayout.EndHorizontal();
            if (session.InDespair)
            {
                var next = session.NextDeserter();
                long at = session.DesertionAt();
                GUILayout.Label(next == null ? "<color=#f87>Жители в отчаянии.</color>"
                    : string.Format("<color=#f87><b>Жители ропщут:</b> через {0} уйдёт в туман {1}, если дух не поднимется. Накормите людей, " +
                                    "дайте очагу угля или разожгите его до «Пылающего» (+10).</color>", Duration(Math.Max(0, at - now)), next.Name), Rich());
            }
            if (moraleOpen)
            {
                var m = session.Balance.People.Morale;
                GUILayout.Label(string.Format("<size=12>Основа {0:0}; дух идёт к цели: две трети пути за {1:0.#} ч.</size>", m.Base, m.Hours), Rich());
                foreach (var f in session.MoraleFactors(now))
                    GUILayout.Label(string.Format("<size=12>   <color={0}>{1}</color>  {2}</size>", f.Value > 0 ? "#9e8" : "#f98", Points(f.Value), f.Name), Rich());
                GUILayout.Label("<size=12>" + string.Join(" · ", m.Bands.Select(b => string.Format("{0:0}+ {1}: {2}", b.From, b.Name, Percent(b.Output)))) + "</size>", Rich());
                var present = session.State.Dwellers.SelectMany(d => d.Traits ?? new System.Collections.Generic.List<Trait>()).Distinct().ToList();
                if (present.Count > 0)
                    GUILayout.Label("<size=12>Черты: " + string.Join("; ", present.Select(t => session.TraitInfo(t).Name + " — " + session.TraitInfo(t).Text)) + "</size>", Rich());
            }
            var deserters = session.Deserters;
            int recall = -1;
            for (int i = 0; i < deserters.Count; i++)
            {
                var x = deserters[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("<color=#d8c8a8>{0} (ур. {1}) ушёл в туман · ждёт ещё {2}</color>", x.Dweller.Name, x.Dweller.Level,
                    Duration(Math.Max(0, x.Until - now))), Rich());
                if (GUILayout.Button("Вернуть: " + CostText(session.RecallCost(x)), GUILayout.Width(230)))
                    recall = i;
                GUILayout.EndHorizontal();
            }
            if (recall >= 0)
            {
                var fail = session.Recall(recall, now, out var back);
                if (fail == Fail.RulesNotMet)
                    message = "Сначала поднимите дух крепости до " + session.Balance.People.Morale.RecallMorale;
                else
                    Report(fail);
                if (back != null)
                    message = back.Name + " вернулся в крепость";
            }
        }

        string TraitNames(Dweller d) => d.Traits == null || d.Traits.Count == 0 ? "" : string.Join(", ", d.Traits.Select(t => session.TraitInfo(t).Name));

        /// <summary>«ур. 7 · опыт 3/14 · редкий · Трудяга»: the dweller's growth in one short line.</summary>
        string GrowthLine(Dweller d)
        {
            string xp = d.Level >= session.MaxDwellerLevel ? "предел" : string.Format("опыт {0:0}/{1:0}", Math.Floor(d.Xp), Math.Ceiling(session.XpToNext(d.Level)));
            var rarity = session.RarityOf(d.Id);
            string rare = rarity == Rarity.Common ? "" : " · " + session.RarityInfo(rarity).Name;
            string traits = TraitNames(d);
            return string.Format("ур. {0} · {1}{2}{3}", d.Level, xp, rare, traits == "" ? "" : " · " + traits);
        }

        // ------------------------------------------------------------------ the developer's menu, page «Жители»

        void AdminPeople(AdminList list, long now)
        {
            var st = session.State;
            var band = session.MoraleBand();
            list.Row(string.Format("Дух крепости {0:0} — {1} ({2}) · цель {3:0} · ушли {4}, ждут в тумане {5}", session.Morale, band.Name, Percent(band.Output),
                session.MoraleTarget(now), st.People.DesertedTotal, session.Deserters.Count));
            foreach (int v in new[] { 0, 15, 30, 50, 85, 100 })
                list.Add("Дух " + v, t => "Дух: " + session.AdminSetMorale(v, t).ToString("0"));
            list.Add("−10", t => "Дух: " + session.AdminSetMorale(session.Morale - 10, t).ToString("0"));
            list.Add("+10", t => "Дух: " + session.AdminSetMorale(session.Morale + 10, t).ToString("0"));
            list.Row(session.InDespair ? "Отчаяние: уход через " + Duration(Math.Max(0, session.DesertionAt() - now)) : "Уход жителей: только в отчаянии");
            list.Add("Проверить уход сейчас", t =>
            {
                var gone = session.AdminDesertionCheck(t);
                return gone != null ? gone.Name + " ушёл в туман" : session.InDespair ? "Уходить некому (герои не уходят)" : "Дух не в отчаянии: никто не уходит";
            });
            list.Add("Вернуть всех ушедших", t => "Вернулись: " + session.AdminRecallAll(t));
            list.Row("Вести крепости");
            foreach (MoraleNews n in Enum.GetValues(typeof(MoraleNews)))
            {
                var info = session.NewsInfo(n);
                list.Add(info.Name, t =>
                {
                    session.AdminNews(n, t);
                    return string.Format("{0}: {1} к духу на {2:0} ч", info.Name, Points(info.Value), info.Hours);
                });
            }

            var d = session.FindDweller(adminPeopleDweller) ?? st.Dwellers.FirstOrDefault();
            if (d == null)
                return;
            adminPeopleDweller = d.Id;
            int id = d.Id;
            list.Row(string.Format("{0}: {1}", d.Name, GrowthLine(d)));
            list.Add("Следующий житель", _ =>
            {
                var next = session.AdminNextDweller(id);
                adminPeopleDweller = next?.Id ?? -1;
                return next == null ? "Жителей нет" : next.Name + ": " + GrowthLine(next);
            });
            foreach (int xp in new[] { 10, 100 })
                list.Add("+" + xp + " опыта", _ => string.Format("{0}: +{1} ур. → {2}", d.Name, session.AdminGiveXp(id, xp), GrowthLine(d)));
            foreach (int n in new[] { 1, 10 })
                list.Add("+" + n + " ур.", _ => string.Format("{0}: уровень {1}", d.Name, session.AdminAddLevels(id, n)));
            list.Add("До " + session.MaxDwellerLevel, _ => string.Format("{0}: уровень {1}", d.Name, session.AdminAddLevels(id, session.MaxDwellerLevel)));
            foreach (Rarity r in Enum.GetValues(typeof(Rarity)))
                list.Add(session.RarityInfo(r).Name, _ => d.Name + ": " + session.RarityInfo(session.AdminSetRarity(id, r)).Name);
            var trait = session.TraitInfo(adminPeopleTrait);
            list.Row(string.Format("Черта «{0}» ({1}): {2}", trait.Name, trait.Good ? "хорошая" : "плохая", trait.Text));
            list.Add("Следующая черта", _ =>
            {
                adminPeopleTrait = session.AdminNextTrait(adminPeopleTrait);
                return "Черта: " + session.TraitInfo(adminPeopleTrait).Name;
            });
            list.Add("Добавить", _ => session.AdminAddTrait(id, adminPeopleTrait)
                ? d.Name + ": " + TraitNames(d)
                : d.Name + ": уже есть или черт три");
            list.Add("Убрать", _ => session.AdminRemoveTrait(id, adminPeopleTrait)
                ? d.Name + ": " + TraitNames(d)
                : d.Name + ": такой черты нет или она последняя");
            list.Row("Все жители");
            list.Add("+5 уровней всем", _ =>
            {
                foreach (var x in session.State.Dwellers.ToList())
                    session.AdminAddLevels(x.Id, 5);
                return "Средний уровень: " + session.State.Dwellers.Average(x => x.Level).ToString("0.0");
            });
        }
    }
}
