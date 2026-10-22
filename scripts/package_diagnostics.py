#!/usr/bin/env python3
# SPDX-License-Identifier: MPL-2.0
"""Publish and package the Windows x64 GUI with its runtime, guide and notices."""

import argparse
import hashlib
import json
import shutil
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src/Grelecy.Diagnostics.Windows/Grelecy.Diagnostics.Windows.csproj"
ARTIFACTS = ROOT / "artifacts"


def package(dotnet: str) -> None:
    publish = ARTIFACTS / "publish" / "win-x64"
    stage = ARTIFACTS / "package" / "GRELECY-Diagnostics"
    # Only generated paths owned by this packaging script are replaced.
    for path in (publish, stage):
        if path.exists():
            shutil.rmtree(path)
        path.mkdir(parents=True)
    subprocess.run([
        dotnet, "publish", str(PROJECT), "--configuration", "Release",
        "--runtime", "win-x64", "--self-contained", "true",
        "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true", "-p:PublishTrimmed=false",
        "-p:DebugType=None", "-p:DebugSymbols=false", "--output", str(publish),
    ], cwd=ROOT, check=True)
    executable = publish / "GRELECY-Diagnostics.exe"
    if not executable.is_file() or executable.read_bytes()[:2] != b"MZ":
        raise RuntimeError("The Windows executable was not produced")
    shutil.copytree(publish, stage, dirs_exist_ok=True)
    shutil.copyfile(ROOT / "docs/DIAGNOSTICS.md", stage / "LIRE-MOI_READ-ME.md")
    shutil.copyfile(ROOT / "LICENSE", stage / "LICENSE-GRELECY-MPL-2.0.txt")
    shutil.copyfile(ROOT / "LICENSES/CC-BY-4.0.txt", stage / "LICENSE-GUIDE-CC-BY-4.0.txt")

    # Resolve the actual restored package roots; do not assume the user's home/cache location.
    assets = json.loads((PROJECT.parent / "obj/project.assets.json").read_text(encoding="utf-8"))
    folders = [Path(folder) for folder in assets["packageFolders"]]
    notices = stage / "third-party-notices"
    notices.mkdir()
    found = set()
    for folder in folders:
        for package_name in ("system.management", "system.codedom", "microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64"):
            directory = folder / package_name
            if not directory.is_dir():
                continue
            # The current assets file selects the used version, including runtime download dependencies.
            versions = {key.split("/", 1)[1] for key in assets["libraries"] if key.split("/", 1)[0].lower() == package_name}
            for framework in assets.get("project", {}).get("frameworks", {}).values():
                for dep in framework.get("downloadDependencies", []):
                    if dep["name"].lower() == package_name:
                        bounds = {part.strip() for part in dep["version"].strip("[]").split(",")}
                        if len(bounds) != 1:
                            raise RuntimeError("Runtime license lookup requires a resolved exact version")
                        versions.update(bounds)
            for version in versions:
                for source in (directory / version).iterdir():
                    normalized_name = source.name.lower().replace("-", "").replace("_", "")
                    if source.is_file() and ("license" in normalized_name or "thirdpartynotice" in normalized_name):
                        shutil.copyfile(source, notices / f"{package_name}-{version}-{source.name}")
                        found.add(package_name)
    required = {"system.management", "microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64"}
    if not required.issubset(found):
        raise RuntimeError("Missing third-party license notices for: " + ", ".join(sorted(required - found)))

    archive = ARTIFACTS / "GRELECY-Diagnostics-win-x64.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as output:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                output.write(path, path.relative_to(stage.parent))
    with archive.open("rb") as handle:
        digest = hashlib.file_digest(handle, "sha256").hexdigest()
    archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n", encoding="utf-8")
    print(f"Packaged {archive} ({archive.stat().st_size:,} bytes)")
    print(f"SHA-256: {digest}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="Path to the .NET SDK executable")
    package(parser.parse_args().dotnet)
