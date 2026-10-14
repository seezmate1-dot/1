// Copyright (c) AshRPG project. All rights reserved.

using UnrealBuildTool;

// Общие типы, теги, логи, сохранения и интерфейсы сервисов (Backend, Trade).
public class AshCore : ModuleRules
{
	public AshCore(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] { "Core", "CoreUObject", "Engine", "GameplayTags" });
	}
}
