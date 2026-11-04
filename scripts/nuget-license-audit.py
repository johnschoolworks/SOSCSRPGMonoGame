#!/usr/bin/env python3

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PACKAGES = {
    "MonoGame.Framework.DesktopGL": "3.8.6.1",
    "Myra": "1.6.8",
    "AssetManagementBase": "0.7.3",
    "Cyotek.Drawing.BitmapFont": "2.0.4",
    "FontStashSharp.Base": "1.2.9",
    "FontStashSharp.MonoGame": "1.6.2",
    "FontStashSharp.Rasterizers.StbTrueTypeSharp": "1.2.9",
    "MonoGame.Library.OpenAL": "1.24.3.4",
    "MonoGame.Library.SDL": "2.32.10.2",
    "NVorbis": "0.10.4",
    "StbImageSharp": "2.30.16",
    "StbTrueTypeSharp": "1.26.13",
    "XNAssets.FontStashSharp.MonoGame": "0.8.9",
    "XNAssets.MonoGame": "0.8.9",
}

ROOT = Path.home() / ".nuget/packages"
REPORT = Path("audit-logs/nuget-license-audit.json")

results = []
missing_metadata = 0

for name, version in PACKAGES.items():
    directory = ROOT / name.lower() / version
    nuspecs = sorted(directory.glob("*.nuspec"))

    record = {
        "package": name,
        "version": version,
        "license": None,
        "license_type": None,
        "license_url": None,
        "project_url": None,
        "license_files": [],
        "status": "metadata_missing",
    }

    if not nuspecs:
        missing_metadata += 1
        results.append(record)
        continue

    try:
        tree = ET.parse(nuspecs[0])

        metadata = next(
            (
                node for node in tree.getroot()
                if node.tag.split("}")[-1] == "metadata"
            ),
            None,
        )

        if metadata is None:
            raise ValueError("Missing metadata element")

        fields = {
            node.tag.split("}")[-1]: node
            for node in metadata
        }

        license_node = fields.get("license")

        if license_node is not None:
            record["license"] = license_node.text
            record["license_type"] = license_node.attrib.get("type")

        for field, key in [
            ("licenseUrl", "license_url"),
            ("projectUrl", "project_url"),
        ]:
            node = fields.get(field)
            if node is not None:
                record[key] = node.text

        record["license_files"] = [
            str(path.relative_to(directory))
            for path in sorted(directory.rglob("*"))
            if path.is_file()
            and any(
                token in path.name.lower()
                for token in ("license", "copying", "notice")
            )
        ]

        record["status"] = (
            "declared"
            if record["license"] or record["license_url"]
            else "license_not_declared"
        )

    except (ET.ParseError, ValueError) as exc:
        record["status"] = f"parse_error: {exc}"
        missing_metadata += 1

    results.append(record)

REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(
    json.dumps(results, indent=2, ensure_ascii=False) + "\n"
)

print(f"{'Package':47} {'Version':12} {'License':20} Status")
print("-" * 100)

for item in results:
    print(
        f"{item['package'][:47]:47} "
        f"{item['version']:12} "
        f"{str(item['license'] or 'UNDECLARED')[:20]:20} "
        f"{item['status']}"
    )

print(f"\nPackages: {len(results)}")
print(f"Metadata errors: {missing_metadata}")
print(f"Report: {REPORT}")
print("Note: License declarations require manual compliance review.")

sys.exit(1 if missing_metadata else 0)
