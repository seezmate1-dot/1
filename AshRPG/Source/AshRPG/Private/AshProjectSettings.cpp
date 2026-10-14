// Copyright (c) AshRPG project. All rights reserved.

#include "AshProjectSettings.h"

#include "Animation/AnimInstance.h"
#include "Engine/SkeletalMesh.h"

void UAshProjectSettings::SetHeroAssets(USkeletalMesh* Mesh, TSubclassOf<UAnimInstance> AnimClass)
{
	HeroMesh = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(Mesh));
	HeroAnimClass = TSoftClassPtr<UAnimInstance>(FSoftObjectPath(AnimClass.Get()));
#if WITH_EDITOR
	TryUpdateDefaultConfigFile();
#endif
}
