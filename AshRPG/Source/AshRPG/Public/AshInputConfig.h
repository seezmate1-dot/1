// Copyright (c) AshRPG project. All rights reserved.
// Input actions and default key bindings, created in code so no input assets are needed.
// Rebinding in the settings menu comes in stage 7.

#pragma once

#include "CoreMinimal.h"
#include "UObject/Object.h"
#include "AshInputConfig.generated.h"

class UInputAction;
class UInputMappingContext;

UCLASS()
class ASHRPG_API UAshInputConfig : public UObject
{
	GENERATED_BODY()

public:
	// Builds the actions and the default mapping context.
	void Build();

	UPROPERTY(Transient) TObjectPtr<UInputAction> Move;
	UPROPERTY(Transient) TObjectPtr<UInputAction> Look;
	UPROPERTY(Transient) TObjectPtr<UInputAction> Jump;
	UPROPERTY(Transient) TObjectPtr<UInputAction> Sprint;
	UPROPERTY(Transient) TObjectPtr<UInputAction> Roll;
	UPROPERTY(Transient) TObjectPtr<UInputMappingContext> DefaultContext;
};
