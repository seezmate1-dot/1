// Copyright (c) AshRPG project. All rights reserved.

#include "AshAttributeSet.h"

#include "Net/UnrealNetwork.h"
#include "Rules/AshRulesCombat.h"

UAshAttributeSet::UAshAttributeSet()
{
	// 21.1: level 1 hero. Enemies override these from their data table.
	const AshRules::FPrimaryStats Start;
	InitStrength(static_cast<float>(Start.Strength));
	InitDexterity(static_cast<float>(Start.Dexterity));
	InitIntelligence(static_cast<float>(Start.Intelligence));
	InitVitality(static_cast<float>(Start.Vitality));
	InitArmor(10.f);
	RecalculateDerived(true);
}

void UAshAttributeSet::RecalculateDerived(bool bRefill)
{
	AshRules::FPrimaryStats Stats;
	Stats.Strength = FMath::RoundToInt(GetStrength());
	Stats.Dexterity = FMath::RoundToInt(GetDexterity());
	Stats.Intelligence = FMath::RoundToInt(GetIntelligence());
	Stats.Vitality = FMath::RoundToInt(GetVitality());

	InitMaxHealth(AshRules::MaxHealthFor(Stats));
	InitMaxStamina(AshRules::MaxStaminaFor(Stats));
	InitMaxMana(AshRules::MaxManaFor(Stats));

	if (bRefill)
	{
		InitHealth(GetMaxHealth());
		InitStamina(GetMaxStamina());
		InitMana(GetMaxMana());
	}
}

void UAshAttributeSet::ClampResource(const FGameplayAttribute& Attribute, float& NewValue) const
{
	if (Attribute == GetHealthAttribute())
	{
		NewValue = FMath::Clamp(NewValue, 0.f, GetMaxHealth());
	}
	else if (Attribute == GetStaminaAttribute())
	{
		NewValue = FMath::Clamp(NewValue, 0.f, GetMaxStamina());
	}
	else if (Attribute == GetManaAttribute())
	{
		NewValue = FMath::Clamp(NewValue, 0.f, GetMaxMana());
	}
	else if (Attribute == GetArmorAttribute())
	{
		NewValue = FMath::Max(NewValue, 0.f);
	}
}

void UAshAttributeSet::PreAttributeChange(const FGameplayAttribute& Attribute, float& NewValue)
{
	Super::PreAttributeChange(Attribute, NewValue);
	ClampResource(Attribute, NewValue);
}

void UAshAttributeSet::PreAttributeBaseChange(const FGameplayAttribute& Attribute, float& NewValue) const
{
	Super::PreAttributeBaseChange(Attribute, NewValue);
	ClampResource(Attribute, NewValue);
}

void UAshAttributeSet::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);

	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Strength, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Dexterity, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Intelligence, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Vitality, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Health, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, MaxHealth, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Stamina, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, MaxStamina, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Mana, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, MaxMana, COND_None, REPNOTIFY_Always);
	DOREPLIFETIME_CONDITION_NOTIFY(UAshAttributeSet, Armor, COND_None, REPNOTIFY_Always);
}

void UAshAttributeSet::OnRep_Strength(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Strength, OldValue); }
void UAshAttributeSet::OnRep_Dexterity(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Dexterity, OldValue); }
void UAshAttributeSet::OnRep_Intelligence(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Intelligence, OldValue); }
void UAshAttributeSet::OnRep_Vitality(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Vitality, OldValue); }
void UAshAttributeSet::OnRep_Health(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Health, OldValue); }
void UAshAttributeSet::OnRep_MaxHealth(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, MaxHealth, OldValue); }
void UAshAttributeSet::OnRep_Stamina(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Stamina, OldValue); }
void UAshAttributeSet::OnRep_MaxStamina(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, MaxStamina, OldValue); }
void UAshAttributeSet::OnRep_Mana(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Mana, OldValue); }
void UAshAttributeSet::OnRep_MaxMana(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, MaxMana, OldValue); }
void UAshAttributeSet::OnRep_Armor(const FGameplayAttributeData& OldValue) { GAMEPLAYATTRIBUTE_REPNOTIFY(UAshAttributeSet, Armor, OldValue); }
