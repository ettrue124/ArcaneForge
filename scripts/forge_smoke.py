"""Exercise real Forge cards/rules and export a real Arena-ID initial snapshot."""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))
from server.engine import Engine


def main():
    catalogue = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf-8'))
    names = {}
    for card in catalogue:
        raw = card.get('arenaMetadata', {})
        if not raw.get('IsDigitalOnly') and not raw.get('IsRebalanced'):
            names.setdefault(card['name'], card)
    def entry(name, quantity):
        card = names[name]
        return {'name': name, 'quantity': quantity, 'grpId': card['grpId']}
    # A deliberately simple rules fixture. Copy counts exceed constructed limits;
    # this verifies engine execution, not tournament format legality.
    decks = [
        {'name': 'Forest bears', 'cards': [entry('Forest', 36), entry('Grizzly Bears', 24)]},
        {'name': 'White lions', 'cards': [entry('Plains', 36), entry('Savannah Lions', 24)]}
    ]
    engine = Engine.from_config(ROOT / 'engine-bridge/launch.json')
    try:
        status = engine.status(timeout=180)
        assert status['engine'] == 'Forge' and status['ready']
        lookup = engine.cards(names=['Forest', 'Grizzly Bears', 'Invalid Card Fixture'])
        assert [c['supported'] for c in lookup['cards']] == [True, True, False]
        result = engine.simulate(decks=decks, seed=42, maxSeconds=30, matchId='forge-smoke', viewerSeat=1, timeout=90)
        assert not result['timedOut'], 'Fixture timed out'
        assert result['winnerSeat'] in (1, 2) and result['turns'] > 1
        initial = result['initialSnapshot']
        assert initial and len(initial['players']) == 2 and len(initial['cards']) == 120
        valid_ids = {c['grpId'] for c in catalogue}
        assert all(c['grpId'] in valid_ids for c in initial['cards'])
        evidence = ROOT / 'data/forge-smoke.json'
        evidence.write_text(json.dumps(result, indent=2), encoding='utf-8')
        (ROOT / 'data/forge-initial-state.json').write_text(json.dumps(initial, indent=2), encoding='utf-8')
        print('PASS: actual Forge rules, unsupported card rejection, natural outcome and 120 real Arena-ID card instances')
        print(json.dumps({k: result[k] for k in ('engine', 'winner', 'winnerSeat', 'turns', 'endReason', 'timedOut')}))
    finally:
        engine.close()


if __name__ == '__main__':
    main()
