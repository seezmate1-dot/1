"""Asset audit for TZ section 53.5. Run in the editor:
Tools > Execute Python Script > this file.

Read-only: it never changes assets. It lists what breaks the rules and how to
fix it without losing visible quality. The report goes to the Output Log and
to Saved/AshPerfAudit.txt.
"""
import os
import unreal

# ---------------------------------------------------------------- rules (TZ 17.4, 53.5)
ROOT = "/Game"
SKIP_FOLDERS = [
    "/Game/Characters/Mannequins",  # template placeholders, replaced later
    "/Game/LevelPrototyping",
    "/Game/Python",
]
TEXTURE_MAX = 2048
TEXTURE_4K_FOLDERS = ["/Game/Characters/Hero", "/Game/Characters/Bosses", "/Game/Key"]
NANITE_MIN_TRIANGLES = 5000
SKELETAL_MIN_LODS = 4
SKELETAL_MIN_LODS_FOLDERS = ["/Game/Characters"]
BIG_PACKAGE_MB = 100
LIST_LIMIT = 40
GC_EVERY = 200  # free loaded assets regularly, the dev PC has 16 GB RAM


def tag(data, name):
    """Asset registry tag as a string, or None when the tag is missing."""
    try:
        value = data.get_tag_value(name)
        return str(value) if value else None
    except Exception:
        pass
    try:
        found, value = unreal.AssetRegistryHelpers.get_tag_value(data, name)
        return str(value) if found else None
    except Exception:
        return None


def maybe_collect_garbage(index):
    if index and index % GC_EVERY == 0:
        unreal.SystemLibrary.collect_garbage()


def in_folders(path, folders):
    return any(path == f or path.startswith(f + "/") for f in folders)


def class_name(data):
    return str(data.asset_class_path.asset_name)


class Report:
    def __init__(self):
        self.lines = []
        self.problems = 0

    def out(self, text=""):
        self.lines.append(text)
        unreal.log(text)

    def section(self, title, items, fix):
        self.out("")
        self.out("--- %s: %d" % (title, len(items)))
        if not items:
            return
        self.problems += len(items)
        self.out("    Fix: " + fix)
        for item in items[:LIST_LIMIT]:
            self.out("    " + item)
        if len(items) > LIST_LIMIT:
            self.out("    ... and %d more" % (len(items) - LIST_LIMIT))

    def skipped(self, title, error):
        self.out("")
        self.out("--- %s: check skipped (%s)" % (title, error))


def collect():
    registry = unreal.AssetRegistryHelpers.get_asset_registry()
    assets = []
    for data in registry.get_assets_by_path(ROOT, recursive=True):
        path = str(data.package_name)
        if not in_folders(path, SKIP_FOLDERS):
            assets.append(data)
    return assets


def check_textures(assets, report):
    big, no_mips, no_stream = [], [], []
    textures = [d for d in assets if class_name(d) == "Texture2D"]
    with unreal.ScopedSlowTask(len(textures), "Audit: textures") as task:
        task.make_dialog(True)
        for index, data in enumerate(textures):
            if task.should_cancel():
                break
            task.enter_progress_frame(1)
            maybe_collect_garbage(index)
            path = str(data.package_name)
            tex = unreal.load_asset(path)
            if tex is None:
                continue
            size = max(tex.blueprint_get_size_x(), tex.blueprint_get_size_y())
            limit = 4096 if in_folders(path, TEXTURE_4K_FOLDERS) else TEXTURE_MAX
            if size > limit:
                big.append("%s  %d px (limit %d)" % (path, size, limit))
            is_ui = tex.get_editor_property("lod_group") == unreal.TextureGroup.TEXTUREGROUP_UI
            if is_ui:
                continue
            if tex.get_editor_property("mip_gen_settings") == unreal.TextureMipGenSettings.TMGS_NO_MIPMAPS:
                no_mips.append(path)
            if tex.get_editor_property("never_stream"):
                no_stream.append(path)
    report.section("Textures above size limit", big,
                   "set Maximum Texture Size to the limit in the texture editor. The source file stays, "
                   "only the in-game copy shrinks, so it can be raised back at any time.")
    report.section("Textures without mip maps (not UI)", no_mips,
                   "Mip Gen Settings = FromTextureGroup. Without mips distant surfaces shimmer and waste memory.")
    report.section("Textures with Never Stream", no_stream,
                   "turn Never Stream off, otherwise the full texture always sits in video memory.")


def check_static_meshes(assets, report):
    not_nanite, complex_collision = [], []
    meshes = [d for d in assets if class_name(d) == "StaticMesh"]
    with unreal.ScopedSlowTask(len(meshes), "Audit: static meshes") as task:
        task.make_dialog(True)
        for index, data in enumerate(meshes):
            if task.should_cancel():
                break
            task.enter_progress_frame(1)
            maybe_collect_garbage(index)
            path = str(data.package_name)
            triangles = tag(data, "Triangles")
            try:
                triangles = int(triangles.replace(",", "")) if triangles else None
            except ValueError:
                triangles = None
            if triangles is not None and triangles < NANITE_MIN_TRIANGLES:
                continue
            mesh = unreal.load_asset(path)
            if mesh is None:
                continue
            try:
                if not mesh.get_editor_property("nanite_settings").enabled:
                    not_nanite.append("%s  %s triangles" % (path, triangles if triangles is not None else "?"))
            except Exception:
                pass
            try:
                body = mesh.get_editor_property("body_setup")
                if body and body.get_editor_property("collision_trace_flag") == \
                        unreal.CollisionTraceFlag.CTF_USE_COMPLEX_AS_SIMPLE:
                    complex_collision.append(path)
            except Exception:
                pass
    report.section("Opaque static meshes over %d triangles without Nanite" % NANITE_MIN_TRIANGLES, not_nanite,
                   "enable Nanite in the mesh editor (Nanite Settings > Enabled). Skip only meshes with "
                   "translucent materials. Nanite keeps the look and removes LOD popping.")
    report.section("Meshes using complex collision as simple", complex_collision,
                   "Collision Complexity = Project Default with simple shapes. Complex is allowed only for big "
                   "walkable rocks (53.5).")


def check_skeletal_meshes(assets, report):
    few_lods = []
    meshes = [d for d in assets if class_name(d) == "SkeletalMesh"
              and in_folders(str(d.package_name), SKELETAL_MIN_LODS_FOLDERS)]
    subsystem = unreal.get_editor_subsystem(unreal.SkeletalMeshEditorSubsystem)
    for data in meshes:
        path = str(data.package_name)
        mesh = unreal.load_asset(path)
        if mesh is None:
            continue
        count = subsystem.get_lod_count(mesh)
        if count < SKELETAL_MIN_LODS:
            few_lods.append("%s  %d LOD" % (path, count))
    report.section("Characters with fewer than %d LODs" % SKELETAL_MIN_LODS, few_lods,
                   "generate LODs in the Skeletal Mesh editor (LOD Settings): 100%, 50%, 25%, 12%. "
                   "LOD1 from 15 m, so close-ups stay at full detail.")


def check_materials(assets, report):
    masters = [str(d.package_name) for d in assets if class_name(d) == "Material"
               and not str(d.package_name).startswith("/Game/Materials/Master")]
    report.section("Base materials outside /Game/Materials/Master", masters,
                   "use instances of the master materials (17.3). Every base material adds shaders, "
                   "compile time and hitch risk.")


def check_package_sizes(assets, report):
    content_dir = unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_content_dir())
    sizes = []
    for data in assets:
        path = str(data.package_name)
        relative = path[len("/Game/"):]
        for ext in (".uasset", ".umap"):
            filename = os.path.join(content_dir, relative + ext)
            if os.path.exists(filename):
                sizes.append((os.path.getsize(filename), path))
                break
    sizes.sort(reverse=True)
    big = ["%s  %.0f MB" % (p, s / 1048576.0) for s, p in sizes if s > BIG_PACKAGE_MB * 1048576]
    report.section("Packages over %d MB" % BIG_PACKAGE_MB, big,
                   "usually 8K textures or uncompressed audio. Check them first.")
    report.out("")
    report.out("Largest packages:")
    for size, path in sizes[:10]:
        report.out("    %s  %.1f MB" % (path, size / 1048576.0))


def main():
    report = Report()
    report.out("=== AshRPG asset audit (TZ 53.5) ===")
    report.out("Engine: %s" % unreal.SystemLibrary.get_engine_version())
    assets = collect()
    report.out("Assets checked: %d (skipped folders: %s)" % (len(assets), ", ".join(SKIP_FOLDERS)))

    for title, check in (("Textures", check_textures), ("Static meshes", check_static_meshes),
                         ("Skeletal meshes", check_skeletal_meshes), ("Materials", check_materials),
                         ("Package sizes", check_package_sizes)):
        try:
            check(assets, report)
        except Exception as error:
            report.skipped(title, error)

    report.out("")
    report.out("RESULT: %s" % ("no problems found" if report.problems == 0
                                else "%d items to review" % report.problems))
    filename = os.path.join(unreal.Paths.project_saved_dir(), "AshPerfAudit.txt")
    with open(filename, "w", encoding="utf-8") as f:
        f.write("\n".join(report.lines) + "\n")
    unreal.log("Report saved: %s" % filename)


main()
