"""Exercise real Forge human prompts without an Arena client."""
import sys
import time
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from server.engine import Engine, EngineError

engine = Engine.from_config(Path(__file__).with_name('launch.json'))
try:
    deck = {'name': 'Controller test', 'cards': [{'name': 'Plains', 'quantity': 60}]}
    result = engine.request('interactiveStart', decks=[deck, deck])
    assert result['mode'] == 'human-vs-ai'
    deadline = time.monotonic() + 90
    played_land = False
    verified_land = False
    saw_buttons = False
    saw_mulligan = False
    redraws = 0
    bottomed = False
    bottom_counts = []
    while time.monotonic() < deadline:
        batch = engine.request('interactivePoll')
        state = batch.get('state') or {}
        if batch.get('inputType') == 'InputConfirmMulligan':
            saw_mulligan = True
            if batch.get('buttonEventId'):
                assert len(state['hand']) == 7, 'London redraw must show seven before keeping'
            snapshot = engine.request('interactiveSnapshot')
            assert not snapshot['legalActions'], 'Cards offered as normal plays during mulligan'
        verified_land = verified_land or bool(state.get('battlefield'))
        for event in batch['events']:
            if event['blocking']:
                method = event['method']
                args = event['arguments']
                if method in ('confirm', 'showConfirmDialog'):
                    answer = True
                elif method in ('showOptionDialog', 'getAbilityToPlay'):
                    answer = 0
                elif method == 'getChoices':
                    answer = list(range(args[1]))
                else:
                    raise AssertionError(f'Unhandled real prompt: {method}')
                engine.request('interactiveAnswer', eventId=event['eventId'], answer=answer)
            elif event['method'] == 'updateButtons':
                saw_buttons = True
                if event['eventId'] != batch.get('buttonEventId'):
                    continue
                if batch.get('inputType') == 'InputConfirmMulligan' and redraws < 2:
                    try:
                        engine.request('interactiveAction', type='cancel', eventId=event['eventId'])
                    except EngineError as error:
                        if 'Stale human action' in str(error):
                            continue
                        raise
                    redraws += 1
                    continue
                if batch.get('inputType') == 'InputLondonMulligan':
                    assert batch['mulliganCount'] == redraws
                    assert len(state['hand']) == 7
                    selected = [card['instanceId'] for card in state['hand'][:redraws]]
                    engine.request('interactiveAction', type='bottomCards', instanceIds=selected, eventId=event['eventId'])
                    bottomed = True
                    bottom_counts.append(redraws)
                    continue
                if state.get('humanTurn') and state.get('phase') == 'MAIN1' and not played_land and state.get('hand'):
                    try:
                        engine.request('interactiveAction', type='selectCard', eventId=event['eventId'], instanceId=state['hand'][0]['instanceId'])
                    except EngineError as error:
                        if 'Stale human action' in str(error):
                            continue
                        raise
                    played_land = True
                else:
                    try:
                        engine.request('interactiveAction', type='ok', eventId=event['eventId'])
                    except EngineError as error:
                        if 'Stale human action' not in str(error):
                            raise
        if verified_land and state.get('turn', 0) >= 3:
            break
        time.sleep(.05)
    assert verified_land, 'Explicit human action did not play a land'
    assert state.get('turn', 0) >= 3, 'AI did not finish its turn'
    assert saw_buttons, 'No real human action buttons received'
    assert saw_mulligan, 'Real Forge mulligan input was not exposed'
    assert redraws == 2 and bottomed, 'Two redraws and London bottom selection did not complete'
    assert bottom_counts == [2], 'London bottom selection must occur once after keeping'
    assert len(state['hand']) <= 5, 'Two mulligans must leave five cards before drawing'
    engine.request('interactiveAction', type='concede')
    engine.request('interactiveClose')
    print('PASS: real Forge human prompts and explicit controller actions against AI')
finally:
    engine.close()
