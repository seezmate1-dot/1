// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

public class AshRPGEditorTarget : TargetRules
{
	public AshRPGEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.AddRange(new string[] { "AshCore", "AshCombat", "AshInventory", "AshQuests", "AshUI", "AshRPG" });
	}
}
