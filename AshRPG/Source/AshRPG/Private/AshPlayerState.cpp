// Copyright (c) AshRPG project. All rights reserved.

#include "AshPlayerState.h"

#include "AbilitySystemComponent.h"
#include "AshAttributeSet.h"

AAshPlayerState::AAshPlayerState()
{
	AbilitySystem = CreateDefaultSubobject<UAbilitySystemComponent>(TEXT("AbilitySystem"));
	AbilitySystem->SetIsReplicated(true);
	AbilitySystem->SetReplicationMode(EGameplayEffectReplicationMode::Mixed);

	// The ability system finds attribute sets created on its owner.
	Attributes = CreateDefaultSubobject<UAshAttributeSet>(TEXT("Attributes"));
}
