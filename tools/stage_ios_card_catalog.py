"""Stage standalone custom card records into a COPY of an iOS card catalogue.

Offline only. Does not install anything or patch executable code. Requires an
artwork build report from build_ios_card_art.py and the matching original iOS
catalogue. Complex linked cards and dependent custom abilities fail explicitly.
"""
import argparse
import base64
import hashlib
import json
import re
import shutil
import sqlite3
from contextlib import closing
from pathlib import Path


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").digest()


def payload(value):
    return (json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def quote(value):
    return '"' + value.replace('"', '""') + '"'


def schema(db, table):
    return [tuple(row) for row in db.execute("PRAGMA table_info(" + quote(table) + ")")]


def insert_new_or_equal(db, table, row, key_columns):
    columns = list(row)
    existing = db.execute("SELECT * FROM " + quote(table) + " WHERE " +
                          " AND ".join(quote(key) + "=?" for key in key_columns),
                          [row[key] for key in key_columns]).fetchone()
    if existing is not None:
        if dict(existing) != row:
            raise ValueError("Conflicting existing record in " + table)
        return
    db.execute("INSERT INTO " + quote(table) + " (" + ",".join(map(quote, columns)) + ") VALUES (" +
               ",".join("?" for _ in columns) + ")", [row[column] for column in columns])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ios-database", type=Path, required=True)
    parser.add_argument("--pc-database", type=Path, required=True)
    parser.add_argument("--art-report", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New local staging directory")
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError("Output exists; choose a new staging directory")
    art = read_json(args.art_report)
    if art.get("status") != "offline-artifact-build-only" or not art.get("cards"):
        raise ValueError("Expected a completed offline artwork report")
    art_root = args.art_report.resolve().parent
    manifest_path = art_root / art["manifest"]
    if sha(manifest_path).hex() != art["manifestSha256"]:
        raise ValueError("Artwork build manifest changed")
    manifest = read_json(manifest_path)
    raw_entries = [entry for entry in manifest["Assets"] if entry.get("AssetType") == "Raw"
                   and entry.get("Name", "").startswith("Raw_CardDatabase_")]
    source_hash = sha(args.ios_database)
    if len(raw_entries) != 1 or raw_entries[0].get("sha256") != base64.b64encode(source_hash).decode("ascii"):
        raise ValueError("iOS source catalogue does not match the source manifest")

    args.output.mkdir(parents=True)
    try:
        with closing(sqlite3.connect(args.pc_database.resolve().as_uri() + "?mode=ro", uri=True)) as pc, \
                closing(sqlite3.connect(args.ios_database.resolve().as_uri() + "?mode=ro", uri=True)) as ios:
            pc.row_factory = ios.row_factory = sqlite3.Row
            languages = [row[0] for row in ios.execute(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Localizations_%'")]
            for table in ["Cards", "Abilities", "Localizations", "Enums", *languages]:
                if schema(pc, table) != schema(ios, table):
                    raise ValueError("PC/iOS catalogue schema differs: " + table)

            cards, abilities, loc_ids = [], {}, set()
            for item in art["cards"]:
                if item["grpId"] < 1800000:
                    raise ValueError("Only reserved custom grpIds may be staged")
                found = pc.execute("SELECT * FROM Cards WHERE GrpId=?", (item["grpId"],)).fetchone()
                if found is None:
                    raise ValueError("Custom printing missing from the PC catalogue")
                card = dict(found)
                if card["ArtId"] != item["artId"]:
                    raise ValueError("Card/art report ID mismatch")
                if ios.execute("SELECT 1 FROM Cards WHERE GrpId=? OR ArtId=?",
                               (card["GrpId"], card["ArtId"])).fetchone():
                    raise ValueError("Custom card/art ID is already occupied in the iOS catalogue")
                if card["LinkedFaceType"] or card["IsToken"] or card["HiddenAbilityIds"] or any(
                    card.get(key) for key in ("LinkedAbilityTemplateCardGrpIds", "AbilityIdToLinkedTokenGrpId",
                                              "AbilityIdToLinkedConjurations", "RebalancedCardGrpId",
                                              "DefunctRebalancedCardGrpId")
                ):
                    raise ValueError("Linked/token/hidden-ability cards need a dedicated staging implementation")
                for key, value in card.items():
                    if value and (key.endswith("TitleId") or key.endswith("TextId")):
                        loc_ids.add(int(value))
                for pair in filter(None, card["AbilityIds"].split(",")):
                    match = re.fullmatch(r"(\d+):(\d+)", pair)
                    if not match:
                        raise ValueError("Unsupported ability reference grammar")
                    ability_id, loc_id = map(int, match.groups())
                    found = pc.execute("SELECT * FROM Abilities WHERE Id=?", (ability_id,)).fetchone()
                    if found is None:
                        raise ValueError("Referenced ability is absent")
                    ability = dict(found)
                    dependency_fields = ("EmbeddedActiveAbilityIds", "EmbeddedIntoAbilityIds", "HiddenAbilityIds",
                                         "HiddenByAbilityIds", "ReferencedAbilityIds", "ModalChildIds", "ModalParentIds")
                    if any(ability.get(key) for key in dependency_fields) or ability["BaseId"]:
                        raise ValueError("Dependent/base abilities need a dedicated staging implementation")
                    abilities[ability_id] = ability
                    loc_ids.update((loc_id, ability["TextId"]))
                # A newly allocated subtype cannot silently borrow another native enum.
                for field, kind in (("Types", "CardType"), ("Subtypes", "SubType"),
                                    ("Supertypes", "SuperType"), ("Colors", "CardColor"),
                                    ("ColorIdentity", "CardColor")):
                    for number in filter(None, str(card[field] or "").split(",")):
                        source = [tuple(row) for row in pc.execute(
                            "SELECT * FROM Enums WHERE Type=? AND Value=?", (kind, int(number)))]
                        target = [tuple(row) for row in ios.execute(
                            "SELECT * FROM Enums WHERE Type=? AND Value=?", (kind, int(number)))]
                        if not source or source != target:
                            raise ValueError("Required native enum differs between PC and iOS: " + kind)
                cards.append(card)

            raw_dir = args.output / "Raw"
            raw_dir.mkdir()
            staged = raw_dir / "CardDatabase.building.sqlite"
            with closing(sqlite3.connect(staged)) as db:
                ios.backup(db)
                db.row_factory = sqlite3.Row
                with db:
                    for card in cards:
                        insert_new_or_equal(db, "Cards", card, ["GrpId"])
                    for ability in abilities.values():
                        insert_new_or_equal(db, "Abilities", ability, ["Id"])
                    for loc_id in sorted(loc_ids - {0}):
                        found = pc.execute("SELECT * FROM Localizations WHERE LocId=?", (loc_id,)).fetchone()
                        if found is None:
                            raise ValueError("Localization identity missing")
                        insert_new_or_equal(db, "Localizations", dict(found), ["LocId"])
                        for language in languages:
                            for row in pc.execute("SELECT * FROM " + quote(language) + " WHERE LocId=?", (loc_id,)):
                                insert_new_or_equal(db, language, dict(row), ["LocId", "Formatted"])
                if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                    raise ValueError("Staged SQLite catalogue is inconsistent")
            with staged.open("rb") as stream:
                name = "Raw_CardDatabase_" + hashlib.file_digest(stream, "md5").hexdigest() + ".mtga"
            destination = staged.with_name(name)
            staged.rename(destination)

        raw_entries[0].update(Name=name, Length=destination.stat().st_size,
                              sha256=base64.b64encode(sha(destination)).decode("ascii"))
        raw_entries[0].pop("crc", None)
        for item in art["cards"]:
            source = (art_root / item["path"]).resolve()
            if not source.is_relative_to(art_root) or sha(source).hex() != item["bundleSha256"]:
                raise ValueError("Artwork output changed or escapes the artwork build")
            target = args.output / "AssetBundle" / source.name
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(source, target)
        manifest_data = payload(manifest)
        manifest_name = "Manifest_" + hashlib.md5(manifest_data).hexdigest() + ".mtga"
        (args.output / manifest_name).write_bytes(manifest_data)
        report = {"status": "offline-catalogue-and-art-build-only", "nativeClientVerified": False,
                  "sourceIosDatabaseSha256": source_hash.hex(), "sourcePcDatabaseSha256": sha(args.pc_database).hex(),
                  "database": "Raw/" + name, "databaseSha256": sha(destination).hex(),
                  "manifest": manifest_name, "manifestSha256": hashlib.sha256(manifest_data).hexdigest(),
                  "cards": [{"grpId": card["GrpId"], "artId": card["ArtId"], "name": card["Order_Title"]}
                            for card in cards], "abilities": len(abilities), "localizationIds": len(loc_ids - {0}),
                  "remaining": art["remaining"]}
        report["remaining"] = [value for value in report["remaining"] if value != "Native custom-card database/art-ID integration"]
        report["remaining"].append("Confirm native custom-card/art lookup on an isolated iOS installation")
        (args.output / "build-report.json").write_bytes(payload(report))
        print(json.dumps({"status": report["status"], "cards": len(cards),
                          "report": str(args.output / "build-report.json")}))
    except Exception:
        (args.output / "BUILD-INCOMPLETE.txt").write_text(
            "Build failed; do not install partial outputs. Source catalogues were not changed.\n", encoding="utf-8")
        raise


if __name__ == "__main__":
    main()
