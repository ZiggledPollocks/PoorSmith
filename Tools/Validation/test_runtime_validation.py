import unittest
from pathlib import Path
import runtime_validation as r

class RuntimeGuards(unittest.TestCase):
    def test_identity_override_is_narrow(self):
        original = '  companyName: DefaultCompany\n  productName: batterMap\n  other: untouched\n'
        changed = r.isolate_settings(original, 'abcdef012345')
        self.assertEqual(r.identity(changed), ['BatterMapValidation', 'Run_abcdef012345'])
        self.assertTrue(changed.endswith('  other: untouched\n'))
        with self.assertRaises(ValueError): r.isolate_settings(original, '../unsafe')
        with self.assertRaises(ValueError): r.identity(original + '  productName: duplicate\n')

    def test_completion_does_not_accept_process_exit_alone(self):
        args = (0, False, None, '', 'abcdef012345', Path('project'), '6000.3.11f1')
        self.assertEqual(r.classify(*args)[0], 'failed')

    def test_timeout_and_compiler_error_override_completion(self):
        marker = self.marker()
        for code, timeout, log, expected in [(0, True, self.log(), 'blocked'),
                (0, False, 'error CS1000: broken', 'failed'),
                (1, False, self.log(), 'failed')]:
            self.assertEqual(r.classify(code, timeout, marker, log, 'abcdef012345', Path('project'), '6000.3.11f1')[0], expected)

    def marker(self):
        return dict(runId='abcdef012345', unityVersion='6000.3.11f1', projectPath='project',
            isPlaying=True, status='passed', checks=[{'name': n, 'passed': True} for n in r.REQUIRED_CHECKS], errors=[])

    def log(self): return 'BATTERMAP_RUNTIME_COMPLETE abcdef012345'

    def test_stale_failed_and_incomplete_checks_are_rejected(self):
        for key, value in [('runId', 'old'), ('isPlaying', False), ('checks', []),
                ('errors', ['NullReferenceException']), ('status', 'failed')]:
            marker = self.marker()
            marker[key] = value
            self.assertEqual(r.classify(0, False, marker, self.log(), 'abcdef012345', Path('project'), '6000.3.11f1')[0], 'failed')
        marker = self.marker()
        marker['checks'][0]['passed'] = False
        self.assertEqual(r.classify(0, False, marker, self.log(), 'abcdef012345', Path('project'), '6000.3.11f1')[0], 'failed')

    def test_required_scope_and_editor_exceptions(self):
        self.assertEqual(r.classify(0, False, self.marker(), self.log(), 'abcdef012345', Path('project'), '6000.3.11f1')[0], 'passed')
        marker = self.marker()
        marker['checks'] = [{'name': 'font', 'passed': True} for _ in range(200)]
        self.assertEqual(r.classify(0, False, marker, self.log(), 'abcdef012345', Path('project'), '6000.3.11f1')[0], 'failed')
        self.assertEqual(r.classify(0, False, self.marker(), self.log() + '\nArgumentOutOfRangeException: editor error', 'abcdef012345', Path('project'), '6000.3.11f1')[0], 'failed')

if __name__ == '__main__': unittest.main()
