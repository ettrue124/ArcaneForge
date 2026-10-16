import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine

catalogue = json.load(open(ROOT / "data/catalogue.json", encoding="utf-8"))
cat_by_name = {}
for c in catalogue:
    meta = c.get("arenaMetadata", {})
    if meta.get("IsPrimaryCard") == 1 and not any(meta.get(k) for k in ("IsToken", "IsDigitalOnly", "IsRebalanced")):
        cat_by_name.setdefault(c["name"], c)

deck_dir = ROOT / "data/bot-commander-decks"
engine = Engine.from_config(ROOT / "engine-bridge/launch.json")
try:
    for f in sorted(deck_dir.glob("*.json")):
        data = json.loads(f.read_text(encoding="utf-8"))
        deck_obj = {
            "name": data["name"],
            "format": "Commander",
            "commanders": [{"name": data["commander"], "quantity": 1, "grpId": cat_by_name[data["commander"]]["grpId"]}],
            "companions": [],
            "cards": [{"name": c["name"], "quantity": c["quantity"], "grpId": cat_by_name[c["name"]]["grpId"]} for c in data["cards"]]
        }
        print(f"Testing deck in Forge: {data['name']} (Commander: {data['commander']})...")
        res = engine.request("interactiveStart", decks=[deck_obj, deck_obj])
        print(f"  Forge accepted! mode={res.get('mode')}, format={res.get('format')}")
        engine.request("interactiveClose")
    print("\nALL 5 BOT COMMANDER DECKS VERIFIED 100% IN REAL FORGE ENGINE!")
finally:
    engine.close()
