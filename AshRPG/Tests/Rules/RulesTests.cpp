// Unit tests for AshRules. They check the formulas against the numbers in the TZ.
// Build and run: tools/run_rule_tests.sh (g++ or clang++, no engine needed).

#include "Rules/AshRules.h"

#include <cmath>
#include <cstdio>
#include <initializer_list>

using namespace AshRules;

static int Failures = 0;
static int Checks = 0;

#define EXPECT_TRUE(Cond) Expect((Cond), #Cond, __FILE__, __LINE__)
#define EXPECT_NEAR(A, B, Eps) Expect(std::fabs((A) - (B)) <= (Eps), #A " ~= " #B, __FILE__, __LINE__)
#define EXPECT_EQ(A, B) Expect((A) == (B), #A " == " #B, __FILE__, __LINE__)

static void Expect(bool bOk, const char* Text, const char* File, int Line)
{
	++Checks;
	if (!bOk)
	{
		++Failures;
		std::printf("FAIL %s:%d  %s\n", File, Line, Text);
	}
}

// Steps time in small increments, like the game would.
static float Run(FStatusSet& Set, float Seconds)
{
	float Damage = 0.f;
	for (int Step = 0; Step < static_cast<int>(Seconds * 100.f + 0.5f); ++Step)
	{
		Damage += Set.Advance(0.01f);
	}
	return Damage;
}

static void Run(FStaggerMeter& Meter, float Seconds, bool bDecayStopped = false)
{
	for (int Step = 0; Step < static_cast<int>(Seconds * 100.f + 0.5f); ++Step)
	{
		Meter.Advance(0.01f, bDecayStopped);
	}
}

static void TestHeroAndCombat()
{
	const FPrimaryStats Start;
	EXPECT_NEAR(MaxHealthFor(Start), 100.f, 1e-4f);   // 21.1
	EXPECT_NEAR(MaxStaminaFor(Start), 100.f, 1e-4f);
	EXPECT_NEAR(MaxManaFor(Start), 50.f, 1e-4f);
	EXPECT_NEAR(CarryLimitFor(Start), 130.f, 1e-4f);  // 50.3
	EXPECT_NEAR(CarryLimitFor(Start, 20.f), 150.f, 1e-4f); // belt +20

	// 21.2: starter sword 15 -> about 20 per hit; armor 10 lets 91% through, 40 lets 71%.
	EXPECT_NEAR(BaseDamage(15.f, 10), 20.f, 1e-4f);
	EXPECT_NEAR(ArmorFactor(10.f), 0.909f, 0.001f);
	EXPECT_NEAR(ArmorFactor(40.f), 0.714f, 0.001f);
	EXPECT_NEAR(FinalDamage(20.f, EHitType::Light3, false, 0.f), 28.f, 1e-3f);
	EXPECT_NEAR(FinalDamage(20.f, EHitType::Riposte, true, 0.f), 90.f, 1e-3f);
	EXPECT_NEAR(CritChance(10), 0.05f, 1e-6f);
	EXPECT_NEAR(CritChance(20), 0.08f, 1e-6f);

	// 21.2: block absorbs 80% and costs 0.6 stamina per absorbed point.
	const FBlockResult Block = ResolveBlock(30.f, 100.f);
	EXPECT_NEAR(Block.DamageTaken, 6.f, 1e-4f);
	EXPECT_NEAR(Block.StaminaCost, 14.4f, 1e-4f);
	EXPECT_TRUE(!Block.bGuardBroken);
	const FBlockResult Broken = ResolveBlock(100.f, 12.f);
	EXPECT_TRUE(Broken.bGuardBroken);
	EXPECT_NEAR(Broken.StaminaCost, 12.f, 1e-4f);
	EXPECT_NEAR(Broken.DamageTaken, 80.f, 1e-3f);

	// 21.3 difficulty.
	EXPECT_NEAR(DifficultyParams(EDifficulty::Warden).RollInvulnerability, 0.35f, 1e-6f);
	EXPECT_NEAR(DifficultyParams(EDifficulty::Wanderer).ParryWindow, 0.35f, 1e-6f);
	EXPECT_EQ(DifficultyParams(EDifficulty::Wanderer).FlaskCharges, 5);
	EXPECT_EQ(DifficultyParams(EDifficulty::Ashen).FlaskCharges, 2);
	EXPECT_TRUE(RollInvulnerabilityStart + DifficultyParams(EDifficulty::Wanderer).RollInvulnerability <= RollDuration);

	EXPECT_NEAR(FireBoltDamage(10), 37.f, 1e-4f);
}

static void TestProgression()
{
	// 25.1 thresholds.
	const int Expected[] = { 0, 0, 150, 450, 900, 1500, 2250, 3150, 4200, 5400, 6750, 8250, 9900, 11700, 13650, 15750 };
	for (int Level = 1; Level <= 15; ++Level)
	{
		EXPECT_EQ(TotalXpForLevel(Level), Expected[Level]);
	}
	// 41.2 + 51.1: levels by chapter with hunts.
	EXPECT_EQ(LevelForXp(890), 3);
	EXPECT_EQ(LevelForXp(2100), 5);
	EXPECT_EQ(LevelForXp(3260), 7);
	EXPECT_EQ(LevelForXp(4840), 8);
	EXPECT_EQ(LevelForXp(6190), 9);
	EXPECT_EQ(LevelForXp(7840), 10);
	EXPECT_EQ(LevelForXp(1000000), 15);

	int Perks = 0;
	for (int Level = 1; Level <= 15; ++Level) Perks += GrantsPerkPoint(Level) ? 1 : 0;
	EXPECT_EQ(Perks, 5);                       // 25.2
	EXPECT_EQ(StatPointsPerLevel * 14, 42);    // 25.2: 42 points by level 15

	// 22.4 + 40.1: penalties do not stack, the smaller one applies.
	EXPECT_EQ(KillXp(30, 4, 4, 0, false), 30);
	EXPECT_EQ(KillXp(30, 4, 4, 0, true), 90);  // first of species x3
	EXPECT_EQ(KillXp(30, 7, 4, 0, false), 15); // level gap 3
	EXPECT_EQ(KillXp(30, 7, 4, 1, false), 15); // min(0.5, 0.5), not 0.25
	EXPECT_EQ(KillXp(30, 10, 4, 1, false), 3); // min(0.1, 0.5)
	EXPECT_EQ(KillXp(40, 4, 4, 2, false), 10); // second respawn x0.25
	EXPECT_TRUE(!RespawnRewards(1).bDropsMaterials);
	EXPECT_NEAR(RespawnRewards(2).Gold, 0.f, 1e-6f);
}

static void TestEconomy()
{
	// 26.2: no profit from buying and selling, at any trust, for all prices in 26.1.
	const int Prices[] = { 20, 60, 200, 700, 2500, 8, 25, 12, 10, 6, 50, 10, 12, 15, 40, 40, 30, 5, 2 };
	for (int Price : Prices)
	{
		for (int Trust = 0; Trust <= 100; ++Trust)
		{
			const int Buy = BuyPrice(Price, Trust);
			const int Sell = SellPrice(Price);
			EXPECT_TRUE(Buy >= Sell);
			EXPECT_TRUE(Sell == 0 || static_cast<double>(Buy) / Sell >= 0.8 / 0.3 - 1e-9);
		}
	}
	EXPECT_EQ(BuyPrice(200, 50), 200);
	EXPECT_EQ(BuyPrice(200, 0), 240);
	EXPECT_EQ(BuyPrice(200, 100), 160);
	EXPECT_EQ(SellPrice(200), 60);

	// 27.1 barters (after the fix in REVIEW.md: cold iron base 50, belt for 9 skins).
	{
		const int Given[] = { 12 }; const int Count[] = { 5 };
		EXPECT_TRUE(BarterHasNoArbitrage(Given, Count, 1, 50));    // 5 rusty iron -> cold iron
	}
	{
		const int Given[] = { 8 }; const int Count[] = { 9 };
		EXPECT_TRUE(BarterHasNoArbitrage(Given, Count, 1, 60));    // 9 wolf skins -> belt (uncommon)
	}
	{
		const int Given[] = { 25 }; const int Count[] = { 3 };
		EXPECT_TRUE(BarterHasNoArbitrage(Given, Count, 1, 60));    // 3 fangs -> amulet
	}
	{
		const int Given[] = { 10 }; const int Count[] = { 4 };
		EXPECT_TRUE(BarterHasNoArbitrage(Given, Count, 1, 30));    // 4 roots -> antidote
	}
	{
		const int Given[] = { 200 }; const int Count[] = { 4 };
		EXPECT_TRUE(BarterHasNoArbitrage(Given, Count, 1, 700));   // 4 rare -> epic
	}
	{
		// The old numbers broke the rule: 5 x 3 = 15 < 18 and 8 x 2 = 16 < 18.
		const int Iron[] = { 12 }; const int Five[] = { 5 };
		EXPECT_TRUE(!BarterHasNoArbitrage(Iron, Five, 1, 60));
		const int Skin[] = { 8 }; const int Eight[] = { 8 };
		EXPECT_TRUE(!BarterHasNoArbitrage(Skin, Eight, 1, 60));
	}

	// 48.2 reforge prices.
	EXPECT_EQ(ReforgePrice(60), 30);
	EXPECT_EQ(ReforgePrice(200), 100);
	EXPECT_EQ(ReforgePrice(700), 350);

	// 48.1 branches.
	FUpgradeChoice Best; Best.Damage = 3; Best.Speed = 2;
	EXPECT_TRUE(Best.IsValid());
	EXPECT_NEAR(Best.DamageRate(), 1.3888f, 1e-3f);
	FUpgradeChoice Other; Other.Damage = 2; Other.Speed = 3;
	EXPECT_TRUE(Best.DamageRate() > Other.DamageRate());
	FUpgradeChoice AllDamage; AllDamage.Damage = 5;
	EXPECT_TRUE(!AllDamage.IsValid());
	FUpgradeChoice Tank; Tank.Damage = 3; Tank.Poise = 2;
	EXPECT_EQ(Tank.PoiseBonus(), 20);
	EXPECT_EQ(HeroPoise(30, 10), 30);          // 46.4 cap
	EXPECT_EQ(WeaponUpgradeCost(4).ColdIron, 1);
	EXPECT_EQ(WeaponUpgradeCost(5).ThicketHeart, 1);
	int Gold = 0, Iron = 0;
	for (int Step = 1; Step <= 5; ++Step) { Gold += WeaponUpgradeCost(Step).Gold; Iron += WeaponUpgradeCost(Step).RustyIron; }
	EXPECT_EQ(Gold, 1500);                     // 41.3: weapon to +5 costs 1 500
	EXPECT_EQ(Iron + 5, 20);                   // 40.1: about 20 iron with cold iron (5 rusty each)
}

static void TestStatuses()
{
	const FStatusTuning Tuning;

	// 45.2: a fire bolt (60) burns a vulnerable wolf at once.
	FStatusTarget Wolf;
	Wolf.Affinity[static_cast<int>(EStatus::Burn)] = EAffinity::Vulnerable;
	Wolf.Affinity[static_cast<int>(EStatus::Poison)] = EAffinity::Resistant;
	FStatusSet WolfSet(Wolf, Tuning);
	EXPECT_TRUE(WolfSet.AddBuildup(EStatus::Burn, Buildup::FireBoltBurn));
	EXPECT_TRUE(WolfSet.PanicsBeasts());
	EXPECT_NEAR(WolfSet.ElementMultiplier(EStatus::Burn), 1.5f, 1e-6f);
	EXPECT_NEAR(WolfSet.ElementMultiplier(EStatus::Poison), 0.5f, 1e-6f);
	// Burn deals 6 x 5 = 30 and the wolf is immune to burn for 6 s.
	EXPECT_NEAR(Run(WolfSet, 5.f), 30.f, 0.05f);
	EXPECT_TRUE(!WolfSet.AddBuildup(EStatus::Burn, 100.f));
	Run(WolfSet, 1.1f);
	EXPECT_TRUE(WolfSet.AddBuildup(EStatus::Burn, 60.f));

	// Oil on a normal target: 4 hits of 25.
	FStatusSet Normal(FStatusTarget(), Tuning);
	EXPECT_TRUE(!Normal.AddBuildup(EStatus::Poison, 25.f));
	EXPECT_TRUE(!Normal.AddBuildup(EStatus::Poison, 25.f));
	EXPECT_TRUE(!Normal.AddBuildup(EStatus::Poison, 25.f));
	EXPECT_TRUE(Normal.AddBuildup(EStatus::Poison, 25.f));
	EXPECT_NEAR(Normal.OutgoingDamageMultiplier(), 0.9f, 1e-6f);

	// Decay: 3 s delay, then 8 per second.
	FStatusSet Decay(FStatusTarget(), Tuning);
	Decay.AddBuildup(EStatus::Bleed, 50.f);
	Run(Decay, 3.f);
	EXPECT_NEAR(Decay.MeterValue(EStatus::Bleed), 50.f, 0.01f);
	Run(Decay, 2.f);
	EXPECT_NEAR(Decay.MeterValue(EStatus::Bleed), 34.f, 0.1f);

	// Fire and frost cancel each other.
	FStatusSet Mixed(FStatusTarget(), Tuning);
	Mixed.AddBuildup(EStatus::Frost, 100.f);
	EXPECT_TRUE(Mixed.IsActive(EStatus::Frost));
	EXPECT_NEAR(Mixed.StaggerGainMultiplier(), 1.25f, 1e-6f);
	Mixed.AddBuildup(EStatus::Burn, 100.f);
	EXPECT_TRUE(Mixed.IsActive(EStatus::Burn));
	EXPECT_TRUE(!Mixed.IsActive(EStatus::Frost));

	// Immune: never applies, oil bonus 0.
	FStatusTarget Spore;
	Spore.Affinity[static_cast<int>(EStatus::Poison)] = EAffinity::Immune;
	FStatusSet SporeSet(Spore, Tuning);
	EXPECT_TRUE(!SporeSet.AddBuildup(EStatus::Poison, 1000.f));
	EXPECT_NEAR(SporeSet.ElementMultiplier(EStatus::Poison), 0.f, 1e-6f);

	// 45.3 Mother of the Thicket: fire vulnerable x3 boss = 180, half duration, own multipliers.
	FStatusTarget Mother;
	Mother.Rank = EEnemyRank::Boss;
	Mother.Affinity[static_cast<int>(EStatus::Burn)] = EAffinity::Vulnerable;
	Mother.Affinity[static_cast<int>(EStatus::Frost)] = EAffinity::Resistant;
	Mother.Affinity[static_cast<int>(EStatus::Bleed)] = EAffinity::Resistant;
	Mother.Affinity[static_cast<int>(EStatus::Poison)] = EAffinity::Immune;
	Mother.Affinity[static_cast<int>(EStatus::Wither)] = EAffinity::Immune;
	Mother.ElementMultiplierOverride[static_cast<int>(EStatus::Burn)] = 1.5f;
	Mother.ElementMultiplierOverride[static_cast<int>(EStatus::Frost)] = 0.8f;
	FStatusSet MotherSet(Mother, Tuning);
	EXPECT_NEAR(MotherSet.Threshold(EStatus::Burn), 180.f, 1e-4f);
	EXPECT_NEAR(MotherSet.Threshold(EStatus::Frost), 600.f, 1e-4f);
	EXPECT_NEAR(MotherSet.Duration(EStatus::Burn), 2.5f, 1e-6f);
	EXPECT_NEAR(MotherSet.ElementMultiplier(EStatus::Frost), 0.8f, 1e-6f);
	EXPECT_TRUE(!MotherSet.ApplyDirect(EStatus::Wither));
	MotherSet.AddBuildup(EStatus::Frost, 600.f);
	EXPECT_NEAR(MotherSet.SpeedMultiplier(), 1.f, 1e-6f); // no slow on bosses
	EXPECT_NEAR(MotherSet.StaggerGainMultiplier(), 1.25f, 1e-6f);

	// Wither stops stagger decay.
	FStatusSet Withered(FStatusTarget(), Tuning);
	EXPECT_TRUE(Withered.ApplyDirect(EStatus::Wither));
	EXPECT_TRUE(Withered.StopsStaggerDecay());
}

static void TestStagger()
{
	const FStaggerTuning Tuning;

	// 46.5: two parries stagger the hunter captain (100).
	FStaggerTarget Captain{ 100.f, 2.f, EEnemyRank::Elite, false };
	FStaggerMeter CaptainMeter(Captain, 1.f, Tuning);
	EXPECT_TRUE(!CaptainMeter.Add(StaggerGain(EWeaponStyle::SwordAndShield, EStaggerAction::Parry)));
	EXPECT_TRUE(CaptainMeter.Add(StaggerGain(EWeaponStyle::SwordAndShield, EStaggerAction::Parry)));
	EXPECT_NEAR(StunAfterParry(true, 2.f), 2.f, 1e-6f);
	// Elite immunity 15 s from the stagger start.
	Run(CaptainMeter, 14.9f);
	EXPECT_TRUE(!CaptainMeter.Add(1000.f));
	Run(CaptainMeter, 0.2f);
	EXPECT_TRUE(CaptainMeter.Add(1000.f));

	// 46.5: Crush 60 + heavy 30 stagger a root creature (90).
	FStaggerMeter Root(FStaggerTarget{ 90.f, 3.f, EEnemyRank::Normal, false }, 1.f, Tuning);
	EXPECT_TRUE(!Root.Add(StaggerGain(EWeaponStyle::Greatsword, EStaggerAction::Special)));
	EXPECT_TRUE(Root.Add(StaggerGain(EWeaponStyle::Greatsword, EStaggerAction::Heavy)));

	// 46.5: one axe series (5 + 5 + 12 + 5 = 27) staggers a villager, a wolf and a spore-man.
	const float Series = 3.f * StaggerGain(EWeaponStyle::Axe, EStaggerAction::Light)
		+ StaggerGain(EWeaponStyle::Axe, EStaggerAction::Special);
	EXPECT_NEAR(Series, 27.f, 1e-4f);
	for (float Pool : { 15.f, 20.f, 25.f })
	{
		FStaggerMeter Small(FStaggerTarget{ Pool, 3.f, EEnemyRank::Normal, false }, 1.f, Tuning);
		EXPECT_TRUE(Small.Add(Series));
	}

	// 39.3 + 49.2: hardened shield-bearer has pool 91.
	FStaggerMeter Iron(FStaggerTarget{ 70.f, 3.f, EEnemyRank::Normal, true }, 1.f, Tuning);
	EXPECT_NEAR(Iron.Pool(), 91.f, 1e-3f);
	// 46.1 difficulty multiplier.
	FStaggerMeter Easy(FStaggerTarget{ 100.f, 3.f, EEnemyRank::Normal, false },
		DifficultyParams(EDifficulty::Wanderer).StaggerPoolMultiplier, Tuning);
	EXPECT_NEAR(Easy.Pool(), 70.f, 1e-3f);

	// Decay: 4 s delay then 10/s, stopped by wither; boss decays at 5/s.
	FStaggerMeter Decay(FStaggerTarget{ 100.f, 3.f, EEnemyRank::Normal, false }, 1.f, Tuning);
	Decay.Add(50.f);
	Run(Decay, 4.f);
	EXPECT_NEAR(Decay.Current(), 50.f, 0.01f);
	Run(Decay, 1.f, true);
	EXPECT_NEAR(Decay.Current(), 50.f, 0.01f);
	Run(Decay, 2.f);
	EXPECT_NEAR(Decay.Current(), 30.f, 0.2f);
	FStaggerMeter Boss(FStaggerTarget{ 150.f, 2.5f, EEnemyRank::Boss, false }, 1.f, Tuning);
	Boss.Add(50.f);
	Run(Boss, 6.f);
	EXPECT_NEAR(Boss.Current(), 40.f, 0.2f);

	// 46.4 hero poise.
	EXPECT_TRUE(InterruptsHero(EAttackWeight::Fast, 0));
	EXPECT_TRUE(!InterruptsHero(EAttackWeight::Fast, 10));
	EXPECT_TRUE(!InterruptsHero(EAttackWeight::Medium, 20));
	EXPECT_TRUE(InterruptsHero(EAttackWeight::Heavy, MaxHeroPoise));
}

int main()
{
	TestHeroAndCombat();
	TestProgression();
	TestEconomy();
	TestStatuses();
	TestStagger();
	std::printf("%d checks, %d failed\n", Checks, Failures);
	return Failures == 0 ? 0 : 1;
}
