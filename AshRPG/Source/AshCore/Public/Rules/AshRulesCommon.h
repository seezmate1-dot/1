// Copyright (c) AshRPG project. All rights reserved.
//
// Game rules from the design document (TZ) as plain C++ with no engine headers.
// The same files compile inside Unreal and in the unit tests (Tests/Rules),
// so every formula is checked without the editor.
//
// Engine rules for these headers: no exceptions, no RTTI, no names that clash
// with Unreal macros (check, verify, ensure), no variable shadowing.

#pragma once

#include <cmath>

namespace AshRules
{
	// 21.3 Difficulty levels.
	enum class EDifficulty : int
	{
		Wanderer = 0, // "Putnik", easy
		Warden = 1,   // "Strazh", normal (default)
		Ashen = 2     // "Pepelny", hard
	};

	// 45.3, 46.3: enemy rank changes thresholds and immunity windows.
	enum class EEnemyRank : int
	{
		Normal = 0,
		Elite = 1,    // elites and mini-bosses
		Boss = 2
	};

	template <typename T>
	constexpr T MinOf(T A, T B) { return A < B ? A : B; }

	template <typename T>
	constexpr T MaxOf(T A, T B) { return A > B ? A : B; }

	template <typename T>
	constexpr T ClampTo(T Value, T Low, T High) { return Value < Low ? Low : (Value > High ? High : Value); }

	// 27.2: gold is an integer and is always rounded down.
	inline int FloorToGold(double Value)
	{
		return static_cast<int>(std::floor(Value + 1e-9));
	}
}
