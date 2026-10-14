// Copyright (c) Babushka project. All rights reserved.

using UnrealBuildTool;

public class BabushkaGameTarget : TargetRules
{
	public BabushkaGameTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("Babushka");
	}
}
