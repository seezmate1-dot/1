// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// Предметы, инвентарь, экипировка, торговля, крафт.
public class AshInventory : ModuleRules
{
	public AshInventory(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "GameplayTags", "AshCore" });
	}
}
