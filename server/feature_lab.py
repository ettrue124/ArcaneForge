"""Prepared local matches, separate from saved decks and normal match rules."""
from collections import Counter
from .storage import StorageError

LANDS = ['Plains', 'Island', 'Swamp', 'Mountain', 'Forest'] * 4
SCENARIOS = {
    'life-payments': dict(name='Pay life confirmations',
        goal='Play Hallowed Fountain and pay 2 life, then play Blood Crypt on a later turn and decline. Check the Pay Life prompt, life total, and tapped/untapped entry.',
        hand=['Hallowed Fountain','Blood Crypt'], battlefield=[]),
    'blocking-restrictions': dict(name='Conditional blocking icons',
        goal='Inspect Questing Beast’s blocking restriction and tooltip. Cast Minimus Containment on it; the restriction icon should disappear when its abilities are removed.',
        hand=['Minimus Containment'], battlefield=['Questing Beast'], opponentBattlefield=['Grizzly Bears']),
    'illegal-attachments': dict(name='Protection removes an Aura',
        goal='Cast Pacifism on your Grizzly Bears, then Gods Willing on it and choose white. The Aura should detach and go to the graveyard with the protection effect.',
        hand=['Pacifism','Gods Willing'], battlefield=['Grizzly Bears']),
    'temporary-stats': dict(name='Temporary power and toughness',
        goal='Attack with Goblin Piledriver and other Goblins. Its boosted power should be blue, then return to normal after combat. Giant Growth checks both power and toughness.',
        hand=['Giant Growth'], battlefield=['Goblin Piledriver','Goblin Instigator','Goblin Arsonist']),
    'color-choice': dict(name='Chosen colors',
        goal='Cast Caged Sun and choose a color. Check that the chosen color remains visible on the card and the choice plays once.',
        hand=['Caged Sun'], battlefield=['Grizzly Bears']),
    'ability-changes': dict(name='Remove printed abilities and grant new abilities',
        goal='Cast Minimus Containment on Serra Angel. It should become a Treasure artifact, lose flying and vigilance, and gain the tap-and-sacrifice mana ability. Check the changed type, rules text and available activation.',
        hand=['Minimus Containment'], battlefield=['Serra Angel', 'Grizzly Bears']),
    'designations': dict(name='Monstrous, monarch and city blessing',
        goal='Activate Fleecemane Lion’s monstrosity ability. Cast Palace Jailer to become the monarch. Arch of Orazca checks the city’s blessing with ten permanents. Check that status indicators persist and monarch can transfer through combat.',
        hand=['Palace Jailer'], battlefield=['Fleecemane Lion', 'Arch of Orazca'], opponentBattlefield=['Grizzly Bears']),
    'models': dict(name='Class levels, color and type changes',
        goal='Cast Wizard Class and activate its level abilities. Use Scuttlemutt’s color ability on Grizzly Bears. Witness Protection checks creature type, stats and removed abilities. Watch cards entering their zones and markers clearing on later turns.',
        hand=['Wizard Class', 'Witness Protection'], battlefield=['Grizzly Bears', 'Scuttlemutt']),
    'multistep': dict(name='Scry, surveil, explore, proliferate and connive',
        goal='Cast Opt for scry, Consider for surveil, Merfolk Branchwalker for explore, Experimental Augury for proliferate and Raffine’s Informant for connive. Check effect start/finish, card destinations and counter updates.',
        hand=['Opt', 'Consider', 'Merfolk Branchwalker', 'Experimental Augury', "Raffine's Informant"], battlefield=['Ugin, the Spirit Dragon'], loyalty=6),
    'copies': dict(name='Copies and changed card models',
        goal='Cast Clone and choose Serra Angel. Check that it takes the copied art, rules and stats, without showing a false reverse side. Giant Growth checks a temporary model change.',
        hand=['Clone', 'Giant Growth', 'Unsummon'], battlefield=['Serra Angel', 'Grizzly Bears']),
    'facedown': dict(name='Morph, cloak and face-up actions',
        goal='Cast Rattleclaw Mystic or Sagu Mauler face down, then use its face-up action. Cryptic Coat checks cloak and ward. Hide in Plain Sight checks cloaked cards entering from the library.',
        hand=['Rattleclaw Mystic', 'Sagu Mauler', 'Hooded Hydra', 'Cryptic Coat', 'Hide in Plain Sight'], battlefield=[]),
    'replacements': dict(name='Replacement effects and layered changes',
        goal='Cast Mossborn Hydra under Doubling Season to check counter replacement playback. Play a land for its landfall trigger. Giant Growth checks power/toughness playback, and Grizzly Bears under the enchantment checks ordinary creature entry.',
        hand=['Mossborn Hydra', 'Giant Growth', 'Forest', 'Grizzly Bears', 'Opt'],
        battlefield=['Doubling Season', 'Grizzly Bears'], opponentBattlefield=['Grizzly Bears']),
    'effects': dict(name='Random results, regeneration and control',
        goal='Cast Grizzly Bears under Mirror March for coin flips, Contact Other Plane for a d20, and Opt for scry. Choose regeneration with Golgari Charm before casting Murder on your creature. Act of Treason checks control changes.',
        hand=['Grizzly Bears', 'Contact Other Plane', 'Opt', 'Golgari Charm', 'Murder', 'Act of Treason'],
        battlefield=['Mirror March', 'Grizzly Bears'], opponentBattlefield=['Grizzly Bears']),
    'ugin': dict(name="Ugin's ultimate", goal='Activate Ugin −10. Choose zero, one, or several permanents; confirm and check that resolution finishes.',
        hand=['Esika, God of the Tree', 'Sol Ring', 'Llanowar Elves', 'Elvish Mystic', 'Forest', 'Island', 'Doubling Season'],
        battlefield=['Ugin, the Spirit Dragon'], loyalty=12),
    'discard': dict(name='Opponent-hand discard', goal='Cast Thoughtseize or Duress. Check that the opponent’s hand is visible and only legal cards can be selected.',
        hand=['Thoughtseize', 'Duress', 'Thought Erasure'], battlefield=[]),
    'library': dict(name='Library choices', goal='Use Opt for scry, Consider for surveil, Impulse for selection and bottom ordering, and Evolving Wilds for search.',
        hand=['Opt', 'Consider', 'Impulse', 'Evolving Wilds', 'Cultivate'], battlefield=[]),
    'combat': dict(name='Combat and phasing', goal='Attack the opponent or their planeswalker. Use Teferi’s Protection to check phasing; use Revitalize for life gain.',
        hand=["Teferi's Protection", 'Revitalize', 'Giant Growth'], battlefield=['Serra Angel', 'Grizzly Bears'],
        opponentBattlefield=['Grizzly Bears', 'Ugin, the Spirit Dragon']),
    'counters': dict(name='Energy, loyalty and proliferate', goal='Cast Attune with Aether for energy, Experimental Augury for proliferate, and Doubling Season for counter replacement choices. Activate Teferi −12 for an emblem.',
        hand=['Attune with Aether', 'Experimental Augury', 'Doubling Season'], battlefield=['Teferi, Hero of Dominaria', 'Vorinclex, Monstrous Raider'], loyalty=14),
    'choices': dict(name='Modes, types and cycling', goal='Cast Supreme Will for modes, Icon of Ancestry for a creature type, and Arcane Adaptation for another type choice. Select cycling explicitly on Lonely Sandbar.',
        hand=['Supreme Will', 'Icon of Ancestry', 'Arcane Adaptation', 'Lonely Sandbar'], battlefield=['Grizzly Bears']),
    'mdfc': dict(name='Modal double-faced cards (Esika)', goal='Drag or double-click Esika, God of the Tree from hand to cast it. The modal picker appears to choose between Esika {1}{G}{G} and The Prismatic Bridge {W}{U}{B}{R}{G}.',
        hand=['Esika, God of the Tree', 'Branchloft Pathway', 'Bala Ged Recovery', "Emeria's Call", 'Valakut Awakening'], battlefield=[]),
}

def prepare(scenario_id, catalogue):
    if scenario_id not in SCENARIOS:
        raise StorageError('Unknown feature scenario', 400)
    s = SCENARIOS[scenario_id]
    by_name = {}
    for gid, card in catalogue.items():
        by_name.setdefault(card['name'], int(gid))
    opponent_hand = ['Grizzly Bears', 'Serra Angel', 'Giant Growth', 'Forest', 'Island', 'Sol Ring', 'Duress']
    setups = [dict(hand=s['hand'], battlefield=LANDS+s['battlefield'], loyalty=s.get('loyalty', 6)),
              dict(hand=opponent_hand, battlefield=s.get('opponentBattlefield', []), loyalty=6)]
    decks = []
    for i, setup in enumerate(setups):
        names = setup['hand'] + setup['battlefield'] + ['Forest']*30 + ['Island']*10
        missing = sorted(set(names)-by_name.keys())
        if missing:
            raise StorageError('Scenario cards unavailable: '+', '.join(missing), 400)
        decks.append(dict(name='Feature Test '+str(i+1), format='Historic', cards=[
            dict(name=n, quantity=q, grpId=by_name[n]) for n,q in Counter(names).items()]))
    return decks, dict(id=scenario_id, players=setups)

def listing():
    return [dict(id=k, name=v['name'], goal=v['goal']) for k,v in SCENARIOS.items()]
