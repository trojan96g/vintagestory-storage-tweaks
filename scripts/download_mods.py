#!/usr/bin/env python3

"""
Loads a mods.txt file in the current directory and downloads the mods to Mods directory in the current directory
The mods.txt should be a comma separated list of {identifier}@{version} pairs, where {identifier} is the
identifier specified in the modinfo, and {version} is the semvar string of the desired version.
"""

from pathlib import Path
import requests

text = Path("mods.txt").read_text(encoding="utf-8")

mods = {}

for entry in text.split(","):
    entry = entry.strip()

    if not entry:
        continue

    modid, version = entry.split("@", 1)
    mods[modid] = version

# http://mods.vintagestory.at/api/v2/mods/install-information

ids = result = ",".join(f"{modid}@{version}" for modid, version in mods.items())

response = requests.get("https://mods.vintagestory.at/api/v2/mods/install-information", params={"ids": ids})

Path("Mods").mkdir(exist_ok=True)

failed_mods = []

for modid, info in response.json()["data"].items():
    if "fileUrl" not in info:
        print(f"fileUrl not in info for '{modid}':")
        print(info)
        failed_mods.append(modid)
        continue

    # { 'fileName': 'adventurers-walking-stick-net10_3.0.9.zip', 'fileUrl' }
    url = f"https://mods.vintagestory.at{info['fileUrl']}"
    path = f"Mods/{info['fileName']}"

    if Path(path).exists():
        print(f"Mod already downloaded: {path}")
        continue

    response = requests.get(url)

    if response.status_code == 200:
        with open(path, "wb") as f:
            f.write(response.content)
        print(f"Downloaded mod to: {path}")
    else:
        print(f"Failed to download file: {info}")

if len(failed_mods) > 0:
    print(f"The following mods failed to download: {failed_mods}")
