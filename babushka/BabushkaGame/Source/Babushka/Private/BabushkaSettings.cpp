// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaSettings.h"

#include "Animation/AnimInstance.h"
#include "Engine/SkeletalMesh.h"

void UBabushkaSettings::SetCharacterAssets(USkeletalMesh* InKidMesh, USkeletalMesh* InGrandmaMesh, TSubclassOf<UAnimInstance> InAnimClass)
{
	KidMesh = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(InKidMesh));
	GrandmaMesh = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(InGrandmaMesh));
	AnimClass = TSoftClassPtr<UAnimInstance>(FSoftObjectPath(InAnimClass.Get()));
#if WITH_EDITOR
	TryUpdateDefaultConfigFile();
#endif
}
