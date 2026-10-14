// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaHUD.h"

#include "BabushkaGameMode.h"
#include "BabushkaGrandma.h"
#include "BabushkaInteractable.h"
#include "BabushkaKid.h"
#include "BabushkaText.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/Font.h"

namespace
{
	const FLinearColor Milk(0.95f, 0.91f, 0.84f);
	const FLinearColor Crust(0.91f, 0.64f, 0.23f);
	const FLinearColor Scarf(0.82f, 0.29f, 0.24f);
	const FLinearColor Muted(0.6f, 0.66f, 0.69f);
	const FLinearColor Night(0.055f, 0.094f, 0.133f, 0.82f);

	FString Item(bool bHas, FName Id)
	{
		return FString::Printf(TEXT("%s %s"), bHas ? TEXT("[x]") : TEXT("[ ]"), *BabushkaText::Toast(Id).ToString());
	}
}

void ABabushkaHUD::DrawPanel(float X, float Y, float W, float H, const FLinearColor& Color)
{
	DrawRect(Color, X, Y, W, H);
}

void ABabushkaHUD::DrawLabel(const FString& Text, float X, float Y, const FLinearColor& Color, UFont* Font, float Scale, bool bCentered)
{
	float W = 0.f, H = 0.f;
	GetTextSize(Text, W, H, Font, Scale);
	DrawText(Text, Color, bCentered ? X - W / 2.f : X, Y, Font, Scale);
}

void ABabushkaHUD::DrawBubble(const FVector& WorldLocation, const FString& Text, bool bAlarm)
{
	const FVector Screen = Project(WorldLocation, false);
	if (Screen.Z <= 0.f)
	{
		return; // behind the camera
	}
	UFont* Font = GEngine->GetMediumFont();
	float W = 0.f, H = 0.f;
	GetTextSize(Text, W, H, Font, 1.f);
	const float X = Screen.X - W / 2.f - 10.f, Y = Screen.Y - H - 16.f;
	DrawPanel(X, Y, W + 20.f, H + 10.f, bAlarm ? FLinearColor(0.96f, 0.79f, 0.72f) : FLinearColor(0.95f, 0.91f, 0.84f, 0.95f));
	DrawText(Text, FLinearColor(0.1f, 0.07f, 0.02f), X + 10.f, Y + 5.f, Font, 1.f);
}

void ABabushkaHUD::DrawHUD()
{
	Super::DrawHUD();
	ABabushkaGameMode* Mode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	ABabushkaKid* Kid = Mode ? Mode->GetKid() : nullptr;
	if (!Mode || !Kid || !Canvas)
	{
		return;
	}
	UFont* Large = GEngine->GetLargeFont();
	UFont* Medium = GEngine->GetMediumFont();
	const float S = Canvas->ClipY / 1080.f; // scale with the screen height

	// Top-left panel: clock, items, fullness.
	DrawPanel(16.f * S, 16.f * S, 340.f * S, 210.f * S, Night);
	DrawLabel(Mode->GetClockText(), 32.f * S, 26.f * S, Milk, Large, 1.6f * S);
	DrawLabel(Item(Kid->bHasShoes, TEXT("HudShoes")), 32.f * S, 80.f * S, Kid->bHasShoes ? Crust : Muted, Medium, S);
	DrawLabel(Item(Kid->bHasPhone, TEXT("HudPhone")), 32.f * S, 104.f * S, Kid->bHasPhone ? Crust : Muted, Medium, S);
	DrawLabel(Item(Kid->bHasMoney, TEXT("HudMoney")), 32.f * S, 128.f * S, Kid->bHasMoney ? Crust : Muted, Medium, S);
	DrawLabel(Item(Kid->bHasKey, TEXT("HudKey")), 32.f * S, 152.f * S, Kid->bHasKey ? Crust : Muted, Medium, S);
	const float Fill = static_cast<float>(Kid->GetFullness()) / static_cast<float>(BabushkaFullness::MaxLevel);
	DrawPanel(32.f * S, 186.f * S, 120.f * S, 12.f * S, FLinearColor(0.09f, 0.15f, 0.2f));
	DrawPanel(32.f * S, 186.f * S, 120.f * S * Fill, 12.f * S, Crust);
	DrawLabel(BabushkaText::FullnessName(Kid->GetFullness()).ToString(), 164.f * S, 180.f * S, Milk, Medium, S);

	// Grandma: speech bubble and suspicion marks.
	if (ABabushkaGrandma* Grandma = Mode->GetGrandma())
	{
		const FVector Head = Grandma->GetActorLocation() + FVector(0.f, 0.f, 130.f);
		if (Grandma->HasLine())
		{
			DrawBubble(Head, Grandma->GetLine().ToString(), Grandma->IsChasing());
		}
		else if (Grandma->GetSuspicion() > 0.05f && !Grandma->IsChasing())
		{
			const FVector Screen = Project(Head, false);
			if (Screen.Z > 0.f)
			{
				DrawLabel(Grandma->GetSuspicion() > 0.6f ? TEXT("?!") : TEXT("?"), Screen.X, Screen.Y - 30.f * S, Crust, Large, 1.4f * S, true);
			}
		}
	}

	// Hint over the nearest usable thing.
	if (!Kid->IsHiddenInSpot() && !Kid->IsBeingFed() && !Mode->IsNightOver())
	{
		if (ABabushkaInteractable* Near = Kid->FindNearestInteractable())
		{
			const FVector Screen = Project(Near->GetActorLocation() + FVector(0.f, 0.f, 60.f), false);
			if (Screen.Z > 0.f)
			{
				DrawLabel(FString::Printf(TEXT("E: %s"), *Near->GetLabel().ToString()), Screen.X, Screen.Y, Milk, Medium, S, true);
			}
		}
	}

	// Toast at the bottom.
	if (Mode->HasToast())
	{
		const FString Text = Mode->GetToast().ToString();
		float W = 0.f, H = 0.f;
		GetTextSize(Text, W, H, Medium, 1.2f * S);
		DrawPanel(Canvas->ClipX / 2.f - W / 2.f - 16.f, Canvas->ClipY - 90.f * S, W + 32.f, H + 16.f, Night);
		DrawText(Text, Milk, Canvas->ClipX / 2.f - W / 2.f, Canvas->ClipY - 82.f * S, Medium, 1.2f * S);
	}

	if (Mode->IsNightOver())
	{
		DrawResult();
	}
}

void ABabushkaHUD::DrawResult()
{
	ABabushkaGameMode* Mode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	ABabushkaKid* Kid = Mode->GetKid();
	UFont* Large = GEngine->GetLargeFont();
	UFont* Medium = GEngine->GetMediumFont();
	const float S = Canvas->ClipY / 1080.f;
	const float CX = Canvas->ClipX / 2.f;
	const bool bEscaped = Mode->DidEscape();

	DrawPanel(0.f, 0.f, Canvas->ClipX, Canvas->ClipY, FLinearColor(0.04f, 0.07f, 0.09f, 0.86f));
	float Y = Canvas->ClipY * 0.28f;
	DrawLabel(BabushkaText::EndTitle(bEscaped, Kid->GetFullness(), Kid->PiesEaten).ToString(), CX, Y, Crust, Large, 2.2f * S, true);
	Y += 80.f * S;
	DrawLabel(BabushkaText::EndLine(bEscaped, Kid->GetFullness(), Kid->PiesEaten).ToString(), CX, Y, Milk, Medium, 1.4f * S, true);
	Y += 70.f * S;
	const FString Stats[] = {
		FString::Printf(TEXT("%s: %s"), *BabushkaText::Toast(TEXT("StatTime")).ToString(), *Mode->GetClockText()),
		FString::Printf(TEXT("%s: %d"), *BabushkaText::Toast(TEXT("StatScore")).ToString(), Mode->GetScore()),
		FString::Printf(TEXT("%s: %d"), *BabushkaText::Toast(TEXT("StatPies")).ToString(), Kid->PiesEaten),
		FString::Printf(TEXT("%s: %d"), *BabushkaText::Toast(TEXT("StatCaught")).ToString(), Kid->TimesCaught),
		FString::Printf(TEXT("%s: %s"), *BabushkaText::Toast(TEXT("HudFullness")).ToString(), *BabushkaText::FullnessName(Kid->GetFullness()).ToString())
	};
	for (const FString& Line : Stats)
	{
		DrawLabel(Line, CX, Y, Milk, Medium, 1.1f * S, true);
		Y += 34.f * S;
	}
	Y += 30.f * S;
	DrawLabel(BabushkaText::Toast(TEXT("Restart")).ToString(), CX, Y, Crust, Medium, 1.2f * S, true);
}
