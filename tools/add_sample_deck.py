"""Add the sample to local accounts, keeping their existing decks intact."""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.arena import Arena
from server.samples import add_commander_sample
from server.storage import Store

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--user-id', type=int)
    options = parser.parse_args()
    store = Store(ROOT / 'data/server.sqlite3')
    arena = Arena(store, json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8')))
    with store._db() as db:
        ids = [options.user_id] if options.user_id else [r['id'] for r in db.execute('SELECT id FROM users')]
    for uid in ids:
        deck = add_commander_sample(arena, uid)
        print(f'Account {uid}: {deck["Name"]}, 100 cards including commander.')
