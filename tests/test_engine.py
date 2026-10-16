"""Transport tests use a synthetic sidecar; they do not assert game outcomes."""
import concurrent.futures
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest

from server.engine import Engine, EngineError, EngineTimeout
from scripts.forge_coverage import build_report, classify


SIDECAR = '''import json,sys,time,os
for line in sys.stdin:
    request=json.loads(line)
    op=request['op']
    if op=='sleep': time.sleep(5)
    if op=='crash': sys.exit(3)
    if op=='malformed': print('bad-json',flush=True); continue
    if op=='wrongid': request['id']='wrong'
    if op=='flood':
        for _ in range(10000): print('log diagnostic',file=sys.stderr,flush=True)
    if op=='huge': print('x'*1100000,flush=True); continue
    if op=='fail':
        print(json.dumps({'id':request['id'],'ok':False,'error':'Missing real card'}),flush=True)
        continue
    if op=='environment':
        print(json.dumps({'id':request['id'],'ok':True,'result':{'appdata':os.environ.get('APPDATA'),'pathPresent':bool(os.environ.get('PATH'))}}),flush=True)
        continue
    print(json.dumps({'id':request['id'],'ok':True,'result':{'op':op,'fields':request}}),flush=True)
    if op=='stopreading': time.sleep(5)
'''


class EngineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.script = self.root / 'sidecar.py'
        self.script.write_text(SIDECAR, encoding='utf-8')
        self.command = [str(Path(sys.executable).resolve()), '-u', str(self.script)]
        self.engine = Engine(self.command, self.root, default_timeout=3)

    def tearDown(self):
        self.engine.close()
        self.temp.cleanup()

    def test_config_and_real_envelopes(self):
        config = self.root / 'launch.json'
        config.write_text(json.dumps({'command': self.command, 'cwd': '.', 'defaultTimeout': 3}))
        with Engine.from_config(config) as engine:
            self.assertEqual(engine.status()['op'], 'status')
            self.assertEqual(engine.cards(names=['Island'])['fields']['names'], ['Island'])
            result = engine.simulate(decks=[{'name': 'A', 'cards': [{'name': 'Island', 'quantity': 60}]}])
            self.assertEqual(result['op'], 'simulate')
        self.assertFalse(engine.running)

    def test_timeout_kills_process_and_allows_fresh_restart(self):
        started = time.monotonic()
        with self.assertRaises(EngineTimeout):
            self.engine.request('sleep', timeout=0.2)
        self.assertLess(time.monotonic() - started, 3)
        self.assertEqual(self.engine.status()['op'], 'status')

    def test_timeout_also_bounds_stalled_stdin_writes(self):
        self.engine.request('stopreading')
        started = time.monotonic()
        with self.assertRaises(EngineTimeout):
            self.engine.request('echo', timeout=0.2, payload='x' * 900000)
        self.assertLess(time.monotonic() - started, 3)
        self.assertEqual(self.engine.status()['op'], 'status')

    def test_stderr_is_drained_and_bounded(self):
        self.assertEqual(self.engine.request('flood')['op'], 'flood')
        self.assertLessEqual(len(self.engine.diagnostics), 32)

    def test_crash_bad_json_wrong_id_oversize_and_engine_failure(self):
        for op in ('crash', 'malformed', 'wrongid', 'huge', 'fail'):
            with self.subTest(op=op), self.assertRaises(EngineError):
                self.engine.request(op)
            self.assertEqual(self.engine.status()['op'], 'status')

    def test_concurrent_requests_serialized(self):
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda n: self.engine.request('echo', index=n)['fields']['index'], range(12)))
        self.assertEqual(results, list(range(12)))

    def test_correlated_rejection_preserves_active_process(self):
        self.engine.status()
        process = self.engine._process
        with self.assertRaisesRegex(EngineError, 'Missing real card'):
            self.engine.request('fail')
        self.assertTrue(self.engine.running)
        self.assertIs(self.engine._process, process)
        self.assertEqual(self.engine.status()['op'], 'status')

    def test_close_interrupts_inflight_request(self):
        self.engine.start()
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(self.engine.request, 'sleep')
            time.sleep(0.1)
            self.engine.close()
            with self.assertRaises(EngineError):
                future.result(timeout=3)
        self.assertFalse(self.engine.running)

    def test_request_validation_and_no_shell(self):
        for bad in (0, -1, 3601, '1'):
            with self.assertRaises(ValueError):
                self.engine.request('status', timeout=bad)
        with self.assertRaises(ValueError):
            self.engine.request('status', invalid=float('nan'))
        with self.assertRaises(ValueError):
            Engine(['python', 'sidecar.py'], self.root)

    def test_environment_is_per_child_and_configured(self):
        previous = os.environ.get('APPDATA')
        isolated = str(self.root / 'isolated-profile')
        config = self.root / 'launch.json'
        config.write_text(json.dumps({'command': self.command, 'cwd': '.', 'env': {'APPDATA': isolated}}))
        with Engine.from_config(config) as engine:
            result = engine.request('environment')
        self.assertEqual(result['appdata'], isolated)
        self.assertTrue(result['pathPresent'])
        self.assertEqual(os.environ.get('APPDATA'), previous)
        for env in ({'APPDATA': None}, {'bad=key': 'x'}, {'APPDATA': 'a\0b'}, []):
            with self.assertRaises(ValueError):
                Engine(self.command, self.root, env=env)


class CoverageTests(unittest.TestCase):
    def test_review_flags_prevent_silent_digital_mapping(self):
        for metadata in ({'IsDigitalOnly': 1}, {'IsRebalanced': 1}):
            card = {'grpId': 1, 'name': 'Island', 'arenaMetadata': metadata}
            result = classify(card, {'supported': True, 'forgeName': 'Island'})
            self.assertTrue(result['nameResolved'])
            self.assertTrue(result['requiresReview'])
            self.assertFalse(result['eligibleForForgeSimulation'])
            self.assertEqual(result['rulesEquivalence'], 'unverified')
        alias = classify({'grpId': 2, 'name': 'A-Island'}, {'supported': True, 'forgeName': 'Island'})
        self.assertEqual(alias['reviewReasons'], ['rebalanced', 'name_mismatch'])

    def test_chunked_coverage_duplicate_names_and_missing_names(self):
        class LookupFixture:
            def __init__(self):
                self.calls = []
            def status(self, timeout):
                return {'fixture': True}
            def cards(self, names, timeout):
                self.calls.append(names)
                return {'cards': [{'name': name, 'supported': name != 'Unknown',
                                   'forgeName': name if name != 'Unknown' else None} for name in names]}
        engine = LookupFixture()
        catalogue = [{'grpId': 1, 'name': 'Island'}, {'grpId': 2, 'name': 'Island'},
                     {'grpId': 3, 'name': 'Unknown'}]
        report = build_report(catalogue, engine, chunk_size=1)
        self.assertEqual(engine.calls, [['Island'], ['Unknown']])
        self.assertEqual(report['summary']['nameResolved'], 2)
        self.assertEqual(report['unsupportedNames'], ['Unknown'])
        engine.cards = lambda names, timeout: {'cards': []}
        with self.assertRaises(EngineError):
            build_report(catalogue, engine)


if __name__ == '__main__':
    unittest.main()
