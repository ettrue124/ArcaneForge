"""Build an OFFLINE Arena iOS artwork candidate from a local iOS template.

Requires UnityPy 1.25.4 and Pillow. Never touches an installed app, phone,
server, executable, certificate or signing identity. Native client loading
and custom-card database integration remain separate, unverified steps.
"""
import argparse
import base64
import copy
import hashlib
import json
import math
from pathlib import Path

import UnityPy
from PIL import Image, ImageOps
from UnityPy.export.Texture2DConverter import image_to_texture2d

ROOT = Path(__file__).resolve().parents[1]


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def local_path(value):
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def art_path(art_id):
    # Observed PC CardArtUtil.GetArtPath. Same path convention in iOS sample;
    # execution of this lookup for custom IDs on iOS is not yet established.
    number = str(art_id).zfill(6)
    return f"Assets/Core/CardArt/{number[:3].ljust(6, '0')}/{number}_AIF.tga"


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def build_one(record, template_bytes, args, output):
    art_id = record["artId"]
    if not isinstance(art_id, int) or isinstance(art_id, bool) or art_id <= 0:
        raise ValueError("Expected a positive integer artId")
    source = local_path(record["art"])
    source_bytes = source.read_bytes()
    env = UnityPy.load(template_bytes)
    textures = [obj for obj in env.objects if obj.type.name == "Texture2D"]
    bundles = [obj for obj in env.objects if obj.type.name == "AssetBundle"]
    if len(textures) != 1 or len(bundles) != 1 or len(env.container) != 1:
        raise ValueError("Template must have exactly one Texture2D and one indexed AssetBundle")
    texture = textures[0].read()
    bundle = bundles[0].read()
    serialized = textures[0].assets_file
    if int(serialized.target_platform) != 9 or texture.m_TextureFormat != 51:
        raise ValueError("Template must be iOS (9) with ASTC 8x8 (51), never a Windows bundle")
    if serialized.externals or bundle.m_Dependencies:
        raise ValueError("Dependent template bundles are not supported")
    if not (0 < texture.m_Width <= 2048 and 0 < texture.m_Height <= 2048):
        raise ValueError("Unexpected template texture dimensions")

    with Image.open(source) as opened:
        image = ImageOps.exif_transpose(opened).convert("RGBA")
    size = (texture.m_Width, texture.m_Height)
    if args.fit == "contain":
        image = ImageOps.pad(image, size, method=Image.Resampling.LANCZOS, color=(0, 0, 0, 255))
    else:
        image = ImageOps.fit(image, size, method=Image.Resampling.LANCZOS)

    mip_count = min(texture.m_MipCount or 1, int(math.log2(max(size))) + 1)
    encoded = []
    width, height = size
    for level in range(mip_count):
        mip = image if level == 0 else image.resize((width, height), Image.Resampling.LANCZOS)
        payload, fmt = image_to_texture2d(mip, 51, platform=9)
        if int(fmt) != 51 or len(payload) != math.ceil(width / 8) * math.ceil(height / 8) * 16:
            raise ValueError("ASTC encoder returned an unexpected format or byte count")
        encoded.append(payload)
        width, height = max(1, width // 2), max(1, height // 2)
    texture.image_data = b"".join(encoded)
    texture.m_CompleteImageSize = len(texture.image_data)
    texture.m_MipCount = mip_count
    if texture.m_MipMap is not None:
        texture.m_MipMap = mip_count > 1
    if texture.m_StreamData is not None:
        texture.m_StreamData.path = ""
        texture.m_StreamData.offset = 0
        texture.m_StreamData.size = 0
    texture.m_Name = str(art_id).zfill(6) + "_AIF"
    texture.save()

    indexed_path = art_path(art_id)
    internal_name = str(art_id).zfill(6) + "_CardArt.mtga"
    bundle.m_Name = internal_name
    if bundle.m_AssetBundleName is not None:
        bundle.m_AssetBundleName = internal_name
    bundle.m_Container = [(indexed_path, bundle.m_Container[0][1])]
    bundle.save()

    # Give each cloned bundle a distinct serialized-file identity. Otherwise
    # multiple bundles can retain the template's CAB name in Unity's cache.
    identity = hashlib.md5(json_bytes({"artId": art_id, "image": digest(source_bytes),
                                     "template": digest(template_bytes), "fit": args.fit,
                                     "version": args.unity_version})).hexdigest()
    cab_name = "CAB-" + identity
    serialized.name = cab_name
    # Texture bytes are now embedded; exclude the original .resS artwork.
    env.file.files = {cab_name: serialized}
    data = env.file.save(packer="lz4")
    filename = internal_name[:-5] + "_" + digest(data)[:32] + ".mtga"
    destination = output / "AssetBundle" / filename
    destination.parent.mkdir(exist_ok=True)
    destination.write_bytes(data)

    # Re-open the built artifact as part of the build, without running Arena.
    result = UnityPy.load(data)
    rebuilt = [obj for obj in result.objects if obj.type.name == "Texture2D"]
    if len(rebuilt) != 1 or list(result.container) != [indexed_path]:
        raise ValueError("Built bundle did not retain its texture/indexed path")
    decoded = rebuilt[0].read()
    if (int(rebuilt[0].assets_file.target_platform), decoded.m_TextureFormat,
            decoded.m_Width, decoded.m_Height, decoded.m_MipCount) != (9, 51, *size, mip_count):
        raise ValueError("Built bundle platform/texture metadata changed unexpectedly")
    if decoded.get_image_data() != texture.image_data:
        raise ValueError("Built bundle texture bytes differ from the encoded image")
    preview = output / (str(art_id) + "-preview.png")
    decoded.image.save(preview)
    entry = {"Name": filename, "Length": len(data), "Priority": 50,
             "sha256": base64.b64encode(hashlib.sha256(data).digest()).decode("ascii"),
             "IndexedAssets": [indexed_path]}
    # CRC is optional in AssetFileInfoConverter. The original Unity CRC must
    # not be reused, and a ZIP/file CRC is not Unity's uncompressed-data CRC.
    return entry, {"name": record["name"], "grpId": record["grpId"], "artId": art_id,
                   "sourceSha256": digest(source_bytes), "bundleSha256": digest(data),
                   "path": str(destination.relative_to(output)), "indexedPath": indexed_path,
                   "preview": preview.name, "width": size[0], "height": size[1],
                   "textureFormat": 51, "platform": 9, "mipCount": mip_count,
                   "serializedFile": cab_name}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--template", required=True, type=Path)
    parser.add_argument("--base-manifest", required=True, type=Path)
    parser.add_argument("--cards", type=Path, default=ROOT / "data/custom-arena-cards/manifest.json")
    parser.add_argument("--grp-id", type=int, action="append", required=True)
    parser.add_argument("--output", type=Path, required=True, help="New local staging directory")
    parser.add_argument("--unity-version", required=True, help="Explicit parser fallback; recorded as an assumption")
    parser.add_argument("--fit", choices=("contain", "cover"), default="contain")
    args = parser.parse_args()
    UnityPy.config.FALLBACK_UNITY_VERSION = args.unity_version
    if args.output.exists():
        raise FileExistsError("Output exists; choose a fresh staging directory")
    records = read_json(args.cards)
    selected = [record for record in records if record["grpId"] in set(args.grp_id)]
    if {record["grpId"] for record in selected} != set(args.grp_id):
        raise ValueError("Requested grpId is missing from the custom card manifest")
    if len(selected) != len(set(args.grp_id)) or len({r["artId"] for r in selected}) != len(selected):
        raise ValueError("Selected grpIds and artIds must be unique")
    template = args.template.read_bytes()
    base = read_json(args.base_manifest)
    original_assets = base["Assets"]
    if not isinstance(original_assets, list):
        raise ValueError("Base manifest must contain an Assets list")
    # Bind the supplied template to an existing iOS manifest entry by SHA-256.
    template_hash = base64.b64encode(hashlib.sha256(template).digest()).decode("ascii")
    if not any(asset.get("sha256") == template_hash and asset.get("Length") == len(template)
               for asset in original_assets):
        raise ValueError("Template does not match the supplied base manifest")
    occupied_paths = {path.casefold() for asset in original_assets for path in asset.get("IndexedAssets", [])}
    if any(art_path(record["artId"]).casefold() in occupied_paths for record in selected):
        raise ValueError("Custom art ID collides with an existing native indexed path")

    args.output.mkdir(parents=True)
    try:
        added, cards = [], []
        for record in selected:
            entry, detail = build_one(record, template, args, args.output)
            added.append(entry)
            cards.append(detail)
        merged = copy.deepcopy(base)
        merged["Assets"].extend(added)
        manifest_data = json_bytes(merged)
        manifest_name = "Manifest_" + hashlib.md5(manifest_data).hexdigest() + ".mtga"
        (args.output / manifest_name).write_bytes(manifest_data)
        (args.output / "custom-assets.json").write_bytes(json_bytes({"Assets": added}))
        report = {"status": "offline-artifact-build-only", "nativeClientVerified": False,
                  "templateSha256": digest(template), "baseManifestSha256": digest(args.base_manifest.read_bytes()),
                  "unityVersionFallbackAssumed": args.unity_version, "fit": args.fit,
                  "manifest": manifest_name, "manifestSha256": digest(manifest_data), "cards": cards,
                  "remaining": ["Usable separately signed Arena build",
                                "Native custom-card database/art-ID integration",
                                "iOS crop metadata and native display validation",
                                "iOS replacements for any required injected PC hooks",
                                "Private server network/TLS setup and device validation"]}
        (args.output / "build-report.json").write_bytes(json_bytes(report))
        print(json.dumps({"status": report["status"], "cards": len(cards),
                          "report": str(args.output / "build-report.json")}))
    except Exception:
        (args.output / "BUILD-INCOMPLETE.txt").write_text(
            "Build failed. Do not install these partial outputs. Sources were not changed.\n", encoding="utf-8")
        raise


if __name__ == "__main__":
    main()
