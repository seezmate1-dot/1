// Copyright (c) Babushka project. All rights reserved.
// One night (GDD 4): the clock from 22:00 to 05:00, grandma's nap, escape and the result card.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "BabushkaGameMode.generated.h"

class ABabushkaGrandma;
class ABabushkaKid;

UCLASS()
class BABUSHKA_API ABabushkaGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	ABabushkaGameMode();

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	// Clock: 22:00 -> 05:00 is 420 game minutes over NightSeconds of real time.
	int32 GetClockMinutes() const;
	FString GetClockText() const;
	bool IsSleepTime() const;

	bool IsNightOver() const { return bNightOver; }
	bool DidEscape() const { return bEscaped; }
	int32 GetScore() const { return Score; }
	void FinishNight(bool bKidEscaped);
	void RestartNight();

	void ShowToast(FName Id, float Seconds = 2.5f);
	const FText& GetToast() const { return Toast; }
	bool HasToast() const { return ToastLeft > 0.f; }

	ABabushkaGrandma* GetGrandma() const { return Grandma.Get(); }
	ABabushkaKid* GetKid() const;

private:
	float Elapsed = 0.f;
	float NightSeconds = 600.f;
	bool bNightOver = false;
	bool bEscaped = false;
	int32 Score = 0;
	FText Toast;
	float ToastLeft = 0.f;
	TWeakObjectPtr<ABabushkaGrandma> Grandma;
};
