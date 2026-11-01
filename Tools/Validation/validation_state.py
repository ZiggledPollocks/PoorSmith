"""Content identity, owned mirrors, process locks and durable evidence. Stdlib only."""
from __future__ import annotations
from contextlib import contextmanager
from pathlib import Path, PurePosixPath
import hashlib, json, os, shutil, stat, time, uuid

INPUTS = ('Assets', 'Packages', 'ProjectSettings')
SCHEMA = 1

class Blocked(RuntimeError):
    def __init__(self, code, detail):
        super().__init__(detail); self.code = code


def safe_path(path, under=None):
    path = Path(path).absolute()
    for part in (path, *path.parents):
        if part.is_symlink(): raise Blocked('unsafe_path', f'Link refused: {part}')
        try: attributes = getattr(part.lstat(), 'st_file_attributes', 0)
        except FileNotFoundError: continue
        if attributes & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 1024):
            raise Blocked('unsafe_path', f'Reparse point refused: {part}')
    resolved = path.resolve()
    if under is not None:
        base = safe_path(under)
        if resolved == base or base not in resolved.parents:
            raise Blocked('unsafe_path', f'Path escapes owned root: {path}')
    return resolved


def relative_file(root, relative):
    p = PurePosixPath(relative)
    if p.is_absolute() or '..' in p.parts or ':' in relative or '\\' in relative:
        raise Blocked('unsafe_path', f'Invalid manifest path: {relative}')
    return safe_path(Path(root) / relative, root)


def read_json(path, default=None):
    try: return json.loads(Path(path).read_text(encoding='utf-8-sig'))
    except FileNotFoundError: return default
    except (ValueError, OSError) as exc: raise Blocked('invalid_record', f'{path}: {exc}') from exc


def write_json(path, value):
    path = safe_path(path); path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temporary.open('x', encoding='utf-8', newline='\n') as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2); stream.write('\n')
            stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists(): temporary.unlink()


def signature(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()


class Hashes:
    """One content scan per declared purpose; dependency fingerprints consume its manifest."""
    def __init__(self): self.files = 0; self.bytes = 0
    def file(self, path):
        safe_path(path)
        before = path.stat(); h = hashlib.sha256()
        with path.open('rb') as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                h.update(chunk); self.bytes += len(chunk)
        after = path.stat(); self.files += 1
        if (before.st_size, before.st_mtime_ns, before.st_ino) != (after.st_size, after.st_mtime_ns, after.st_ino):
            raise Blocked('source_drift', f'Changed while hashing: {path}')
        return h.hexdigest()
    def snapshot(self, root, inputs=INPUTS, reject_hardlinks=False):
        root = safe_path(root); result = {}
        for name in inputs:
            folder = relative_file(root, name)
            if not folder.is_dir(): raise Blocked('invalid_configuration', f'Missing input: {folder}')
            for parent, directories, files in os.walk(folder, followlinks=False):
                for n in directories: safe_path(Path(parent) / n, root)
                for n in sorted(files):
                    p = safe_path(Path(parent) / n, root)
                    if reject_hardlinks and p.stat().st_nlink > 1:
                        raise Blocked('unsafe_path', f'Mutable mirror file is hard-linked: {p}')
                    result[p.relative_to(root).as_posix()] = {'sha256': self.file(p), 'bytes': p.stat().st_size}
        return result


class Timings:
    def __init__(self, output): self.output = Path(output); self.records = []
    @contextmanager
    def phase(self, name):
        start = time.monotonic(); record = {'phase': name, 'status': 'running'}
        self.records.append(record); self.flush(); print('PHASE ' + name, flush=True)
        try:
            yield record
            record['status'] = 'passed'
        except BaseException:
            record['status'] = 'failed'; raise
        finally:
            record['seconds'] = round(time.monotonic() - start, 6); self.flush()
    def unavailable(self, name, reason):
        self.records.append({'phase': name, 'seconds': None, 'status': 'not_separately_observed', 'reason': reason})
        self.flush()
    def flush(self): write_json(self.output, self.records)


def process_identity(pid):
    """Birth identity prevents PID-reuse mistakes; this never terminates a process."""
    if os.name == 'nt':
        import ctypes
        from ctypes import wintypes
        dll = ctypes.WinDLL('kernel32', use_last_error=True)
        dll.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        dll.OpenProcess.restype = wintypes.HANDLE
        handle = dll.OpenProcess(0x1000, False, pid)
        if not handle:
            return {'alive': ctypes.get_last_error() != 87, 'birth': None}
        try:
            code = wintypes.DWORD()
            dll.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
            dll.GetExitCodeProcess(handle, ctypes.byref(code))
            values = [wintypes.FILETIME() for _ in range(4)]
            dll.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
            ok = dll.GetProcessTimes(handle, *(ctypes.byref(v) for v in values))
            born = str((values[0].dwHighDateTime << 32) | values[0].dwLowDateTime) if ok else None
            return {'alive': code.value == 259, 'birth': born}
        finally:
            dll.CloseHandle.argtypes = [wintypes.HANDLE]; dll.CloseHandle(handle)
    try:
        os.kill(pid, 0)
        proc = Path(f'/proc/{pid}/stat')
        return {'alive': True, 'birth': proc.read_text().split(') ', 1)[1].split()[19] if proc.exists() else None}
    except ProcessLookupError: return {'alive': False, 'birth': None}
    except PermissionError: return {'alive': True, 'birth': None}


def same_live_process(record):
    current = process_identity(record['pid'])
    return current['alive'] and (current['birth'] is None or record.get('birth') is None or current['birth'] == record['birth'])


class WorkspaceLock:
    """Kernel-owned lock; old metadata alone never grants permission to break a live lock."""
    def __init__(self, path, workspace, run_id, wait=0):
        self.path = safe_path(path); self.workspace = str(safe_path(workspace))
        self.run_id = run_id; self.wait = wait; self.token = uuid.uuid4().hex; self.stream = None
    def _lock(self, unlock=False):
        self.stream.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(self.stream.fileno(), msvcrt.LK_UNLCK if unlock else msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(self.stream.fileno(), fcntl.LOCK_UN if unlock else fcntl.LOCK_EX | fcntl.LOCK_NB)
    def _metadata(self):
        self.stream.seek(1)
        try: return json.loads(self.stream.read().decode())
        except (ValueError, UnicodeError) as exc:
            if self.path.stat().st_size>1: raise Blocked('invalid_record','Unreadable ownership metadata; manual investigation required') from exc
            return {}
    def update(self, **changes):
        self.owner.update(changes); self.stream.seek(1); self.stream.truncate()
        self.stream.write(json.dumps(self.owner).encode()); self.stream.flush(); os.fsync(self.stream.fileno())
    def __enter__(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        try: self.stream = self.path.open('r+b')
        except FileNotFoundError:
            try: self.stream = self.path.open('x+b')
            except FileExistsError: self.stream = self.path.open('r+b')
        if self.path.stat().st_size == 0: self.stream.write(b' '); self.stream.flush()
        deadline = time.monotonic() + self.wait
        while True:
            try: self._lock(); break
            except OSError:
                if time.monotonic() >= deadline:
                    owner = self._metadata(); self.stream.close(); self.stream = None
                    raise Blocked('workspace_busy', f'Live workspace owner: {owner}')
                time.sleep(min(.5, max(.01, deadline - time.monotonic())))
        try: previous = self._metadata()
        except Exception:
            self._lock(True); self.stream.close(); self.stream=None; raise
        operation = previous.get('operation')
        if operation and same_live_process(operation):
            self._lock(True); self.stream.close(); self.stream = None
            raise Blocked('workspace_busy', f'Previous owner exited but its Unity operation remains live: {operation}')
        self.owner = {'workspace': self.workspace, 'run_id': self.run_id, 'owner_token': self.token,
                      'pid': os.getpid(), 'birth': process_identity(os.getpid()).get('birth'), 'state': 'acquired',
                      'recovered_run': previous.get('run_id'), 'operation': None}
        self.update(); return self
    def __exit__(self, *exc):
        if self.stream is not None:
            operation = self.owner.get('operation')
            self.update(state='released', operation=operation if operation and same_live_process(operation) else None)
            self._lock(True); self.stream.close(); self.stream = None


def synchronize(source, target, desired, state, hashes):
    """Only manifest-owned paths may be removed. Unknown existing files are preserved/rejected."""
    source = safe_path(source); target = safe_path(target)
    if source == target or source in target.parents or target in source.parents:
        raise Blocked('unsafe_path', 'Source and mirror overlap')
    owned = state.setdefault('files', {})
    overrides = state.get('overrides', {})
    metrics = {'copied_files': 0, 'copied_bytes': 0, 'deleted_files': 0}
    for rel, info in desired.items():
        src, dst = relative_file(source, rel), relative_file(target, rel)
        if dst.exists():
            if dst.stat().st_nlink > 1: raise Blocked('unsafe_path', f'Hard-linked destination: {dst}')
            current = hashes.file(dst)
            expected = overrides.get(rel) if owned.get(rel) == info else None
            if current == (expected or info['sha256']): continue
            if rel not in owned: raise Blocked('foreign_workspace', f'Unowned destination: {dst}')
            if current not in (owned[rel]['sha256'], overrides.get(rel)):
                raise Blocked('candidate_drift', f'Owned input changed outside runner: {dst}')
        dst.parent.mkdir(parents=True, exist_ok=True)
        temporary = dst.with_name(dst.name + '.sync-' + uuid.uuid4().hex)
        try:
            shutil.copyfile(src, temporary)
            if hashes.file(temporary) != info['sha256']:
                raise Blocked('source_drift', f'Source changed during copy: {src}')
            os.replace(temporary, dst)
        finally:
            if temporary.exists(): temporary.unlink()
        owned[rel] = info; overrides.pop(rel, None)
        metrics['copied_files'] += 1; metrics['copied_bytes'] += info['bytes']
    for rel in sorted(set(owned) - set(desired)):
        dst = relative_file(target, rel)
        if dst.exists():
            if not dst.is_file() or dst.stat().st_nlink > 1: raise Blocked('unsafe_path', f'Unsafe removal: {dst}')
            if hashes.file(dst) not in (owned[rel]['sha256'],overrides.get(rel)):
                raise Blocked('candidate_drift',f'Changed owned input cannot be deleted: {dst}')
            dst.unlink(); metrics['deleted_files'] += 1
        owned.pop(rel, None); overrides.pop(rel, None)
    return metrics


class Evidence:
    def __init__(self, root): self.root = safe_path(root); self.root.mkdir(parents=True, exist_ok=True)
    def path(self, check_id, fingerprint): return self.root / (check_id + '-' + fingerprint + '.json')
    def get(self, check_id, fingerprint, method, hashes):
        path = self.path(check_id, fingerprint)
        try: record = read_json(path)
        except Blocked: return None, 'corrupt_record'
        if not record: return None, 'no_previous_evidence'
        if not isinstance(record,dict) or not isinstance(record.get('provenance'),dict) or not isinstance(record.get('artifacts'),list): return None,'invalid_provenance'
        if any(not isinstance(a,dict) or not isinstance(a.get('path'),str) or not isinstance(a.get('sha256'),str) for a in record['artifacts']):return None,'invalid_artifacts'
        if (record.get('schema') != SCHEMA or record.get('status') != 'passed' or
            record.get('check_id') != check_id or record.get('fingerprint') != fingerprint or
            record.get('method') != method or not record.get('provenance', {}).get('completion_verified') or
            not record.get('provenance', {}).get('run_id')):
            return None, 'invalid_provenance'
        for artifact in record.get('artifacts', []):
            p = Path(artifact['path'])
            if not p.is_file() or hashes.file(p) != artifact['sha256']: return None, 'missing_or_changed_artifact'
        if not record.get('artifacts'): return None, 'missing_artifacts'
        return record, 'valid_previous_pass'
    def put(self, check_id, fingerprint, method, provenance, artifacts, hashes):
        record = {'schema': SCHEMA, 'check_id': check_id, 'fingerprint': fingerprint, 'method': method,
                  'status': 'passed', 'provenance': provenance,
                  'artifacts': [{'path': str(p), 'sha256': hashes.file(p)} for p in artifacts]}
        write_json(self.path(check_id, fingerprint), record)


class FailureLedger:
    """Stable source/environment/cause identity, independent of run IDs and command spelling."""
    def __init__(self, path): self.path = Path(path)
    def load(self): return read_json(self.path, {'connections': {}, 'issues': {}})
    def budget(self, environment): return max(0, 30 - self.load()['connections'].get(environment, 0))
    def _ownership(self):
        return WorkspaceLock(self.path.with_suffix('.lock'), self.path.parent, 'ledger', wait=5)
    def connection(self, environment, seconds):
        with self._ownership():
            data = self.load(); data['connections'][environment] = min(30, data['connections'].get(environment, 0) + seconds)
            write_json(self.path, data)
    def reopen(self, environment, reason):
        with self._ownership():
            if not reason.strip(): raise Blocked('invalid_configuration', 'Explicit environment-change reason required')
            data = self.load(); data['connections'][environment] = 0
            data.setdefault('reopened', []).append({'environment': environment, 'reason': reason})
            write_json(self.path, data)
    def guard(self, cause, approach, operation):
        issue = self.load()['issues'].get(cause + '|' + approach)
        if not issue: return
        if issue['count'] >= 3: raise Blocked('approach_exhausted', f'Three failed cycles; require new diagnosis/approach: {issue}')
        if issue['operation'] == operation: raise Blocked('unchanged_failed_operation', str(issue))
    def failure(self, cause, approach, operation, context):
        with self._ownership():
            data = self.load(); key = cause + '|' + approach; old = data['issues'].get(key, {})
            data['issues'][key] = {'count': old.get('count', 0) + 1, 'operation': operation,
                                  'first_context': old.get('first_context', context), 'last_context': context}
            write_json(self.path, data)
