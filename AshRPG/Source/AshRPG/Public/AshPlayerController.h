// Copyright (c) AshRPG project. All rights reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "AshPlayerController.generated.h"

class UAshInputConfig;

UCLASS()
class ASHRPG_API AAshPlayerController : public APlayerController
{
	GENERATED_BODY()

public:
	// Created on first use, so the pawn can bind input in any order.
	UAshInputConfig* GetInputConfig();

protected:
	virtual void BeginPlay() override;

private:
	UPROPERTY(Transient)
	TObjectPtr<UAshInputConfig> InputConfig;
};
