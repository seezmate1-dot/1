// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaKid.h"

#include "Animation/AnimInstance.h"
#include "Babushka.h"
#include "BabushkaGameMode.h"
#include "BabushkaInteractable.h"
#include "BabushkaSettings.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/LocalPlayer.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/StaticMesh.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputAction.h"
#include "InputActionValue.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "Perception/AISense_Hearing.h"
#include "TimerManager.h"
#include "UObject/ConstructorHelpers.h"

#include <initializer_list>

namespace
{
	constexpr float CapsuleRadius = 34.f;
	constexpr float CapsuleHalfHeight = 88.f;
	constexpr float BallRadius = 60.f;
	constexpr float KidScale = 0.8f; // mannequin scaled down to a kid

	ABabushkaGameMode* GetMode(const UObject* Context)
	{
		const UWorld* World = Context ? Context->GetWorld() : nullptr;
		return World ? World->GetAuthGameMode<ABabushkaGameMode>() : nullptr;
	}

	UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(Outer, Name);
		Action->ValueType = Type;
		return Action;
	}
}

ABabushkaKid::ABabushkaKid()
{
	PrimaryActorTick.bCanEverTick = true;

	GetCapsuleComponent()->InitCapsuleSize(CapsuleRadius, CapsuleHalfHeight);
	bUseControllerRotationPitch = false;
	bUseControllerRotationYaw = false;
	bUseControllerRotationRoll = false;

	UCharacterMovementComponent* Movement = GetCharacterMovement();
	Movement->bOrientRotationToMovement = true;
	Movement->RotationRate = FRotator(0.f, 720.f, 0.f);
	Movement->MaxWalkSpeed = RunSpeed;
	Movement->BrakingDecelerationWalking = 2048.f;
	Movement->JumpZVelocity = 0.f; // no jumping in this game

	// 3/4 top-down camera: grandma's flashlight and the rooms stay readable (GDD 12).
	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(RootComponent);
	CameraBoom->SetUsingAbsoluteRotation(true);
	CameraBoom->SetRelativeRotation(FRotator(-55.f, -90.f, 0.f));
	CameraBoom->TargetArmLength = 1500.f;
	CameraBoom->bDoCollisionTest = false;
	CameraBoom->bEnableCameraLag = true;
	CameraBoom->CameraLagSpeed = 6.f;

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	Camera->FieldOfView = 55.f;

	BallMesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("BallMesh"));
	BallMesh->SetupAttachment(RootComponent);
	BallMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	BallMesh->SetHiddenInGame(true);
	BallMesh->SetRelativeScale3D(FVector(BallRadius * 2.f / 100.f));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	if (Sphere.Succeeded())
	{
		BallMesh->SetStaticMesh(Sphere.Object);
	}

	InteractSphere = CreateDefaultSubobject<USphereComponent>(TEXT("InteractSphere"));
	InteractSphere->SetupAttachment(RootComponent);
	InteractSphere->InitSphereRadius(170.f);
	InteractSphere->SetCollisionProfileName(TEXT("Trigger"));

	GetMesh()->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -CapsuleHalfHeight), FRotator(0.f, -90.f, 0.f));
	GetMesh()->SetRelativeScale3D(FVector(KidScale));
}

void ABabushkaKid::PostInitializeComponents()
{
	Super::PostInitializeComponents();
	const UBabushkaSettings* Settings = UBabushkaSettings::Get();
	if (USkeletalMesh* Body = Settings->KidMesh.LoadSynchronous())
	{
		GetMesh()->SetSkeletalMeshAsset(Body);
		if (UClass* Anim = Settings->AnimClass.LoadSynchronous())
		{
			GetMesh()->SetAnimInstanceClass(Anim);
		}
	}
}

void ABabushkaKid::BeginPlay()
{
	Super::BeginPlay();
	SpawnTransform = GetActorTransform();
	ApplyFullness();
	GetWorldTimerManager().SetTimer(StepTimer, this, &ABabushkaKid::StepNoise, 0.35f, true);
	if (ABabushkaGameMode* Mode = GetMode(this))
	{
		Mode->ShowToast(TEXT("Start"), 4.f);
	}
}

// ---------------------------------------------------------------- input

void ABabushkaKid::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	APlayerController* PC = Cast<APlayerController>(GetController());
	if (!Input || !PC)
	{
		UE_LOG(LogBabushka, Error, TEXT("Enhanced Input is not set up, the kid cannot move."));
		return;
	}

	// Actions are created in code, so the project needs no input assets.
	UInputAction* MoveAction = MakeAction(this, TEXT("IA_Move"), EInputActionValueType::Axis2D);
	UInputAction* InteractAction = MakeAction(this, TEXT("IA_Interact"), EInputActionValueType::Boolean);
	UInputAction* TiptoeAction = MakeAction(this, TEXT("IA_Tiptoe"), EInputActionValueType::Boolean);
	UInputAction* RestartAction = MakeAction(this, TEXT("IA_Restart"), EInputActionValueType::Boolean);
	UInputMappingContext* Context = NewObject<UInputMappingContext>(this, TEXT("IMC_Kid"));

	auto Swizzle = [Context](FEnhancedActionKeyMapping& Mapping) { Mapping.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(Context)); };
	auto Negate = [Context](FEnhancedActionKeyMapping& Mapping, bool bX, bool bY)
	{
		UInputModifierNegate* Modifier = NewObject<UInputModifierNegate>(Context);
		Modifier->bX = bX; Modifier->bY = bY; Modifier->bZ = false;
		Mapping.Modifiers.Add(Modifier);
	};
	for (const FKey& Key : { EKeys::W, EKeys::Up })
	{
		Swizzle(Context->MapKey(MoveAction, Key));
	}
	for (const FKey& Key : { EKeys::S, EKeys::Down })
	{
		FEnhancedActionKeyMapping& Mapping = Context->MapKey(MoveAction, Key);
		Swizzle(Mapping);
		Negate(Mapping, true, true);
	}
	for (const FKey& Key : { EKeys::A, EKeys::Left })
	{
		Negate(Context->MapKey(MoveAction, Key), true, false);
	}
	Context->MapKey(MoveAction, EKeys::D);
	Context->MapKey(MoveAction, EKeys::Right);
	Context->MapKey(MoveAction, EKeys::Gamepad_Left2D).Modifiers.Add(NewObject<UInputModifierDeadZone>(Context));
	Context->MapKey(InteractAction, EKeys::E);
	Context->MapKey(InteractAction, EKeys::Gamepad_FaceButton_Bottom);
	Context->MapKey(TiptoeAction, EKeys::LeftShift);
	Context->MapKey(TiptoeAction, EKeys::Gamepad_LeftTrigger);
	Context->MapKey(RestartAction, EKeys::R);
	Context->MapKey(RestartAction, EKeys::Gamepad_Special_Right);

	if (UEnhancedInputLocalPlayerSubsystem* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer()))
	{
		Subsystem->ClearAllMappings();
		Subsystem->AddMappingContext(Context, 0);
	}

	Input->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ABabushkaKid::Move);
	Input->BindAction(InteractAction, ETriggerEvent::Started, this, &ABabushkaKid::InteractPressed);
	Input->BindAction(TiptoeAction, ETriggerEvent::Started, this, &ABabushkaKid::TiptoePressed);
	Input->BindAction(TiptoeAction, ETriggerEvent::Completed, this, &ABabushkaKid::TiptoeReleased);
	Input->BindAction(RestartAction, ETriggerEvent::Started, this, &ABabushkaKid::RestartPressed);
}

void ABabushkaKid::Move(const FInputActionValue& Value)
{
	if (IsHiddenInSpot() || IsBeingFed())
	{
		return;
	}
	const ABabushkaGameMode* Mode = GetMode(this);
	if (Mode && Mode->IsNightOver())
	{
		return;
	}
	// The camera looks north (-Y): up on the screen is -Y, right is +X.
	const FVector2D Axis = Value.Get<FVector2D>();
	AddMovementInput(FVector(0.f, -1.f, 0.f), static_cast<float>(Axis.Y));
	AddMovementInput(FVector(1.f, 0.f, 0.f), static_cast<float>(Axis.X));
}

void ABabushkaKid::TiptoePressed() { bTiptoe = true; UpdateSpeed(); }
void ABabushkaKid::TiptoeReleased() { bTiptoe = false; UpdateSpeed(); }

void ABabushkaKid::RestartPressed()
{
	ABabushkaGameMode* Mode = GetMode(this);
	if (Mode && Mode->IsNightOver())
	{
		Mode->RestartNight();
	}
}

void ABabushkaKid::InteractPressed()
{
	ABabushkaGameMode* Mode = GetMode(this);
	if (IsBeingFed() || (Mode && Mode->IsNightOver()))
	{
		return;
	}
	if (IsHiddenInSpot())
	{
		LeaveHiding();
		if (Mode) Mode->ShowToast(TEXT("Unhide"));
		return;
	}
	if (ABabushkaInteractable* Target = FindNearestInteractable())
	{
		Target->Use(this);
	}
}

ABabushkaInteractable* ABabushkaKid::FindNearestInteractable() const
{
	TArray<AActor*> Nearby;
	InteractSphere->GetOverlappingActors(Nearby, ABabushkaInteractable::StaticClass());
	ABabushkaInteractable* Best = nullptr;
	float BestDistance = TNumericLimits<float>::Max();
	for (AActor* Actor : Nearby)
	{
		ABabushkaInteractable* Candidate = Cast<ABabushkaInteractable>(Actor);
		if (!Candidate || !Candidate->IsAvailable())
		{
			continue;
		}
		const float Distance = FVector::DistSquared2D(Candidate->GetActorLocation(), GetActorLocation());
		if (Distance < BestDistance)
		{
			BestDistance = Distance;
			Best = Candidate;
		}
	}
	return Best;
}

// ---------------------------------------------------------------- fullness (GDD 5)

void ABabushkaKid::ChangeFullness(int32 Delta)
{
	const int32 Old = Fullness;
	Fullness = FMath::Clamp(Fullness + Delta, 0, BabushkaFullness::MaxLevel);
	if (Fullness == Old)
	{
		return;
	}
	ApplyFullness();
	if (ABabushkaGameMode* Mode = GetMode(this))
	{
		if (IsBall()) Mode->ShowToast(TEXT("BecameBall"), 3.5f);
		else if (Delta > 0) Mode->ShowToast(TEXT("FullnessUp"));
	}
}

void ABabushkaKid::ApplyFullness()
{
	const bool bBall = IsBall();
	if (bBall)
	{
		GetCapsuleComponent()->SetCapsuleSize(BallRadius, BallRadius);
		GetMesh()->SetHiddenInGame(true);
		BallMesh->SetHiddenInGame(false);
	}
	else
	{
		const float Width = 1.f + 0.15f * static_cast<float>(Fullness);
		GetCapsuleComponent()->SetCapsuleSize(CapsuleRadius * Width, CapsuleHalfHeight);
		GetMesh()->SetHiddenInGame(false);
		GetMesh()->SetRelativeScale3D(FVector(KidScale * Width, KidScale * Width, KidScale));
		BallMesh->SetHiddenInGame(true);
	}
	UpdateSpeed();
}

void ABabushkaKid::UpdateSpeed()
{
	UCharacterMovementComponent* Movement = GetCharacterMovement();
	if (IsBall())
	{
		// Momentum: slow to start, hard to turn, fast downhill (GDD 5).
		Movement->MaxWalkSpeed = BallMaxSpeed;
		Movement->MaxAcceleration = 450.f;
		Movement->BrakingDecelerationWalking = 80.f;
		Movement->GroundFriction = 0.6f;
		Movement->bOrientRotationToMovement = false;
		return;
	}
	const bool bGarden = ZoneCounts[static_cast<int32>(EBabushkaZoneKind::Garden)] > 0;
	Movement->MaxWalkSpeed = RunSpeed * BabushkaFullness::SpeedMultiplier(Fullness)
		* (bTiptoe ? 0.5f : 1.f) * (RushLeft > 0.f ? 1.35f : 1.f) * (bGarden ? 0.85f : 1.f);
	Movement->MaxAcceleration = 2048.f;
	Movement->BrakingDecelerationWalking = 2048.f;
	Movement->GroundFriction = 8.f;
	Movement->bOrientRotationToMovement = true;
}

void ABabushkaKid::StartSugarRush()
{
	RushLeft = 8.f;
	UpdateSpeed();
}

void ABabushkaKid::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (RushLeft > 0.f)
	{
		RushLeft -= DeltaSeconds;
		if (RushLeft <= 0.f) UpdateSpeed();
	}

	if (FeedingLeft > 0.f)
	{
		FeedingLeft -= DeltaSeconds;
		if (FeedingLeft <= 0.f)
		{
			if (!IsBall())
			{
				++PiesEaten;
				ChangeFullness(1);
			}
			// Back to bed.
			SetActorLocation(SpawnTransform.GetLocation(), false, nullptr, ETeleportType::TeleportPhysics);
			GetCharacterMovement()->StopMovementImmediately();
		}
	}

	if (IsBall())
	{
		// Roll the sphere along the ground.
		const FVector Velocity = GetVelocity();
		const float Speed = Velocity.Size2D();
		if (Speed > 1.f)
		{
			const FVector Axis = FVector::CrossProduct(FVector::UpVector, Velocity.GetSafeNormal2D());
			BallMesh->AddWorldRotation(FQuat(Axis, Speed * DeltaSeconds / BallRadius));
		}
	}
}

// ---------------------------------------------------------------- noise (GDD 6: grandma hears)

void ABabushkaKid::MakeKidNoise(float Radius)
{
	// Loudness x MaxRange gives the hearing radius in cm; grandma reads it back from the stimulus strength.
	UAISense_Hearing::ReportNoiseEvent(this, GetActorLocation(), Radius / 1000.f, this, 1000.f, NAME_None);
}

void ABabushkaKid::StepNoise()
{
	if (IsHiddenInSpot() || IsBeingFed() || GetSpeed2D() < 50.f || !GetCharacterMovement()->IsMovingOnGround())
	{
		return;
	}
	float Radius = IsBall() ? 1400.f : (bTiptoe ? 150.f : 600.f + 150.f * static_cast<float>(Fullness));
	if (ZoneCounts[static_cast<int32>(EBabushkaZoneKind::Garden)] > 0)
	{
		Radius += 200.f;
	}
	if (ZoneCounts[static_cast<int32>(EBabushkaZoneKind::ChickenCoop)] > 0)
	{
		Radius = bTiptoe ? 1000.f : 2000.f;
		if (FMath::FRand() < 0.3f)
		{
			if (ABabushkaGameMode* Mode = GetMode(this)) Mode->ShowToast(TEXT("Chickens"), 1.2f);
		}
	}
	MakeKidNoise(Radius);
}

// ---------------------------------------------------------------- hiding and feeding

void ABabushkaKid::HideIn(ABabushkaInteractable* Spot, bool bSeen)
{
	HidingSpot = Spot;
	bSeenHiding = bSeen;
	GetCharacterMovement()->StopMovementImmediately();
	GetCharacterMovement()->DisableMovement();
	GetMesh()->SetHiddenInGame(true);
}

void ABabushkaKid::LeaveHiding()
{
	HidingSpot = nullptr;
	bSeenHiding = false;
	GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	GetMesh()->SetHiddenInGame(IsBall());
}

void ABabushkaKid::StartFeeding(float Seconds)
{
	if (IsHiddenInSpot())
	{
		LeaveHiding();
	}
	++TimesCaught;
	FeedingLeft = Seconds;
	GetCharacterMovement()->StopMovementImmediately();
}

// ---------------------------------------------------------------- zones

void ABabushkaKid::EnterZone(EBabushkaZoneKind Kind)
{
	++ZoneCounts[static_cast<int32>(Kind)];
	ABabushkaGameMode* Mode = GetMode(this);
	switch (Kind)
	{
	case EBabushkaZoneKind::Exit:
		if (bHasShoes)
		{
			if (Mode) Mode->FinishNight(true);
		}
		else
		{
			if (Mode) Mode->ShowToast(TEXT("NoShoes"));
			SetActorLocation(GetActorLocation() - GetVelocity().GetSafeNormal2D() * 250.f);
			GetCharacterMovement()->StopMovementImmediately();
		}
		break;
	case EBabushkaZoneKind::Banya:
		BanyaSeconds = 0.f;
		GetWorldTimerManager().SetTimer(BanyaTimer, this, &ABabushkaKid::BanyaTick, 0.25f, true);
		break;
	case EBabushkaZoneKind::Garden:
		UpdateSpeed();
		break;
	default:
		break;
	}
}

void ABabushkaKid::LeaveZone(EBabushkaZoneKind Kind)
{
	int32& Count = ZoneCounts[static_cast<int32>(Kind)];
	Count = FMath::Max(Count - 1, 0);
	if (Kind == EBabushkaZoneKind::Banya)
	{
		GetWorldTimerManager().ClearTimer(BanyaTimer);
	}
	if (Kind == EBabushkaZoneKind::Garden)
	{
		UpdateSpeed();
	}
}

void ABabushkaKid::BanyaTick()
{
	// GDD 5: four seconds in the steam room take one level off, the ball turns back into a kid.
	if (Fullness <= 0)
	{
		return;
	}
	BanyaSeconds += 0.25f;
	if (BanyaSeconds >= 4.f)
	{
		BanyaSeconds = 0.f;
		ChangeFullness(-1);
		if (ABabushkaGameMode* Mode = GetMode(this)) Mode->ShowToast(TEXT("Banya"));
	}
}
