"""Package only this project's compiled diagnostic code and instructions."""
import hashlib
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
build = root / "src/BuildingRotation.Smapi/bin/Release/net6.0"
manifest_path = build / "manifest.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
source_manifest = json.loads((root / "src/BuildingRotation.Smapi/manifest.json").read_text(encoding="utf-8"))
if manifest != source_manifest:
    raise SystemExit("Build manifest is stale; rebuild Release before packaging.")
if manifest["UniqueID"] != "quellah.BuildingRotation":
    raise SystemExit("Unexpected package identity.")
version = manifest["Version"]
if not all(c in "0123456789." for c in version):
    raise SystemExit("Unexpected package version.")
files = {
    "manifest.json": manifest_path,
    "BuildingRotation.Smapi.dll": build / "BuildingRotation.Smapi.dll",
    "BuildingRotation.Runtime.dll": build / "BuildingRotation.Runtime.dll",
    "BuildingRotation.Core.dll": build / "BuildingRotation.Core.dll",
    "README.md": root / "docs/diagnostic-test.md",
}
# Resolve the complete allowlist before creating the output archive.
contents = {name: path.read_bytes() for name, path in files.items()}
output = root / "artifacts" / f"BuildingRotation.Diagnostics-{version}.zip"
output.parent.mkdir(exist_ok=True)
prefix = "BuildingRotation.Diagnostics/"
with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for name, data in sorted(contents.items()):
        info = zipfile.ZipInfo(prefix + name, date_time=(2000, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o100644 << 16
        archive.writestr(info, data)
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == {prefix + name for name in files}
    assert all(archive.read(prefix + name) == data for name, data in contents.items())
print(output.relative_to(root))
print("SHA-256:", hashlib.sha256(output.read_bytes()).hexdigest())
print("Files:", len(files), "Bytes:", output.stat().st_size)
