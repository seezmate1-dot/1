// Copyright (c) AshRPG project. All rights reserved.

#include "AshGameplayTags.h"

namespace AshTags
{
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(State_Rolling, "State.Rolling", "Hero is in a roll (TZ 21.2).");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(State_Sprinting, "State.Sprinting", "Hero is sprinting and spends stamina.");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(State_Invulnerable, "State.Invulnerable", "Incoming damage is ignored (roll i-frames).");
}
