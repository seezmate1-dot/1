// Copyright (c) Babushka project. All rights reserved.

#include "BabushkaInteractable.h"

#include "BabushkaGameMode.h"
#include "BabushkaGrandma.h"
#include "BabushkaKid.h"
#include "BabushkaText.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"

ABabushkaInteractable::ABabushkaInteractable()
{
	PrimaryActorTick.bCanEverTick = false;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	RootComponent = Mesh;
	// Interactables must overlap the kid's interact sphere.
	Mesh->SetGenerateOverlapEvents(true);
}

bool ABabushkaInteractable::IsAvailable() const
{
	switch (Kind)
	{
	case EBabushkaInteractKind::Hide:
	case EBabushkaInteractKind::TV:
		return true;
	case EBabushkaInteractKind::Pie:
		return PiesLeft > 0;
	default:
		return !bUsed;
	}
}

FText ABabushkaInteractable::GetLabel() const
{
	return BabushkaText::InteractLabel(static_cast<uint8>(Kind));
}

void ABabushkaInteractable::Use(ABabushkaKid* Kid)
{
	ABabushkaGameMode* Mode = GetWorld()->GetAuthGameMode<ABabushkaGameMode>();
	if (!Kid || !Mode)
	{
		return;
	}
	ABabushkaGrandma* Grandma = Mode->GetGrandma();
	const FVector Here = GetActorLocation();

	switch (Kind)
	{
	case EBabushkaInteractKind::Hide:
	{
		if (Kid->IsBall()) { Mode->ShowToast(TEXT("BallCantHide")); return; }
		if (bNeedsThinKid && Kid->GetFullness() > BabushkaFullness::MaxLevelForWardrobe) { Mode->ShowToast(TEXT("WardrobeFull")); return; }
		// Hiding in front of grandma does not work: she remembers where you went (GDD 6).
		const bool bSeen = Grandma && Grandma->IsChasing() && Grandma->CanSeeNow(Kid);
		Kid->HideIn(this, bSeen);
		Mode->ShowToast(bSeen ? TEXT("HiddenSeen") : TEXT("Hidden"));
		return;
	}
	case EBabushkaInteractKind::Shoes:
		bUsed = true; Kid->bHasShoes = true; Mode->ShowToast(TEXT("Shoes"));
		break;
	case EBabushkaInteractKind::Phone:
		bUsed = true; Kid->bHasPhone = true; Kid->MakeKidNoise(750.f); Mode->ShowToast(TEXT("Phone"));
		break;
	case EBabushkaInteractKind::Money:
		bUsed = true; Kid->bHasMoney = true; Kid->MakeKidNoise(550.f); Mode->ShowToast(TEXT("Money"));
		break;
	case EBabushkaInteractKind::Key:
		bUsed = true; Kid->bHasKey = true; Kid->MakeKidNoise(2000.f); Mode->ShowToast(TEXT("Key"));
		break;
	case EBabushkaInteractKind::Pie:
		if (Kid->IsBall()) { Mode->ShowToast(TEXT("PieBall")); return; }
		if (PiesLeft <= 0) { Mode->ShowToast(TEXT("PieEmpty")); return; }
		--PiesLeft;
		++Kid->PiesEaten;
		Kid->ChangeFullness(1);
		Kid->StartSugarRush();
		Mode->ShowToast(TEXT("PieRush"));
		break;
	case EBabushkaInteractKind::TV:
	{
		const float Now = GetWorld()->GetTimeSeconds();
		if (Now < TVCooldownUntil) { Mode->ShowToast(TEXT("TVCooldown")); return; }
		TVCooldownUntil = Now + 40.f;
		if (Grandma)
		{
			// Grandma sits two meters in front of the screen.
			Grandma->OnTVTurnedOn(Here + GetActorForwardVector() * 200.f, Here);
		}
		Mode->ShowToast(TEXT("TVOn"));
		break;
	}
	case EBabushkaInteractKind::Gate:
		if (!Kid->bHasKey) { Mode->ShowToast(TEXT("GateLocked")); return; }
		bUsed = true;
		SetActorEnableCollision(false);
		SetActorHiddenInGame(true);
		Kid->MakeKidNoise(1500.f);
		Mode->ShowToast(TEXT("GateOpen"));
		break;
	}
}
