"""Refresh Commander bans from Wizards, then build the local Arena format entries."""
import argparse
import json
import re
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[1]
BANS_URL = 'https://magic.wizards.com/en/banned-restricted-list'
GLOBAL_URL = 'https://magic.wizards.com/en/news/announcements/depictions-racism-magic-2020-06-10'


class Lists(HTMLParser):
    def __init__(self):
        super().__init__()
        self.heading = ''
        self.capture = None
        self.text = []
        self.items = []

    def handle_starttag(self, tag, attrs):
        if tag in ('h2', 'h3', 'li') and self.capture is None:
            self.capture, self.text = tag, []

    def handle_data(self, data):
        if self.capture:
            self.text.append(data)

    def handle_endtag(self, tag):
        if tag != self.capture:
            return
        value = ' '.join(''.join(self.text).split())
        if tag in ('h2', 'h3'):
            self.heading = value
        else:
            self.items.append((self.heading, value))
        self.capture = None


def fetch(url):
    with urlopen(Request(url, headers={'User-Agent': 'ArenaLab-Commander/1.0'}), timeout=30) as response:
        parser = Lists()
        parser.feed(response.read().decode('utf8'))
        return parser.items


def refresh():
    entries = [value for heading, value in fetch(BANS_URL) if heading.casefold() == 'commander banned cards']
    categories = [v for v in entries if v.startswith(('25 cards', '9 cards', 'Cards whose'))]
    companion = [v.split(' - only banned as a companion.')[0] for v in entries if 'only banned as a companion' in v]
    named = [v for v in entries if v not in categories and 'only banned as a companion' not in v]
    if not 30 <= len(named) <= 80 or len(categories) != 3 or companion != ['Lutri, the Spellchaser']:
        raise ValueError('Wizards Commander page structure changed; refusing to replace the verified ban data')
    global_bans = [v for _, v in fetch(GLOBAL_URL) if v in {
        'Invoke Prejudice', 'Cleanse', 'Stone-Throwing Devils', 'Pradesh Gypsies', 'Jihad', 'Imprison', 'Crusade'}]
    if len(set(global_bans)) != 7:
        raise ValueError('Global ban source changed; refusing an incomplete update')
    # Wizards bans these entire categories. Expand using the pinned Forge card
    # definitions so the saved list also includes cards absent from Arena.
    conspiracy, ante = [], []
    for path in (ROOT / 'vendor/forge/forge-gui/res/cardsfolder').rglob('*.txt'):
        rules = path.read_text(encoding='utf8')
        name = re.search(r'^Name:(.+)$', rules, re.M)
        if not name:
            continue
        if re.search(r'^Types:.*\bConspiracy\b', rules, re.M):
            conspiracy.append(name[1])
        if re.search(r'^K:.*[Rr]emove .*playing for ante', rules, re.M):
            ante.append(name[1])
    if len(set(conspiracy)) != 25 or len(set(ante)) != 9:
        raise ValueError(f'Category expansion changed: {len(set(conspiracy))} conspiracies, {len(set(ante))} ante cards')
    data = {
        'format': 'Commander', 'checkedAt': datetime.now(timezone.utc).isoformat(),
        'sources': [BANS_URL, GLOBAL_URL],
        'bannedNames': sorted(set(named + global_bans + conspiracy + ante)),
        'namedCommanderBans': sorted(named), 'bannedAsCompanion': companion,
        'categoryBans': {'Conspiracy': sorted(set(conspiracy)), 'Ante': sorted(set(ante)), 'Global': sorted(set(global_bans))},
        'excludedCardPools': ['silver border', 'gold border', 'acorn stamp', 'digital-only', 'rebalanced']
    }
    (ROOT / 'data/commander-bans.json').write_text(json.dumps(data, indent=2) + '\n', encoding='utf8')
    return data


def formats(data):
    catalogue = json.loads((ROOT / 'data/catalogue.json').read_text(encoding='utf8'))
    # Rebuild title-specific exceptions whenever installed printings change.
    from export_rulebreakers import export
    rulebreaker_table = export(catalogue)
    configuration = ROOT / 'data/local-client/configuration-state.json'
    if configuration.exists():
        installation = Path(json.loads(configuration.read_text(encoding='utf-8-sig'))['installation'])
        destination = installation / 'MTGA_Data/StreamingAssets/ArcaneForgeCards/rulebreaker-decks.tsv'
        if destination.parent.exists():
            destination.write_bytes(rulebreaker_table.read_bytes())
    banned = set(data['bannedNames'])
    ban_ids = sorted({c['arenaMetadata']['TitleId'] for c in catalogue if c['name'] in banned})
    # Arena shares title IDs between some paper and digital printings, including
    # Forest and Wastes. A title ban would exclude every printing of those cards.
    paper_ids = {c['arenaMetadata']['TitleId'] for c in catalogue
                 if not any(c['arenaMetadata'].get(k) for k in ('IsDigitalOnly', 'IsRebalanced', 'IsToken'))}
    digital_ids = {c['arenaMetadata']['TitleId'] for c in catalogue
                   if c['arenaMetadata'].get('IsDigitalOnly') or c['arenaMetadata'].get('IsRebalanced')} - paper_ids
    eligible = set(json.loads((ROOT / 'data/commander-eligible.json').read_text(encoding='utf8'))['names'])
    commander_ids = sorted({c['arenaMetadata']['TitleId'] for c in catalogue if c['name'] in eligible
                            and c['name'] not in banned and not any(c['arenaMetadata'].get(k)
                            for k in ('IsDigitalOnly', 'IsRebalanced', 'IsToken'))})
    fixture_path = ROOT / 'analysis/deckbuilder-formats.json'
    fixture = json.loads(fixture_path.read_text(encoding='utf8'))
    historic = next(f for f in fixture['GetFormatsResponse']['formats'] if f['name'] == 'Historic')
    no_bans_60 = dict(historic, name='NoBans60', mainDeckQuota={'min': 60, 'max': 60},
                      commandZoneQuota={'min': 0, 'max': 0}, sideBoardQuota={'min': 0, 'max': 15},
                      bannedTitleIds=[], suspendedTitleIds=[], supressedTitleIds=[], individualCardQuotas={})
    commander = dict(historic, name='Commander', cardCountRestrictionInternal=1,
                     mainDeckQuota={'min': 99, 'max': 99}, commandZoneQuota={'min': 1, 'max': 1},
                     sideBoardQuota={'min': 0, 'max': 1}, sideboardBehaviorInternal=1,
                     bannedTitleIds=sorted(set(ban_ids) | digital_ids), allowedCommanderTitleIds=commander_ids,
                     restrictToCommandZoneCardTypeInternal=[], useRebalancedCards=False)
    multiplayer = dict(commander, name='Commander4')
    unbanned = dict(commander, name='CommanderUnbanned', bannedTitleIds=[],
                    allowedCommanderTitleIds=sorted({c['arenaMetadata']['TitleId'] for c in catalogue if c['name'] in eligible}))
    response = fixture['GetFormatsResponse']
    response['formats'] = [f for f in response['formats'] if f['name'] not in ('Commander', 'Commander4', 'CommanderUnbanned', 'NoBans60')] + [commander, multiplayer, unbanned, no_bans_60]
    for group in response['formatGroups']:
        values = group.get('formats', group.get('formatNames', []))
        for name in ('Commander', 'Commander4', 'CommanderUnbanned', 'NoBans60'):
            if name not in values:
                values.append(name)
    fixture['commanderBanSources'] = data['sources']
    fixture['commanderBanCheckedAt'] = data['checkedAt']
    fixture['commands']['GetFormats']['payload'] = response
    fixture_path.write_text(json.dumps(fixture, indent=2) + '\n', encoding='utf8')
    modes_path = ROOT / 'analysis/startup-fixtures.json'
    modes_fixture = json.loads(modes_path.read_text(encoding='utf8'))
    modes = [('AIBotMatch', 'ArenaLab/Modes/Play', 'Historic', 'ArenaLab/DeckSize/60'),
             ('CommanderAI', 'ArenaLab/Modes/Commander', 'Commander', 'ArenaLab/DeckSize/100'),
             ('CommanderAI4', 'ArenaLab/Modes/Commander4', 'Commander', 'ArenaLab/DeckSize/100'),
             ('CommanderUnbannedAI', 'ArenaLab/Modes/CommanderUnbanned', 'CommanderUnbanned', 'ArenaLab/DeckSize/100'),
             ('NoBans60AI', 'ArenaLab/Modes/NoBans60', 'NoBans60', 'ArenaLab/DeckSize/Exactly60')]
    modes_fixture['commands']['GetPlayBladeQueueConfig']['payload'] = [
        {'Id': event, 'QueueType': 'Unranked', 'LocTitle': title,
         'EventNameBO1': event, 'EventNameBO3': None, 'DeckSizeBO1': size,
         'DeckSizeBO3': None, 'SideBoardBO1': 'ArenaLab/Sideboard/15' if format_name == 'NoBans60' else 'ArenaLab/Sideboard/None', 'SideBoardBO3': None,
         'Tags': [format_name]} for event, title, format_name, size in modes]
    active = modes_fixture['commands']['EventGetActiveEventsV2']['payload']
    active['CacheVersion'] = 2
    active['AiBotMatches'] = []
    active['Events'] = [
        {'InternalEventName': event, 'EventState': 'Active', 'FormatType': 'Constructed',
         'StartTime': '2000-01-01T00:00:00Z', 'LockedTime': '2099-01-01T00:00:00Z',
         'ClosedTime': '2099-01-01T00:00:00Z', 'Flags': ['IsArenaPlayModeEvent', 'IsAiBotMatch'],
         'PastEntries': {}, 'EntryFees': [{'CurrencyType': 'None', 'Quantity': 0}],
         'EventTags': [], 'WinCondition': 'SingleElimination', 'MinimumRank': -1,
         'AllowedCountryCodes': [], 'ExcludedCountryCodes': [],
         'EventUXInfo': {'PublicEventName': event, 'DisplayPriority': index,
            'Parameters': {}, 'Group': '', 'EventBladeBehavior': 'Queue',
            'Joinability': 'Open', 'DynamicFilterTagIds': [], 'DeckSelectFormat': format_name,
            'Prizes': {}, 'FactionSealedUXInfo': [], 'EventComponentData': {
                'TitleRankText': {'LocKey': title}, 'DescriptionText': {'LocKey': title},
                'SelectedDeckWidget': {'DeckButtonBehavior': 'Editable'},
                'MainButtonWidget': {}}}}
        for index, (event, title, format_name, _) in enumerate(modes)]
    modes_path.write_text(json.dumps(modes_fixture, indent=2) + '\n', encoding='utf8')
    print(f'Commander: {len(banned)} banned card names; {len(ban_ids)} installed banned titles; Lutri banned only as companion.')


if __name__ == '__main__':
    args = argparse.ArgumentParser()
    args.add_argument('--cached', action='store_true', help='Rebuild format entries from previously verified bans')
    opts = args.parse_args()
    formats(json.loads((ROOT / 'data/commander-bans.json').read_text(encoding='utf8')) if opts.cached else refresh())
