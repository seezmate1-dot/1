// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaGrandmaAI.h"

#include "BabushkaGameMode.h"
#include "BabushkaGrandma.h"
#include "BabushkaKid.h"
#include "BabushkaZones.h"
#include "Algo/Sort.h"
#include "EngineUtils.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Navigation/PathFollowingComponent.h"
#include "Perception/AIPerceptionComponent.h"
#include "Perception/AISense_Hearing.h"
#include "Perception/AISense_Sight.h"
#include "Perception/AISenseConfig_Hearing.h"
#include "Perception/AISenseConfig_Sight.h"

ABabushkaGrandmaAI::ABabushkaGrandmaAI()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.1f; // 10 decisions per second are plenty

	UAIPerceptionComponent* Senses = CreateDefaultSubobject<UAIPerceptionComponent>(TEXT("Perception"));

	SightConfig = CreateDefaultSubobject<UAISenseConfig_Sight>(TEXT("Sight"));
	SightConfig->SightRadius = 800.f;
	SightConfig->LoseSightRadius = 900.f;
	SightConfig->PeripheralVisionAngleDegrees = 40.f;
	SightConfig->DetectionByAffiliation.bDetectEnemies = true;
	SightConfig->DetectionByAffiliation.bDetectNeutrals = true;
	SightConfig->DetectionByAffiliation.bDetectFriendlies = true;
	SightConfig->SetMaxAge(0.5f);
	Senses->ConfigureSense(*SightConfig);

	HearingConfig = CreateDefaultSubobject<UAISenseConfig_Hearing>(TEXT("Hearing"));
	HearingConfig->HearingRange = 3000.f;
	HearingConfig->DetectionByAffiliation.bDetectEnemies = true;
	HearingConfig->DetectionByAffiliation.bDetectNeutrals = true;
	HearingConfig->DetectionByAffiliation.bDetectFriendlies = true;
	Senses->ConfigureSense(*HearingConfig);

	Senses->SetDominantSense(SightConfig->GetSenseImplementation());
	SetPerceptionComponent(*Senses);
}

void ABabushkaGrandmaAI::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	GetPerceptionComponent()->OnTargetPerceptionUpdated.AddUniqueDynamic(this, &ABabushkaGrandmaAI::OnPerceptionUpdated);

	Waypoints.Reset();
	for (TActorIterator<ABabushkaWaypoint> It(GetWorld()); It; ++It)
	{
		if (It->bIsBed) Bed = *It;
		else Waypoints.Add(*It);
	}
	// Stable order so the patrol is the same every night.
	Algo::Sort(Waypoints, [](const ABabushkaWaypoint* A, const ABabushkaWaypoint* B) { return A->GetName() < B->GetName(); });
	SetMode(EGrandmaMode::Patrol);
}

ABabushkaGrandma* ABabushkaGrandmaAI::GetGrandma() const
{
	return Cast<ABabushkaGrandma>(GetPawn());
}

void ABabushkaGrandmaAI::SetMode(EGrandmaMode NewMode)
{
	Mode = NewMode;
	ABabushkaGrandma* Grandma = GetGrandma();
	if (!Grandma)
	{
		return;
	}
	float Speed = PatrolSpeed;
	if (Mode == EGrandmaMode::Suspicious) Speed = SuspiciousSpeed;
	if (Mode == EGrandmaMode::Chase) Speed = ChaseSpeed;
	Grandma->GetCharacterMovement()->MaxWalkSpeed = Speed;
	Grandma->SetFlashlightAlarm(Mode == EGrandmaMode::Chase);
}

void ABabushkaGrandmaAI::GoTo(const FVector& Location)
{
	MoveToLocation(Location, 40.f, true, true, true, false);
}

bool ABabushkaGrandmaAI::HasArrived() const
{
	return GetMoveStatus() == EPathFollowingStatus::Idle;
}

void ABabushkaGrandmaAI::NextWaypoint()
{
	if (Waypoints.Num() == 0)
	{
		return;
	}
	WaypointIndex = (WaypointIndex + 1 + FMath::RandRange(0, 2)) % Waypoints.Num();
	GoTo(Waypoints[WaypointIndex]->GetActorLocation());
	WaitLeft = Waypoints[WaypointIndex]->WaitSeconds + FMath::FRandRange(0.f, 1.5f);
}

// ---------------------------------------------------------------- senses

bool ABabushkaGrandmaAI::CanSee(const ABabushkaKid* Kid) const
{
	const ABabushkaGrandma* Grandma = GetGrandma();
	if (!Kid || !Grandma || Kid->IsHiddenInSpot() || Kid->IsBeingFed())
	{
		return false;
	}
	if (Mode == EGrandmaMode::Sleeping || Mode == EGrandmaMode::GoingToSleep || Mode == EGrandmaMode::Feeding || Mode == EGrandmaMode::Stunned)
	{
		return false;
	}
	const float Distance = FVector::Dist2D(Kid->GetActorLocation(), Grandma->GetActorLocation());
	if (Distance < 120.f)
	{
		return true; // bumped into her
	}
	TArray<AActor*> Seen;
	GetPerceptionComponent()->GetCurrentlyPerceivedActors(UAISense_Sight::StaticClass(), Seen);
	if (!Seen.Contains(Kid))
	{
		return false;
	}
	return Kid->IsInLitArea() || Distance < DarkSightDistance;
}

ABabushkaKid* ABabushkaGrandmaAI::FindVisibleKid() const
{
	for (TActorIterator<ABabushkaKid> It(GetWorld()); It; ++It)
	{
		if (CanSee(*It))
		{
			return *It;
		}
	}
	return nullptr;
}

void ABabushkaGrandmaAI::OnPerceptionUpdated(AActor* Actor, FAIStimulus Stimulus)
{
	if (Stimulus.Type == UAISense::GetSenseID<UAISense_Hearing>() && Stimulus.WasSuccessfullySensed())
	{
		// Loudness was set to radius / 1000 by the kid (see ABabushkaKid::MakeKidNoise).
		Hear(Stimulus.StimulusLocation, Stimulus.Strength * 1000.f);
	}
}

void ABabushkaGrandmaAI::Hear(const FVector& Location, float Radius)
{
	ABabushkaGrandma* Grandma = GetGrandma();
	if (!Grandma || Mode == EGrandmaMode::Chase || Mode == EGrandmaMode::Feeding || Mode == EGrandmaMode::Stunned)
	{
		return;
	}
	float Effective = Radius;
	if (Mode == EGrandmaMode::WatchingTV) Effective *= 0.45f;  // the series is loud
	if (Mode == EGrandmaMode::Sleeping) Effective *= 0.3f;     // light sleeper, but asleep
	if (FVector::Dist2D(Location, Grandma->GetActorLocation()) > Effective)
	{
		return;
	}
	Grandma->Say(Mode == EGrandmaMode::Sleeping ? EGrandmaLine::WokeUp : EGrandmaLine::Suspicious);
	SetMode(EGrandmaMode::Suspicious);
	WaitLeft = 2.2f;
	GoTo(Location);
}

void ABabushkaGrandmaAI::GoWatchTV(const FVector& Seat, const FVector& Screen)
{
	if (Mode == EGrandmaMode::Chase || Mode == EGrandmaMode::Feeding || Mode == EGrandmaMode::Stunned
		|| Mode == EGrandmaMode::Sleeping || Mode == EGrandmaMode::GoingToSleep)
	{
		return;
	}
	if (ABabushkaGrandma* Grandma = GetGrandma())
	{
		Grandma->Say(EGrandmaLine::TV);
	}
	TVScreen = Screen;
	ModeTimeLeft = 18.f;
	SetMode(EGrandmaMode::WatchingTV);
	GoTo(Seat);
}

// ---------------------------------------------------------------- catching

void ABabushkaGrandmaAI::CatchKid(ABabushkaKid* Kid)
{
	ABabushkaGrandma* Grandma = GetGrandma();
	ABabushkaGameMode* GameMode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	if (!Grandma || !Kid)
	{
		return;
	}
	StopMovement();
	// GDD 5: a fast ball knocks grandma over instead of being caught.
	if (Kid->IsBall() && Kid->GetSpeed2D() > 450.f)
	{
		Grandma->Say(EGrandmaLine::Stunned);
		ModeTimeLeft = 2.2f;
		SetMode(EGrandmaMode::Stunned);
		if (GameMode) GameMode->ShowToast(TEXT("BallHitGrandma"));
		return;
	}
	Grandma->Say(Kid->IsBall() ? EGrandmaLine::FeedingBall : EGrandmaLine::Feeding, 2.6f);
	Kid->StartFeeding(2.6f);
	ModeTimeLeft = 2.6f;
	Suspicion = 0.f;
	SetMode(EGrandmaMode::Feeding);
}

// ---------------------------------------------------------------- brain

void ABabushkaGrandmaAI::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	ABabushkaGrandma* Grandma = GetGrandma();
	ABabushkaGameMode* GameMode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	if (!Grandma || !GameMode || GameMode->IsNightOver())
	{
		return;
	}
	const FVector Here = Grandma->GetActorLocation();

	// Timed states first.
	if (Mode == EGrandmaMode::Stunned || Mode == EGrandmaMode::Feeding)
	{
		ModeTimeLeft -= DeltaSeconds;
		if (ModeTimeLeft <= 0.f)
		{
			Grandma->Say(Mode == EGrandmaMode::Stunned ? EGrandmaLine::Recovered : EGrandmaLine::AfterFeeding);
			SetMode(EGrandmaMode::Patrol);
			WaitLeft = 1.5f;
		}
		return;
	}

	// A rolling ball knocks her over whatever she is doing.
	for (TActorIterator<ABabushkaKid> It(GetWorld()); It; ++It)
	{
		if (It->IsBall() && It->GetSpeed2D() > 450.f && FVector::Dist2D(It->GetActorLocation(), Here) < 130.f)
		{
			CatchKid(*It);
			return;
		}
	}

	// Night schedule: nap from 01:00 to 02:30 (GDD 4).
	const bool bSleepTime = GameMode->IsSleepTime();
	if (bSleepTime && Bed && (Mode == EGrandmaMode::Patrol || Mode == EGrandmaMode::WatchingTV))
	{
		Grandma->Say(EGrandmaLine::GoingToSleep);
		SetMode(EGrandmaMode::GoingToSleep);
		GoTo(Bed->GetActorLocation());
	}
	if (!bSleepTime && (Mode == EGrandmaMode::Sleeping || Mode == EGrandmaMode::GoingToSleep))
	{
		Grandma->Say(EGrandmaLine::Idle);
		SetMode(EGrandmaMode::Patrol);
	}

	// Sight builds suspicion; closer kids are noticed faster ("?" then "?!").
	ABabushkaKid* Visible = FindVisibleKid();
	if (Visible)
	{
		const float Distance = FMath::Max(FVector::Dist2D(Visible->GetActorLocation(), Here), 100.f);
		Suspicion = FMath::Min(1.f, Suspicion + DeltaSeconds * (1.6f + 900.f / Distance));
		LastSeen = Visible->GetActorLocation();
		Target = Visible;
	}
	else
	{
		Suspicion = FMath::Max(0.f, Suspicion - DeltaSeconds * 0.5f);
	}
	if (Suspicion >= 1.f && Mode != EGrandmaMode::Chase)
	{
		Grandma->Say(Target.IsValid() && Target->IsBall() ? EGrandmaLine::ChaseBall : EGrandmaLine::Chase);
		LostTime = 0.f;
		SetMode(EGrandmaMode::Chase);
	}

	switch (Mode)
	{
	case EGrandmaMode::Chase:
	{
		ABabushkaKid* Kid = Target.Get();
		LostTime = Visible ? 0.f : LostTime + DeltaSeconds;
		RepathLeft -= DeltaSeconds;
		if (RepathLeft <= 0.f)
		{
			RepathLeft = 0.3f;
			if (Visible) MoveToActor(Visible, 20.f);
			else GoTo(LastSeen);
		}
		if (Kid && !Kid->IsHiddenInSpot() && !Kid->IsBeingFed() && FVector::Dist2D(Kid->GetActorLocation(), Here) < 120.f)
		{
			CatchKid(Kid);
		}
		else if (Kid && Kid->IsHiddenInSpot() && Kid->WasSeenHiding() && FVector::Dist2D(Kid->GetActorLocation(), Here) < 200.f)
		{
			Grandma->Say(EGrandmaLine::SawHiding);
			Kid->LeaveHiding();
			CatchKid(Kid);
		}
		else if (LostTime > 2.5f)
		{
			Grandma->Say(EGrandmaLine::LostTarget);
			SetMode(EGrandmaMode::Suspicious);
			WaitLeft = 2.5f;
			GoTo(LastSeen);
		}
		break;
	}
	case EGrandmaMode::Suspicious:
		if (HasArrived())
		{
			WaitLeft -= DeltaSeconds;
			Grandma->AddActorWorldRotation(FRotator(0.f, 90.f * DeltaSeconds, 0.f)); // looks around
			if (WaitLeft <= 0.f)
			{
				Grandma->Say(EGrandmaLine::FalseAlarm);
				SetMode(EGrandmaMode::Patrol);
				NextWaypoint();
			}
		}
		break;
	case EGrandmaMode::WatchingTV:
		if (HasArrived())
		{
			const FVector ToScreen = (TVScreen - Here).GetSafeNormal2D();
			if (!ToScreen.IsNearlyZero()) Grandma->SetActorRotation(ToScreen.Rotation());
			ModeTimeLeft -= DeltaSeconds;
			if (ModeTimeLeft <= 0.f)
			{
				Grandma->Say(EGrandmaLine::TVOver);
				SetMode(EGrandmaMode::Patrol);
				NextWaypoint();
			}
		}
		break;
	case EGrandmaMode::GoingToSleep:
		if (HasArrived())
		{
			Grandma->Say(EGrandmaLine::Sleep, 100000.f);
			SetMode(EGrandmaMode::Sleeping);
		}
		break;
	case EGrandmaMode::Sleeping:
		break;
	case EGrandmaMode::Patrol:
	default:
		if (HasArrived())
		{
			WaitLeft -= DeltaSeconds;
			Grandma->AddActorWorldRotation(FRotator(0.f, FMath::Sin(GetWorld()->GetTimeSeconds() * 2.3f) * 60.f * DeltaSeconds, 0.f));
			if (WaitLeft <= 0.f)
			{
				if (FMath::FRand() < 0.45f) Grandma->Say(EGrandmaLine::Idle);
				NextWaypoint();
			}
		}
		break;
	}
}
