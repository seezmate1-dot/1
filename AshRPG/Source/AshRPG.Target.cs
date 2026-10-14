// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

public class AshRPGTarget : TargetRules
{
	public AshRPGTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.AddRange(new string[] { "AshCore", "AshCombat", "AshInventory", "AshQuests", "AshUI", "AshRPG" });
	}
}
