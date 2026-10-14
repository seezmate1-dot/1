// Copyright (c) AshRPG project. All rights reserved.

#include "AshHeroCharacter.h"

#include "AbilitySystemComponent.h"
#include "AshAttributeSet.h"
#include "AshCore.h"
#include "AshGameplayTags.h"
#include "AshInputConfig.h"
#include "AshPlayerController.h"
#include "AshPlayerState.h"
#include "AshProjectSettings.h"
#include "Animation/AnimInstance.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "EnhancedInputComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "InputActionValue.h"
#include "Rules/AshRulesCombat.h"
#include "TimerManager.h"

AAshHeroCharacter::AAshHeroCharacter()
{
	// TZ 53.6: tick is off and only switched on for the duration of a roll.
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bStartWithTickEnabled = false;

	GetCapsuleComponent()->InitCapsuleSize(42.f, 96.f);

	bUseControllerRotationPitch = false;
	bUseControllerRotationYaw = false;
	bUseControllerRotationRoll = false;

	UCharacterMovementComponent* Movement = GetCharacterMovement();
	Movement->bOrientRotationToMovement = true;
	Movement->RotationRate = FRotator(0.f, 540.f, 0.f);
	Movement->JumpZVelocity = 500.f;
	Movement->AirControl = 0.35f;
	Movement->MaxWalkSpeed = RunSpeed;
	Movement->MinAnalogWalkSpeed = 20.f;
	Movement->BrakingDecelerationWalking = 2000.f;
	Movement->BrakingDecelerationFalling = 1500.f;

	// Camera: soft follow with collision so it does not go through walls (TZ 7).
	CameraBoom = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraBoom"));
	CameraBoom->SetupAttachment(RootComponent);
	CameraBoom->TargetArmLength = 380.f;
	CameraBoom->SocketOffset = FVector(0.f, 45.f, 55.f);
	CameraBoom->bUsePawnControlRotation = true;
	CameraBoom->bDoCollisionTest = true;
	CameraBoom->ProbeSize = 12.f;
	CameraBoom->bEnableCameraLag = true;
	CameraBoom->CameraLagSpeed = 12.f;

	FollowCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FollowCamera"));
	FollowCamera->SetupAttachment(CameraBoom, USpringArmComponent::SocketName);
	FollowCamera->bUsePawnControlRotation = false;

	// The mannequin's pivot is at the feet; the capsule's is in the middle.
	GetMesh()->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -96.f), FRotator(0.f, -90.f, 0.f));
	// The hero always evaluates its pose: attack traces will run on animation notifies (TZ 8).
	// OnlyTickPoseWhenRendered from TZ 53.6 is for enemies.
	GetMesh()->VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption::AlwaysTickPose;
}

void AAshHeroCharacter::PostInitializeComponents()
{
	Super::PostInitializeComponents();
	ApplyHeroAssets();
	DefaultMaxAcceleration = GetCharacterMovement()->MaxAcceleration;
}

void AAshHeroCharacter::ApplyHeroAssets()
{
	USkeletalMeshComponent* MeshComponent = GetMesh();
	if (!MeshComponent || MeshComponent->GetSkeletalMeshAsset())
	{
		return;
	}
	const UAshProjectSettings* Settings = UAshProjectSettings::Get();
	if (USkeletalMesh* Body = Settings->HeroMesh.LoadSynchronous())
	{
		MeshComponent->SetSkeletalMeshAsset(Body);
		if (UClass* AnimClass = Settings->HeroAnimClass.LoadSynchronous())
		{
			MeshComponent->SetAnimInstanceClass(AnimClass);
		}
	}
	else if (!HasAnyFlags(RF_ClassDefaultObject))
	{
		UE_LOG(LogAsh, Warning, TEXT("Hero mesh is not set. Run Content/Python/ash_setup_hero.py (see docs/SETUP.md)."));
	}
}

void AAshHeroCharacter::BeginPlay()
{
	Super::BeginPlay();
	GetCharacterMovement()->MaxWalkSpeed = RunSpeed;
	if (HasAuthority())
	{
		GetWorldTimerManager().SetTimer(StaminaTimer, this, &AAshHeroCharacter::StaminaTick, StaminaTickInterval, true);
	}
}

void AAshHeroCharacter::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	GetWorldTimerManager().ClearAllTimersForObject(this);
	Super::EndPlay(EndPlayReason);
}

// ---------------------------------------------------------------- ability system

UAbilitySystemComponent* AAshHeroCharacter::GetAbilitySystemComponent() const
{
	return AbilitySystem.Get();
}

void AAshHeroCharacter::PossessedBy(AController* NewController)
{
	Super::PossessedBy(NewController);
	InitAbilitySystem(); // server
}

void AAshHeroCharacter::OnRep_PlayerState()
{
	Super::OnRep_PlayerState();
	InitAbilitySystem(); // client
}

void AAshHeroCharacter::InitAbilitySystem()
{
	AAshPlayerState* State = GetPlayerState<AAshPlayerState>();
	if (!State)
	{
		return;
	}
	UAbilitySystemComponent* Component = State->GetAbilitySystemComponent();
	Component->InitAbilityActorInfo(State, this);
	AbilitySystem = Component;
}

float AAshHeroCharacter::GetAttribute(const FGameplayAttribute& Attribute) const
{
	return AbilitySystem.IsValid() ? AbilitySystem->GetNumericAttribute(Attribute) : 0.f;
}

void AAshHeroCharacter::SetAttribute(const FGameplayAttribute& Attribute, float Value)
{
	if (AbilitySystem.IsValid() && HasAuthority())
	{
		AbilitySystem->SetNumericAttributeBase(Attribute, Value);
	}
}

void AAshHeroCharacter::SetStateTag(const FGameplayTag& Tag, bool bEnable)
{
	if (!AbilitySystem.IsValid())
	{
		return;
	}
	if (bEnable)
	{
		AbilitySystem->AddLooseGameplayTag(Tag);
	}
	else
	{
		AbilitySystem->RemoveLooseGameplayTag(Tag);
	}
}

// ---------------------------------------------------------------- input

void AAshHeroCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	Super::SetupPlayerInputComponent(PlayerInputComponent);

	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	AAshPlayerController* PC = Cast<AAshPlayerController>(GetController());
	if (!Input || !PC)
	{
		UE_LOG(LogAsh, Error, TEXT("Enhanced Input or AshPlayerController is missing, input is not bound."));
		return;
	}
	const UAshInputConfig* Config = PC->GetInputConfig();
	Input->BindAction(Config->Move, ETriggerEvent::Triggered, this, &AAshHeroCharacter::Move);
	Input->BindAction(Config->Look, ETriggerEvent::Triggered, this, &AAshHeroCharacter::Look);
	Input->BindAction(Config->Jump, ETriggerEvent::Started, this, &AAshHeroCharacter::JumpPressed);
	Input->BindAction(Config->Jump, ETriggerEvent::Completed, this, &AAshHeroCharacter::JumpReleased);
	Input->BindAction(Config->Sprint, ETriggerEvent::Started, this, &AAshHeroCharacter::SprintPressed);
	Input->BindAction(Config->Sprint, ETriggerEvent::Completed, this, &AAshHeroCharacter::SprintReleased);
	Input->BindAction(Config->Roll, ETriggerEvent::Started, this, &AAshHeroCharacter::RollPressed);
}

void AAshHeroCharacter::Move(const FInputActionValue& Value)
{
	if (bIsRolling || !Controller)
	{
		return;
	}
	const FVector2D Axis = Value.Get<FVector2D>();
	const FRotator YawOnly(0.f, Controller->GetControlRotation().Yaw, 0.f);
	const FRotationMatrix Basis(YawOnly);
	AddMovementInput(Basis.GetUnitAxis(EAxis::X), static_cast<float>(Axis.Y));
	AddMovementInput(Basis.GetUnitAxis(EAxis::Y), static_cast<float>(Axis.X));
}

void AAshHeroCharacter::Look(const FInputActionValue& Value)
{
	const FVector2D Axis = Value.Get<FVector2D>();
	AddControllerYawInput(static_cast<float>(Axis.X));
	AddControllerPitchInput(static_cast<float>(Axis.Y));
}

void AAshHeroCharacter::JumpPressed()
{
	if (!bIsRolling)
	{
		Jump();
	}
}

void AAshHeroCharacter::JumpReleased()
{
	StopJumping();
}

void AAshHeroCharacter::SprintPressed()
{
	// After running out of stamina the key has to be pressed again.
	SetSprinting(GetAttribute(UAshAttributeSet::GetStaminaAttribute()) > 0.f);
}

void AAshHeroCharacter::SprintReleased()
{
	SetSprinting(false);
}

void AAshHeroCharacter::SetSprinting(bool bEnable)
{
	bIsSprinting = bEnable;
	SetStateTag(AshTags::State_Sprinting, bEnable);
	if (!bIsRolling)
	{
		GetCharacterMovement()->MaxWalkSpeed = bEnable ? SprintSpeed : RunSpeed;
	}
}

// ---------------------------------------------------------------- stamina (21.2)

bool AAshHeroCharacter::TrySpendStamina(float Amount)
{
	const float Current = GetAttribute(UAshAttributeSet::GetStaminaAttribute());
	// Like most action RPGs: any stamina left allows the action and can go to zero.
	if (Current <= 0.f)
	{
		return false;
	}
	SetAttribute(UAshAttributeSet::GetStaminaAttribute(), FMath::Max(Current - Amount, 0.f));
	LastStaminaSpendTime = GetWorld()->GetTimeSeconds();
	return true;
}

void AAshHeroCharacter::StaminaTick()
{
	if (!AbilitySystem.IsValid())
	{
		return;
	}
	const float Now = GetWorld()->GetTimeSeconds();
	const float Current = GetAttribute(UAshAttributeSet::GetStaminaAttribute());
	const float Max = GetAttribute(UAshAttributeSet::GetMaxStaminaAttribute());
	const bool bMoving = GetVelocity().SizeSquared2D() > 25.f;

	if (bIsSprinting && bMoving)
	{
		const float Next = Current - AshRules::Stamina::SprintPerSecond * StaminaTickInterval;
		LastStaminaSpendTime = Now;
		SetAttribute(UAshAttributeSet::GetStaminaAttribute(), FMath::Max(Next, 0.f));
		if (Next <= 0.f)
		{
			SetSprinting(false);
		}
	}
	else if (Current < Max && Now - LastStaminaSpendTime >= AshRules::Stamina::RegenDelay)
	{
		const float Next = Current + AshRules::Stamina::RegenPerSecond * StaminaTickInterval;
		SetAttribute(UAshAttributeSet::GetStaminaAttribute(), FMath::Min(Next, Max));
	}
}

// ---------------------------------------------------------------- roll (21.2, 21.3)

void AAshHeroCharacter::RollPressed()
{
	UCharacterMovementComponent* Movement = GetCharacterMovement();
	if (bIsRolling || Movement->IsFalling() || !TrySpendStamina(AshRules::Stamina::Roll))
	{
		return;
	}

	FVector Direction = GetLastMovementInputVector();
	Direction.Z = 0.f;
	if (!Direction.Normalize())
	{
		Direction = GetActorForwardVector();
	}
	RollDirection = Direction;
	SetActorRotation(Direction.Rotation());

	bIsRolling = true;
	SetStateTag(AshTags::State_Rolling, true);
	Movement->MaxWalkSpeed = RollSpeed;
	Movement->MaxAcceleration = 10000.f;
	SetActorTickEnabled(true);

	const AshRules::FDifficultyParams Params =
		AshRules::DifficultyParams(static_cast<AshRules::EDifficulty>(UAshProjectSettings::Get()->Difficulty));
	FTimerManager& Timers = GetWorldTimerManager();
	Timers.SetTimer(InvulnerabilityStartTimer, this, &AAshHeroCharacter::BeginInvulnerability,
		AshRules::RollInvulnerabilityStart, false);
	Timers.SetTimer(InvulnerabilityEndTimer, this, &AAshHeroCharacter::EndInvulnerability,
		AshRules::RollInvulnerabilityStart + Params.RollInvulnerability, false);
	Timers.SetTimer(RollEndTimer, this, &AAshHeroCharacter::EndRoll, AshRules::RollDuration, false);
}

void AAshHeroCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (bIsRolling)
	{
		AddMovementInput(RollDirection, 1.f);
	}
}

void AAshHeroCharacter::BeginInvulnerability()
{
	bInvulnerable = true;
	SetStateTag(AshTags::State_Invulnerable, true);
}

void AAshHeroCharacter::EndInvulnerability()
{
	bInvulnerable = false;
	SetStateTag(AshTags::State_Invulnerable, false);
}

void AAshHeroCharacter::EndRoll()
{
	bIsRolling = false;
	SetStateTag(AshTags::State_Rolling, false);
	SetActorTickEnabled(false);
	UCharacterMovementComponent* Movement = GetCharacterMovement();
	Movement->MaxAcceleration = DefaultMaxAcceleration;
	Movement->MaxWalkSpeed = bIsSprinting ? SprintSpeed : RunSpeed;
}
