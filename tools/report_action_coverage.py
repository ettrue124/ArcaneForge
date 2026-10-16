"""Report implementation coverage separately from the client's declared schema."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DIRECT = {
    'GameStateMessage': 'ForgeEncoder.cs / BattleUpdates.cs',
    'ActionsAvailableReq': 'BattlePresentation.cs / BattleActionTypes.java',
    'ChooseStartingPlayerReq': 'BattleRequests.cs',
    'ConnectResp': 'MatchDoorHost.cs',
    'GetSettingsResp': 'BattleFlow.cs',
    'SetSettingsResp': 'BattleFlow.cs',
    'GroupReq': 'BattleGuiRequests.cs / BattlePrompts.java (scry top/bottom)',
    'MulliganReq': 'MatchDoorHost.cs / LondonMulligan.java',
    'OrderReq': 'BattleGuiRequests.cs / BattlePrompts.java (card order)',
    'SelectNReq': 'BattleGuiRequests.cs / BattleRequests.cs (entities and labeled options)',
    'DeclareAttackersReq': 'BattleRequests.cs',
    'DeclareBlockersReq': 'BattleRequests.cs / BattleInputs.java (multiple attackers per blocker)',
    'AssignDamageReq': 'BattleGuiRequests.cs / BattlePrompts.java',
    'SelectTargetsReq': 'BattleRequests.cs / BattleInputs.java / BattlePrompts.java (entities and stack spells/abilities)',
    'IntermissionReq': 'BattleCompletion.cs (end-of-game result)',
    'DistributionReq': 'BattleGuiRequests.cs / BattlePrompts.java',
    'NumericInputReq': 'BattleRequests.cs / ForgeInteractiveSession.java',
    'OptionalActionMessage': 'BattleRequests.cs',
    'CastingTimeOptionsReq': 'BattleRequests.cs / BattleNativeMenus.cs (X and resolvable modal choices)',
    'StringInputReq': 'BattleGuiRequests.cs / BattlePrompts.java',
}
DIRECT.update({name:'BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved.'
    for name in ('SelectReplacementReq','SelectNGroupReq','SearchReq','SelectFromGroupsReq','SearchFromGroupsReq','SelectCountersReq','GatherReq','SubmitDeckReq')})
ALTERNATE = {
    'PayCostsReq': 'Forge payment input uses mana actions, bounded numbers, card selections, and confirmations.',
    'SelectReplacementReq': 'Forge replacement choices/order use labeled options; dedicated replacement browser not implemented.',
    'SelectNGroupReq': 'Forge choices use SelectNReq; dedicated pile browser not implemented.',
    'SearchReq': 'Forge search candidates use SelectNReq; dedicated name-search browser not implemented.',
    'SelectFromGroupsReq': 'Forge choices use SelectNReq; dedicated group-search browser not implemented.',
    'SearchFromGroupsReq': 'Forge choices use SelectNReq; dedicated group-search browser not implemented.',
    'SelectCountersReq': 'Counter type/entity choices use labeled options; dedicated counter-transfer browser not implemented.',
}
ACTION_VALUES = {1, 2, 3, 4, 5, 8, 10, 11, 16, 18, 19, 21, 22, 23, 24}

def main():
    directory = ROOT/'analysis/action-inventory'
    inventory = json.loads((directory/'actions.json').read_text(encoding='utf8'))
    entries = []
    for message in inventory['incoming_messages']:
        name = message['name']
        status = 'native adapter' if name in DIRECT else 'alternate workflow' if name in ALTERNATE else 'not implemented'
        if not message['registered'] and name not in DIRECT:
            status = 'declared only / no recovered handler'
        entries.append({'name': name, 'value': message['value'], 'status': status,
            'details': DIRECT.get(name, ALTERNATE.get(name, 'No adapter claimed.')),
            'visual_gameplay_verified': False})
    result = {'assembly_sha256': inventory['assembly_sha256'], 'messages': entries,
        'action_types': [dict(action, emitted=action['value'] in ACTION_VALUES,
            visual_gameplay_verified=False) for action in inventory['action_types']],
        'validation': ['Python regression suite; current counts/results are in the latest audit', 'Native installed-client parser/response tests',
            'Real Forge scry/modes/divided-damage/trample tests', 'Real Forge X/fight regression',
            'Real Forge MDFC land and Adventure casting/linked-face tests',
            'Real Forge Treasure/Food/creature-token identities, creation and sacrifice effects',
            'Real Forge counterspell stack targeting and multi-attacker blocking',
            'Installed Arena player energy/poison counter-state parsers'],
        'remaining': ['Stock Arena visual workflows and all card variants need playtesting.',
            'Best-of-three sideboarding is not supported.',
            'Generic amount prompts do not transmit per-recipient maximums to Arena; Forge validates them.',
            'Arbitrary non-card ordering uses click-selection order; its native UI order is unverified.',
            'No full official-server gameplay/animation parity is claimed.']}
    (directory/'adapter-coverage.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf8')
    lines = ['# Arena adapter coverage', '',
        'This implementation report is separate from the extracted protocol inventory. '
        'A declared enum is not proof of a gameplay feature. No entry claims full visual verification.', '',
        'Real Forge tests pass for scry placement, mode/card choices, divided damage, trample damage, X/fight spells, MDFC land faces and Adventure spell faces. '
        'Treasure/Food/creature tokens, counterspell stack targets and multiple attackers per blocker also pass real Forge tests. '
        'The installed Arena request constructors, token animation parsers, player counter state and response mappings are tested without a Unity scene.', '',
        '| Message | Implementation | Adapter / limitation |', '| --- | --- | --- |']
    lines += [f"| {e['name']} | {e['status']} | {e['details']} |" for e in entries]
    lines += ['', '## Action values', '',
        'The bridge now distinguishes ordinary casts/activations/land plays/mana, pass, turning face up, '
        'split-card halves, Adventure, modal double-faced backs, Prototype, Rooms and Omen from Forge state. '
        'Test/placeholder and specialized payment action values are not sent blindly.', '', '## Remaining', '']
    lines += ['- '+item for item in result['remaining']]
    (directory/'adapter-coverage.md').write_text('\n'.join(lines)+'\n', encoding='utf8')
    print('Wrote '+str(directory/'adapter-coverage.md'))

if __name__ == '__main__':
    main()
