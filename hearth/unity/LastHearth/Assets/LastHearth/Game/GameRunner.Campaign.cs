using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core;
using LastHearth.Core.Combat;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// The campaign window (MODES.md, 1): the act's story, the card of the next battle with its recommended power
    /// against the squad's (and the lock below 80%), its kind and stars, the act's secret, the hard mode of cleared
    /// acts; the battle screen's objective line and the defeat hints; the admin's «Кампания» page.
    /// </summary>
    public sealed partial class GameRunner
    {
        /// <summary>The act the window shows (0: the current one); a tap on an act of the map picks it.</summary>
        int campaignAct;
        bool campaignHard;
        /// <summary>What the last campaign battle left to read under its result: hints, the story record, the act's end.</summary>
        readonly List<string> campaignNotes = new List<string>();

        static readonly Color StoryText = new Color(0.95f, 0.9f, 0.78f, 1f);

        void DrawCampaign(long now)
        {
            var c = session.Balance.Campaign;
            int next = session.MissionsCleared;
            int current = Math.Min(session.CurrentAct, c.Acts.Count);
            int shown = campaignAct >= 1 && campaignAct <= current ? campaignAct : current;
            bool anyHard = Enumerable.Range(1, c.Acts.Count).Any(session.HardOpen);
            if (!anyHard)
                campaignHard = false;

            // the header: the act, the progress, the two modes
            var head = Row(40);
            Txt(new Rect(head.x, head.y, head.width * 0.6f, 40), string.Format("Акт {0}: «{1}» · пройдено {2}/{3}", shown,
                session.Act(shown).Name, next, c.Missions.Count), 17, GoldText, TextAnchor.MiddleLeft);
            float bw = Mathf.Min(150, head.width * 0.19f);
            if (Plank(new Rect(head.xMax - 2 * bw - 6, head.y + 2, bw, 36), "Обычный", !campaignHard, true, 14, "campNormal"))
                campaignHard = false;
            if (Plank(new Rect(head.xMax - bw, head.y + 2, bw, 36), anyHard ? "Сложный" : "Сложный (закрыт)", campaignHard, anyHard, 14, "campHard"))
                campaignHard = true;
            if (!anyHard)
                GUILayout.Label("Сложный режим откроется, когда будет пройден первый акт: 6 боёв ×1,6 силы, 3 попытки в день, вещи на ступень реже.");

            // the act selector: the passed acts and the current one
            if (current > 1)
            {
                var acts = Row(34);
                float aw = Mathf.Min(64, (acts.width - 4 * (current - 1)) / current);
                for (int a = 1; a <= current; a++)
                {
                    if (Plank(new Rect(acts.x + (a - 1) * (aw + 4), acts.y, aw, 32), a.ToString(), a == shown, true, 14, "campAct" + a))
                        campaignAct = a;
                }
            }

            DrawStory(shown);
            GUILayout.Space(6);
            if (campaignHard)
                DrawHard(shown, now);
            else
                DrawNormal(shown, next, now);
        }

        /// <summary>The act's story: its opening, and its end once the boss has fallen.</summary>
        void DrawStory(int act)
        {
            var a = session.Act(act);
            bool done = session.ActCleared(act);
            string text = a.Intro + (done && a.Outro != "" ? "\n\n" + a.Outro : "");
            if (text == "")
                return;
            float h = Mathf.Max(70, 28 + 18 * (float)Math.Ceiling(text.Length * 7.5f / Mathf.Max(200, viewWidth - 40)));
            var r = Row(h);
            Paper(r);
            Txt(new Rect(r.x + 16, r.y + 8, r.width - 32, h - 16), text, 13, StoryText, TextAnchor.UpperLeft, true);
        }

        void DrawNormal(int act, int next, long now)
        {
            var c = session.Balance.Campaign;
            if (next < c.Missions.Count && session.Mission(next).Act == act)
                MissionCard(next, false, now);
            else if (next >= c.Missions.Count)
                GUILayout.Label("Кампания пройдена! Сложный режим и тайники ждут.");
            else
                GUILayout.Label(session.ActCleared(act) ? "Акт пройден: можно переиграть бои за звёзды или пойти в сложный режим." : "");
            DrawSecret(act, now);
            if (next == 0)
                return;

            // the act's cleared battles: replays for stars
            int from = Math.Max(session.ActStart(act), 0), to = Math.Min(session.ActEnd(act), next);
            var stars = session.State.CampaignStars;
            var list = Enumerable.Range(from, Math.Max(0, to - from)).Reverse().Take(6).ToList();
            if (list.Count > 0)
            {
                GUILayout.Label("Пройденные бои акта (переиграть за звёзды):");
                var row = Row(40);
                float w = Mathf.Min(140, (row.width - 6 * 5) / 6);
                for (int k = 0; k < list.Count; k++)
                {
                    int i = list[k];
                    if (Plank(new Rect(row.x + k * (w + 6), row.y, w, 38), string.Format("{0} {1}", i + 1, StarText(stars[i])), false, true, 12, "replay" + i))
                        StartMissionBattle(i, now);
                }
            }
            int last = next - 1;
            var rep = Row(40);
            if (Plank(new Rect(rep.x, rep.y, Mathf.Min(420, rep.width), 38), string.Format("Повторить бой {0} мгновенно ({1}/{2} на сегодня)", last + 1,
                    session.RepeatsLeft(now), c.RepeatsPerDay), false, session.RepeatsLeft(now) > 0, 13, "repeat"))
            {
                var fail = session.RepeatMission(last, now, out var reward);
                Report(fail);
                if (fail == Fail.None)
                    message = reward.Stars > 0 ? "Повтор: " + CostText(reward.Got) : "Повтор проигран: отряд не справился автобоем";
            }
        }

        static Color KindColor(MissionKind k)
        {
            switch (k)
            {
                case MissionKind.Elite: return new Color(0.75f, 0.5f, 1f, 1);
                case MissionKind.Boss: return new Color(1f, 0.35f, 0.25f, 1);
                case MissionKind.Defense: return new Color(0.45f, 0.75f, 1f, 1);
                case MissionKind.Escort: return new Color(0.5f, 0.9f, 0.55f, 1);
                case MissionKind.Secret: return new Color(1f, 0.8f, 0.3f, 1);
                default: return new Color(0.85f, 0.8f, 0.7f, 1);
            }
        }

        static string KindGlyph(MissionKind k)
        {
            switch (k)
            {
                case MissionKind.Elite: return "Э";
                case MissionKind.Boss: return "Б";
                case MissionKind.Defense: return "З";
                case MissionKind.Escort: return "С";
                case MissionKind.Secret: return "Т";
                default: return "В";
            }
        }

        static string KindGoal(MissionKind k)
        {
            switch (k)
            {
                case MissionKind.Defense: return "удержите точку 60 секунд";
                case MissionKind.Escort: return "беженцы должны дойти живыми";
                case MissionKind.Elite: return "один сильный враг с тяжёлыми ударами";
                case MissionKind.Boss: return "босс акта: новый герой и 50 искр";
                case MissionKind.Secret: return "тайный бой: сюжетная запись";
                default: return "три волны врагов";
            }
        }

        /// <summary>The kind's badge: a coloured round plaque with its letter.</summary>
        void KindBadge(Rect r, MissionKind k)
        {
            var old = GUI.color;
            GUI.color = KindColor(k);
            ArtLibrary.Draw(r, "UI/Badge");
            GUI.color = old;
            Txt(r, KindGlyph(k), 16, CreamText);
        }

        /// <summary>The power line of a card: recommended against ours, in the colour of the chances.</summary>
        void PowerLine(Rect r, int recommended)
        {
            double ours = session.SquadPower();
            double share = session.PowerShare(recommended);
            Color color = share >= session.AutoShare ? GoodText : share >= 1 ? EvenText : share >= session.LockShare ? GoldText : BadText;
            string note = share >= session.AutoShare ? " · пройдёт автобой" : share >= 1 ? " · с умом" : share >= session.LockShare ? " · трудно" :
                session.PowerGateOn ? " · закрыт" : " · очень трудно";
            Txt(r, string.Format("Сила: рекомендуемая {0} · ваша {1}{2}", ShortNum(recommended), ShortNum(ours), note), 13, color, TextAnchor.MiddleLeft);
        }

        /// <summary>The card of one battle: kind, enemies, power, rewards and the button (or the lock and what to improve).</summary>
        void MissionCard(int index, bool hard, long now)
        {
            var m = session.Mission(index);
            int rec = session.RecommendedPower(index, hard);
            bool locked = session.TooWeak(rec) && (hard || index == session.MissionsCleared);
            var r = Row(118);
            Card(r);
            KindBadge(new Rect(r.x + 10, r.y + 10, 44, 44), m.Kind);
            float x = r.x + 64, w = r.width - 64 - 170;
            int stars = hard ? session.HardStars(index) : index < session.MissionsCleared ? session.State.CampaignStars[index] : 0;
            Txt(new Rect(x, r.y + 6, w, 22), string.Format("Бой {0}{1} · {2} · {3}", index + 1, hard ? " (сложный)" : "",
                GameSession.KindTitle(m.Kind), StarText(stars)), 15, KindColor(m.Kind), TextAnchor.MiddleLeft);
            Txt(new Rect(x, r.y + 28, w, 18), string.Format("{0}, ранг {1} — {2}", combat.Family(m.Family).Name, m.Rank, KindGoal(m.Kind)), 12, DimText, TextAnchor.MiddleLeft);
            PowerLine(new Rect(x, r.y + 48, w, 20), rec);
            var h = session.Balance.Campaign.Hard;
            long essence = hard ? (long)Math.Round(m.Essence * (stars == 0 ? h.FirstEssence : h.RepeatShare)) : m.Essence;
            long materials = hard ? (long)Math.Round(m.Materials * (stars == 0 ? h.FirstMaterials : h.RepeatShare)) : m.Materials;
            long sparks = hard ? (stars == 0 ? h.FirstSparks : 0) : m.Sparks;
            float iw = Mathf.Min(120, w / 3);
            IconAmount(new Rect(x, r.y + 74, iw, 22), "Icons/Essence", ShortNum(essence), CreamText, 12);
            IconAmount(new Rect(x + iw, r.y + 74, iw, 22), "Icons/Silver", ShortNum(materials), CreamText, 12);
            if (sparks > 0)
                IconAmount(new Rect(x + 2 * iw, r.y + 74, iw, 22), "Icons/Sparks", sparks.ToString(), CreamText, 12);
            var go = new Rect(r.xMax - 160, r.y + 30, 150, 52);
            if (locked)
            {
                Plank(go, "Замок: нужно " + ShortNum(rec * session.LockShare), false, false, 12, "locked" + index);
                foreach (string hint in session.DefeatHints(null, rec).Skip(1).Take(3))
                    GUILayout.Label("   • " + hint);
            }
            else if (Plank(go, "Бой!", true, true, 17, (hard ? "hardGo" : "go") + index))
            {
                if (hard)
                    StartHardBattle(index, now);
                else
                    StartMissionBattle(index, now);
            }
        }

        /// <summary>The act's secret battle: its hint while hidden, then its button.</summary>
        void DrawSecret(int act, long now)
        {
            var s = session.Secret(act);
            if (s == null)
                return;
            if (session.SecretFound(act))
            {
                var r = Row(48);
                Card(r);
                KindBadge(new Rect(r.x + 8, r.y + 6, 36, 36), MissionKind.Secret);
                string state = session.SecretCleared(act) ? "найден · " + StarText(session.SecretStars(act)) : "найден! Награда — сюжетная запись";
                Txt(new Rect(r.x + 52, r.y + 4, r.width - 230, 20), "Тайник: «" + s.Title + "» — " + state, 13, GoldText, TextAnchor.MiddleLeft);
                PowerLine(new Rect(r.x + 52, r.y + 24, r.width - 230, 20), session.SecretRecommended(act));
                if (Plank(new Rect(r.xMax - 168, r.y + 6, 160, 36), "В тайник", !session.SecretCleared(act), true, 14, "secret" + act))
                {
                    var fail = session.StartSecretMission(act, now, out var started);
                    Report(fail);
                    if (fail == Fail.None)
                        BeginBattle(started, done => FinishCampaignBattle(done, "Тайник"), "Тайник «" + s.Title + "»");
                }
            }
            else
            {
                GUILayout.Label("<color=#fd8>Тайник акта:</color> " + session.SecretHint(act), Rich());
            }
        }

        void DrawHard(int act, long now)
        {
            if (!session.HardOpen(act))
            {
                GUILayout.Label("Сложный режим этого акта откроется, когда будет пройден его босс.");
                return;
            }
            var h = session.Balance.Campaign.Hard;
            GUILayout.Label(string.Format("<b>Сложный режим</b> · враги ×{0:0.#} · попыток сегодня {1}/{2} · первая победа: ×{3:0.#} эссенции, {4} искр и редкая или эпическая вещь",
                h.Power, session.HardAttemptsLeft(now), h.AttemptsPerDay, h.FirstEssence, h.FirstSparks), Rich());
            foreach (int index in session.HardMissions(act))
                MissionCard(index, true, now);
        }

        void StartHardBattle(int index, long now)
        {
            var fail = session.StartHardMission(index, now, out var started);
            Report(fail);
            if (fail == Fail.None)
                BeginBattle(started, done => FinishCampaignBattle(done, ""), "Сложный бой " + (index + 1));
        }

        void StartMissionBattle(int index, long now)
        {
            var fail = session.StartMission(index, now, out var started);
            Report(fail);
            if (fail == Fail.TooWeak)
                message = "Отряд слишком слаб: нужно " + ShortNum(session.RecommendedPower(index) * session.LockShare) + " силы, у вас " + ShortNum(session.SquadPower());
            if (fail != Fail.None)
                return;
            BeginBattle(started, done => FinishCampaignBattle(done, ""), "Бой " + (index + 1));
        }

        /// <summary>The result line of any campaign battle; the hints, the record and the act's end go under it.</summary>
        string FinishCampaignBattle(Battle done, string what)
        {
            session.FinishMission(done, out var reward);
            campaignNotes.Clear();
            string text = (done.Won ? "Победа " : "Поражение ") + StarText(reward.Stars);
            if (reward.FirstClear)
                text += " · первая победа: " + CostText(reward.Got);
            else if (done.Won && reward.Got[ResourceType.Essence] + reward.Got[ResourceType.Silver] > 0)
                text += " · " + CostText(reward.Got);
            if (reward.NewHero != null)
                text += " · к отряду примкнул " + reward.NewHero.Name;
            if (!string.IsNullOrEmpty(reward.Lore))
                campaignNotes.Add("Сюжетная запись: " + reward.Lore);
            if (!string.IsNullOrEmpty(reward.Outro))
                campaignNotes.Add("Конец акта: " + reward.Outro);
            if (!done.Won)
            {
                campaignNotes.Add("Что улучшить:");
                campaignNotes.AddRange(reward.Hints.Take(5).Select(h => "• " + h));
            }
            return text;
        }

        // ------------------------------------------------------------------ the battle screen (GameRunner.cs DrawBattle)

        /// <summary>Hold and escort: the time to stand and the refugees' health.</summary>
        void DrawObjective()
        {
            if (battle.Objective == BattleObjective.Hold)
                GUILayout.Label(string.Format("<color=#8cf>Оборона:</color> держитесь {0:0} с из {1:0} {2}", battle.Tick * combat.TickSeconds,
                    battle.HoldTicks * combat.TickSeconds, Bar(battle.HoldProgress)), Rich());
            else if (battle.Objective == BattleObjective.Escort)
                GUILayout.Label(string.Format("<color=#8f8>Беженцы:</color> {0:0} / {1:0} {2} · путь {3:0}%", Math.Max(0, battle.EscortHp), battle.EscortMaxHp,
                    Bar(battle.EscortMaxHp <= 0 ? 0 : battle.EscortHp / battle.EscortMaxHp), 100 * battle.HoldProgress), Rich());
        }

        /// <summary>Under the result of a campaign battle: what to improve, the story record, the act's end.</summary>
        void DrawCampaignNotes()
        {
            foreach (string note in campaignNotes)
                GUILayout.Label(note, Rich());
        }

        // ------------------------------------------------------------------ admin

        void AdminCampaign(AdminList list, long now)
        {
            var c = session.Balance.Campaign;
            int next = session.MissionsCleared;
            list.Row(string.Format("Поход: пройдено {0} из {1}, акт {2} · замок силы {3}", next, c.Missions.Count, Math.Min(session.CurrentAct, c.Acts.Count),
                session.PowerGateOn ? "включён" : "выключен"));
            list.Add(session.PowerGateOn ? "Выключить замок силы" : "Включить замок силы", _ =>
            {
                session.AdminSetPowerGate(!session.PowerGateOn);
                return "Замок силы " + (session.PowerGateOn ? "включён" : "выключен");
            });
            list.Add("−1 бой", _ => "Пройдено миссий: " + session.AdminSetCampaignProgress(session.MissionsCleared - 1));
            list.Add("+1 бой", _ => "Пройдено миссий: " + session.AdminSetCampaignProgress(session.MissionsCleared + 1));
            list.Row("Перейти к началу акта");
            for (int a = 1; a <= c.Acts.Count; a++)
            {
                int act = a;
                list.Add(act.ToString(), _ =>
                {
                    session.AdminSetCampaignProgress(session.ActStart(act));
                    campaignAct = act;
                    return string.Format("Акт {0} «{1}»: пройдено {2}", act, session.Act(act).Name, session.MissionsCleared);
                });
            }
            list.Row(string.Format("Сложный режим: {0} · попыток {1}/{2}", session.AdminHardForAll ? "открыт для всех актов" : "по пройденным актам",
                session.HardAttemptsLeft(now), c.Hard.AttemptsPerDay));
            list.Add(session.AdminHardForAll ? "Закрыть для непройденных" : "Открыть сложный режим", _ =>
            {
                session.AdminSetHardForAll(!session.AdminHardForAll);
                return session.AdminHardForAll ? "Сложный режим открыт во всех актах" : "Сложный режим — только в пройденных актах";
            });
            list.Add("Вернуть попытки", t => "Попыток сложного режима: " + session.AdminRefillHard(t));
            list.Row("Тайники: " + (session.AdminSecretsForAll ? "все найдены" : "по условиям"));
            list.Add(session.AdminSecretsForAll ? "Прятать по условиям" : "Найти все тайники", _ =>
            {
                session.AdminSetSecretsForAll(!session.AdminSecretsForAll);
                return session.AdminSecretsForAll ? "Все тайники найдены" : "Тайники снова по условиям";
            });
        }
    }
}
