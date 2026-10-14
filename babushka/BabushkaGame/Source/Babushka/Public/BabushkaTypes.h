// Copyright (c) Babushka project. All rights reserved.
// Shared enums and the fullness table from the GDD (section 5).

#pragma once

#include "CoreMinimal.h"
#include "BabushkaTypes.generated.h"

UENUM(BlueprintType)
enum class EBabushkaInteractKind : uint8
{
	Hide,
	Shoes,
	Phone,
	Money,
	Key,
	Pie,
	TV,
	Gate
};

UENUM(BlueprintType)
enum class EBabushkaZoneKind : uint8
{
	House,       // lit interior: grandma sees farther
	Garden,      // beds: slower and rustling
	ChickenCoop, // chickens wake up on any step
	Banya,       // sweat off one level of fullness
	Exit         // outside the fence: escape
};

UENUM(BlueprintType)
enum class EGrandmaMode : uint8
{
	Patrol,
	Suspicious,
	Chase,
	Feeding,
	WatchingTV,
	GoingToSleep,
	Sleeping,
	Stunned
};

// GDD 5: fullness levels 0..5. Level 5 is the ball.
namespace BabushkaFullness
{
	constexpr int32 MaxLevel = 5;
	constexpr int32 MaxLevelToSqueezeUnderFence = 1;
	constexpr int32 MaxLevelForWardrobe = 2;

	inline float SpeedMultiplier(int32 Level)
	{
		static const float Table[MaxLevel + 1] = { 1.f, 0.92f, 0.82f, 0.72f, 0.62f, 1.f };
		return Table[FMath::Clamp(Level, 0, MaxLevel)];
	}

}
