"""Meaningful guard and outcome tests. All writes use temporary disposable fixtures."""
import argparse
import json
from pathlib import Path
import sys
import tempfile
import unittest
import unity_validation as v


class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.source = self.root / 'source'
        for folder in v.INPUTS: (self.source / folder).mkdir(parents=True)
        (self.source / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.3.11f1\n')
        (self.source / 'Packages/manifest.json').write_text('{"dependencies": {}}')
        self.unity = self.root / '6000.3.11f1/Editor/Unity.exe'
        self.unity.parent.mkdir(parents=True)
        self.unity.write_bytes(b'fixture only, never launched')
        self.dest = self.root / 'runs/new'
        self.marker = dict(runId='trial', unityVersion='6000.3.11f1', projectPath=str(self.source),
                           status='passed', isPlaying=False, target='StandaloneWindows64')

    def outcome(self, code=0, timeout=False, marker=None, log=''):
        return v.classify(code, timeout, marker, log, 'trial', self.source, '6000.3.11f1')

    def test_positive_evidence_required(self):
        self.assertEqual(self.outcome(marker=self.marker, log='BATTERMAP_COMPILE_COMPLETE trial')[0], 'passed')
        self.assertEqual(self.outcome()[0], 'failed')
        self.assertEqual(self.outcome(marker=self.marker)[0], 'failed')

    def test_stale_marker_and_nonzero_exit_cannot_pass(self):
        self.marker['runId'] = 'previous-run'
        self.assertEqual(self.outcome(marker=self.marker, log='BATTERMAP_COMPILE_COMPLETE trial')[0], 'failed')
        self.marker['runId'] = 'trial'
        self.assertEqual(self.outcome(code=1, marker=self.marker, log='BATTERMAP_COMPILE_COMPLETE trial')[0], 'failed')

    def test_compiler_error_overrides_a_marker(self):
        self.assertEqual(self.outcome(marker=self.marker, log='error CS1029: #error intentional\nBATTERMAP_COMPILE_COMPLETE trial'), ('failed', 'compiler_error'))

    def test_blocked_conditions(self):
        self.assertEqual(self.outcome(timeout=True), ('blocked', 'timeout'))
        self.assertEqual(self.outcome(code=1, log='No valid Unity Editor license'), ('blocked', 'licensing'))
        self.assertEqual(self.outcome(code=1, log='An error occurred while resolving packages'), ('blocked', 'package_resolution'))

    def test_recovered_license_warning_does_not_override_positive_completion(self):
        log = '[Licensing::Client] Error during initial check\nRecovered\nBATTERMAP_COMPILE_COMPLETE trial'
        self.assertEqual(self.outcome(marker=self.marker, log=log)[0], 'passed')

    def test_asset_import_failure_prevents_success_even_after_compilation(self):
        log = 'Asset import failed, DirectoryNotFoundException\nBATTERMAP_COMPILE_COMPLETE trial'
        self.assertEqual(self.outcome(marker=self.marker, log=log), ('failed', 'asset_import_error'))

    def test_long_windows_output_path_is_rejected(self):
        if sys.platform != 'win32': self.skipTest('Windows path guard')
        with self.assertRaises(ValueError):
            v.preflight(self.source, self.unity, self.root / ('too-long-' * 20) / 'new')

    def test_original_and_overlapping_paths_rejected_without_writes(self):
        before = v.snapshot(self.source)
        for destination in [self.source, self.source / 'nested', self.root]:
            with self.assertRaises(ValueError): v.preflight(self.source, self.unity, destination)
        self.assertEqual(before, v.snapshot(self.source))
        self.assertFalse((self.source / 'nested').exists())

    def test_existing_destination_and_missing_executable_rejected(self):
        self.dest.mkdir(parents=True)
        sentry = self.dest / 'keep.txt'
        sentry.write_text('preserve')
        with self.assertRaises(ValueError): v.preflight(self.source, self.unity, self.dest)
        self.assertEqual(sentry.read_text(), 'preserve')
        with self.assertRaises(ValueError): v.preflight(self.source, self.root / 'absent.exe', self.root / 'other')

    def test_external_dependency_rejected(self):
        (self.source / 'Packages/manifest.json').write_text('{"dependencies":{"custom":"file:../../outside"}}')
        with self.assertRaises(ValueError): v.preflight(self.source, self.unity, self.dest)

    def test_snapshot_detects_modified_added_and_removed_files(self):
        asset = self.source / 'Assets/saved.txt'
        asset.write_text('old')
        before = v.snapshot(self.source)
        asset.write_text('new')
        extra = self.source / 'Assets/new.meta'
        extra.write_text('guid: sample')
        (self.source / 'Packages/manifest.json').unlink()
        self.assertEqual(v.differences(before, v.snapshot(self.source)),
                         ['Assets/new.meta', 'Assets/saved.txt', 'Packages/manifest.json'])

    def test_owned_process_timeout(self):
        code, timed_out = v.launch([sys.executable, '-c', 'import time; time.sleep(30)'], 0.15, self.root / 'process.log')
        self.assertTrue(timed_out)
        self.assertNotEqual(code, 0)


if __name__ == '__main__': unittest.main(verbosity=2)
