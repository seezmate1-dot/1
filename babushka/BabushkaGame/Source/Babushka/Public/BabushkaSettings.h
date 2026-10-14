// Copyright (c) Babushka project. All rights reserved.
// Project Settings > Game > Babushka. Filled by Content/Python/babushka_build_dacha.py.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "BabushkaSettings.generated.h"

class UAnimInstance;
class USkeletalMesh;

UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "Babushka"))
class BABUSHKA_API UBabushkaSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	// Placeholder bodies from the Third Person pack until the real low-poly characters exist.
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Characters")
	TSoftObjectPtr<USkeletalMesh> KidMesh;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Characters")
	TSoftObjectPtr<USkeletalMesh> GrandmaMesh;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Characters")
	TSoftClassPtr<UAnimInstance> AnimClass;

	// GDD 4: a night lasts 20 minutes. The first build uses 10 for faster testing.
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Night", meta = (ClampMin = "60"))
	float NightSeconds = 600.f;

	UFUNCTION(BlueprintCallable, Category = "Babushka")
	void SetCharacterAssets(USkeletalMesh* InKidMesh, USkeletalMesh* InGrandmaMesh, TSubclassOf<UAnimInstance> InAnimClass);

	static const UBabushkaSettings* Get() { return GetDefault<UBabushkaSettings>(); }
};
