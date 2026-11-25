"""Build the cumulative Windows asset consumed by the in-game update checker."""
import argparse
import hashlib
import os
import re
import tempfile
import zipfile
from pathlib import Path


def pack(client, destination):
    client = client.resolve(strict=True)
    data = client / "MTGB_Data"
    version = (data / "mtgb-version.txt").read_text(encoding="utf-8-sig").strip()
    if not re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", version):
        raise ValueError("Invalid client build version")
    roots = (data / "StreamingAssets/ClientProgramFiles.txt").read_text(encoding="utf-8-sig").splitlines()
    for required in ("MTGB.exe", "UnityPlayer.dll", "MTGB_Data/globalgamemanagers", "MTGB_Data/Managed/Assembly-CSharp.dll",
                     "MTGB_Data/StreamingAssets/InstallClientUpdate.ps1", "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
                     "ForgeRuntime/runtime.json", "ForgeRuntime/jre/bin/java.exe"):
        if not (client / required).is_file():
            raise ValueError(f"Incomplete client build: {required}")
    destination = destination.resolve()
    if destination.is_relative_to(client):
        raise ValueError("Choose an output directory outside the installed client")
    destination.mkdir(parents=True, exist_ok=True)
    package = destination / "MTGB-Windows-x64.zip"
    with tempfile.NamedTemporaryFile(dir=destination, suffix=".partial", delete=False) as temp:
        pending = Path(temp.name)
    try:
        with zipfile.ZipFile(pending, "w", zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
            for name in roots:
                root = client / name
                if root.parent != client:
                    raise ValueError(f"Invalid program entry: {name}")
                for entry in ([root] if root.is_file() else sorted(root.rglob("*"))):
                    if entry.is_symlink() or not entry.resolve().is_relative_to(client):
                        raise ValueError(f"Linked program file: {entry}")
                    if entry.is_file():
                        archive.write(entry, entry.relative_to(client).as_posix())
        with zipfile.ZipFile(pending) as archive:
            failed = archive.testzip()
            if failed:
                raise ValueError(f"ZIP readback failed: {failed}")
        with pending.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        os.replace(pending, package)
        print(f"v{version}  {package}  {package.stat().st_size} bytes  sha256:{digest}")
    finally:
        pending.unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    pack(args.client, args.destination)
