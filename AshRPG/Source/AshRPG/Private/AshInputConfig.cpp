// Copyright (c) AshRPG project. All rights reserved.

#include "AshInputConfig.h"

#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"

namespace
{
	UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(Outer, Name);
		Action->ValueType = Type;
		return Action;
	}

	void AddNegate(UInputMappingContext* Context, FEnhancedActionKeyMapping& Mapping, bool bX, bool bY)
	{
		UInputModifierNegate* Negate = NewObject<UInputModifierNegate>(Context);
		Negate->bX = bX;
		Negate->bY = bY;
		Negate->bZ = false;
		Mapping.Modifiers.Add(Negate);
	}

	void AddSwizzle(UInputMappingContext* Context, FEnhancedActionKeyMapping& Mapping)
	{
		// Default order YXZ: a 1D key value goes to Y (forward / back).
		Mapping.Modifiers.Add(NewObject<UInputModifierSwizzleAxis>(Context));
	}

	void AddDeadZone(UInputMappingContext* Context, FEnhancedActionKeyMapping& Mapping)
	{
		Mapping.Modifiers.Add(NewObject<UInputModifierDeadZone>(Context));
	}
}

void UAshInputConfig::Build()
{
	Move = MakeAction(this, TEXT("IA_Move"), EInputActionValueType::Axis2D);
	Look = MakeAction(this, TEXT("IA_Look"), EInputActionValueType::Axis2D);
	Jump = MakeAction(this, TEXT("IA_Jump"), EInputActionValueType::Boolean);
	Sprint = MakeAction(this, TEXT("IA_Sprint"), EInputActionValueType::Boolean);
	Roll = MakeAction(this, TEXT("IA_Roll"), EInputActionValueType::Boolean);

	DefaultContext = NewObject<UInputMappingContext>(this, TEXT("IMC_Default"));
	UInputMappingContext* Context = DefaultContext;

	// Movement: WASD and the left stick. X = right, Y = forward.
	AddSwizzle(Context, Context->MapKey(Move, EKeys::W));
	{
		FEnhancedActionKeyMapping& Back = Context->MapKey(Move, EKeys::S);
		AddSwizzle(Context, Back);
		AddNegate(Context, Back, true, true);
	}
	AddNegate(Context, Context->MapKey(Move, EKeys::A), true, false);
	Context->MapKey(Move, EKeys::D);
	AddDeadZone(Context, Context->MapKey(Move, EKeys::Gamepad_Left2D));

	// Camera: mouse and the right stick. Y is negated like in the Epic Third Person template.
	AddNegate(Context, Context->MapKey(Look, EKeys::Mouse2D), false, true);
	{
		FEnhancedActionKeyMapping& Stick = Context->MapKey(Look, EKeys::Gamepad_Right2D);
		AddDeadZone(Context, Stick);
		AddNegate(Context, Stick, false, true);
	}

	// Actions. Space is the roll because dodging matters more than jumping in this combat.
	Context->MapKey(Roll, EKeys::SpaceBar);
	Context->MapKey(Roll, EKeys::Gamepad_FaceButton_Right);
	Context->MapKey(Jump, EKeys::F);
	Context->MapKey(Jump, EKeys::Gamepad_FaceButton_Bottom);
	Context->MapKey(Sprint, EKeys::LeftShift);
	Context->MapKey(Sprint, EKeys::Gamepad_LeftThumbstick);
}
