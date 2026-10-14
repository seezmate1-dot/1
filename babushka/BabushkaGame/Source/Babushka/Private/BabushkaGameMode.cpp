// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaGameMode.h"

#include "BabushkaGrandma.h"
#include "BabushkaHUD.h"
#include "BabushkaKid.h"
#include "BabushkaSettings.h"
#include "BabushkaText.h"
#include "EngineUtils.h"
#include "GameFramework/PlayerController.h"
#include "Kismet/GameplayStatics.h"

namespace
{
	constexpr int32 StartMinutes = 22 * 60;
	constexpr int32 NightMinutes = 7 * 60;          // until 05:00
	constexpr int32 SleepFrom = 3 * 60;             // 01:00
	constexpr int32 SleepTo = 4 * 60 + 30;          // 02:30
}

ABabushkaGameMode::ABabushkaGameMode()
{
	PrimaryActorTick.bCanEverTick = true;
	DefaultPawnClass = ABabushkaKid::StaticClass();
	HUDClass = ABabushkaHUD::StaticClass();
}

void ABabushkaGameMode::BeginPlay()
{
	Super::BeginPlay();
	NightSeconds = FMath::Max(60.f, UBabushkaSettings::Get()->NightSeconds);
	for (TActorIterator<ABabushkaGrandma> It(GetWorld()); It; ++It)
	{
		Grandma = *It;
		break;
	}
}

void ABabushkaGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (ToastLeft > 0.f)
	{
		ToastLeft -= DeltaSeconds;
	}
	if (bNightOver)
	{
		return;
	}
	Elapsed += DeltaSeconds;
	if (Elapsed >= NightSeconds)
	{
		Elapsed = NightSeconds;
		FinishNight(false);
	}
}

int32 ABabushkaGameMode::GetClockMinutes() const
{
	return FMath::FloorToInt(Elapsed / NightSeconds * NightMinutes);
}

FString ABabushkaGameMode::GetClockText() const
{
	const int32 Minutes = StartMinutes + GetClockMinutes();
	return FString::Printf(TEXT("%02d:%02d"), (Minutes / 60) % 24, Minutes % 60);
}

bool ABabushkaGameMode::IsSleepTime() const
{
	const int32 Minutes = GetClockMinutes();
	return Minutes >= SleepFrom && Minutes < SleepTo;
}

ABabushkaKid* ABabushkaGameMode::GetKid() const
{
	const APlayerController* PC = GetWorld()->GetFirstPlayerController();
	return PC ? Cast<ABabushkaKid>(PC->GetPawn()) : nullptr;
}

void ABabushkaGameMode::FinishNight(bool bKidEscaped)
{
	if (bNightOver)
	{
		return;
	}
	bNightOver = true;
	bEscaped = bKidEscaped;
	const ABabushkaKid* Kid = GetKid();
	if (bEscaped && Kid)
	{
		// GDD 4: escaping early, with the phone and money, scores more.
		const int32 MinutesLeft = NightMinutes - GetClockMinutes();
		Score = 1000 + (Kid->bHasPhone ? 300 : 0) + (Kid->bHasMoney ? 300 : 0) + MinutesLeft * 2 + Kid->GetFullness() * 50;
	}
	else
	{
		Score = Kid ? Kid->PiesEaten * 10 : 0;
	}
}

void ABabushkaGameMode::RestartNight()
{
	UGameplayStatics::OpenLevel(this, FName(*UGameplayStatics::GetCurrentLevelName(this)));
}

void ABabushkaGameMode::ShowToast(FName Id, float Seconds)
{
	Toast = BabushkaText::Toast(Id);
	ToastLeft = Seconds;
}
