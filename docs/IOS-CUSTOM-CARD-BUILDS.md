# Offline Arena iOS custom-card builds

The target remains a second native Arena installation alongside the official
app. These tools advance its custom-card assets; they do not produce an IPA,
remove executable encryption, sign an app, or install anything on the phone.

## Built on 2026-10-27

An offline Basalt Monolith pack was generated under ignored
`data/ios/staged-basalt-pack/`:

- Custom printing 1800001 / art ID 9000001, preserving the PC/server mapping.
- Three ability records and five localization identities in a copy of the
  actual iPhone card database. Existing conflicting records are rejected.
- An iOS platform-9 ASTC 8x8 texture bundle with ten mip levels, a distinct
  serialized CAB identity and native-style indexed artwork path.
- A merged manifest with hashes and lengths for the generated artwork and
  catalogue. Unchanged assets retain the original iPhone manifest records.
- A JSON build report explicitly marked `nativeClientVerified: false`.

The artwork builder reopens its output, checks platform, format, dimensions,
mip count, indexed path and encoded payload, and decodes a preview. The
catalogue builder compares source schemas, checks allocations/references,
copies records into a new local database and runs SQLite integrity checking.
These are artifact-build checks, not iPhone gameplay or native-rendering
confirmation. No test suite was added or run.

## Tools

`tools/ios_arena_files.py` uses the phone's existing pairing with automatic
pairing disabled. `list`, `pull` and `logs` only read shared Documents. `pull`
refuses existing destinations and nonregular files, with a default 64 MiB
limit and an explicit larger-file option. This turn copied the public asset
manifest and card catalogue from the phone; it did not access an account
database or write to the device.

`tools/build_ios_card_art.py` binds a local iOS template to its source manifest
by SHA-256 and length, rejects Windows or dependent templates, and builds only
the requested custom printings. `--fit contain` preserves illustration aspect
ratio with padding; `--fit cover` crops to the template shape. Crop/display
metadata needs separate device work. The builder records the explicitly
supplied Unity fallback version as an assumption, not a recovered version.

`tools/stage_ios_card_catalog.py` combines that artwork build with a read-only
PC catalogue and an exact manifest-matched original iOS catalogue. It supports
standalone custom cards. Linked faces, tokens, hidden/dependent abilities,
new native enums and conflicting allocations stop the build. It does not
silently flatten complex cards or apply the PC managed-code extensions.

Both builders require a fresh output directory and retain partial output with
`BUILD-INCOMPLETE.txt` on failure. Never install incomplete candidates. Tools
and reports have not been exhaustively audited or verified by a test suite.

## Reproduce the artwork build

From the repository root in PowerShell, choose a new output folder:

```powershell
& 'data/ios/asset-tools-env/Scripts/python.exe' tools/build_ios_card_art.py `
  --template 'data/ios/asset-samples/ios_000002_CardArt_ab4497f8-2ca0c286ef9213dff052588dadf30c4b.mtga' `
  --base-manifest 'data/ios/asset-samples/Manifest_5121d58291174762ef788d68bb0d7e40.mtga' `
  --grp-id 1800001 --unity-version '6000.3.14f1' `
  --output 'data/ios/another-art-build'
```

UnityPy 1.25.4 and its ASTC encoder dependencies are isolated in
`data/ios/asset-tools-env/`; the installed package list is recorded at
`data/ios/asset-tools-requirements.txt`. Proprietary source assets and generated
bundles stay ignored under data/ios and are not committed or uploaded.

For catalogue staging, provide the PC catalogue explicitly:

```powershell
python tools/stage_ios_card_catalog.py `
  --ios-database 'data/ios/source-catalogues/Raw_CardDatabase_90272dd2ac53d22f71040410dad8f38b.mtga' `
  --pc-database '<absolute path to the installed PC Raw_CardDatabase file>' `
  --art-report 'data/ios/another-art-build/build-report.json' `
  --output 'data/ios/another-catalogue-build'
```

## Evidence and remaining blockers

The native path convention was recovered from the matching PC
`SharedClientCore.dll`, `Wotc.Mtga.Cards.Database.CardArtUtil.GetArtPath`:
pad the art ID to at least six digits, use its first three digits padded to
six for the directory, then `<id>_AIF`. The original iOS sample matches this
convention. `CardArtTextureLoader` checks indexed assets and uses Arena's
native texture loader. `AssetFileInfoConverter` accepts SHA-256/length with
an absent optional Unity CRC. The original template's CRC is not reused;
ordinary file CRC32 is not substituted for Unity's CRC.

These matching-PC observations support a candidate asset workflow. They do
not prove all iOS paths behave identically. Source references for the codec:
[UnityPy](https://github.com/K0lb3/UnityPy) and
[ASTC encoder bindings](https://github.com/K0lb3/astc-encoder-py).

Remaining required work:

1. Obtain a usable Arena iOS application build that can be independently
   signed. The acquired App Store IPA still has encrypted executable ranges;
   these tools do not change that, and no second-app install was attempted.
2. Configure bundle identity, entitlements and signing for that build using
   the user's signing assets. None were supplied or created here.
3. Verify native custom-card lookup, artwork cropping, rules text, card actions
   and manifestations in an isolated iOS installation. PC extension behavior
   (commander tax, deck validation, loyalty and special layouts) remains
   a separate compatibility problem.
4. Complete phone-to-server networking, TLS trust and mobile manifest bootstrap.
   The existing live server's certificates/listeners were not changed.

No conclusion here establishes that all custom cards work or that a separate
Arena app is ready. The concrete progress is a generated, internally checked
iOS artwork/catalogue candidate using the existing custom-card IDs.
