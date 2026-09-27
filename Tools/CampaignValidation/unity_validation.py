"""M1: saved-tree snapshot and isolated Unity editor compile. Python >= 3.10, stdlib only."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time
import uuid

INPUTS = ('Assets', 'Packages', 'ProjectSettings')
PROBE = Path(__file__).with_name('CompileProbe.cs.txt')


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest() if hasattr(hashlib, 'file_digest') else _digest(f)


def _digest(f):
    h = hashlib.sha256()
    for chunk in iter(lambda: f.read(1024 * 1024), b''): h.update(chunk)
    return h.hexdigest()


def reject_links(path):
    for p in (path, *path.parents):
        if p.exists() and (p.is_symlink() or getattr(p.lstat(), 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT):
            raise ValueError('Linked/reparse paths are unsupported: ' + str(p))


def snapshot(root):
    found = {}
    for name in INPUTS:
        folder = root / name
        if not folder.is_dir(): raise ValueError('Missing input directory: ' + str(folder))
        reject_links(folder)
        for p in sorted(folder.rglob('*')):
            if p.is_symlink() or getattr(p.lstat(), 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise ValueError('Linked input is unsupported: ' + str(p))
            if p.is_file():
                found[p.relative_to(root).as_posix()] = {'sha256': digest(p), 'bytes': p.stat().st_size}
    return found


def differences(before, after):
    return [p for p in sorted(before.keys() | after.keys()) if before.get(p) != after.get(p)]


def git_context(root):
    context = {}
    for key, args in [('branch', ['branch', '--show-current']), ('commit', ['rev-parse', 'HEAD']), ('status', ['status', '--porcelain=v1'])]:
        call = subprocess.run(['git', '-c', 'safe.directory=' + root.as_posix(), '-C', str(root), *args], capture_output=True, text=True, encoding='utf-8', errors='replace')
        if call.returncode: raise ValueError('Cannot establish Git context: ' + call.stderr[:300])
        context[key] = call.stdout.strip()
    return context


def preflight(source, unity, destination):
    for p in (source, unity, destination): reject_links(p)
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError('Source and run destination must not overlap.')
    if destination.exists(): raise ValueError('Run destination already exists; never overwrite.')
    if os.name == 'nt' and len(str(destination / 'project')) > 64:
        raise ValueError('Run project prefix exceeds 64 characters; use a shorter run root (e.g. Documents/Codex/UV). Unity package importers may hit Windows path limits.')
    if not unity.is_file(): raise ValueError('Unity executable not found: ' + str(unity))
    match = re.search(r'^m_EditorVersion:\s*(\S+)', (source / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig'), re.M)
    if not match: raise ValueError('Project Unity version is missing.')
    version = match[1]
    # Standard Hub layout is deliberately required. Runtime marker also verifies the version.
    if unity.parent.parent.name != version: raise ValueError('Unity installation directory does not match project version.')
    for p in (source / 'Packages').rglob('*.json'):
        data = json.loads(p.read_text(encoding='utf-8-sig'))
        text = json.dumps(data)
        if re.search(r'file:|"source"\s*:\s*"local"', text):
            raise ValueError('Local package dependency requires an explicit copy policy: ' + str(p))
    reserved = source / 'Assets/__BatterMapValidation'
    if reserved.exists() or reserved.with_suffix('.meta').exists():
        raise ValueError('Reserved validation asset path already exists.')
    for folder in INPUTS:
        if not (source / folder).is_dir(): raise ValueError('Missing project input: ' + folder)
    return version


def classify(exit_code, timed_out, marker, log, run_id, project, version):
    if timed_out: return 'blocked', 'timeout'
    if re.search(r'error CS\d{4}\b|Scripts have compiler errors|script compilation failed', log, re.I):
        return 'failed', 'compiler_error'
    if 'Asset import failed' in log:
        return 'failed', 'asset_import_error'
    if re.search(r'An error occurred while resolving packages|Failed to resolve packages|Unable to resolve packages', log, re.I):
        return 'blocked', 'package_resolution'
    valid = (isinstance(marker, dict) and marker.get('runId') == run_id and
             marker.get('status') == 'passed' and marker.get('unityVersion') == version and
             marker.get('isPlaying') is False and marker.get('target') == 'StandaloneWindows64' and
             os.path.normcase(marker.get('projectPath', '')) == os.path.normcase(str(project)))
    if exit_code == 0 and valid and ('BATTERMAP_COMPILE_COMPLETE ' + run_id) in log:
        return 'passed', 'compile_completion_confirmed'
    # Transient startup licensing messages can precede successful recovery and completion.
    if re.search(r'No valid Unity Editor license|No valid license|Failed to activate|Failed to update license|Licensing.*(?:failed|error)|Entitlement.*failed', log, re.I):
        return 'blocked', 'licensing'
    if exit_code != 0: return 'failed', 'editor_exit_without_success'
    return 'failed', 'missing_or_invalid_completion'


def launch(command, timeout, stdout_path):
    startup = None
    flags = 0
    if os.name == 'nt':
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = subprocess.SW_HIDE
        flags = subprocess.CREATE_NO_WINDOW
    with stdout_path.open('wb') as output:
        process = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT,
                                   startupinfo=startup, creationflags=flags)
        write_json(stdout_path.with_name('process.json'), {'pid': process.pid, 'command': command})
        timed_out = False
        try:
            process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            process.kill()  # Only the child process this runner owns. Never kill by name.
            process.wait(timeout=30)
        except BaseException:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=30)
            raise
    return process.returncode, timed_out


def run(args):
    source, unity = Path(args.project).resolve(), Path(args.unity).resolve()
    run_id = uuid.uuid4().hex[:12]  # Short Windows path; timestamp is stored in the report.
    destination = Path(args.run_root).resolve() / run_id
    result = {'schema': 1, 'run_id': run_id, 'scope': 'editor_compile_windows64', 'status': 'blocked',
              'reason': 'preflight', 'ui': 'not_run', 'play': 'not_run', 'build': 'not_run',
              'source': str(source), 'evidence': str(destination / 'evidence'),
              'negative_control': args.negative_control, 'started_utc': datetime.now(timezone.utc).isoformat()}
    evidence = None
    original = None
    started = time.monotonic()
    try:
        version = preflight(source, unity, destination)
        original = snapshot(source)
        total = sum(v['bytes'] for v in original.values())
        root_existing = destination.parent
        while not root_existing.exists(): root_existing = root_existing.parent
        if shutil.disk_usage(root_existing).free < total * 3 + 5 * 1024**3:
            raise ValueError('Insufficient free space for snapshot and fresh import (5 GiB reserve).')
        context = git_context(source)
        destination.mkdir(parents=True, exist_ok=False)
        evidence = destination / 'evidence'
        (evidence / 'logs').mkdir(parents=True)
        project = destination / 'project'
        project.mkdir()
        print('RUN_ROOT=' + str(destination), flush=True)
        print('Snapshot: ' + str(len(original)) + ' files; ' + str(total) + ' bytes', flush=True)
        for rel in original:
            target = project / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source / rel, target)
        for name in INPUTS: (project / name).mkdir(exist_ok=True)
        copied = snapshot(project)
        drift = differences(original, snapshot(source))
        if differences(original, copied) or drift:
            raise ValueError('Snapshot inconsistency or concurrent source drift: ' + str(drift[:10]))
        write_json(evidence / 'snapshot.json', {'source': str(source), 'git': context, 'unity_version': version,
                   'files': original, 'excluded': ['Library', 'Temp', 'Logs', 'obj', '.git', 'UserSettings', 'root generated IDE files'],
                   'input_scope': 'Saved Assets, Packages and ProjectSettings; no unsaved editor memory',
                   'tool_sha256': digest(Path(__file__)), 'probe_sha256': digest(PROBE)})
        probe = project / 'Assets/__BatterMapValidation/Editor/CompileProbe.cs'
        probe.parent.mkdir(parents=True)
        probe.write_bytes(PROBE.read_bytes())
        (project / '.validation-run-id').write_text(run_id, encoding='utf-8')
        if args.negative_control:
            (probe.parent / 'IntentionalCompileFailure.cs').write_text('#error BATTERMAP_EXPECTED_NEGATIVE_CONTROL\n', encoding='utf-8')
        write_json(evidence / 'injected-files.json', {'probe': str(probe.relative_to(project)),
                   'negative_control': args.negative_control, 'source_project_modified': False})
        marker_path = evidence / 'completion.json'
        log_path = evidence / 'logs/unity.log'
        command = [str(unity), '-batchmode', '-nographics', '-quit', '-buildTarget', 'Win64',
                   '-projectPath', str(project), '-logFile', str(log_path), '-upmLogFile', str(evidence / 'logs/upm.log'),
                   '-executeMethod', 'BatterMap.Validation.CompileProbe.Run', '-validationRunId', run_id,
                   '-validationProject', str(project), '-validationMarker', str(marker_path), '-validationVersion', version]
        result.update(unity_version=version, command=command, project=str(project), git=context)
        write_json(evidence / 'result.json', {**result, 'reason': 'running'})
        print('Launching isolated Unity compile; timeout=' + str(args.timeout), flush=True)
        code, timed_out = launch(command, args.timeout, evidence / 'logs/process-output.log')
        log = log_path.read_text(encoding='utf-8-sig', errors='replace') if log_path.exists() else ''
        marker = None
        try:
            if marker_path.exists(): marker = json.loads(marker_path.read_text(encoding='utf-8-sig'))
        except (ValueError, OSError): pass
        status, reason = classify(code, timed_out, marker, log, run_id, project, version)
        result.update(status=status, reason=reason, exit_code=code, timed_out=timed_out, completion=marker)
        result['compiler_errors'] = list(dict.fromkeys(line.strip() for line in log.splitlines() if re.search(r'error CS\d{4}\b', line)))[:50]
        result['asset_import_errors'] = list(dict.fromkeys(line.strip() for line in log.splitlines() if 'Asset import failed' in line))[:30]
        result['negative_control_detected'] = bool(args.negative_control and reason == 'compiler_error' and
                                                  'BATTERMAP_EXPECTED_NEGATIVE_CONTROL' in log and marker is None)
        # Never silently pass changed source/lock files in the snapshot after import.
        after_copy = snapshot(project)
        result['snapshot_added_files'] = sorted(after_copy.keys() - original.keys())
        mutations = [p for p in original if original[p] != after_copy.get(p)]
        result['snapshot_original_files_changed_by_unity'] = mutations
        if mutations and status == 'passed': result.update(status='failed', reason='snapshot_mutated_during_import')
    except (Exception, KeyboardInterrupt) as exc:
        result.update(status='blocked', reason='runner_or_preflight_error', detail=str(exc))
    finally:
        if original is not None:
            try:
                drift = differences(original, snapshot(source))
                result['source_drift'] = drift
                result['source_preserved'] = not drift
                if drift: result.update(status='blocked', reason='source_drift')
            except Exception as exc:
                result.update(status='blocked', reason='preservation_check_failed', detail=str(exc))
        result['elapsed_seconds'] = round(time.monotonic() - started, 2)
        if evidence is not None:
            write_json(evidence / 'result.json', result)
            (evidence / 'summary.md').write_text(
                '# Unity M1 검증 결과\n\n' + '\n'.join([
                    '- 실행 ID: ' + run_id, '- 상태: ' + result['status'], '- 사유: ' + result['reason'],
                    '- 원본 보존: ' + str(result.get('source_preserved', '미확인')),
                    '- 고의 오류 감지: ' + str(result.get('negative_control_detected', False)),
                    '- 소요 시간: ' + str(result['elapsed_seconds']) + '초',
                    '- UI·Play Mode·플레이어 빌드: 미실행',
                    '- 검사 범위: 저장된 작업 트리의 Windows64 에디터 컴파일. 게임 동작 검증 아님.',
                    '- 세부 근거: result.json, snapshot.json, logs/unity.log']) + '\n', encoding='utf-8')
        print(json.dumps(result, ensure_ascii=False, indent=2), flush=True)
    if args.negative_control:
        return 0 if result.get('negative_control_detected') and result.get('source_preserved') else 1
    return 0 if result['status'] == 'passed' else (2 if result['status'] == 'blocked' else 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--unity', required=True)
    parser.add_argument('--run-root', required=True)
    parser.add_argument('--timeout', type=int, default=1200)
    parser.add_argument('--negative-control', action='store_true')
    args = parser.parse_args()
    if not 1 <= args.timeout <= 7200: parser.error('timeout must be between 1 and 7200 seconds')
    return run(args)


if __name__ == '__main__':
    raise SystemExit(main())
