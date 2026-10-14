// Copyright (c) Babushka project. All rights reserved.
// Anything the kid can use with E: hiding spots, items, pies, the TV, the gate.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "BabushkaTypes.h"
#include "BabushkaInteractable.generated.h"

class ABabushkaKid;
class UStaticMeshComponent;

UCLASS()
class BABUSHKA_API ABabushkaInteractable : public AActor
{
	GENERATED_BODY()

public:
	ABabushkaInteractable();

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Babushka")
	EBabushkaInteractKind Kind = EBabushkaInteractKind::Hide;

	// Hiding spots that need a thin kid (wardrobe).
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Babushka", meta = (EditCondition = "Kind == EBabushkaInteractKind::Hide"))
	bool bNeedsThinKid = false;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category = "Babushka", meta = (EditCondition = "Kind == EBabushkaInteractKind::Pie"))
	int32 PiesLeft = 3;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Babushka")
	TObjectPtr<UStaticMeshComponent> Mesh;

	bool IsAvailable() const;
	void Use(ABabushkaKid* Kid);
	FText GetLabel() const;

private:
	bool bUsed = false;
	float TVCooldownUntil = 0.f;
};
