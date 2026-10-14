// Copyright (c) Babushka project. All rights reserved.

using UnrealBuildTool;

public class BabushkaGameEditorTarget : TargetRules
{
	public BabushkaGameEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("Babushka");
	}
}
