// Copyright (c) AshRPG project. All rights reserved.
// The ability system lives on the player state, so it survives death and respawn
// at a hearth and is ready for the server in the MMO (TZ section 10, Lyra pattern).

#pragma once

#include "CoreMinimal.h"
#include "AbilitySystemInterface.h"
#include "GameFramework/PlayerState.h"
#include "AshPlayerState.generated.h"

class UAbilitySystemComponent;
class UAshAttributeSet;

UCLASS()
class ASHRPG_API AAshPlayerState : public APlayerState, public IAbilitySystemInterface
{
	GENERATED_BODY()

public:
	AAshPlayerState();

	virtual UAbilitySystemComponent* GetAbilitySystemComponent() const override { return AbilitySystem; }
	UAshAttributeSet* GetAttributes() const { return Attributes; }

private:
	UPROPERTY(VisibleAnywhere, Category = "Ash")
	TObjectPtr<UAbilitySystemComponent> AbilitySystem;

	UPROPERTY()
	TObjectPtr<UAshAttributeSet> Attributes;
};
