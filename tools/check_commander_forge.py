"""Exercise Commander against the real Forge engine; refresh eligible commanders."""
import json
import time
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server.engine import Engine


def main():
    catalogue = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
    names = sorted({c['name'] for c in catalogue if 'Legendary' in c.get('supertypes', [])
                    and not any(c.get('arenaMetadata', {}).get(k) for k in ('IsToken', 'IsDigitalOnly', 'IsRebalanced'))})
    engine = Engine.from_config(ROOT / 'engine-bridge/launch.json')
    try:
        eligible = []
        for start in range(0, len(names), 400):
            eligible.extend(c['name'] for c in engine.request('cards', names=names[start:start+400], timeout=220)['cards']
                            if c['canBeCommander'])
        (ROOT / 'data/commander-eligible.json').write_text(json.dumps({
            'source': 'Installed Forge CardRules.canBeCommander', 'names': sorted(eligible)
        }, indent=2) + '\n', encoding='utf8')
        print(f'Checked {len(names)} legendary names; {len(eligible)} Commander-eligible.', flush=True)
        bill = next(c for c in catalogue if c['name'] == 'Bristly Bill, Spine Sower')
        forest = next(c for c in catalogue if c['name'] == 'Forest')
        deck = {'name': 'Commander regression', 'format': 'Commander',
                'cards': [{'name': 'Forest', 'quantity': 99, 'grpId': forest['grpId']}],
                'commanders': [{'name': bill['name'], 'quantity': 1, 'grpId': bill['grpId']}]}
        for count, mulligans in ((2, 1), (4, 1), (4, 2)):
            engine.request('interactiveStart', decks=[deck] * count)
            def wait(predicate, limit=15):
                deadline = time.monotonic() + limit
                last = None
                while time.monotonic() < deadline:
                    last = engine.request('interactiveSnapshot', viewerSeat=1, gameStateId=1, msgId=1, matchId='commander-check')
                    if last.get('buttonEventId', 0) and predicate(last):
                        return last
                    time.sleep(.05)
                raise AssertionError(f'Forge did not reach expected state: {last.get("input") if last else None}')
            def action(snapshot, kind, **values):
                engine.request('interactiveAction', type=kind, eventId=snapshot['buttonEventId'], **values)
            snap = wait(lambda s: s.get('input', {}).get('type') in ('InputConfirm', 'InputConfirmMulligan', 'InputSelectEntitiesFromList'))
            if snap['input']['type'] == 'InputSelectEntitiesFromList':
                # Multiplayer asks the coin-flip winner which player starts.
                # Select our seat explicitly; do not treat later card choices
                # as a starting-player prompt.
                assert snap['input']['choices'] == list(range(1, count+1)), snap['input']
                assert snap['input']['min'] == snap['input']['max'] == 1
                action(snap, 'selectMany', instanceIds=[1])
                snap = wait(lambda s: s.get('input', {}).get('type') in ('InputConfirm', 'InputConfirmMulligan'))
            if snap['input']['type'] == 'InputConfirm':
                action(snap, 'ok')
                snap = wait(lambda s: s.get('input', {}).get('type') == 'InputConfirmMulligan')
            assert len(snap['players']) == count and all(p['life'] == 40 for p in snap['players'])
            command = [c for c in snap['cards'] if c.get('commander')]
            assert len(command) == count and all(c['zone'] == 'Command' for c in command), {
                'commanders': command, 'commandZoneCards': [c for c in snap['cards'] if c['zone'] == 'Command'],
                'omitted': snap.get('omittedCards')}
            for attempt in range(mulligans):
                old_event = snap['buttonEventId']
                action(snap, 'cancel')
                snap = wait(lambda s: s.get('input', {}).get('type') == 'InputConfirmMulligan'
                            and s['buttonEventId'] != old_event and s['mulliganCount'] == attempt+1)
            action(snap, 'ok')
            bottom = max(0, mulligans - (1 if count == 4 else 0))
            snap = wait(lambda s: s.get('input', {}).get('type') == ('InputLondonMulligan' if bottom else 'InputPassPriority'))
            if bottom:
                assert snap['bottomCount'] == bottom
                hand = [c['instanceId'] for c in snap['cards'] if c['ownerSeat'] == 1 and c['zone'] == 'Hand']
                assert len(hand) == 7
                action(snap, 'bottomCards', instanceIds=hand[:bottom])
                snap = wait(lambda s: s.get('input', {}).get('type') == 'InputPassPriority')
            hand = [c for c in snap['cards'] if c['ownerSeat'] == 1 and c['zone'] == 'Hand']
            assert len(hand) == 7-bottom, f'Unexpected retained hand: {len(hand)}'
            # Preserve a real initial board for the installed native GRE parser.
            (ROOT / f'analysis/commander/forge-{count}-players.json').write_text(json.dumps(snap), encoding='utf8')
            print(f'PASS: {count} players, 40 life, {mulligans} mulligan(s), kept {len(hand)} cards, command zones.', flush=True)
            engine.request('interactiveClose')
    finally:
        engine.close()


if __name__ == '__main__':
    main()
