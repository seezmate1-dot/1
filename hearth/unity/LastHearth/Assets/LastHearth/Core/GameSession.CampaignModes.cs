using System;
using System.Collections.Generic;
using System.Linq;
using LastHearth.Core.Combat;

namespace LastHearth.Core
{
    /// <summary>MODES.md, 1: the recommended power of a battle and the lock below it.</summary>
    public sealed class CampaignGateData
    {
        /// <summary>The free player's squad against the reference squad of ReferencePower (calibrated on the 30-day bot).</summary>
        public double ReferenceScale = 1;
        /// <summary>The recommended power: the free player's squad at the mission's rank, times this.</summary>
        public double RecommendedShare = 0.95;
        /// <summary>A first attempt is locked while the squad is below this share of the recommended power.</summary>
        public double LockBelow = 0.8;
        /// <summary>From this share on the battle is won by the auto-battle (the card says so).</summary>
        public double AutoAt = 1.2;
    }

    /// <summary>MODES.md, 1: the hard mode of a cleared act — six of its battles, stronger, three tries a day.</summary>
    public sealed class HardModeData
    {
        /// <summary>The battles of an act that have a hard version: numbers inside the act (1-19).</summary>
        public int[] Slots = new int[0];
        /// <summary>The enemies' health and attack, so the recommended power too.</summary>
        public double Power = 1.6;
        public int AttemptsPerDay = 3;
        /// <summary>The first hard win: this share of the battle's first-clear essence and materials, plus Sparks.</summary>
        public double FirstEssence = 1.5;
        public double FirstMaterials = 1;
        public long FirstSparks = 5;
        /// <summary>Later hard wins: this share of the battle's first-clear essence and materials.</summary>
        public double RepeatShare = 0.5;
    }

    /// <summary>MODES.md, 1: the act's secret battle («тайник»), off the line, found by play; it brings a story record.</summary>
    public sealed class SecretData
    {
        public int Act;
        /// <summary>The battle of the act (1-19) that must be cleared first.</summary>
        public int After;
        /// <summary>And the play that finds it (the same counters as the Archive's records).</summary>
        public RecordKind Kind;
        public int Need;
        public MonsterFamily Family;
        public int Rank;
        public string Title = "";
        /// <summary>The hint shown while it is not found.</summary>
        public string Hint = "";
        /// <summary>The story record it brings.</summary>
        public string Lore = "";
        public long Essence;
        public long Materials;
    }

    public enum CampaignMode
    {
        Normal,
        Hard,
        Secret,
    }

    /// <summary>The saved state of the hard mode and the secret battles.</summary>
    public sealed class CampaignState
    {
        /// <summary>What State.PendingMission means: a mission (normal or hard) or an act's secret (act - 1).</summary>
        public CampaignMode PendingMode;
        /// <summary>Best stars of the hard battles, by mission index.</summary>
        public Dictionary<int, int> HardStars = new Dictionary<int, int>();
        public long HardDay = -1;
        public int HardUsed;
        /// <summary>Best stars of the secret battles, by act.</summary>
        public Dictionary<int, int> SecretStars = new Dictionary<int, int>();
        /// <summary>Admin: hard mode of every act, every secret found, the power gate off.</summary>
        public bool HardForAll;
        public bool SecretsForAll;
        public bool GateOff;
    }

    /// <summary>MODES.md, 1: the power gate, hard mode, secret battles, defeat hints and the acts' story.</summary>
    public sealed partial class GameSession
    {
        static readonly HeroClass[] ReferenceOrder =
        {
            HeroClass.Guardian, HeroClass.Swordsman, HeroClass.Cleaver, HeroClass.Archer, HeroClass.CoalPriest,
            HeroClass.AshMage, HeroClass.Swordsman, HeroClass.Archer, HeroClass.AshMage,
        };

        readonly Dictionary<int, double> referencePower = new Dictionary<int, double>();

        CampaignState Modes => State.CampaignModes ?? (State.CampaignModes = new CampaignState());
        CampaignGateData Gate => Balance.Campaign.Gate ?? new CampaignGateData();
        HardModeData Hard => Balance.Campaign.Hard ?? new HardModeData();

        // ------------------------------------------------------------------ the power gate

        /// <summary>
        /// The squad of a free player at this rank (hearth level), measured like HeroPower: five heroes at 2L and the
        /// rest three levels behind, main stats 5 + 2L, the forge's kit of hearth L, and the kennel and finds the
        /// campaign's enemies already count (KennelExpected, GearEdge).
        /// </summary>
        public double ReferencePower(int rank)
        {
            rank = Math.Max(1, rank);
            if (referencePower.TryGetValue(rank, out double cached))
                return cached;
            if (Combat == null)
                return 0;
            int size = 5 + Balance.SquadSlots.Count(gate => rank >= gate);
            double atk = KennelExpected(Balance.Companions.CampaignAttack, rank) * GearEdge(rank);
            double hp = KennelExpected(Balance.Companions.CampaignHp, rank) * GearEdge(rank);
            var kit = IssuedKit(ItemNumbers, rank);
            double sum = 0;
            for (int i = 0; i < size; i++)
            {
                int level = i < 5 ? 2 * rank : Math.Max(1, 2 * rank - 3);
                int main = 5 + 2 * rank;
                var f = Battle.HeroFighter(Combat, new HeroSetup
                {
                    Class = ReferenceOrder[i % ReferenceOrder.Length],
                    Level = level,
                    Stats = new[] { main, main, main, main, main },
                    Gear = kit,
                });
                double seconds = Math.Max(1, f.Interval) * Combat.TickSeconds;
                sum += Math.Round(f.Attack * atk / seconds * 4 + f.MaxHp * hp / 2);
            }
            return referencePower[rank] = sum;
        }

        /// <summary>The power a battle asks for (MODES.md, 1: the free player's squad on its day, x0.95; hard mode x1.6).</summary>
        public int RecommendedPower(int index, bool hard = false) =>
            RecommendedAt(Mission(index).Rank, hard ? Hard.Power : 1);

        public int SecretRecommended(int act)
        {
            var s = Secret(act);
            return s == null ? 0 : RecommendedAt(s.Rank, 1);
        }

        int RecommendedAt(int rank, double mult) => (int)(Math.Round(ReferencePower(rank) * Gate.ReferenceScale * Gate.RecommendedShare * mult / 10) * 10);

        public bool PowerGateOn => !Modes.GateOff;

        /// <summary>The squad against the recommended power: below LockBelow the first attempt is locked.</summary>
        public bool TooWeak(int recommended) => PowerGateOn && recommended > 0 && SquadPower() < recommended * Gate.LockBelow;

        /// <summary>The squad's power as a share of the recommended (the colour of the card).</summary>
        public double PowerShare(int recommended) => recommended <= 0 ? 1 : SquadPower() / recommended;

        public double AutoShare => Gate.AutoAt;
        public double LockShare => Gate.LockBelow;

        // ------------------------------------------------------------------ acts and the story

        public ActData Act(int act) => Balance.Campaign.Acts[Math.Max(0, Math.Min(Balance.Campaign.Acts.Count - 1, act - 1))];

        /// <summary>The first mission index of an act and the index after its last.</summary>
        public int ActStart(int act) => Balance.Campaign.Missions.FindIndex(m => m.Act == act);

        public int ActEnd(int act) => Balance.Campaign.Missions.FindLastIndex(m => m.Act == act) + 1;

        public bool ActCleared(int act) => ActEnd(act) > 0 && MissionsCleared >= ActEnd(act);

        /// <summary>The act the player is in (Acts.Count + 1 once the campaign is over).</summary>
        public int CurrentAct => MissionsCleared < Balance.Campaign.Missions.Count ? Mission(MissionsCleared).Act : Balance.Campaign.Acts.Count + 1;

        // ------------------------------------------------------------------ hard mode

        public bool HardOpen(int act) => Modes.HardForAll || ActCleared(act);

        /// <summary>The hard battles of an act: mission indices.</summary>
        public List<int> HardMissions(int act)
        {
            var list = new List<int>();
            int start = ActStart(act);
            if (start < 0)
                return list;
            foreach (int slot in Hard.Slots)
            {
                int i = start + slot - 1;
                if (i < ActEnd(act))
                    list.Add(i);
            }
            return list;
        }

        public int HardStars(int index) => Modes.HardStars.TryGetValue(index, out int s) ? s : 0;

        public int HardAttemptsLeft(long now)
        {
            var m = Modes;
            if (m.HardDay != LocalDay(now))
            {
                m.HardDay = LocalDay(now);
                m.HardUsed = 0;
            }
            return Hard.AttemptsPerDay - m.HardUsed;
        }

        public Fail StartHardMission(int index, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (index < 0 || index >= Balance.Campaign.Missions.Count)
                return Fail.Locked;
            int act = Mission(index).Act;
            if (!HardOpen(act) || !HardMissions(act).Contains(index))
                return Fail.Locked;
            var busy = CampaignBusy();
            if (busy != Fail.None)
                return busy;
            if (HardAttemptsLeft(now) <= 0)
                return Fail.NoTracks;
            if (TooWeak(RecommendedPower(index, true)))
                return Fail.TooWeak;
            Modes.HardUsed++;
            battle = BeginCampaignBattle(index, CampaignMode.Hard);
            return Fail.None;
        }

        void FinishHard(Battle battle, int index, int seed, MissionReward reward)
        {
            reward.Hard = true;
            if (!battle.Won)
                return;
            var m = Mission(index);
            var h = Hard;
            bool first = !Modes.HardStars.ContainsKey(index);
            reward.FirstClear = first;
            double share = first ? h.FirstEssence : h.RepeatShare;
            reward.Got[ResourceType.Essence] += (long)Math.Round(m.Essence * share);
            reward.Got.Add(ResourceBag.Materials((long)Math.Round(m.Materials * (first ? h.FirstMaterials : h.RepeatShare)), Balance.SilverShare));
            if (first)
                reward.Got[ResourceType.Sparks] += h.FirstSparks;
            // gear a step of rarity above the normal battles (MODES.md, 1)
            AddFind(reward.Items, Drop(first ? "hard" : "hardRepeat", m.Rank, seed, "Сложный бой " + (index + 1)));
            Modes.HardStars[index] = Math.Max(HardStars(index), battle.Stars);
            Count(DailyTask.Campaign);
            Pay(reward.Got);
        }

        // ------------------------------------------------------------------ the secret battles

        public SecretData Secret(int act) => Balance.Campaign.Secrets.Find(s => s.Act == act);

        /// <summary>How far the play is on the way to the act's secret (the Archive's counters).</summary>
        public int SecretProgress(int act)
        {
            var s = Secret(act);
            return s == null ? 0 : RecordProgress(s.Kind);
        }

        /// <summary>The act's battle After is cleared and the play that finds the secret is done.</summary>
        public bool SecretFound(int act)
        {
            var s = Secret(act);
            if (s == null)
                return false;
            if (Modes.SecretsForAll)
                return true;
            int start = ActStart(act);
            return start >= 0 && MissionsCleared >= start + s.After && RecordProgress(s.Kind) >= s.Need;
        }

        /// <summary>The hint of a secret not found yet, with the progress.</summary>
        public string SecretHint(int act)
        {
            var s = Secret(act);
            if (s == null)
                return "";
            int start = ActStart(act);
            string battle = MissionsCleared >= start + s.After ? "" : " Сначала пройдите бой " + s.After + " акта.";
            return s.Hint + string.Format(" ({0}: {1}/{2}).", RecordKindName(s.Kind), Math.Min(SecretProgress(act), s.Need), s.Need) + battle;
        }

        public int SecretStars(int act) => Modes.SecretStars.TryGetValue(act, out int s) ? s : 0;

        public bool SecretCleared(int act) => Modes.SecretStars.ContainsKey(act);

        public Fail StartSecretMission(int act, long now, out Battle battle)
        {
            battle = null;
            Advance(now);
            if (!SecretFound(act))
                return Fail.Locked;
            var busy = CampaignBusy();
            if (busy != Fail.None)
                return busy;
            if (!SecretCleared(act) && TooWeak(SecretRecommended(act)))
                return Fail.TooWeak;
            battle = BeginCampaignBattle(act - 1, CampaignMode.Secret);
            return Fail.None;
        }

        void FinishSecret(Battle battle, int act, int seed, MissionReward reward)
        {
            var s = Secret(act);
            if (!battle.Won || s == null)
                return;
            bool first = !SecretCleared(act);
            reward.FirstClear = first;
            if (first)
            {
                reward.Lore = s.Lore;
                reward.Got[ResourceType.Essence] += s.Essence;
                reward.Got.Add(ResourceBag.Materials(s.Materials, Balance.SilverShare));
                AddFind(reward.Items, Drop("secret", s.Rank, seed, "Тайник: " + s.Title));
            }
            Modes.SecretStars[act] = Math.Max(SecretStars(act), battle.Stars);
            Count(DailyTask.Campaign);
            Pay(reward.Got);
        }

        // ------------------------------------------------------------------ battles of every mode

        Fail CampaignBusy()
        {
            if (State.PendingMission >= 0 || State.Hunt != null)
                return Fail.HuntInProgress;
            if (Squad().Count == 0)
                return Fail.NoHeroes;
            return Fail.None;
        }

        Battle BeginCampaignBattle(int pending, CampaignMode mode)
        {
            State.PendingMission = pending;
            Modes.PendingMode = mode;
            State.PendingMissionSeed = State.NextBattleSeed++;
            return new Battle(Combat, PendingMissionSetup());
        }

        /// <summary>The battle of State.PendingMission in its mode (a mission, its hard version or an act's secret).</summary>
        public BattleSetup PendingMissionSetup()
        {
            int seed = State.PendingMissionSeed;
            switch (Modes.PendingMode)
            {
                case CampaignMode.Hard: return MissionSetup(State.PendingMission, seed, true);
                case CampaignMode.Secret: return SecretSetup(State.PendingMission + 1, seed);
                default: return MissionSetup(State.PendingMission, seed);
            }
        }

        public BattleSetup SecretSetup(int act, int seed)
        {
            var s = Secret(act);
            return CampaignSetup(MissionKind.Secret, s?.Family ?? MonsterFamily.Fiend, s?.Rank ?? 1, seed, 1);
        }

        /// <summary>A campaign battle of a kind: its waves and what wins it (combat.json "missionKinds").</summary>
        BattleSetup CampaignSetup(MissionKind kindOf, MonsterFamily family, int rank, int seed, double mult)
        {
            if (!Combat.MissionKinds.TryGetValue(kindOf.ToString(), out var kind))
                kind = Combat.MissionKinds[kindOf == MissionKind.Secret ? "Elite" : "Normal"];
            var setup = SquadSetup(seed);
            setup.Objective = kind.Objective;
            setup.HoldTicks = kind.HoldTicks;
            setup.EscortHp = kind.EscortHp;
            setup.EscortEvery = kind.EscortEvery;
            for (int w = 0; w < kind.Waves; w++)
            {
                setup.Waves.Add(new WaveSpec
                {
                    Family = family,
                    Rank = rank,
                    // the free player's kennel and finds are part of the recommended strength
                    HpMult = mult * kind.HpMult * KennelExpected(Balance.Companions.CampaignHp, rank) * GearEdge(rank),
                    AttackMult = mult * kind.AttackMult * KennelExpected(Balance.Companions.CampaignAttack, rank) * GearEdge(rank),
                    Telegraphs = kind.Telegraphs,
                });
            }
            return setup;
        }

        // ------------------------------------------------------------------ the defeat screen

        /// <summary>
        /// MODES.md, 1, «Что делать, если застрял»: what the lost battle lacked — power (and where to get it: levels,
        /// gear, companions, research, training), the squad (the class the enemies fear) or the skills (the strikes
        /// that landed, the refugees, the time).
        /// </summary>
        public List<string> DefeatHints(Battle b, int recommended)
        {
            var hints = new List<string>();
            var squad = Squad();
            double power = SquadPower();
            if (recommended > 0 && power < recommended)
                hints.Add(string.Format("Сила отряда {0:0} из рекомендуемых {1} (не хватает {2:0}%).", power, recommended, 100 * (1 - power / recommended)));
            if (b != null && b.EscortLost)
                hints.Add("Беженцы пали: каждый " + Math.Max(1, Combat.MissionKinds.TryGetValue("Escort", out var e) ? e.EscortEvery : 3) +
                          "-й удар врага идёт по ним. «Стена» Стража прикрывает и их, а быстрые волны — меньше ударов.");
            else if (b != null && !b.Won && b.Objective == BattleObjective.Hold && !b.Heroes.Exists(h => h.Alive))
                hints.Add("Оборона: точку держит живучесть — Страж со «Стеной» и Угольный жрец, лечение вещей.");
            else if (b != null && !b.Won && b.Heroes.Exists(h => h.Alive) && b.Tick >= Combat.MaxTicks)
                hints.Add("Время вышло: не хватило урона. Стрелок и Пепельный маг, Мечник после «Стены» и оглушения бьют сильнее.");
            if (b != null && b.Family != null && !squad.Exists(d => d.HeroClass == b.Family.Weak))
                hints.Add(string.Format("Состав: «{0}» уязвимы к классу «{1}» — возьмите его в отряд.", b.Family.Name, ClassTitle(b.Family.Weak)));
            if (b != null && b.HeavyLanded > 0)
                hints.Add(string.Format("Умения: сильные удары попали {0} раз. Жмите «Стену», когда враг готовит удар, а от удара по передней линии — «Отход».", b.HeavyLanded));
            int cap = Formulas.HeroLevelCap(Balance, State.HearthLevel);
            int below = squad.Count(d => d.HeroLevel < cap);
            if (below > 0)
                hints.Add(string.Format("Уровни: героев ниже предела ({0}) — {1}. Эссенция — в Наплыве, кампании и повторах.", cap, below));
            else if (squad.Count > 0)
                hints.Add("Уровни: все герои на пределе — поднимите очаг, он открывает следующие уровни.");
            if (squad.Count < SquadSize())
                hints.Add(string.Format("Отряд: занято {0} из {1} мест — наймите героев в таверне.", squad.Count, SquadSize()));
            int issued = squad.Sum(d => Slots.Count(s => Worn(d.Id, s) == null));
            int bag = Bag().Count;
            if (issued > 0)
                hints.Add(string.Format("Снаряжение: {0} мест отряда в казённых вещах — наденьте находки{1} или выкуйте новые в кузне.",
                    issued, bag > 0 ? " (в сумке " + bag + ")" : ""));
            else
                hints.Add("Снаряжение: улучшайте вещи в кузне (+1…+10) и собирайте комплекты.");
            if (Companions.Unlocked.Count > 0)
            {
                int without = squad.Count(d => CompanionSetupFor(d) == null);
                if (without > 0)
                    hints.Add(string.Format("Спутники: у {0} героев нет спутника — приставьте их в псарне.", without));
            }
            if (MaxRoomLevel(RoomType.Library) >= 1)
                hints.Add("Исследования: библиотека усиливает весь отряд (атака, здоровье, умения).");
            if (MaxRoomLevel(RoomType.TrainingHall) >= 1)
                hints.Add("Тренировка: зал поднимает основные свойства героев.");
            return hints;
        }

        public static string ClassTitle(HeroClass c)
        {
            switch (c)
            {
                case HeroClass.Guardian: return "Страж";
                case HeroClass.Swordsman: return "Мечник";
                case HeroClass.Cleaver: return "Секач";
                case HeroClass.Archer: return "Стрелок";
                case HeroClass.CoalPriest: return "Угольный жрец";
                case HeroClass.AshMage: return "Пепельный маг";
                default: return "житель";
            }
        }

        public static string KindTitle(MissionKind k)
        {
            switch (k)
            {
                case MissionKind.Elite: return "элита";
                case MissionKind.Boss: return "босс";
                case MissionKind.Defense: return "оборона";
                case MissionKind.Escort: return "сопровождение";
                case MissionKind.Secret: return "тайник";
                default: return "обычный";
            }
        }

        static string RecordKindName(RecordKind k)
        {
            switch (k)
            {
                case RecordKind.Hearth: return "уровень очага";
                case RecordKind.Missions: return "боёв кампании";
                case RecordKind.Hunts: return "побед на Охоте";
                case RecordKind.Routes: return "вылазок";
                case RecordKind.Research: return "исследований";
                default: return "возвышений";
            }
        }

        // ------------------------------------------------------------------ admin («Кампания»)

        public void AdminSetHardForAll(bool on) => Modes.HardForAll = on;

        public bool AdminHardForAll => Modes.HardForAll;

        public void AdminSetPowerGate(bool on) => Modes.GateOff = !on;

        public void AdminSetSecretsForAll(bool on) => Modes.SecretsForAll = on;

        public bool AdminSecretsForAll => Modes.SecretsForAll;

        /// <summary>Today's hard attempts back; returns them.</summary>
        public int AdminRefillHard(long now)
        {
            HardAttemptsLeft(now);
            Modes.HardUsed = 0;
            return HardAttemptsLeft(now);
        }
    }
}
