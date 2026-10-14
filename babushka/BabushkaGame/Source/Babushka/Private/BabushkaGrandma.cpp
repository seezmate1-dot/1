// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaGrandma.h"

#include "Animation/AnimInstance.h"
#include "BabushkaGrandmaAI.h"
#include "BabushkaSettings.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/SpotLightComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/World.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "NavigationInvokerComponent.h"

ABabushkaGrandma::ABabushkaGrandma()
{
	PrimaryActorTick.bCanEverTick = false;
	AIControllerClass = ABabushkaGrandmaAI::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;

	GetCapsuleComponent()->InitCapsuleSize(40.f, 90.f);
	bUseControllerRotationYaw = false;
	UCharacterMovementComponent* Movement = GetCharacterMovement();
	Movement->bOrientRotationToMovement = true;
	Movement->RotationRate = FRotator(0.f, 360.f, 0.f);
	Movement->MaxWalkSpeed = 180.f;

	GetMesh()->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -90.f), FRotator(0.f, -90.f, 0.f));
	// Slightly shorter and wider than the default mannequin.
	GetMesh()->SetRelativeScale3D(FVector(1.1f, 1.1f, 0.92f));

	// Cone matches the sight sense: 40 degrees each side (GDD 6). One shadowed light is affordable on low-end GPUs.
	Flashlight = CreateDefaultSubobject<USpotLightComponent>(TEXT("Flashlight"));
	Flashlight->SetupAttachment(RootComponent);
	Flashlight->SetRelativeLocationAndRotation(FVector(20.f, 0.f, 60.f), FRotator(-28.f, 0.f, 0.f));
	Flashlight->SetOuterConeAngle(40.f);
	Flashlight->SetInnerConeAngle(30.f);
	Flashlight->SetAttenuationRadius(950.f);
	Flashlight->SetIntensityUnits(ELightUnits::Candelas);
	Flashlight->SetIntensity(60.f);
	Flashlight->SetCastShadows(true);
	SetFlashlightAlarm(false);

	NavInvoker = CreateDefaultSubobject<UNavigationInvokerComponent>(TEXT("NavInvoker"));
	NavInvoker->SetGenerationRadii(7000.f, 8000.f);
}

void ABabushkaGrandma::PostInitializeComponents()
{
	Super::PostInitializeComponents();
	const UBabushkaSettings* Settings = UBabushkaSettings::Get();
	if (USkeletalMesh* Body = Settings->GrandmaMesh.LoadSynchronous())
	{
		GetMesh()->SetSkeletalMeshAsset(Body);
		if (UClass* Anim = Settings->AnimClass.LoadSynchronous())
		{
			GetMesh()->SetAnimInstanceClass(Anim);
		}
	}
}

void ABabushkaGrandma::SetFlashlightAlarm(bool bAlarm)
{
	Flashlight->SetLightColor(bAlarm ? FLinearColor(1.f, 0.25f, 0.2f) : FLinearColor(1.f, 0.85f, 0.6f));
}

void ABabushkaGrandma::Say(EGrandmaLine NewLine, float Seconds)
{
	Line = BabushkaText::GrandmaLine(NewLine);
	LineUntil = GetWorld()->GetTimeSeconds() + Seconds;
}

bool ABabushkaGrandma::HasLine() const
{
	return GetWorld() && GetWorld()->GetTimeSeconds() < LineUntil;
}

EGrandmaMode ABabushkaGrandma::GetMode() const
{
	const ABabushkaGrandmaAI* Brain = Cast<ABabushkaGrandmaAI>(GetController());
	return Brain ? Brain->GetMode() : EGrandmaMode::Patrol;
}

float ABabushkaGrandma::GetSuspicion() const
{
	const ABabushkaGrandmaAI* Brain = Cast<ABabushkaGrandmaAI>(GetController());
	return Brain ? Brain->GetSuspicion() : 0.f;
}

bool ABabushkaGrandma::CanSeeNow(const ABabushkaKid* Kid) const
{
	const ABabushkaGrandmaAI* Brain = Cast<ABabushkaGrandmaAI>(GetController());
	return Brain && Brain->CanSee(Kid);
}

void ABabushkaGrandma::OnTVTurnedOn(const FVector& Seat, const FVector& Screen)
{
	if (ABabushkaGrandmaAI* Brain = Cast<ABabushkaGrandmaAI>(GetController()))
	{
		Brain->GoWatchTV(Seat, Screen);
	}
}
