// Copyright (c) Babushka project. All rights reserved.
// Grandma: body, flashlight (her visible cone of sight), speech. The brain is ABabushkaGrandmaAI.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "BabushkaText.h"
#include "BabushkaTypes.h"
#include "BabushkaGrandma.generated.h"

class ABabushkaKid;
class USpotLightComponent;

UCLASS()
class BABUSHKA_API ABabushkaGrandma : public ACharacter
{
	GENERATED_BODY()

public:
	ABabushkaGrandma();

	virtual void PostInitializeComponents() override;

	void Say(EGrandmaLine Line, float Seconds = 2.6f);
	const FText& GetLine() const { return Line; }
	bool HasLine() const;

	EGrandmaMode GetMode() const;
	bool IsChasing() const { return GetMode() == EGrandmaMode::Chase; }
	float GetSuspicion() const;
	bool CanSeeNow(const ABabushkaKid* Kid) const;
	void OnTVTurnedOn(const FVector& Seat, const FVector& Screen);

	void SetFlashlightAlarm(bool bAlarm);

protected:
	// GDD 12: the flashlight beam on the floor shows where grandma looks.
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<USpotLightComponent> Flashlight;

	// Builds the nav mesh around grandma at runtime (the whole dacha fits in the radius).
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<class UNavigationInvokerComponent> NavInvoker;

private:
	FText Line;
	float LineUntil = 0.f;
};
