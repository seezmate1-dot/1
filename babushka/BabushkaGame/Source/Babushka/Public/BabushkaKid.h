// Copyright (c) Babushka project. All rights reserved.
// The grandchild: movement, fullness (GDD 5), the ball, noise, hiding, items.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "BabushkaTypes.h"
#include "BabushkaKid.generated.h"

class ABabushkaInteractable;
class UCameraComponent;
class USphereComponent;
class USpringArmComponent;
class UStaticMeshComponent;
struct FInputActionValue;

UCLASS()
class BABUSHKA_API ABabushkaKid : public ACharacter
{
	GENERATED_BODY()

public:
	ABabushkaKid();

	virtual void PostInitializeComponents() override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;
	virtual void Tick(float DeltaSeconds) override;

	// Fullness 0..5 (GDD 5).
	int32 GetFullness() const { return Fullness; }
	bool IsBall() const { return Fullness >= BabushkaFullness::MaxLevel; }
	void ChangeFullness(int32 Delta);

	// Items (GDD 4).
	bool bHasShoes = false;
	bool bHasPhone = false;
	bool bHasMoney = false;
	bool bHasKey = false;
	int32 PiesEaten = 0;
	int32 TimesCaught = 0;

	// Hiding.
	bool IsHiddenInSpot() const { return HidingSpot != nullptr; }
	bool WasSeenHiding() const { return bSeenHiding; }
	void HideIn(ABabushkaInteractable* Spot, bool bSeen);
	void LeaveHiding();

	// Grandma caught the kid: feed, then back to bed.
	void StartFeeding(float Seconds);
	bool IsBeingFed() const { return FeedingLeft > 0.f; }

	void StartSugarRush();
	void MakeKidNoise(float Radius);
	float GetSpeed2D() const { return GetVelocity().Size2D(); }

	// Zones (house, garden, coop, banya, exit).
	void EnterZone(EBabushkaZoneKind Kind);
	void LeaveZone(EBabushkaZoneKind Kind);
	bool IsInLitArea() const { return ZoneCounts[static_cast<int32>(EBabushkaZoneKind::House)] > 0; }

	ABabushkaInteractable* FindNearestInteractable() const;

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<USpringArmComponent> CameraBoom;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<UCameraComponent> Camera;

	// Sphere shown instead of the body at fullness 5.
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<UStaticMeshComponent> BallMesh;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<USphereComponent> InteractSphere;

	// cm/s. GDD 5 scales it by fullness.
	UPROPERTY(EditDefaultsOnly, Category = "Babushka")
	float RunSpeed = 420.f;

	UPROPERTY(EditDefaultsOnly, Category = "Babushka")
	float BallMaxSpeed = 800.f;

private:
	void Move(const FInputActionValue& Value);
	void InteractPressed();
	void TiptoePressed();
	void TiptoeReleased();
	void RestartPressed();
	void ApplyFullness();
	void UpdateSpeed();
	void StepNoise();
	void BanyaTick();

	int32 Fullness = 0;
	int32 ZoneCounts[5] = { 0, 0, 0, 0, 0 };
	bool bTiptoe = false;
	bool bSeenHiding = false;
	float RushLeft = 0.f;
	float FeedingLeft = 0.f;
	float BanyaSeconds = 0.f;
	FTransform SpawnTransform;

	UPROPERTY(Transient)
	TObjectPtr<ABabushkaInteractable> HidingSpot;

	FTimerHandle StepTimer;
	FTimerHandle BanyaTimer;
};
