"""Resolve Arena catalogue names against the installed real Forge card database.

Name resolution is not a claim that Arena and Forge rules are equivalent. Digital
and rebalanced cards always require explicit review before simulation mapping.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import tempfile

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
from server.engine import Engine, EngineError


def flagged(value):
    return value is True or (type(value) is int and value != 0) or (
        isinstance(value, str) and value.strip().casefold() not in ('', '0', 'false', 'no'))


def classify(card, resolution):
    name = card.get('name', '')
    metadata = card.get('arenaMetadata') or {}
    supported = resolution.get('supported') is True
    forge_name = resolution.get('forgeName')
    reasons = []
    if flagged(metadata.get('IsDigitalOnly', card.get('isDigitalOnly', 0))):
        reasons.append('digital_only')
    if flagged(metadata.get('IsRebalanced', card.get('isRebalanced', 0))) or name.startswith('A-'):
        reasons.append('rebalanced')
    if supported and forge_name != name:
        reasons.append('name_mismatch')
    return {'grpId': card['grpId'], 'name': name, 'nameResolved': supported,
            'forgeName': forge_name, 'edition': resolution.get('edition'),
            'requiresReview': bool(reasons), 'reviewReasons': reasons,
            'eligibleForForgeSimulation': supported and not reasons,
            'rulesEquivalence': 'unverified'}


def build_report(cards, engine, chunk_size=200, timeout=90, progress=None):
    if not isinstance(cards, list) or any(not isinstance(c, dict) or
            type(c.get('grpId')) is not int or not isinstance(c.get('name'), str) or
            not c['name'] for c in cards):
        raise ValueError('Catalogue must contain grpId integers and nonempty names')
    if not 1 <= chunk_size <= 1000:
        raise ValueError('Chunk size must be from 1 to 1000')
    names = sorted({c['name'] for c in cards})
    resolutions = {}
    engine_status = engine.status(timeout=timeout)
    for start in range(0, len(names), chunk_size):
        chunk = names[start:start + chunk_size]
        reply = engine.cards(names=chunk, timeout=timeout)
        if not isinstance(reply, dict) or not isinstance(reply.get('cards'), list):
            raise EngineError('Forge coverage received an invalid card lookup response')
        received = {}
        for resolved in reply['cards']:
            if not isinstance(resolved, dict) or resolved.get('name') not in chunk or resolved['name'] in received or type(resolved.get('supported')) is not bool:
                raise EngineError('Forge coverage received invalid or duplicate lookup rows')
            if resolved['supported'] and not isinstance(resolved.get('forgeName'), str):
                raise EngineError('Supported card response is missing its Forge name')
            received[resolved['name']] = resolved
        if set(received) != set(chunk):
            raise EngineError('Forge coverage response omitted requested card names')
        resolutions.update(received)
        if progress:
            progress(min(start + chunk_size, len(names)), len(names))
    mapped = [classify(c, resolutions[c['name']]) for c in cards]
    counts = Counter()
    for card in mapped:
        counts['catalogueEntries'] += 1
        counts['nameResolved'] += int(card['nameResolved'])
        counts['requiresReview'] += int(card['requiresReview'])
        counts['eligibleForForgeSimulation'] += int(card['eligibleForForgeSimulation'])
        counts['unsupported'] += int(not card['nameResolved'])
    return {'generatedAt': datetime.now(timezone.utc).isoformat(), 'engine': engine_status,
            'rulesEquivalence': 'unverified', 'summary': dict(counts),
            'unsupportedNames': sorted(n for n, r in resolutions.items() if not r['supported']),
            'reviewNames': sorted({c['name'] for c in mapped if c['requiresReview']}),
            'cards': mapped}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', type=Path, default=ROOT / 'engine-bridge' / 'launch.json')
    parser.add_argument('--catalogue', type=Path, default=ROOT / 'data' / 'catalogue.json')
    parser.add_argument('--output', type=Path, default=ROOT / 'data' / 'forge-coverage.json')
    parser.add_argument('--chunk-size', type=int, default=200)
    parser.add_argument('--timeout', type=float, default=90)
    args = parser.parse_args()
    try:
        catalogue = json.loads(args.catalogue.read_text(encoding='utf-8'))
        with Engine.from_config(args.config) as engine:
            report = build_report(catalogue, engine, args.chunk_size, args.timeout,
                                  lambda done, total: print(f'Forge resolved {done}/{total} distinct names', file=sys.stderr))
        output = args.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=output.parent,
                                         prefix=output.name + '.', suffix='.tmp', delete=False) as handle:
            temporary = Path(handle.name)
            json.dump(report, handle, ensure_ascii=False, indent=2)
            handle.write('\n')
        try:
            temporary.replace(output)
        finally:
            temporary.unlink(missing_ok=True)
        print(json.dumps({'output': str(output), **report['summary']}))
        return 0
    except (EngineError, OSError, ValueError) as exc:
        print('Forge coverage failed: ' + str(exc), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
