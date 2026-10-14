// Copyright (c) AshRPG project. All rights reserved.
// Prices, barter and upgrade branches, TZ sections 26, 27, 28.3 and 48.

#pragma once

#include "AshRulesCommon.h"

namespace AshRules
{
	// 22.2, 26.1
	enum class ERarity : int
	{
		Common = 0,
		Uncommon,
		Rare,
		Epic,
		Legendary
	};

	inline int RarityBasePrice(ERarity Rarity)
	{
		switch (Rarity)
		{
		case ERarity::Common: return 20;
		case ERarity::Uncommon: return 60;
		case ERarity::Rare: return 200;
		case ERarity::Epic: return 700;
		case ERarity::Legendary: return 2500;
		}
		return 0;
	}

	inline float RarityStatMultiplier(ERarity Rarity)
	{
		switch (Rarity)
		{
		case ERarity::Common: return 1.0f;
		case ERarity::Uncommon: return 1.15f;
		case ERarity::Rare: return 1.3f;
		case ERarity::Epic: return 1.5f;
		case ERarity::Legendary: return 1.8f;
		}
		return 1.f;
	}

	// 26.2: the player sells at 0.30 of base value, rounded down.
	inline int SellPrice(int BasePrice)
	{
		return FloorToGold(0.30 * BasePrice);
	}

	// 26.2: buy price = base x (1.2 - 0.004 x VillageTrust), trust 0..100.
	inline int BuyPrice(int BasePrice, int VillageTrust)
	{
		const int Trust = ClampTo(VillageTrust, 0, 100);
		return FloorToGold(BasePrice * (1.2 - 0.004 * Trust));
	}

	// 27.1: a barter must not let the player trade cheap for valuable and resell.
	// Sum of sell prices given >= sell price received, using the real integer prices.
	inline bool BarterHasNoArbitrage(const int* GivenBasePrices, const int* GivenCounts, int GivenKinds,
		int ReceivedBasePrice, int ReceivedCount = 1)
	{
		int Given = 0;
		for (int Index = 0; Index < GivenKinds; ++Index)
		{
			Given += SellPrice(GivenBasePrices[Index]) * GivenCounts[Index];
		}
		return Given >= SellPrice(ReceivedBasePrice) * ReceivedCount;
	}

	// 48.2: reforge costs half the base value.
	inline int ReforgePrice(int BasePrice) { return FloorToGold(0.5 * BasePrice); }

	// 28.3, 48.1: weapon upgrades, five steps, each step picks a branch.
	enum class EUpgradeBranch : int
	{
		Damage = 0,  // +8% weapon damage
		Speed,       // +6% attack speed
		Poise        // +10 stagger resistance while held
	};

	constexpr int MaxUpgradeSteps = 5;
	constexpr int MaxStepsPerBranch = 3;
	constexpr int MaxHeroPoise = 30; // 46.4

	struct FUpgradeChoice
	{
		int Damage = 0;
		int Speed = 0;
		int Poise = 0;

		int Steps() const { return Damage + Speed + Poise; }
		bool IsValid() const
		{
			return Damage >= 0 && Speed >= 0 && Poise >= 0 && Steps() <= MaxUpgradeSteps
				&& Damage <= MaxStepsPerBranch && Speed <= MaxStepsPerBranch && Poise <= MaxStepsPerBranch;
		}
		float DamageMultiplier() const { return 1.f + 0.08f * static_cast<float>(Damage); }
		float SpeedMultiplier() const { return 1.f + 0.06f * static_cast<float>(Speed); }
		int PoiseBonus() const { return 10 * Poise; }
		// Damage per second relative to an unupgraded weapon.
		float DamageRate() const { return DamageMultiplier() * SpeedMultiplier(); }
	};

	// 28.3: price of the next step (1..5): gold and rusty iron; +4 also needs cold iron, +5 the Heart of the Thicket.
	struct FUpgradeCost
	{
		int Gold = 0;
		int RustyIron = 0;
		int ColdIron = 0;
		int ThicketHeart = 0;
	};

	inline FUpgradeCost WeaponUpgradeCost(int Step)
	{
		FUpgradeCost Cost;
		if (Step < 1 || Step > MaxUpgradeSteps)
		{
			return Cost;
		}
		Cost.Gold = 100 * Step;
		Cost.RustyIron = Step;
		Cost.ColdIron = Step == 4 ? 1 : 0;
		Cost.ThicketHeart = Step == 5 ? 1 : 0;
		return Cost;
	}

	// 48.1: changing an earlier branch choice costs 50 x step number.
	inline int RespecStepPrice(int Step) { return 50 * Step; }

	// 46.4: hero poise from all sources is capped.
	inline int HeroPoise(int FromWeapon, int FromShield)
	{
		return MinOf(FromWeapon + FromShield, MaxHeroPoise);
	}
}
