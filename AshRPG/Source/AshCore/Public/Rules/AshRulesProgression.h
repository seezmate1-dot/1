// Copyright (c) AshRPG project. All rights reserved.
// Levels, experience and anti-farm multipliers, TZ sections 22.4, 25 and 40.1.

#pragma once

#include "AshRulesCommon.h"

namespace AshRules
{
	constexpr int MaxLevelFirstVersion = 15; // 25.1
	constexpr int StatPointsPerLevel = 3;    // 25.2
	constexpr int MaxSingleStat = 40;        // 25.2

	// 25.1: total experience needed for level L is 75 x L x (L - 1).
	constexpr int TotalXpForLevel(int Level)
	{
		return Level <= 1 ? 0 : 75 * Level * (Level - 1);
	}

	inline int LevelForXp(int TotalXp, int MaxLevel = MaxLevelFirstVersion)
	{
		int Level = 1;
		while (Level < MaxLevel && TotalXp >= TotalXpForLevel(Level + 1))
		{
			++Level;
		}
		return Level;
	}

	// 25.2: perk point on levels 3, 6, 9, 12, 15.
	constexpr bool GrantsPerkPoint(int Level)
	{
		return Level > 0 && Level % 3 == 0 && Level <= MaxLevelFirstVersion;
	}

	// 22.4: experience multiplier by level difference (hero minus enemy).
	inline float LevelGapXpMultiplier(int HeroLevel, int EnemyLevel)
	{
		const int Gap = HeroLevel - EnemyLevel;
		if (Gap >= 6) return 0.1f;
		if (Gap >= 3) return 0.5f;
		return 1.f;
	}

	// 40.1: respawned enemies pay less; RespawnCount 0 = first life.
	struct FRespawnRewards
	{
		float Xp = 1.f;
		float Gold = 1.f;
		bool bDropsMaterials = true;
	};

	inline FRespawnRewards RespawnRewards(int RespawnCount)
	{
		FRespawnRewards Rewards;
		if (RespawnCount == 1)
		{
			Rewards = { 0.5f, 0.5f, false };
		}
		else if (RespawnCount >= 2)
		{
			Rewards = { 0.25f, 0.f, false };
		}
		return Rewards;
	}

	constexpr int MaxRespawns = 2; // 40.1: then the enemy is gone for good

	// 40.1: level-gap and respawn penalties do not stack, the smaller one is used.
	// First kill of a species gives triple experience.
	inline int KillXp(int BaseXp, int HeroLevel, int EnemyLevel, int RespawnCount, bool bFirstOfSpecies)
	{
		const float Penalty = MinOf(LevelGapXpMultiplier(HeroLevel, EnemyLevel), RespawnRewards(RespawnCount).Xp);
		const float FirstBonus = (bFirstOfSpecies && RespawnCount == 0) ? 3.f : 1.f;
		return FloorToGold(static_cast<double>(BaseXp) * Penalty * FirstBonus);
	}
}
