// Copyright (c) Babushka project. All rights reserved.
// Helpers the editor Python scripts call. They do nothing in a packaged game.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "BabushkaEditorHelpers.generated.h"

class AVolume;

UCLASS()
class BABUSHKA_API UBabushkaEditorHelpers : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	// Volumes spawned from Python have no brush; this gives one a box of the given size in cm.
	UFUNCTION(BlueprintCallable, Category = "Babushka|Editor")
	static void MakeBoxBrush(AVolume* Volume, FVector Size);
};
