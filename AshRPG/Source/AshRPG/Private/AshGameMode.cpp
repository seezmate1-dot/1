// Copyright (c) AshRPG project. All rights reserved.

#include "AshGameMode.h"

#include "AshHeroCharacter.h"
#include "AshPlayerController.h"
#include "AshPlayerState.h"

AAshGameMode::AAshGameMode()
{
	DefaultPawnClass = AAshHeroCharacter::StaticClass();
	PlayerControllerClass = AAshPlayerController::StaticClass();
	PlayerStateClass = AAshPlayerState::StaticClass();
}
