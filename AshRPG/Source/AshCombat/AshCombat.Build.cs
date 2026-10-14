// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// Бой на GAS: атрибуты, способности, статусы, оглушение.
public class AshCombat : ModuleRules
{
	public AshCombat(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "GameplayTags", "GameplayAbilities", "GameplayTasks", "AshCore" });
	}
}
