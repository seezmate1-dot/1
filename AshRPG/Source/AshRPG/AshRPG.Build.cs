// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// Главный модуль игры: режим игры, персонаж, контроллер.
public class AshRPG : ModuleRules
{
	public AshRPG(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput", "DeveloperSettings", "GameplayTags", "GameplayAbilities", "GameplayTasks", "AshCore", "AshCombat", "AshInventory", "AshQuests", "AshUI" });
	}
}
