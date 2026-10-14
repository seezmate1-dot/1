// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaEditorHelpers.h"

#include "GameFramework/Volume.h"

#if WITH_EDITOR
#include "Builders/CubeBuilder.h"
#include "Components/BrushComponent.h"
#endif

void UBabushkaEditorHelpers::MakeBoxBrush(AVolume* Volume, FVector Size)
{
#if WITH_EDITOR
	if (!Volume || !Volume->GetWorld())
	{
		return;
	}
	UCubeBuilder* Builder = NewObject<UCubeBuilder>();
	Builder->X = static_cast<float>(Size.X);
	Builder->Y = static_cast<float>(Size.Y);
	Builder->Z = static_cast<float>(Size.Z);
	Builder->Build(Volume->GetWorld(), Volume);
	Volume->GetBrushComponent()->RequestUpdateBrushCollision();
	Volume->PostEditChange();
#endif
}
