// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// Квесты, журнал, диалоги, охоты.
public class AshQuests : ModuleRules
{
	public AshQuests(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "GameplayTags", "AshCore" });
	}
}
