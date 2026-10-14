// Copyright (c) Babushka project. All rights reserved.
// Canvas HUD: clock, items, fullness, hints, grandma's speech, the result card.
// Drawn in code so the prototype needs no widget assets; the real UI comes later with UMG.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "BabushkaHUD.generated.h"

UCLASS()
class BABUSHKA_API ABabushkaHUD : public AHUD
{
	GENERATED_BODY()

public:
	virtual void DrawHUD() override;

private:
	void DrawPanel(float X, float Y, float W, float H, const FLinearColor& Color);
	void DrawLabel(const FString& Text, float X, float Y, const FLinearColor& Color, class UFont* Font, float Scale = 1.f, bool bCentered = false);
	void DrawBubble(const FVector& WorldLocation, const FString& Text, bool bAlarm);
	void DrawResult();
};
