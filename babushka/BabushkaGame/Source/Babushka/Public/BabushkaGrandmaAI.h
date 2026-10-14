// Copyright (c) Babushka project. All rights reserved.
// Grandma's brain (GDD 6): sight and hearing through AI Perception, a small state machine,
// navigation on the nav mesh. Runs on the host in co-op.

#pragma once

#include "CoreMinimal.h"
#include "AIController.h"
#include "Perception/AIPerceptionTypes.h"
#include "BabushkaTypes.h"
#include "BabushkaGrandmaAI.generated.h"

class ABabushkaGrandma;
class ABabushkaKid;
class ABabushkaWaypoint;
class UAISenseConfig_Hearing;
class UAISenseConfig_Sight;

UCLASS()
class BABUSHKA_API ABabushkaGrandmaAI : public AAIController
{
	GENERATED_BODY()

public:
	ABabushkaGrandmaAI();

	virtual void Tick(float DeltaSeconds) override;

	EGrandmaMode GetMode() const { return Mode; }
	float GetSuspicion() const { return Suspicion; }
	bool CanSee(const ABabushkaKid* Kid) const;
	void GoWatchTV(const FVector& Seat, const FVector& Screen);

	// Speeds in cm/s (GDD 6). A thin kid (420) outruns her, a well-fed one (344) does not.
	UPROPERTY(EditDefaultsOnly, Category = "Babushka") float PatrolSpeed = 180.f;
	UPROPERTY(EditDefaultsOnly, Category = "Babushka") float SuspiciousSpeed = 250.f;
	UPROPERTY(EditDefaultsOnly, Category = "Babushka") float ChaseSpeed = 380.f;
	// Sight outside the lit house is shorter (GDD 6: 8 m inside, 5 m in the dark yard).
	UPROPERTY(EditDefaultsOnly, Category = "Babushka") float DarkSightDistance = 500.f;

protected:
	virtual void OnPossess(APawn* InPawn) override;

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UAISenseConfig_Sight> SightConfig;

	UPROPERTY(VisibleAnywhere, Category = "Babushka")
	TObjectPtr<UAISenseConfig_Hearing> HearingConfig;

private:
	UFUNCTION()
	void OnPerceptionUpdated(AActor* Actor, FAIStimulus Stimulus);

	void Hear(const FVector& Location, float Radius);
	void SetMode(EGrandmaMode NewMode);
	void GoTo(const FVector& Location);
	bool HasArrived() const;
	void NextWaypoint();
	void CatchKid(ABabushkaKid* Kid);
	ABabushkaGrandma* GetGrandma() const;
	ABabushkaKid* FindVisibleKid() const;

	EGrandmaMode Mode = EGrandmaMode::Patrol;
	float Suspicion = 0.f;
	float WaitLeft = 1.f;
	float LostTime = 0.f;
	float RepathLeft = 0.f;
	float ModeTimeLeft = 0.f;
	int32 WaypointIndex = 0;
	FVector LastSeen = FVector::ZeroVector;
	FVector TVScreen = FVector::ZeroVector;
	TWeakObjectPtr<ABabushkaKid> Target;

	UPROPERTY(Transient)
	TArray<TObjectPtr<ABabushkaWaypoint>> Waypoints;

	UPROPERTY(Transient)
	TObjectPtr<ABabushkaWaypoint> Bed;
};
