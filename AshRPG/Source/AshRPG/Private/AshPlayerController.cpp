// Copyright (c) AshRPG project. All rights reserved.

#include "AshPlayerController.h"

#include "AshInputConfig.h"
#include "EnhancedInputSubsystems.h"
#include "Engine/LocalPlayer.h"
#include "InputMappingContext.h"

UAshInputConfig* AAshPlayerController::GetInputConfig()
{
	if (!InputConfig)
	{
		InputConfig = NewObject<UAshInputConfig>(this, TEXT("AshInputConfig"));
		InputConfig->Build();
	}
	return InputConfig;
}

void AAshPlayerController::BeginPlay()
{
	Super::BeginPlay();

	if (!IsLocalController())
	{
		return;
	}
	if (UEnhancedInputLocalPlayerSubsystem* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(GetLocalPlayer()))
	{
		Subsystem->AddMappingContext(GetInputConfig()->DefaultContext, 0);
	}
}
