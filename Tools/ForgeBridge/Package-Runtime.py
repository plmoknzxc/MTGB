"""Assemble the pinned Windows Forge runtime; verify every downloaded byte."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "ForgeRuntime"
UPSTREAM = ROOT / ".tools/forge-proof/upstream"
JRE_VERSION = "17.0.20.1+1"
JRE_URL = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.20.1%2B1/OpenJDK17U-jre_x64_windows_hotspot_17.0.20.1_1.zip"
JRE_SHA256 = "bc21a93923103cdaac93ee337b0ae4365e739fde36df823dd456bc67c8a9d352"


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def refresh_manifest(config):
    files = sorted(path for folder in ("classes", "lib", "assets/res", "jre")
                   for path in (RUNTIME / folder).rglob("*") if path.is_file())
    manifest = {"files": [{"path": path.relative_to(RUNTIME).as_posix(), "sha256": sha(path)} for path in files]}
    manifest_path = RUNTIME / "files.json"
    manifest_path.write_text(json.dumps(manifest, separators=(",", ":")), encoding="utf-8")
    config["runtimeHash"] = sha(manifest_path)
    (RUNTIME / "runtime.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    return len(files)


def main():
    cache = ROOT / ".tools/forge-migration/downloads"
    cache.mkdir(parents=True, exist_ok=True)
    archive = cache / ("temurin-" + JRE_VERSION + ".zip")
    if not archive.exists() or sha(archive) != JRE_SHA256:
        temporary = archive.with_suffix(".download")
        print("Downloading pinned Eclipse Temurin JRE", flush=True)
        urllib.request.urlretrieve(JRE_URL, temporary)
        if sha(temporary) != JRE_SHA256:
            raise RuntimeError("JRE checksum mismatch")
        temporary.replace(archive)
    destination = (RUNTIME / "jre").resolve()
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        for entry in package.infolist():
            relative = Path(*Path(entry.filename).parts[1:])
            target = (destination / relative).resolve()
            if not target.is_relative_to(destination):
                raise RuntimeError("Unsafe JRE archive member")
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with package.open(entry) as source, target.open("wb") as output:
                    shutil.copyfileobj(source, output)
    print("Copying pinned Forge resources", flush=True)
    assets = RUNTIME / "assets"
    shutil.copytree(UPSTREAM / "forge-gui/res", assets / "res", dirs_exist_ok=True)
    spec = importlib.util.spec_from_file_location("native_patches", ROOT / "Tools/ForgeBridge/Apply-NativePatches.py")
    patches = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(patches)
    definitions = json.loads((ROOT / "Tools/ForgeBridge/NativePatches.json").read_text(encoding="utf8"))
    asset_patches = patches.apply_asset_patches(RUNTIME, definitions)
    # Use Forge's original lookup builder offline; headless startup reads the verified cache.
    subprocess.run([str(RUNTIME / "jre/bin/java.exe"), "-Xmx384m", "-XX:ActiveProcessorCount=2",
                    "-cp", str(RUNTIME / "classes") + ";" + str(RUNTIME / "lib/*"),
                    "mtgb.bridge.ForgeCardNameIndex", str(assets / "res/cardsfolder")],
                   check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    for name in ("LICENSE.txt", "README.txt"):
        shutil.copy2(UPSTREAM / "forge-gui" / name, assets / name)
    config_path = RUNTIME / "runtime.json"
    config = json.loads(config_path.read_text(encoding="utf-8-sig"))
    source = RUNTIME / "source"
    source.mkdir(parents=True, exist_ok=True)
    forge_source = source / ("forge-" + config["revision"] + ".zip")
    if not forge_source.exists():
        print("Archiving pinned Forge source", flush=True)
        subprocess.run(["git", "-C", str(UPSTREAM), "archive", "--format=zip", "--output=" + str(forge_source),
                        config["revision"]], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    shutil.copytree(ROOT / "Tools/ForgeBridge/java", source / "bridge", dirs_exist_ok=True)
    native_source=ROOT/".tools/native-patches/src"
    if native_source.exists():shutil.copytree(native_source,source/"native-patches",dirs_exist_ok=True)
    for name in ("NativePatches.json","Apply-NativePatches.py"):
        shutil.copy2(ROOT/"Tools/ForgeBridge"/name,source/name)
    for name in ("Build-Bridge.ps1", "Package-Runtime.py", "README.zh-CN.md"):
        shutil.copy2(ROOT / "Tools/ForgeBridge" / name, source / name)
    (source / "origins.json").write_text(json.dumps({
        "forgeRevision": config["revision"], "forgeRepository": "https://github.com/Card-Forge/forge",
        "forgeSourceSha256": sha(forge_source), "javaBinaryUrl": JRE_URL, "javaBinarySha256": JRE_SHA256,
        "nativePatchesSha256": sha(ROOT / "Tools/ForgeBridge/NativePatches.json"),
        "bridgeSourcesSha256": {p.relative_to(ROOT / "Tools/ForgeBridge/java").as_posix(): sha(p)
                                for p in sorted((ROOT / "Tools/ForgeBridge/java").rglob("*.java"))},
        "nativeAssetPatches": asset_patches,
        "nativePatchedSources": [{"path": p.relative_to(native_source).as_posix(), "sha256": sha(p)}
                                 for p in sorted(native_source.rglob("*.java"))]
    }, indent=2), encoding="utf-8")
    config.update(java="jre/bin/java.exe", assets="assets", javaVersion=JRE_VERSION)
    count = refresh_manifest(config)
    print(f"Portable runtime prepared: {count} verified files", flush=True)


if __name__ == "__main__":
    main()
