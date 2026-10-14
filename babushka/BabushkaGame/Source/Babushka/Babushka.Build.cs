// Copyright (c) Babushka project. All rights reserved.

using UnrealBuildTool;

public class Babushka : ModuleRules
{
	public Babushka(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput",
			"AIModule", "NavigationSystem", "GameplayTasks", "DeveloperSettings"
		});

		// Brush helper for the dacha builder script (editor only).
		if (Target.bBuildEditor)
		{
			PrivateDependencyModuleNames.Add("UnrealEd");
		}
	}
}
