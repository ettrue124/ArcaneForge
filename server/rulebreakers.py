"""Commander exceptions read from the pinned Forge DeckRule definitions."""
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


@lru_cache(maxsize=1)
def definitions():
    result = {}
    for path in (ROOT / 'vendor/forge/forge-gui/res/cardsfolder').rglob('*.txt'):
        lines = path.read_text(encoding='utf8').splitlines()
        rules = [line[len('DeckRule:'):] for line in lines if line.startswith('DeckRule:')]
        if not rules:
            continue
        name = next(line[5:] for line in lines if line.startswith('Name:'))
        parsed = []
        for raw in rules:
            kind, fields = raw.split(':', 1)
            params = dict(part.strip().split('$', 1) for part in fields.split('|') if '$' in part)
            params = {k.strip(): v.strip() for k, v in params.items()}
            if params.get('ActiveSection', 'Commander') != 'Commander':
                continue
            parsed.append(dict(kind=kind, **params))
        if parsed:
            result[name] = parsed
    return result


def matches(card, branches):
    types = set(card.get('types', [])) | set(card.get('supertypes', [])) | set(card.get('subtypes', []))
    def branch_matches(branch):
        for term in branch.split('.'):
            if term == 'Permanent':
                if not types.intersection(('Creature', 'Artifact', 'Enchantment', 'Land', 'Planeswalker', 'Battle')):
                    return False
            elif term.startswith(('cmcGE', 'powerGE')):
                value = card.get('arenaMetadata', {}).get('Order_CMCWithXLast', 0) if term.startswith('cmc') else card.get('power', '')
                try:
                    if float(value) < int(term[5:] if term.startswith('cmc') else term[7:]):
                        return False
                except (ValueError, TypeError):
                    return False
            elif term not in types:
                return False
        return True
    return any(branch_matches(branch) for branch in branches.split(','))


def active(commanders, catalogue):
    return [rule for entry in commanders for rule in definitions().get(catalogue[entry['cardId']]['name'], [])]


def exempt(card, rules):
    return any(r['kind'] == 'ColorIdentity' and (r.get('Disable') == 'True' or matches(card, r.get('Exempt', '__none__'))) for r in rules)


def unlimited(rules):
    return any(r['kind'] == 'Size' and r.get('AdjustMax') == 'Unlimited' for r in rules)


def additional_colors(cards, identity, rules):
    """Use one shared chosen-color budget, never a separate color per card."""
    chosen = set()
    for card in cards:
        colors = set(filter(None, card.get('arenaMetadata', {}).get('ColorIdentity', '').split(','))) - identity
        if not colors or exempt(card, rules):
            continue
        if not any(r['kind'] == 'ColorIdentity' and int(r.get('AllowedAdditionalColor', 0)) > 0
                   and matches(card, r.get('AllowedAdditionalColorType', '__none__')) for r in rules):
            continue
        chosen.update(colors)
    budget = max((int(r.get('AllowedAdditionalColor', 0)) for r in rules), default=0)
    return chosen if len(chosen) <= budget else set()


def allows(card, identity, rules, chosen):
    colors = set(filter(None, card.get('arenaMetadata', {}).get('ColorIdentity', '').split(',')))
    return colors <= identity or exempt(card, rules) or any(
        r['kind'] == 'ColorIdentity' and int(r.get('AllowedAdditionalColor', 0)) > 0
        and matches(card, r.get('AllowedAdditionalColorType', '__none__')) and colors <= identity | chosen for r in rules)
