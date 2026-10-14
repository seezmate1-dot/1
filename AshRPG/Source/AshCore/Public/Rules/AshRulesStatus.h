// Copyright (c) AshRPG project. All rights reserved.
// Status effects and enemy affinities, TZ section 45.
//
// Numbers live in FStatusTuning. In the game the tuning is filled from a Data Table
// (TZ 8: balance numbers in data, not in code); the defaults here are the TZ values
// and serve as the test baseline.

#pragma once

#include "AshRulesCommon.h"

namespace AshRules
{
	enum class EStatus : int
	{
		Burn = 0,
		Poison,
		Bleed,
		Wither,
		Frost,
		Count
	};

	constexpr int StatusCount = static_cast<int>(EStatus::Count);

	// 45.3: U vulnerable, S resistant, I immune.
	enum class EAffinity : int
	{
		Normal = 0,
		Vulnerable,
		Resistant,
		Immune
	};

	inline bool IsElement(EStatus Status)
	{
		return Status == EStatus::Burn || Status == EStatus::Poison || Status == EStatus::Frost;
	}

	struct FStatusSpec
	{
		float DamagePerSecond = 0.f;
		float Duration = 0.f;
	};

	struct FStatusTuning
	{
		// 45.1
		FStatusSpec Specs[StatusCount] = {
			{ 6.f, 5.f },   // Burn
			{ 4.f, 10.f },  // Poison
			{ 4.f, 6.f },   // Bleed
			{ 3.f, 8.f },   // Wither
			{ 0.f, 5.f }    // Frost
		};
		// 45.2, 45.3
		float BaseThreshold = 100.f;
		float VulnerableThreshold = 60.f;
		float ResistantThreshold = 200.f;
		float DecayDelay = 3.f;
		float DecayPerSecond = 8.f;
		float ReapplyImmunity = 6.f;
		float EliteThresholdMultiplier = 2.f;
		float BossThresholdMultiplier = 3.f;
		float BossDurationMultiplier = 0.5f;
		// 45.3: elemental damage multipliers
		float VulnerableElementMultiplier = 1.5f;
		float ResistantElementMultiplier = 0.5f;
		// 45.1 side effects
		float PoisonOutgoingDamage = 0.9f;
		float FrostSlow = 0.75f;
		float FrostStaggerGain = 1.25f;
	};

	// 45.2: buildup sources.
	namespace Buildup
	{
		constexpr float OilPerHit = 25.f;
		constexpr float FireBoltBurn = 60.f;
		constexpr float SwordBleedPerHit = 10.f;
		constexpr float AxeBleedPerHit = 20.f;
		constexpr float GreatswordBleedPerHit = 0.f;
	}

	struct FStatusTarget
	{
		EAffinity Affinity[StatusCount] = { EAffinity::Normal, EAffinity::Normal, EAffinity::Normal,
			EAffinity::Normal, EAffinity::Normal };
		EEnemyRank Rank = EEnemyRank::Normal;
		// 45.3: some bosses use their own element multipliers (Mother of the Thicket: fire 1.5, frost 0.8).
		// A negative value means "use the affinity".
		float ElementMultiplierOverride[StatusCount] = { -1.f, -1.f, -1.f, -1.f, -1.f };
	};

	class FStatusSet
	{
	public:
		FStatusSet() = default;
		FStatusSet(const FStatusTarget& InTarget, const FStatusTuning& InTuning)
			: Target(InTarget), Tuning(InTuning)
		{
		}

		float Threshold(EStatus Status) const
		{
			const EAffinity Affinity = AffinityOf(Status);
			float Value = Tuning.BaseThreshold;
			if (Affinity == EAffinity::Vulnerable) Value = Tuning.VulnerableThreshold;
			if (Affinity == EAffinity::Resistant) Value = Tuning.ResistantThreshold;
			if (Target.Rank == EEnemyRank::Elite) Value *= Tuning.EliteThresholdMultiplier;
			if (Target.Rank == EEnemyRank::Boss) Value *= Tuning.BossThresholdMultiplier;
			return Value;
		}

		// 45.3: multiplier for elemental bonus damage (oil). Bleed and wither are not elements.
		float ElementMultiplier(EStatus Status) const
		{
			if (!IsElement(Status)) return 1.f;
			const float Override = Target.ElementMultiplierOverride[Index(Status)];
			if (Override >= 0.f) return Override;
			switch (AffinityOf(Status))
			{
			case EAffinity::Vulnerable: return Tuning.VulnerableElementMultiplier;
			case EAffinity::Resistant: return Tuning.ResistantElementMultiplier;
			case EAffinity::Immune: return 0.f;
			case EAffinity::Normal: break;
			}
			return 1.f;
		}

		float Duration(EStatus Status) const
		{
			const float Base = Tuning.Specs[Index(Status)].Duration;
			return Target.Rank == EEnemyRank::Boss ? Base * Tuning.BossDurationMultiplier : Base;
		}

		// Adds buildup from a hit. Returns true when the status was applied by this hit.
		bool AddBuildup(EStatus Status, float Amount)
		{
			const int I = Index(Status);
			if (Amount <= 0.f || AffinityOf(Status) == EAffinity::Immune || Immunity[I] > 0.f)
			{
				return false;
			}
			Meter[I] += Amount;
			SinceLastHit[I] = 0.f;
			if (Meter[I] >= Threshold(Status))
			{
				Apply(Status);
				return true;
			}
			return false;
		}

		// 45.2: wither from Root Blade is applied by chance, without a meter.
		bool ApplyDirect(EStatus Status)
		{
			const int I = Index(Status);
			if (AffinityOf(Status) == EAffinity::Immune || Immunity[I] > 0.f)
			{
				return false;
			}
			Apply(Status);
			return true;
		}

		// Advances time. Returns status damage dealt during the step (ignores armor, never crits).
		float Advance(float DeltaSeconds)
		{
			float Damage = 0.f;
			for (int I = 0; I < StatusCount; ++I)
			{
				if (Remaining[I] > 0.f)
				{
					const float Step = MinOf(DeltaSeconds, Remaining[I]);
					Damage += Tuning.Specs[I].DamagePerSecond * Step;
					Remaining[I] -= Step;
				}
				Immunity[I] = MaxOf(Immunity[I] - DeltaSeconds, 0.f);
				SinceLastHit[I] += DeltaSeconds;
				if (SinceLastHit[I] > Tuning.DecayDelay && Meter[I] > 0.f)
				{
					const float DecayTime = MinOf(DeltaSeconds, SinceLastHit[I] - Tuning.DecayDelay);
					Meter[I] = MaxOf(Meter[I] - Tuning.DecayPerSecond * DecayTime, 0.f);
				}
			}
			return Damage;
		}

		bool IsActive(EStatus Status) const { return Remaining[Index(Status)] > 0.f; }
		float RemainingTime(EStatus Status) const { return Remaining[Index(Status)]; }
		float MeterValue(EStatus Status) const { return Meter[Index(Status)]; }
		bool IsImmuneNow(EStatus Status) const { return Immunity[Index(Status)] > 0.f; }

		// Side effects other systems read.
		bool StopsStaggerDecay() const { return IsActive(EStatus::Wither); }
		float StaggerGainMultiplier() const { return IsActive(EStatus::Frost) ? Tuning.FrostStaggerGain : 1.f; }
		float OutgoingDamageMultiplier() const { return IsActive(EStatus::Poison) ? Tuning.PoisonOutgoingDamage : 1.f; }
		// 45.3: frost slow and burn panic do not work on elites and bosses.
		float SpeedMultiplier() const
		{
			return (IsActive(EStatus::Frost) && Target.Rank == EEnemyRank::Normal) ? Tuning.FrostSlow : 1.f;
		}
		bool PanicsBeasts() const { return IsActive(EStatus::Burn) && Target.Rank == EEnemyRank::Normal; }

	private:
		static int Index(EStatus Status) { return static_cast<int>(Status); }
		EAffinity AffinityOf(EStatus Status) const { return Target.Affinity[Index(Status)]; }

		void Apply(EStatus Status)
		{
			const int I = Index(Status);
			Meter[I] = 0.f;
			Immunity[I] = Tuning.ReapplyImmunity;
			// 45.2: a new application only refreshes the duration.
			Remaining[I] = Duration(Status);
			// 45.2: fire and frost cancel each other.
			if (Status == EStatus::Burn) Remaining[Index(EStatus::Frost)] = 0.f;
			if (Status == EStatus::Frost) Remaining[Index(EStatus::Burn)] = 0.f;
		}

		FStatusTarget Target;
		FStatusTuning Tuning;
		float Meter[StatusCount] = { 0.f, 0.f, 0.f, 0.f, 0.f };
		float SinceLastHit[StatusCount] = { 0.f, 0.f, 0.f, 0.f, 0.f };
		float Remaining[StatusCount] = { 0.f, 0.f, 0.f, 0.f, 0.f };
		float Immunity[StatusCount] = { 0.f, 0.f, 0.f, 0.f, 0.f };
	};
}
