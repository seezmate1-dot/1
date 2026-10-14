// Copyright (c) AshRPG project. All rights reserved.
// Hero and enemy attributes on the Gameplay Ability System (TZ sections 10 and 21.1).

#pragma once

#include "CoreMinimal.h"
#include "AttributeSet.h"
#include "AbilitySystemComponent.h"
#include "AshAttributeSet.generated.h"

#define ASH_ATTRIBUTE_ACCESSORS(ClassName, PropertyName) \
	GAMEPLAYATTRIBUTE_PROPERTY_GETTER(ClassName, PropertyName) \
	GAMEPLAYATTRIBUTE_VALUE_GETTER(PropertyName) \
	GAMEPLAYATTRIBUTE_VALUE_SETTER(PropertyName) \
	GAMEPLAYATTRIBUTE_VALUE_INITTER(PropertyName)

UCLASS()
class ASHCOMBAT_API UAshAttributeSet : public UAttributeSet
{
	GENERATED_BODY()

public:
	UAshAttributeSet();

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void PreAttributeChange(const FGameplayAttribute& Attribute, float& NewValue) override;
	virtual void PreAttributeBaseChange(const FGameplayAttribute& Attribute, float& NewValue) const override;

	// Primary stats (21.1).
	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Strength, Category = "Ash|Stats")
	FGameplayAttributeData Strength;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Strength)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Dexterity, Category = "Ash|Stats")
	FGameplayAttributeData Dexterity;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Dexterity)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Intelligence, Category = "Ash|Stats")
	FGameplayAttributeData Intelligence;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Intelligence)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Vitality, Category = "Ash|Stats")
	FGameplayAttributeData Vitality;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Vitality)

	// Resources.
	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Health, Category = "Ash|Vitals")
	FGameplayAttributeData Health;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Health)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_MaxHealth, Category = "Ash|Vitals")
	FGameplayAttributeData MaxHealth;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, MaxHealth)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Stamina, Category = "Ash|Vitals")
	FGameplayAttributeData Stamina;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Stamina)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_MaxStamina, Category = "Ash|Vitals")
	FGameplayAttributeData MaxStamina;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, MaxStamina)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Mana, Category = "Ash|Vitals")
	FGameplayAttributeData Mana;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Mana)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_MaxMana, Category = "Ash|Vitals")
	FGameplayAttributeData MaxMana;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, MaxMana)

	UPROPERTY(BlueprintReadOnly, ReplicatedUsing = OnRep_Armor, Category = "Ash|Defense")
	FGameplayAttributeData Armor;
	ASH_ATTRIBUTE_ACCESSORS(UAshAttributeSet, Armor)

	// Recomputes MaxHealth, MaxStamina and MaxMana from the primary stats (21.1).
	void RecalculateDerived(bool bRefill);

protected:
	UFUNCTION() void OnRep_Strength(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Dexterity(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Intelligence(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Vitality(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Health(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_MaxHealth(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Stamina(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_MaxStamina(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Mana(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_MaxMana(const FGameplayAttributeData& OldValue);
	UFUNCTION() void OnRep_Armor(const FGameplayAttributeData& OldValue);

private:
	void ClampResource(const FGameplayAttribute& Attribute, float& NewValue) const;
};
