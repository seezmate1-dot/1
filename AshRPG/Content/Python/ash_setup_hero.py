"""Stage 1 setup. Run in the editor after adding the Third Person content pack:
Tools > Execute Python Script > this file.

1. Finds the template mannequin (mesh and its Animation Blueprint) and stores them in
   Project Settings > Game > AshRPG, so the C++ hero has a body and animations.
2. Removes a GameMode override from the open level, so the level uses AshGameMode.
Paths differ between engine versions, so the assets are searched, not hard-coded.
"""
import unreal

SEARCH_ROOT = "/Game/Characters"


def find(registry, class_name, words):
    """First asset of the class whose name contains one of the words, in order of preference."""
    found = [d for d in registry.get_assets_by_path(SEARCH_ROOT, recursive=True)
             if str(d.asset_class_path.asset_name) == class_name]
    for word in words:
        for data in found:
            if word.lower() in str(data.asset_name).lower():
                return data
    return found[0] if found else None


def main():
    registry = unreal.AssetRegistryHelpers.get_asset_registry()

    mesh_data = find(registry, "SkeletalMesh", ["SKM_Manny", "Manny", "Quinn"])
    anim_data = find(registry, "AnimBlueprint", ["ABP_Manny", "ABP_Unarmed", "Manny", "Unarmed", "ABP_"])
    if mesh_data is None or anim_data is None:
        unreal.log_error("Mannequin not found in %s. Add the Third Person content pack first "
                         "(docs/SETUP.md, step 6)." % SEARCH_ROOT)
        return

    mesh = unreal.load_asset(str(mesh_data.package_name))
    # The generated class of a Blueprint is "<package>.<name>_C".
    anim_class = unreal.load_object(None, "%s.%s_C" % (anim_data.package_name, anim_data.asset_name))
    if mesh is None or anim_class is None:
        unreal.log_error("Could not load %s or %s" % (mesh_data.package_name, anim_data.package_name))
        return

    settings = unreal.get_default_object(unreal.AshProjectSettings)
    settings.set_hero_assets(mesh, anim_class)
    unreal.log("Hero mesh: %s" % mesh_data.package_name)
    unreal.log("Hero animation: %s" % anim_data.package_name)

    world = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
    if world:
        world_settings = world.get_world_settings()
        override = world_settings.get_editor_property("default_game_mode")
        if override:
            world_settings.set_editor_property("default_game_mode", None)
            unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).save_current_level()
            unreal.log("Removed GameMode override %s from the level, it now uses AshGameMode." % override.get_name())

    unreal.log("Stage 1 setup done. Press Play: the hero should run, sprint (Shift), roll (Space) and jump (F).")


main()
