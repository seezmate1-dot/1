"""Stage 0 check. Run in the editor: Tools > Execute Python Script > this file.

Prints a report to the Output Log and saves it to Saved/AshStage0Report.txt.
Send the report back so the settings can be verified.
"""
import os
import unreal

EXPECTED_CVARS = {
    # name: (expected value, meaning)
    "r.DynamicGlobalIlluminationMethod": (1, "Lumen global illumination"),
    "r.ReflectionMethod": (1, "Lumen reflections"),
    "r.Shadow.Virtual.Enable": (1, "Virtual Shadow Maps"),
    "r.AntiAliasingMethod": (4, "TSR"),
    "r.AllowStaticLighting": (0, "no baked lighting"),
    "r.GenerateMeshDistanceFields": (1, "mesh distance fields for Lumen"),
    "r.Nanite.ProjectEnabled": (1, "Nanite"),
}

# Third Person pack: paths differ between engine versions, so search by folder.
TEMPLATE_FOLDERS = ["/Game/ThirdPerson", "/Game/Characters"]


def find_assets(registry, folder, class_name, name_part=""):
    found = []
    for data in registry.get_assets_by_path(folder, recursive=True):
        if str(data.asset_class_path.asset_name) == class_name and name_part in str(data.asset_name):
            found.append(str(data.package_name))
    return found


def main():
    lines = []
    ok = True

    def out(text):
        lines.append(text)
        unreal.log(text)

    out("=== AshRPG stage 0 report ===")
    out("Engine: %s" % unreal.SystemLibrary.get_engine_version())
    out("Project: %s" % unreal.Paths.get_project_file_path())

    for name, (expected, meaning) in EXPECTED_CVARS.items():
        value = unreal.SystemLibrary.get_console_variable_int_value(name)
        good = value == expected
        ok = ok and good
        out("[%s] %s = %s (expected %s, %s)" % ("OK" if good else "FAIL", name, value, expected, meaning))

    registry = unreal.AssetRegistryHelpers.get_asset_registry()
    maps = find_assets(registry, TEMPLATE_FOLDERS[0], "World")
    manny = find_assets(registry, TEMPLATE_FOLDERS[1], "SkeletalMesh", "Manny")
    for label, found in (("Third Person map", maps), ("Manny mannequin", manny)):
        good = bool(found)
        ok = ok and good
        out("[%s] %s: %s" % ("OK" if good else "MISSING", label, ", ".join(found[:3]) or "not found"))

    out("RESULT: %s" % ("stage 0 settings are correct" if ok else "see FAIL / MISSING lines above"))

    report = os.path.join(unreal.Paths.project_saved_dir(), "AshStage0Report.txt")
    with open(report, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    unreal.log("Report saved: %s" % report)


main()
