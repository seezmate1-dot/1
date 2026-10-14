// Copyright (c) AshRPG project. All rights reserved.
// Enemy stagger (poise break) and hero poise, TZ section 46.

#pragma once

#include "AshRulesCommon.h"

namespace AshRules
{
	enum class EWeaponStyle : int
	{
		SwordAndShield = 0,
		Greatsword,
		Axe
	};

	enum class EStaggerAction : int
	{
		Light = 0,
		Heavy,
		HeavyCharged,
		Special,   // Counter Thrust / Crush / third hit of the axe series (39.5)
		Parry
	};

	// 46.2: stagger gained per action and weapon style.
	inline float StaggerGain(EWeaponStyle Style, EStaggerAction Action)
	{
		static const float Table[3][5] = {
			// Light, Heavy, Charged, Special, Parry
			{ 4.f, 14.f, 20.f, 25.f, 50.f },  // sword and shield
			{ 7.f, 30.f, 45.f, 60.f, 30.f },  // greatsword
			{ 5.f, 18.f, 26.f, 12.f, 35.f }   // axe
		};
		return Table[static_cast<int>(Style)][static_cast<int>(Action)];
	}

	constexpr float FireBoltStagger = 10.f; // 46.2, any style

	struct FStaggerTuning
	{
		float DecayDelay = 4.f;          // 46.3
		float DecayPerSecond = 10.f;
		float BossDecayPerSecond = 5.f;
		float ImmunityNormal = 8.f;      // counted from the stagger start
		float ImmunityElite = 15.f;
		float ImmunityBoss = 20.f;
		float HardenedPoolMultiplier = 1.3f; // 39.3
	};

	// 46.1 pool and stagger time come from the enemy table; difficulty scales the pool.
	struct FStaggerTarget
	{
		float Pool = 20.f;
		float StaggerSeconds = 3.f;
		EEnemyRank Rank = EEnemyRank::Normal;
		bool bHardened = false;
	};

	class FStaggerMeter
	{
	public:
		FStaggerMeter() = default;
		FStaggerMeter(const FStaggerTarget& InTarget, float DifficultyPoolMultiplier, const FStaggerTuning& InTuning)
			: Target(InTarget), Tuning(InTuning)
		{
			MaxPool = Target.Pool * DifficultyPoolMultiplier * (Target.bHardened ? Tuning.HardenedPoolMultiplier : 1.f);
		}

		// Adds stagger. GainMultiplier comes from statuses (frost 1.25). Returns true when the enemy is staggered now.
		bool Add(float Amount, float GainMultiplier = 1.f)
		{
			if (Amount <= 0.f || Immunity > 0.f)
			{
				return false;
			}
			Value += Amount * GainMultiplier;
			SinceLastHit = 0.f;
			if (Value >= MaxPool)
			{
				Value = 0.f;
				StaggerLeft = Target.StaggerSeconds;
				Immunity = ImmunityFor(Target.Rank);
				return true;
			}
			return false;
		}

		// bDecayStopped: wither is active on the target (45.1).
		void Advance(float DeltaSeconds, bool bDecayStopped = false)
		{
			StaggerLeft = MaxOf(StaggerLeft - DeltaSeconds, 0.f);
			Immunity = MaxOf(Immunity - DeltaSeconds, 0.f);
			SinceLastHit += DeltaSeconds;
			if (!bDecayStopped && SinceLastHit > Tuning.DecayDelay && Value > 0.f)
			{
				const float Rate = Target.Rank == EEnemyRank::Boss ? Tuning.BossDecayPerSecond : Tuning.DecayPerSecond;
				const float DecayTime = MinOf(DeltaSeconds, SinceLastHit - Tuning.DecayDelay);
				Value = MaxOf(Value - Rate * DecayTime, 0.f);
			}
		}

		bool IsStaggered() const { return StaggerLeft > 0.f; }
		float StaggerRemaining() const { return StaggerLeft; }
		float Current() const { return Value; }
		float Pool() const { return MaxPool; }
		bool IsImmune() const { return Immunity > 0.f; }

	private:
		float ImmunityFor(EEnemyRank Rank) const
		{
			switch (Rank)
			{
			case EEnemyRank::Elite: return Tuning.ImmunityElite;
			case EEnemyRank::Boss: return Tuning.ImmunityBoss;
			case EEnemyRank::Normal: break;
			}
			return Tuning.ImmunityNormal;
		}

		FStaggerTarget Target;
		FStaggerTuning Tuning;
		float MaxPool = 20.f;
		float Value = 0.f;
		float SinceLastHit = 0.f;
		float StaggerLeft = 0.f;
		float Immunity = 0.f;
	};

	// 21.2, 46.3: a parry stuns for 1.5 s; if it also fills the meter, the longer stun wins.
	constexpr float ParryStunSeconds = 1.5f;
	inline float StunAfterParry(bool bMeterFilled, float StaggerSeconds)
	{
		return bMeterFilled ? MaxOf(ParryStunSeconds, StaggerSeconds) : ParryStunSeconds;
	}

	// 46.4: enemy attack power by wind-up (23.1); red zones count as heavy.
	enum class EAttackWeight : int
	{
		Fast = 0,
		Medium,
		Heavy
	};

	inline int AttackPower(EAttackWeight Weight)
	{
		switch (Weight)
		{
		case EAttackWeight::Fast: return 10;
		case EAttackWeight::Medium: return 20;
		case EAttackWeight::Heavy: return 40;
		}
		return 40;
	}

	// 46.4: the hero keeps attacking if the hit power does not exceed poise. Damage always applies.
	inline bool InterruptsHero(EAttackWeight Weight, int HeroPoiseValue)
	{
		return AttackPower(Weight) > HeroPoiseValue;
	}
}
