// Copyright (c) AshRPG project. All rights reserved.
// Combat math, TZ sections 21 and 46.3.

#pragma once

#include "AshRulesCommon.h"

namespace AshRules
{
	// 21.1 Hero parameters at level 1 and derived values.
	struct FPrimaryStats
	{
		int Strength = 10;
		int Dexterity = 10;
		int Intelligence = 10;
		int Vitality = 10; // "Vynoslivost" as a stat: feeds health and stamina pool
	};

	inline float MaxHealthFor(const FPrimaryStats& Stats) { return 60.f + 4.f * static_cast<float>(Stats.Vitality); }
	inline float MaxStaminaFor(const FPrimaryStats& Stats) { return 80.f + 2.f * static_cast<float>(Stats.Vitality); }
	inline float MaxManaFor(const FPrimaryStats& Stats) { return 30.f + 2.f * static_cast<float>(Stats.Intelligence); }

	// 21.1, 50.3: carry limit, weight only for weapons, shields and armor.
	inline float CarryLimitFor(const FPrimaryStats& Stats, float BonusFromItems = 0.f)
	{
		return 100.f + 3.f * static_cast<float>(Stats.Strength) + BonusFromItems;
	}

	// 21.2 Hit multipliers.
	enum class EHitType : int
	{
		Light1,
		Light2,
		Light3,
		Heavy,
		HeavyCharged,
		SprintAttack,
		Riposte,          // finisher after a parry
		StaggerFinisher   // finisher on a staggered enemy, 46.3
	};

	inline float HitMultiplier(EHitType Type)
	{
		switch (Type)
		{
		case EHitType::Light1: return 1.0f;
		case EHitType::Light2: return 1.1f;
		case EHitType::Light3: return 1.4f;
		case EHitType::Heavy: return 2.0f;
		case EHitType::HeavyCharged: return 2.8f;
		case EHitType::SprintAttack: return 1.3f;
		case EHitType::Riposte: return 3.0f;
		case EHitType::StaggerFinisher: return 2.0f;
		}
		return 1.0f;
	}

	// 21.2: base damage = weapon damage + 0.5 x Strength (light blades use Dexterity).
	inline float BaseDamage(float WeaponDamage, int ScalingStat)
	{
		return WeaponDamage + 0.5f * static_cast<float>(ScalingStat);
	}

	// 21.2: share of incoming damage that passes armor, 100 / (100 + armor).
	inline float ArmorFactor(float Armor)
	{
		return 100.f / (100.f + MaxOf(Armor, 0.f));
	}

	// 21.2: final damage = base x hit x crit x 100 / (100 + target armor).
	inline float FinalDamage(float Base, EHitType Hit, bool bCritical, float TargetArmor)
	{
		const float Crit = bCritical ? 1.5f : 1.0f;
		return Base * HitMultiplier(Hit) * Crit * ArmorFactor(TargetArmor);
	}

	// 21.2: crit chance 5% + 0.3% per Dexterity point above 10.
	inline float CritChance(int Dexterity)
	{
		return 0.05f + 0.003f * static_cast<float>(MaxOf(Dexterity - 10, 0));
	}

	// 21.2 Stamina.
	namespace Stamina
	{
		constexpr float LightAttack = 15.f;
		constexpr float HeavyAttack = 28.f;
		constexpr float Roll = 20.f;
		constexpr float SprintPerSecond = 8.f;
		constexpr float RegenPerSecond = 25.f;
		constexpr float RegenDelay = 1.f;
	}

	// 21.2, 25.2, 39.2: block absorbs a share of damage and costs 0.6 stamina per absorbed point.
	struct FBlockResult
	{
		float DamageTaken = 0.f;
		float StaminaCost = 0.f;
		bool bGuardBroken = false; // stamina ran out: shield breaks, hero stunned 1.5 s
	};

	inline FBlockResult ResolveBlock(float IncomingDamage, float CurrentStamina, float AbsorbShare = 0.8f)
	{
		FBlockResult Result;
		const float Absorbed = IncomingDamage * AbsorbShare;
		const float Cost = 0.6f * Absorbed;
		if (Cost > CurrentStamina)
		{
			// Only part of the hit is absorbed by the remaining stamina, the rest goes through.
			const float AbsorbedByStamina = CurrentStamina / 0.6f;
			Result.StaminaCost = CurrentStamina;
			Result.DamageTaken = IncomingDamage - AbsorbedByStamina;
			Result.bGuardBroken = true;
			return Result;
		}
		Result.StaminaCost = Cost;
		Result.DamageTaken = IncomingDamage - Absorbed;
		return Result;
	}

	constexpr float GuardBreakStunSeconds = 1.5f;

	// 21.2: roll lasts 0.6 s; invulnerability depends on difficulty (21.3).
	constexpr float RollDuration = 0.6f;
	constexpr float RollInvulnerabilityStart = 0.05f; // proposal, confirm in playtest

	// 21.3 Difficulty parameters.
	struct FDifficultyParams
	{
		float EnemyHealthMultiplier = 1.f;
		float EnemyDamageMultiplier = 1.f;
		float ParryWindow = 0.20f;
		float RollInvulnerability = 0.35f;
		int FlaskCharges = 3;
		float GoldLossOnDeath = 0.10f;
		float BossSpeedMultiplier = 1.f;
		float RareDropMultiplier = 1.f;
		float StaggerPoolMultiplier = 1.f; // 46.1
		float OilMinutes = 8.f;            // 47.1
	};

	inline FDifficultyParams DifficultyParams(EDifficulty Difficulty)
	{
		FDifficultyParams Params;
		switch (Difficulty)
		{
		case EDifficulty::Wanderer:
			Params.EnemyHealthMultiplier = 0.7f;
			Params.EnemyDamageMultiplier = 0.6f;
			Params.ParryWindow = 0.35f;
			Params.RollInvulnerability = 0.45f;
			Params.FlaskCharges = 5;
			Params.GoldLossOnDeath = 0.f;
			Params.StaggerPoolMultiplier = 0.7f;
			Params.OilMinutes = 10.f;
			break;
		case EDifficulty::Warden:
			break;
		case EDifficulty::Ashen:
			Params.EnemyHealthMultiplier = 1.4f;
			Params.EnemyDamageMultiplier = 1.5f;
			Params.ParryWindow = 0.15f;
			Params.RollInvulnerability = 0.30f;
			Params.FlaskCharges = 2;
			Params.GoldLossOnDeath = 0.25f;
			Params.BossSpeedMultiplier = 1.1f;
			Params.RareDropMultiplier = 1.25f;
			Params.StaggerPoolMultiplier = 1.25f;
			Params.OilMinutes = 6.f;
			break;
		}
		return Params;
	}

	// 21.2: Coal gifts.
	inline float FireBoltDamage(int Intelligence) { return 25.f + 1.2f * static_cast<float>(Intelligence); }
	inline float IceShieldAbsorb(int Intelligence) { return 60.f + 2.f * static_cast<float>(Intelligence); }
	constexpr float HealShareOfMaxHealth = 0.20f;
	constexpr float ManaPerHit = 3.f;

	// 21.1: ash flask heals 40% of max health.
	constexpr float FlaskHealShare = 0.40f;
}
