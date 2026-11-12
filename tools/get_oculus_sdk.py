"""Fetch the official Oculus Integration v18.0 (last version supporting Gear VR / Go)
from Meta's developer archive and unpack its libraries into the Unity project.

Usage:
    python -I tools/get_oculus_sdk.py [--cache E:\\GearVR_SDK] [--samples]

- Downloads OculusIntegration_v18.unitypackage once into --cache (default E:\\GearVR_SDK).
- Unpacks Assets/Oculus/{VR,Platform,Spatializer,Avatar,LipSync,AudioManager}
  (with their .meta files, so GUIDs match) into GearVRTest/Assets/Oculus.
- --samples also unpacks SampleFramework (265 MB, mostly Quest samples).
- Assets/Oculus is git-ignored: Meta's SDK is not committed, this script restores it.

By downloading you accept the Oculus SDK License (https://developer.oculus.com/licenses/sdk-3.5/).
"""
import argparse, os, sys, tarfile, urllib.request

URL = "https://securecdn-bcn1-1.oculus.com/binaries/download/?id=3541068782571032"
FILE = "OculusIntegration_v18.unitypackage"
SIZE = 381311955
PARTS = ["VR", "Platform", "Spatializer", "Avatar", "LipSync", "AudioManager"]

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join(ROOT, "GearVRTest")


def download(cache):
    os.makedirs(cache, exist_ok=True)
    path = os.path.join(cache, FILE)
    if os.path.exists(path) and os.path.getsize(path) == SIZE:
        print("Using cached", path)
        return path
    print("Downloading Oculus Integration v18.0 (381 MB)...")
    req = urllib.request.Request(URL, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req) as r, open(path + ".part", "wb") as f:
        while chunk := r.read(1 << 20):
            f.write(chunk)
    os.replace(path + ".part", path)
    return path


def unpack(pkg, parts):
    prefixes = tuple("Assets/Oculus/" + p for p in parts)
    entries = {}  # guid -> {"pathname":..., "asset": bytes, "meta": bytes}
    with tarfile.open(pkg, "r:gz") as t:
        for m in t:
            guid, _, kind = m.name.lstrip("./").partition("/")
            if kind not in ("pathname", "asset", "asset.meta") or not m.isfile():
                continue
            entries.setdefault(guid, {})[kind] = t.extractfile(m).read()
    n = 0
    for e in entries.values():
        if "pathname" not in e:
            continue
        rel = e["pathname"].decode("utf-8").splitlines()[0].strip()
        if not rel.startswith(prefixes) or ".." in rel:
            continue
        dest = os.path.join(PROJECT, *rel.split("/"))
        if not os.path.abspath(dest).startswith(os.path.join(PROJECT, "Assets")):
            continue
        if "asset" in e:
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with open(dest, "wb") as f:
                f.write(e["asset"])
            n += 1
        else:
            os.makedirs(dest, exist_ok=True)
        if "asset.meta" in e:
            with open(dest + ".meta", "wb") as f:
                f.write(e["asset.meta"])
    # folder metas for Assets/Oculus itself
    print(f"Unpacked {n} files into {os.path.join(PROJECT, 'Assets', 'Oculus')}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=r"E:\GearVR_SDK")
    ap.add_argument("--samples", action="store_true")
    a = ap.parse_args()
    parts = PARTS + (["SampleFramework"] if a.samples else [])
    unpack(download(a.cache), parts)
