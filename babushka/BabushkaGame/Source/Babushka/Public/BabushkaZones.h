// Copyright (c) Babushka project. All rights reserved.
// Level pieces placed by the dacha builder: trigger zones, the gap under the fence,
// the weak fence the ball breaks, and patrol waypoints.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "BabushkaTypes.h"
#include "BabushkaZones.generated.h"

class UBoxComponent;
class UStaticMeshComponent;

UCLASS()
class BABUSHKA_API ABabushkaZone : public AActor
{
	GENERATED_BODY()

public:
	ABabushkaZone();

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Babushka")
	EBabushkaZoneKind Kind = EBabushkaZoneKind::House;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<UBoxComponent> Box;

protected:
	virtual void BeginPlay() override;

private:
	UFUNCTION()
	void OnEnter(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit);

	UFUNCTION()
	void OnLeave(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex);
};

// GDD 4: only thin kids (fullness 0-1) squeeze under the fence here.
UCLASS()
class BABUSHKA_API ABabushkaFenceGap : public AActor
{
	GENERATED_BODY()

public:
	ABabushkaFenceGap();

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UStaticMeshComponent> Blocker;

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UBoxComponent> Sensor;

protected:
	virtual void BeginPlay() override;

private:
	UFUNCTION()
	void OnSensor(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit);
};

// GDD 5: the ball (fullness 5) breaks this fence at speed.
UCLASS()
class BABUSHKA_API ABabushkaBreakableFence : public AActor
{
	GENERATED_BODY()

public:
	ABabushkaBreakableFence();

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UStaticMeshComponent> Planks;

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UBoxComponent> Sensor;

	UPROPERTY(EditAnywhere, Category = "Babushka")
	float BreakSpeed = 500.f;

protected:
	virtual void BeginPlay() override;

private:
	UFUNCTION()
	void OnSensor(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit);

	bool bBroken = false;
};

UCLASS()
class BABUSHKA_API ABabushkaWaypoint : public AActor
{
	GENERATED_BODY()

public:
	ABabushkaWaypoint();

	// Seconds grandma lingers here before the next waypoint.
	UPROPERTY(EditAnywhere, Category = "Babushka")
	float WaitSeconds = 2.f;

	// Grandma's bed: she sleeps here from 01:00 to 02:30 (GDD 4) and does not patrol to it.
	UPROPERTY(EditAnywhere, Category = "Babushka")
	bool bIsBed = false;
};
