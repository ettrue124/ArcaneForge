import json
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from tools import repair_arena_animations as runner


class AnimationRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.output = Path(self.temporary.name)
        patcher = patch.object(runner, 'OUTPUT', self.output)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.audit = runner.Audit()

    def test_failed_check_does_not_hide_later_results(self):
        self.assertFalse(self.audit.run('Failure', [sys.executable, '-c', 'raise SystemExit(7)']))
        self.assertTrue(self.audit.run('Later check', [sys.executable, '-c', 'print("ok")']))
        self.audit.finished = True
        self.audit.save()
        report = json.loads((self.audit.directory/'report.json').read_text(encoding='utf8'))
        self.assertEqual(report['status'], 'checks_need_attention')
        self.assertEqual([r['status'] for r in report['results']], ['failed', 'passed'])
        self.assertIn('Exit code 7', report['results'][0]['detail'])

    def test_skipped_tests_and_headless_passes_are_reported_honestly(self):
        self.assertFalse(self.audit.run('Skipped', [sys.executable, '-c', 'print("OK (skipped=1)")'], reject_skips=True))
        self.assertEqual(self.audit.results[-1]['status'], 'incomplete')
        self.audit.results.clear()
        self.assertTrue(self.audit.run('Successful check', [sys.executable, '-c', 'print("ok")']))
        self.audit.finished = True
        self.audit.save()
        report = json.loads((self.audit.directory/'report.json').read_text(encoding='utf8'))
        self.assertEqual(report['status'], 'checks_passed_visual_review_needed')
        self.assertIn('Not performed', report['visual_validation'])

    def test_timeout_terminates_its_process_and_preserves_report(self):
        self.assertFalse(self.audit.run('Hung check', [sys.executable, '-c', 'import time; time.sleep(60)'], timeout=.2))
        self.assertEqual(self.audit.results[-1]['status'], 'failed')
        self.assertIn('Timed out', self.audit.results[-1]['detail'])
        self.assertTrue((self.audit.directory/'report.json').is_file())

    def test_failed_export_restores_all_previous_metadata(self):
        workspace=self.output/'fixture'
        (workspace/'data').mkdir(parents=True)
        original=workspace/'data'/runner.GENERATED[0]
        original.write_text('previous data',encoding='utf8')
        def failed_export(*args, **kwargs):
            original.write_text('partly changed data',encoding='utf8')
            (workspace/'data'/runner.GENERATED[1]).write_text('new data',encoding='utf8')
            return False
        with patch.object(runner,'ROOT',workspace), patch.object(self.audit,'run',side_effect=failed_export):
            self.assertFalse(runner.repair_metadata(self.audit))
        self.assertEqual(original.read_text(encoding='utf8'),'previous data')
        self.assertFalse((workspace/'data'/runner.GENERATED[1]).exists())

    def test_lock_excludes_another_process_and_releases_on_exit(self):
        code = ('import sys\nfrom pathlib import Path\nfrom tools import repair_arena_animations as r\n'
                'r.OUTPUT=Path(sys.argv[1])\nwith r.run_lock(): pass\n')
        command = [sys.executable, '-c', code, str(self.output)]
        with runner.run_lock():
            rejected=subprocess.run(command,cwd=runner.ROOT,capture_output=True,timeout=10)
            self.assertNotEqual(rejected.returncode,0,'Concurrent repair acquired the same lock')
        allowed=subprocess.run(command,cwd=runner.ROOT,capture_output=True,timeout=10)
        self.assertEqual(allowed.returncode,0,allowed.stderr.decode('utf8',errors='replace'))


if __name__ == '__main__':
    unittest.main()
