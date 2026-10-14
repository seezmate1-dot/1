// Copyright (c) AshRPG project. All rights reserved.
// Project settings: Edit > Project Settings > Game > AshRPG. Saved to Config/DefaultGame.ini.

#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "AshProjectSettings.generated.h"

class UAnimInstance;
class USkeletalMesh;

// TZ 21.3. Values match AshRules::EDifficulty.
UENUM(BlueprintType)
enum class EAshDifficulty : uint8
{
	Wanderer = 0 UMETA(DisplayName = "Putnik (easy)"),
	Warden = 1 UMETA(DisplayName = "Strazh (normal)"),
	Ashen = 2 UMETA(DisplayName = "Pepelny (hard)")
};

UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "AshRPG"))
class ASHRPG_API UAshProjectSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	// Hero body until the real hero model exists. Filled by Content/Python/ash_setup_hero.py.
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Hero")
	TSoftObjectPtr<USkeletalMesh> HeroMesh;

	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Hero")
	TSoftClassPtr<UAnimInstance> HeroAnimClass;

	// Until the settings menu exists (stage 7), difficulty is chosen here.
	UPROPERTY(Config, EditAnywhere, BlueprintReadOnly, Category = "Gameplay")
	EAshDifficulty Difficulty = EAshDifficulty::Warden;

	// Called by the setup script: stores the hero assets and writes DefaultGame.ini.
	UFUNCTION(BlueprintCallable, Category = "AshRPG")
	void SetHeroAssets(USkeletalMesh* Mesh, TSubclassOf<UAnimInstance> AnimClass);

	static const UAshProjectSettings* Get() { return GetDefault<UAshProjectSettings>(); }
};
