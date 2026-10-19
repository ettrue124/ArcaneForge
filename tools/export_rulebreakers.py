"""Export commander-specific native title exceptions from Forge DeckRule data."""
import json
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from server import rulebreakers


def export(catalogue=None):
    cards = catalogue or json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
    lines, report = [], []
    for name, rules in sorted(rulebreakers.definitions().items()):
        commanders = [c for c in cards if c['name'] == name]
        exemptions = sorted({c['arenaMetadata']['TitleId'] for c in cards if rulebreakers.exempt(c, rules)})
        extra = sorted({c['arenaMetadata']['TitleId'] for c in cards if any(
            int(r.get('AllowedAdditionalColor', 0)) and rulebreakers.matches(c, r.get('AllowedAdditionalColorType', '__none__')) for r in rules)})
        budget = max((int(r.get('AllowedAdditionalColor', 0)) for r in rules), default=0)
        for commander in commanders:
            lines.append('\t'.join(map(str, (commander['arenaMetadata']['TitleId'], commander['grpId'], int(rulebreakers.unlimited(rules)), budget,
                ','.join(map(str, exemptions)), ','.join(map(str, extra))))))
        report.append(dict(name=name, printings=len(commanders), rules=rules, exemptTitles=len(exemptions), additionalColorTitles=len(extra)))
    target = ROOT / 'data/custom-arena-cards/rulebreaker-decks.tsv'
    target.write_text('\n'.join(lines)+'\n', encoding='utf8')
    (ROOT / 'data/rulebreaker-report.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf8')
    print(f'Rulebreaker definitions: {len(report)}; installed commanders: {sum(bool(r["printings"]) for r in report)}')
    return target


if __name__ == '__main__':
    export()
