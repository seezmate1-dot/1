using System;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// GDD 6: the work of the halls inside the room window — the tavern's board, the infirmary's patients, the
    /// library's tree, the training hall, the watchtower's scouting, the chapel and the archive (GameSession.Rooms.cs).
    /// </summary>
    public sealed partial class GameRunner
    {
        /// <summary>The branch of the library shown, the trainee chosen, the record opened.</summary>
        int researchBranch;
        int traineeId = -1;
        string openRecord = "";

        /// <summary>Draws the hall's own work under the room's header; nothing for the producing rooms.</summary>
        void DrawHallWork(Room room, long now)
        {
            if (room.Level < 1)
                return;
            switch (room.Type)
            {
                case RoomType.Tavern: DrawTavern(now); break;
                case RoomType.Infirmary: DrawInfirmary(now); break;
                case RoomType.Library: DrawLibrary(now); break;
                case RoomType.TrainingHall: DrawTraining(room, now); break;
                case RoomType.Watchtower: DrawWatchtower(now); break;
                case RoomType.Chapel: DrawChapel(now); break;
                case RoomType.Archive: DrawArchive(now); break;
            }
        }

        static string RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Seasoned: return "#8fd18f";
                case Rarity.Rare: return "#7fb8ff";
                case Rarity.Legendary: return "#ffb347";
                default: return "#dddddd";
            }
        }

        static readonly string[] StatShort = { "Сил", "Лов", "Ум", "Сто", "Дух" };

        static string StatsLine(int[] stats) => string.Join(" ", Enumerable.Range(0, 5).Select(k => StatShort[k] + " " + stats[k]));

        // ------------------------------------------------------------------ tavern

        void DrawTavern(long now)
        {
            var st = session.State;
            var odds = session.RarityOdds();
            GUILayout.Label(string.Format("<b>Таверна</b> · беженцы из тумана ждут найма. Жителей {0} из {1}. Новая доска через {2}. " +
                "Удача найма {3:0}: обычный {4:0}%, опытный {5:0}%, редкий {6:0.#}%, легендарный {7:0.#}% (растёт с уровнем и Духом работников).",
                st.Dwellers.Count, Formulas.DwellerCap(session.Balance, st.HearthLevel), Duration(session.NextBoardAt(now) - now),
                session.RecruitLuck(), odds[0] * 100, odds[1] * 100, odds[2] * 100, odds[3] * 100), Rich());
            var board = session.TavernBoard(now);
            for (int i = 0; i < board.Count; i++)
            {
                var r = board[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("<color={0}>{1}</color> · {2} · {3}", RarityColor(r.Rarity), r.Name,
                    session.RarityInfo(r.Rarity).Name, StatsLine(r.Stats)), Rich());
                if (r.Hired)
                    GUILayout.Label("нанят", GUILayout.Width(170));
                else if (GUILayout.Button("Нанять: " + CostText(session.HireCost(r)), GUILayout.Width(300)))
                {
                    var fail = session.Hire(i, now, out var hired);
                    Report(fail, "Coins");
                    if (fail == Fail.None)
                        message = hired.Name + " теперь живёт в крепости";
                    else if (fail == Fail.LimitReached)
                        message = "Крепость полна: поднимите очаг или отпустите жителя";
                }
                GUILayout.EndHorizontal();
            }
            if (session.PopulationFull())
            {
                // the weakest idle dweller can be let go to make room
                var weakest = st.Dwellers.Where(d => session.CanDismiss(d) == Fail.None).OrderBy(d => d.Stats.Sum()).FirstOrDefault();
                if (weakest != null && GUILayout.Button("Отпустить в туман: " + weakest.Name + " (" + weakest.Stats.Sum() + ")"))
                {
                    Report(session.Dismiss(weakest.Id, now));
                    message = weakest.Name + " ушёл в туман искать свою дорогу";
                }
            }
        }

        // ------------------------------------------------------------------ infirmary

        void DrawInfirmary(long now)
        {
            double speed = session.HealSpeed();
            GUILayout.Label(string.Format("<b>Лазарет</b> · лечение ×{0:0.00}: ранение заживает за {1}, туманная хворь — за {2}. " +
                "Быстрее с уровнем лазарета, Умом лекарей, исследованиями «Быта» и «Пылающим» очагом.",
                speed, Duration((long)(session.Balance.Halls.Infirmary.InjuryHours * 3600 / speed)),
                Duration((long)(session.Balance.Halls.Infirmary.IllnessHours * 3600 / speed))), Rich());
            var patients = session.Patients(now);
            if (patients.Count == 0)
                GUILayout.Label("Больных и раненых нет.");
            foreach (var d in patients)
            {
                string hurt = d.InjuredUntil > now ? "ранен, ещё " + Duration(session.HealingLeft(d.InjuredUntil, now)) : "";
                string sick = session.IsSick(d.Id, now) ? "хворь, ещё " + Duration(session.HealingLeft(session.SickUntil(d.Id), now)) + " (работает вполсилы)" : "";
                GUILayout.Label(d.Name + ": " + string.Join("; ", new[] { hurt, sick }.Where(x => x.Length > 0)));
            }
        }

        // ------------------------------------------------------------------ library

        void DrawLibrary(long now)
        {
            var lib = session.Balance.Halls.Library;
            var current = session.CurrentResearch;
            GUILayout.Label(string.Format("<b>Библиотека</b> · изучено {0} из {1}. Одно исследование за раз; Ум работников ускоряет.",
                session.ResearchCount, lib.Nodes.Count), Rich());
            if (current != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Сейчас: «{0}» — {1}, осталось {2}", current.Name, ResearchText(current), Duration(Math.Max(0, session.ResearchEnd - now))));
                long price = session.ResearchSpeedupCost(now);
                if (GUILayout.Button(price == 0 ? "Завершить бесплатно" : "Завершить: " + price + " искр", GUILayout.Width(260)))
                    Report(session.SpeedUpResearch(now), "Done");
                GUILayout.EndHorizontal();
            }
            researchBranch = Math.Max(0, Math.Min(researchBranch, lib.Branches.Count - 1));
            GUILayout.BeginHorizontal();
            for (int b = 0; b < lib.Branches.Count; b++)
            {
                if (GUILayout.Toggle(researchBranch == b, lib.Branches[b].Name, "Button") && researchBranch != b)
                    researchBranch = b;
            }
            GUILayout.EndHorizontal();
            if (lib.Branches.Count == 0)
                return;
            var branch = lib.Branches[researchBranch];
            foreach (var n in lib.Nodes.Where(x => x.Branch == branch.Id).OrderBy(x => x.Tier))
            {
                GUILayout.BeginHorizontal();
                bool done = session.Researched(n.Id);
                GUILayout.Label(string.Format("{0} {1}. {2} — {3}", done ? "✓" : "·", n.Tier, n.Name, ResearchText(n)));
                var can = session.CanResearch(n);
                if (!done && can == Fail.None &&
                    GUILayout.Button("Изучить: " + CostText(session.ResearchCost(n)) + ", " + Duration(session.ResearchSeconds(n)), GUILayout.Width(420)))
                    Report(session.StartResearch(n.Id, now), "Build");
                else if (!done && can == Fail.Locked && session.MaxRoomLevel(RoomType.Library) < n.Gate)
                    GUILayout.Label("библиотека " + n.Gate, GUILayout.Width(170));
                GUILayout.EndHorizontal();
            }
        }

        static string ResearchText(ResearchNodeData n)
        {
            string p = (n.Value * 100).ToString("0.#");
            switch (n.Effect)
            {
                case ResearchEffect.Supply: return "выработка угля и еды +" + p + "%";
                case ResearchEffect.Materials: return "выработка железа и серебра +" + p + "%";
                case ResearchEffect.Attack: return "атака отряда +" + p + "%";
                case ResearchEffect.Health: return "здоровье отряда +" + p + "%";
                case ResearchEffect.CoalBurn: return "расход угля −" + p + "%";
                case ResearchEffect.Food: return "жители едят на " + p + "% меньше";
                case ResearchEffect.Healing: return "лечение +" + p + "%";
                case ResearchEffect.Training: return "тренировка +" + p + "%";
                case ResearchEffect.ResearchSpeed: return "исследования +" + p + "%";
                case ResearchEffect.RecruitLuck: return "удача найма +" + n.Value.ToString("0");
                case ResearchEffect.SurgeDefense: return "защитники Наплыва +" + p + "%";
                default: return "чтение записей +" + p + "%";
            }
        }

        // ------------------------------------------------------------------ training hall

        void DrawTraining(Room room, long now)
        {
            GUILayout.Label(string.Format("<b>Тренировочный зал</b> · житель или герой растит выбранную характеристику по очку. " +
                "Мест {0}, натренировано очков: {1}.", session.WorkerPlaces(room), session.PointsTrained), Rich());
            foreach (var slot in session.Trainings.ToList())
            {
                var d = session.FindDweller(slot.DwellerId);
                if (d != null)
                    GUILayout.Label(string.Format("{0}: {1} {2} → {3}, ещё {4}", d.Name, StatName(slot.Stat), d.StatOf(slot.Stat),
                        d.StatOf(slot.Stat) + 1, Duration(Math.Max(0, slot.End - now))));
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Кто:", GUILayout.Width(50));
            foreach (var d in session.State.Dwellers.Where(x => x.RouteId == 0 && session.TrainingOf(x.Id) == null && session.PairingOf(x.Id) == null)
                         .OrderByDescending(x => x.RoomId == room.Id).ThenByDescending(x => x.IsHero).ThenByDescending(x => x.Stats.Sum()).Take(6))
            {
                if (GUILayout.Toggle(traineeId == d.Id, d.Name + (d.IsHero ? " (герой)" : ""), "Button") && traineeId != d.Id)
                    traineeId = d.Id;
            }
            GUILayout.EndHorizontal();
            var who = session.FindDweller(traineeId);
            if (who == null || session.TrainingOf(who.Id) != null)
                return;
            GUILayout.Label(string.Format("{0} ({1}): предел {2}", who.Name, session.RarityInfo(session.RarityOf(who.Id)).Name, session.TrainingCap(who)));
            GUILayout.BeginHorizontal();
            foreach (Stat s in Enum.GetValues(typeof(Stat)))
            {
                if (who.StatOf(s) >= session.TrainingCap(who))
                {
                    GUILayout.Label(StatName(s) + " " + who.StatOf(s) + " (предел)", GUILayout.Width(170));
                    continue;
                }
                if (GUILayout.Button(string.Format("{0} {1}+1\n{2}, {3}", StatName(s), who.StatOf(s), CostText(session.TrainingCost(who, s)),
                        Duration(session.TrainingSeconds(who, s)))))
                    Report(session.StartTraining(who.Id, s, now), "Build");
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ watchtower

        void DrawWatchtower(long now)
        {
            var r = session.Scout(now);
            GUILayout.Label(string.Format("<b>Сторожевая башня</b>, уровень {0} · дозорные: защитники Наплыва +{1:0.#}%.",
                r.TowerLevel, session.WatchDefenseBonus() * 100), Rich());
            if (r.Waves > 0)
                GUILayout.Label(string.Format(r.Surge ? "Наплыв сегодня: через {0}, {1} волны, около {2} тварей" : "Сегодняшний Наплыв позади; следующий через {0}, {1} волны, около {2} тварей",
                    Duration(Math.Max(0, r.SurgeAt - now)), r.Waves, r.Creatures));
            if (r.SpawnFloors.Count > 0)
                GUILayout.Label("Твари выйдут на этажах: " + string.Join(", ", r.SpawnFloors.Select(f => (f + 1).ToString())) + " — утеплите эти залы или поставьте туда защитников");
            if (r.Forecast >= 0)
                GUILayout.Label("Прогноз обороны без вас: " + StarText(r.Forecast));
            if (r.Dens.Count > 0)
                GUILayout.Label("Логова Охоты на завтра: " + string.Join(", ", r.Dens.Select(f => session.Combat?.Family(f)?.Name ?? f.ToString())));
            if (r.RouteForks.Count > 0)
                GUILayout.Label("Следующая вылазка проверит: " + string.Join(" и ", r.RouteForks.Select(x => StatName(x.stat) + " " + x.need)));
            foreach (var s in session.Balance.Halls.Watchtower.Scout.Where(x => x.Level > r.TowerLevel).Take(1))
                GUILayout.Label(string.Format("С {0}-го уровня башня покажет больше.", s.Level));
        }

        // ------------------------------------------------------------------ chapel

        void DrawChapel(long now)
        {
            var gift = session.TodayGift(now);
            long amount = session.ChapelGiftAmount(gift);
            string what = gift == null ? "" : gift.Goods == GiftGoods.Essence ? amount + " эссенции" : gift.Goods == GiftGoods.Treats ? amount + " лакомств" : "песочные часы " + Duration(amount);
            GUILayout.Label("<b>Часовня Угля</b> · возвышение героев (+" + (session.Balance.Halls.Chapel.Bonus * 100).ToString("0") +
                "% к атаке и здоровью за ранг) и дар дня.", Rich());
            GUI.enabled = session.GiftReady(now);
            if (GUILayout.Button(session.GiftReady(now) ? "Забрать дар дня: " + what : "Дар дня получен, завтра новый"))
            {
                Report(session.ClaimGift(now, out _, out _), "Coins");
                message = "Дар Угля: " + what;
            }
            GUI.enabled = true;
            foreach (var h in session.Squad())
            {
                var next = session.NextAscension(h);
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("{0}, ур.{1}: ранг {2}", h.Name, h.HeroLevel, session.AscensionRank(h.Id)));
                if (next == null)
                    GUILayout.Label("высший ранг", GUILayout.Width(220));
                else if (h.HeroLevel < next.HeroLevel || session.MaxRoomLevel(RoomType.Chapel) < next.Chapel)
                    GUILayout.Label(string.Format("нужен ур.{0}, часовня {1}", next.HeroLevel, next.Chapel), GUILayout.Width(260));
                else if (GUILayout.Button("Возвысить: " + session.AscensionEssence(next) + " эсс.", GUILayout.Width(260)))
                    Report(session.Ascend(h.Id, now), "Done");
                GUILayout.EndHorizontal();
            }
        }

        // ------------------------------------------------------------------ archive

        void DrawArchive(long now)
        {
            var records = session.Records;
            var reading = session.CurrentReading;
            GUILayout.Label(string.Format("<b>Архив Стражей</b> · прочитано записей {0} из {1}. Записи находятся игрой; прочитанная даёт {2} эссенции.",
                records.Count(r => session.RecordRead(r.Id)), records.Count, session.RecordEssence()), Rich());
            if (reading != null)
                GUILayout.Label(string.Format("Читаем «{0}», ещё {1}", reading.Title, Duration(Math.Max(0, session.ReadingEnd - now))));
            foreach (var r in records)
            {
                bool found = session.RecordFound(r), read = session.RecordRead(r.Id);
                if (!found)
                    continue;
                GUILayout.BeginHorizontal();
                if (read)
                {
                    if (GUILayout.Button((openRecord == r.Id ? "▼ " : "▶ ") + r.Title))
                        openRecord = openRecord == r.Id ? "" : r.Id;
                }
                else
                {
                    GUILayout.Label("· " + r.Title);
                    if (reading == null && GUILayout.Button("Прочесть: " + Duration(session.ReadSeconds(r)), GUILayout.Width(220)))
                        Report(session.StartReading(r.Id, now), "Click");
                }
                GUILayout.EndHorizontal();
                if (read && openRecord == r.Id)
                    GUILayout.Label("<i>" + r.Text + "</i>", Rich());
            }
            int hidden = records.Count(r => !session.RecordFound(r));
            if (hidden > 0)
                GUILayout.Label("Ещё не найдено записей: " + hidden + " — их приносят очаг, кампания, Охота, вылазки и исследования.");
        }
    }
}
