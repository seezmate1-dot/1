// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaZones.h"

#include "BabushkaGameMode.h"
#include "BabushkaKid.h"
#include "Components/BillboardComponent.h"
#include "Components/BoxComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"

// ---------------------------------------------------------------- zone

ABabushkaZone::ABabushkaZone()
{
	PrimaryActorTick.bCanEverTick = false;
	Box = CreateDefaultSubobject<UBoxComponent>(TEXT("Box"));
	Box->SetBoxExtent(FVector(200.f, 200.f, 200.f));
	Box->SetCollisionProfileName(TEXT("Trigger"));
	RootComponent = Box;
}

void ABabushkaZone::BeginPlay()
{
	Super::BeginPlay();
	Box->OnComponentBeginOverlap.AddDynamic(this, &ABabushkaZone::OnEnter);
	Box->OnComponentEndOverlap.AddDynamic(this, &ABabushkaZone::OnLeave);
}

void ABabushkaZone::OnEnter(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit)
{
	ABabushkaKid* Kid = Cast<ABabushkaKid>(Other);
	// Count only the capsule, not the kid's interact sphere.
	if (Kid && OtherComp == Kid->GetCapsuleComponent())
	{
		Kid->EnterZone(Kind);
	}
}

void ABabushkaZone::OnLeave(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex)
{
	ABabushkaKid* Kid = Cast<ABabushkaKid>(Other);
	if (Kid && OtherComp == Kid->GetCapsuleComponent())
	{
		Kid->LeaveZone(Kind);
	}
}

// ---------------------------------------------------------------- gap under the fence

ABabushkaFenceGap::ABabushkaFenceGap()
{
	PrimaryActorTick.bCanEverTick = false;
	Blocker = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Blocker"));
	RootComponent = Blocker;
	Blocker->SetCollisionProfileName(TEXT("BlockAll"));

	Sensor = CreateDefaultSubobject<UBoxComponent>(TEXT("Sensor"));
	Sensor->SetupAttachment(Blocker);
	Sensor->SetCollisionProfileName(TEXT("Trigger"));
	Sensor->SetBoxExtent(FVector(160.f, 160.f, 150.f));
	Sensor->SetUsingAbsoluteScale(true);
}

void ABabushkaFenceGap::BeginPlay()
{
	Super::BeginPlay();
	Sensor->OnComponentBeginOverlap.AddDynamic(this, &ABabushkaFenceGap::OnSensor);
}

void ABabushkaFenceGap::OnSensor(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit)
{
	ABabushkaKid* Kid = Cast<ABabushkaKid>(Other);
	if (!Kid || OtherComp != Kid->GetCapsuleComponent())
	{
		return;
	}
	// Thin kids move through the blocker, everyone else bumps into it.
	const bool bThin = Kid->GetFullness() <= BabushkaFullness::MaxLevelToSqueezeUnderFence;
	Kid->GetCapsuleComponent()->IgnoreActorWhenMoving(this, bThin);
	if (!bThin)
	{
		if (ABabushkaGameMode* Mode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>())
		{
			Mode->ShowToast(TEXT("TooFatForGap"));
		}
	}
}

// ---------------------------------------------------------------- weak fence

ABabushkaBreakableFence::ABabushkaBreakableFence()
{
	PrimaryActorTick.bCanEverTick = false;
	Planks = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Planks"));
	RootComponent = Planks;
	Planks->SetCollisionProfileName(TEXT("BlockAll"));

	Sensor = CreateDefaultSubobject<UBoxComponent>(TEXT("Sensor"));
	Sensor->SetupAttachment(Planks);
	Sensor->SetCollisionProfileName(TEXT("Trigger"));
	Sensor->SetBoxExtent(FVector(160.f, 260.f, 150.f));
	Sensor->SetUsingAbsoluteScale(true);
}

void ABabushkaBreakableFence::BeginPlay()
{
	Super::BeginPlay();
	Sensor->OnComponentBeginOverlap.AddDynamic(this, &ABabushkaBreakableFence::OnSensor);
}

void ABabushkaBreakableFence::OnSensor(UPrimitiveComponent* Overlapped, AActor* Other, UPrimitiveComponent* OtherComp, int32 BodyIndex, bool bFromSweep, const FHitResult& Hit)
{
	ABabushkaKid* Kid = Cast<ABabushkaKid>(Other);
	if (bBroken || !Kid || OtherComp != Kid->GetCapsuleComponent() || !Kid->IsBall())
	{
		return;
	}
	ABabushkaGameMode* Mode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	if (Kid->GetSpeed2D() < BreakSpeed)
	{
		if (Mode) Mode->ShowToast(TEXT("FenceTooSlow"));
		return;
	}
	bBroken = true;
	Planks->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Planks->SetHiddenInGame(true);
	Kid->MakeKidNoise(2600.f);
	if (Mode) Mode->ShowToast(TEXT("FenceBroken"));
}

// ---------------------------------------------------------------- waypoint

ABabushkaWaypoint::ABabushkaWaypoint()
{
	PrimaryActorTick.bCanEverTick = false;
	RootComponent = CreateDefaultSubobject<UBillboardComponent>(TEXT("Billboard"));
	SetActorHiddenInGame(true);
}
