// Copyright (c) AshRPG project. All rights reserved.
// Stage 1 hero: third-person movement, camera, sprint, jump, roll with i-frames,
// stamina on the Gameplay Ability System (TZ sections 7, 8, 21.2).

#pragma once

#include "CoreMinimal.h"
#include "AbilitySystemInterface.h"
#include "GameFramework/Character.h"
#include "AshHeroCharacter.generated.h"

class UAbilitySystemComponent;
class UCameraComponent;
class USpringArmComponent;
struct FGameplayAttribute;
struct FGameplayTag;
struct FInputActionValue;

UCLASS()
class ASHRPG_API AAshHeroCharacter : public ACharacter, public IAbilitySystemInterface
{
	GENERATED_BODY()

public:
	AAshHeroCharacter();

	virtual UAbilitySystemComponent* GetAbilitySystemComponent() const override;
	virtual void PostInitializeComponents() override;
	virtual void PossessedBy(AController* NewController) override;
	virtual void OnRep_PlayerState() override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;
	virtual void Tick(float DeltaSeconds) override;

	UFUNCTION(BlueprintPure, Category = "Ash|Movement")
	bool IsRolling() const { return bIsRolling; }

	UFUNCTION(BlueprintPure, Category = "Ash|Movement")
	bool IsSprinting() const { return bIsSprinting; }

	UFUNCTION(BlueprintPure, Category = "Ash|Combat")
	bool IsInvulnerable() const { return bInvulnerable; }

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Ash|Camera")
	TObjectPtr<USpringArmComponent> CameraBoom;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Ash|Camera")
	TObjectPtr<UCameraComponent> FollowCamera;

	// Movement speeds in cm/s. Not set in the TZ yet: proposals for the playtest.
	UPROPERTY(EditDefaultsOnly, Category = "Ash|Movement")
	float RunSpeed = 450.f;

	UPROPERTY(EditDefaultsOnly, Category = "Ash|Movement")
	float SprintSpeed = 650.f;

	// Roll covers about 4 m in 0.6 s.
	UPROPERTY(EditDefaultsOnly, Category = "Ash|Movement")
	float RollSpeed = 680.f;

private:
	void Move(const FInputActionValue& Value);
	void Look(const FInputActionValue& Value);
	void JumpPressed();
	void JumpReleased();
	void SprintPressed();
	void SprintReleased();
	void RollPressed();

	void InitAbilitySystem();
	void ApplyHeroAssets();
	void SetSprinting(bool bEnable);
	void StaminaTick();
	bool TrySpendStamina(float Amount);
	void BeginInvulnerability();
	void EndInvulnerability();
	void EndRoll();
	void SetStateTag(const FGameplayTag& Tag, bool bEnable);

	float GetAttribute(const FGameplayAttribute& Attribute) const;
	void SetAttribute(const FGameplayAttribute& Attribute, float Value);

	TWeakObjectPtr<UAbilitySystemComponent> AbilitySystem;

	FVector RollDirection = FVector::ForwardVector;
	float DefaultMaxAcceleration = 2048.f;
	float LastStaminaSpendTime = -100.f;
	bool bIsRolling = false;
	bool bIsSprinting = false;
	bool bInvulnerable = false;

	FTimerHandle StaminaTimer;
	FTimerHandle InvulnerabilityStartTimer;
	FTimerHandle InvulnerabilityEndTimer;
	FTimerHandle RollEndTimer;

	static constexpr float StaminaTickInterval = 0.1f;
};
