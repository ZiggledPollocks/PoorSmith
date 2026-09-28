"""M2 saved UI and M3 inventory checks in a fresh, persistence-isolated Windows copy."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import re
import shutil
import time
import uuid
import winreg
import unity_validation as base

COMPANY = 'BatterMapValidation'
SETTINGS = 'ProjectSettings/ProjectSettings.asset'
REQUIRED_CHECKS = {'physical smithy and shared inventory completed'}

def identity(text):
    values = []
    for key in ('companyName', 'productName'):
        found = re.findall(r'^  ' + key + r': (.+)$', text, re.M)
        if len(found) != 1 or not re.fullmatch(r'[A-Za-z0-9_. -]+', found[0]):
            raise ValueError('Unsupported or ambiguous ' + key)
        values.append(found[0])
    return values

def isolate_settings(text, run_id):
    identity(text)
    if not re.fullmatch('[a-f0-9]{12}', run_id): raise ValueError('Invalid run ID')
    text = re.sub(r'^  companyName: .+$', '  companyName: ' + COMPANY, text, flags=re.M)
    return re.sub(r'^  productName: .+$', '  productName: Run_' + run_id, text, flags=re.M)

def persistence(company, product):
    """Read hashes only; never change or remove user preferences/saves."""
    folder = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow' / company / product
    base.reject_links(folder)
    files = {}
    if folder.exists():
        for p in sorted(folder.rglob('*')):
            base.reject_links(p)
            if p.is_file(): files[p.relative_to(folder).as_posix()] = base.digest(p)
    registry = {}
    for root in (rf'Software\Unity\UnityEditor\{company}\{product}', rf'Software\{company}\{product}'):
        def visit(path):
            try: key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, path, 0, winreg.KEY_READ)
            except FileNotFoundError: return None
            with key:
                values, children = {}, {}
                count, valcount, _ = winreg.QueryInfoKey(key)
                for index in range(valcount):
                    name, data, typ = winreg.EnumValue(key, index)
                    values[name] = hashlib.sha256(repr((typ, data)).encode()).hexdigest()
                for index in range(count):
                    name = winreg.EnumKey(key, index)
                    children[name] = visit(path + '\\' + name)
                return {'values': values, 'children': children}
        registry[root] = visit(root)
    return {'path': str(folder), 'exists': folder.exists(), 'files': files, 'registry': registry}

def classify(code, timeout, marker, log, run_id, project, version):
    if timeout: return 'blocked', 'timeout'
    if re.search(r'error CS\d{4}\b', log): return 'failed', 'compiler_error'
    if 'Asset import failed' in log: return 'failed', 'asset_import_error'
    if not isinstance(marker, dict): return 'failed', 'missing_completion'
    valid = (marker.get('runId') == run_id and marker.get('unityVersion') == version and
        os.path.normcase(marker.get('projectPath', '')) == os.path.normcase(str(project)) and
        marker.get('isPlaying') is True and 'BATTERMAP_RUNTIME_COMPLETE ' + run_id in log)
    if not valid: return 'failed', 'invalid_completion'
    checks = marker.get('checks', [])
    # Include editor-startup/shutdown exceptions outside the probe's Play Mode subscription.
    if re.search(r'^(?:[A-Za-z0-9_.]+Exception|Assertion failed):', log, re.M):
        return 'failed', 'exception_in_editor_log'
    names = {c.get('name') for c in checks if c.get('passed')}
    if code == 0 and marker.get('status') == 'passed' and REQUIRED_CHECKS <= names and all(c.get('passed') for c in checks) and not marker.get('errors'):
        return 'passed', 'runtime_completion_confirmed'
    return 'failed', 'runtime_assertion_or_error'

def run(args):
    source, unity = Path(args.project).resolve(), Path(args.unity).resolve()
    run_id = uuid.uuid4().hex[:12]
    destination = Path(args.run_root).resolve() / run_id
    result = {'run_id': run_id, 'scope': 'C011 physical smithy and shared tools/items', 'status': 'blocked',
        'source': str(source), 'build': 'not_run', 'unsaved_editor_state': 'not_inspected'}
    evidence = original = before_data = None
    started = time.monotonic()
    try:
        version = base.preflight(source, unity, destination)
        original = base.snapshot(source)
        settings_text = (source / SETTINGS).read_text(encoding='utf-8-sig')
        company, product = identity(settings_text)
        before_data = persistence(company, product)
        isolated_before = persistence(COMPANY, 'Run_' + run_id)
        if isolated_before['exists'] or any(v is not None for v in isolated_before['registry'].values()):
            raise ValueError('Test persistence namespace already exists')
        parent = destination.parent
        while not parent.exists(): parent = parent.parent
        if shutil.disk_usage(parent).free < sum(v['bytes'] for v in original.values()) * 3 + 5 * 1024**3:
            raise ValueError('Insufficient disk space')
        context = base.git_context(source)
        context['note'] = 'Git context of original repository; source is the staged snapshot identified by file hashes.'
        evidence = destination / 'evidence'
        (evidence / 'logs').mkdir(parents=True, exist_ok=False)
        project = destination / 'project'
        print('RUN_ROOT=' + str(destination), flush=True)
        for rel in original:
            target = project / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source / rel, target)
        if base.differences(original, base.snapshot(project)) or base.differences(original, base.snapshot(source)):
            raise ValueError('Source/copy drift before launch')
        (project / SETTINGS).write_text(isolate_settings(settings_text, run_id), encoding='utf-8')
        expected_copy = base.snapshot(project)
        probe = project / 'Assets/__BatterMapValidation/Editor/RuntimeProbe.cs'
        probe.parent.mkdir(parents=True)
        template = Path(__file__).with_name('RuntimeProbe.cs.txt')
        probe.write_bytes(template.read_bytes())
        if not (project / 'Assets/Campaign/Editor/CampaignValidate.cs').exists():
            (probe.parent / 'CampaignValidate.cs').write_bytes(Path(__file__).with_name('CampaignValidate.cs.txt').read_bytes())
        movement_template = Path(__file__).with_name('MovementCases.cs.txt')
        (probe.parent / 'MovementCases.cs').write_bytes(movement_template.read_bytes())
        (probe.parent / 'LoopCases.cs').write_bytes(Path(__file__).with_name('LoopCases.cs.txt').read_bytes())
        (project / '.validation-run-id').write_text(run_id, encoding='utf-8')
        base.write_json(evidence / 'snapshot.json', {'source': str(source), 'git': context,
            'unity_version': version, 'files': original, 'copy_overrides': {SETTINGS: expected_copy[SETTINGS]},
            'override_reason': 'Test-only company/product identity; all Assets remain byte-identical.',
            'runner_sha256': base.digest(Path(__file__)), 'probe_sha256': base.digest(template), 'movement_probe_sha256': base.digest(movement_template)})
        base.write_json(evidence / 'persistence-before.json', before_data)
        marker_path = evidence / 'completion.json'
        log_path = evidence / 'logs/unity.log'
        command = [str(unity), '-batchmode', '-buildTarget', 'Win64', '-projectPath', str(project),
            '-logFile', str(log_path), '-upmLogFile', str(evidence / 'logs/upm.log'),
            '-executeMethod', 'RuntimeProbe.Run', '-validationRunId', run_id,
            '-validationProject', str(project), '-validationMarker', str(marker_path)]
        result.update(project=str(project), evidence=str(evidence), unity_version=version, git=context, command=command)
        base.write_json(evidence / 'result.json', {**result, 'reason': 'running'})
        print('Launching isolated graphical batch Play Mode; timeout=' + str(args.timeout), flush=True)
        code, timeout = base.launch(command, args.timeout, evidence / 'logs/process-output.log')
        log = log_path.read_text(encoding='utf-8-sig', errors='replace') if log_path.exists() else ''
        marker = json.loads(marker_path.read_text(encoding='utf-8-sig')) if marker_path.exists() else None
        status, reason = classify(code, timeout, marker, log, run_id, project, version)
        result.update(status=status, reason=reason, completion=marker, exit_code=code, timed_out=timeout)
        result['compiler_errors'] = list(dict.fromkeys(l.strip() for l in log.splitlines() if re.search(r'error CS\d{4}\b', l)))[:30]
        copied_after = base.snapshot(project)
        mutations = [p for p in expected_copy if expected_copy[p] != copied_after.get(p)]
        result['unexpected_copy_mutations'] = mutations
        if mutations: result.update(status='failed', reason='unexpected_copy_mutation')
        base.write_json(evidence / 'isolated-persistence-after.json', persistence(COMPANY, 'Run_' + run_id))
    except (Exception, KeyboardInterrupt) as exc:
        result.update(status='blocked', reason='runner_error', detail=str(exc))
    finally:
        if original is not None:
            try:
                drift = base.differences(original, base.snapshot(source))
                result.update(source_drift=drift, source_preserved=not drift)
                if drift: result.update(status='blocked', reason='source_drift')
                if before_data is not None:
                    after_data = persistence(company, product)
                    result['user_persistence_preserved'] = before_data == after_data
                    if evidence is not None: base.write_json(evidence / 'persistence-after.json', after_data)
                    if before_data != after_data: result.update(status='blocked', reason='user_persistence_drift')
            except Exception as exc: result.update(status='blocked', reason='preservation_check_failed', detail=str(exc))
        result['elapsed_seconds'] = round(time.monotonic() - started, 2)
        if evidence is not None: base.write_json(evidence / 'result.json', result)
        print(json.dumps({k: v for k, v in result.items() if k not in ('completion', 'command', 'git')}, ensure_ascii=False, indent=2), flush=True)
    return 0 if result['status'] == 'passed' else 1

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True)
    parser.add_argument('--unity', required=True)
    parser.add_argument('--run-root', required=True)
    parser.add_argument('--timeout', type=int, default=1200)
    args = parser.parse_args()
    if not 1 <= args.timeout <= 7200: parser.error('timeout must be 1..7200 seconds')
    raise SystemExit(run(args))
