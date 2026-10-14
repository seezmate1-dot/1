// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// HUD, меню и окна на Common UI.
public class AshUI : ModuleRules
{
	public AshUI(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "GameplayTags", "UMG", "CommonUI", "AshCore" });

		PrivateDependencyModuleNames.AddRange(new string[] { "Slate", "SlateCore" });
	}
}
