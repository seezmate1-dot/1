// Copyright (c) Babushka project. All rights reserved.
// All player-facing text in one place (Russian first, localized later, GDD 13).
// BabushkaText.cpp is saved as UTF-8 with BOM so every compiler reads it correctly.

#pragma once

#include "CoreMinimal.h"

enum class EGrandmaLine : uint8
{
	Idle,
	Suspicious,
	Chase,
	ChaseBall,
	Feeding,
	FeedingBall,
	AfterFeeding,
	LostTarget,
	FalseAlarm,
	WokeUp,
	Sleep,
	GoingToSleep,
	TV,
	TVOver,
	SawHiding,
	Stunned,
	Recovered
};

namespace BabushkaText
{
	BABUSHKA_API FText GrandmaLine(EGrandmaLine Line);
	BABUSHKA_API FText FullnessName(int32 Level);
	BABUSHKA_API FText InteractLabel(uint8 Kind);
	BABUSHKA_API FText Toast(FName Id);
	BABUSHKA_API FText EndTitle(bool bEscaped, int32 Level, int32 Pies);
	BABUSHKA_API FText EndLine(bool bEscaped, int32 Level, int32 Pies);
}
