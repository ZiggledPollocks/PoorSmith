"""Plan once, reuse owned Unity workspaces, execute only selected registered checks."""
from __future__ import annotations
import argparse, fnmatch, json, os, re, shutil, subprocess, sys, time, uuid
from dataclasses import dataclass, asdict
from pathlib import Path
import unity_validation as legacy
import validation_state as state

ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
VERSION = 'workflow-1'
MAX_PROJECT_PREFIX = 64
DEFAULT_ROOT = Path.home() / 'Documents/Codex/UV'
UNITY = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe'

@dataclass(frozen=True)
class Check:
    id: str
    purpose: str
    implementation: str
    dependencies: tuple
    fixture: tuple
    method: str
    cost: str
    reusable: bool = True
    requirements: tuple = ()

COMMON = ('Packages/**', 'ProjectSettings/**')
CHECKS = {
 'static.instructions': Check('static.instructions', 'Entry points/instruction links and protected runner contract', 'static_instructions', ('AGENTS.md','Docs/README.md','Docs/Validation.md','Tools/Validation/**'), (), 'static', 'small'),
 'static.build-config': Check('static.build-config', 'Enabled scene paths/GUID references; not a player build', 'static_build_config', ('ProjectSettings/EditorBuildSettings.asset','Assets/**/*.unity','Assets/**/*.unity.meta'), (), 'static', 'small', requirements=('windows-build',)),
 'compile.windows64': Check('compile.windows64', 'Current saved input Windows64 Editor compilation/import', 'CompileProbe.Run', ('Assets/**',)+COMMON, ('CompileProbe.cs.txt',), 'compile', 'unity'),
 'services.release': Check('services.release', 'Existing source-defined temporary save/crafting regressions', 'ReleaseStabilityValidation.Run', ('Assets/JinHo/**',)+COMMON, ('CompileProbe.cs.txt',), 'service', 'unity'),
 'runtime.ui': Check('runtime.ui', 'Existing saved-UI/input assertions and canvas captures', 'RuntimeProbe(ui)', ('Assets/**',)+COMMON, ('RuntimeProbe.cs.txt','MovementCases.cs.txt'), 'synthetic', 'play', False, ('visual','native-input')),
 'runtime.inventory': Check('runtime.inventory', 'Existing disposable inventory boundary/atomic quantity checks', 'RuntimeProbe(inventory)', ('Assets/JinHo/**',)+COMMON, ('RuntimeProbe.cs.txt','MovementCases.cs.txt'), 'play-component', 'play'),
 'runtime.movement': Check('runtime.movement', 'Existing movement mode/registration component cases; not traversal', 'RuntimeProbe(movement)', ('Assets/JinHo/**',)+COMMON, ('RuntimeProbe.cs.txt','MovementCases.cs.txt'), 'play-component', 'play', requirements=('live-movement',)),
}
SCOPES = {
 'docs': ('static.instructions',), 'static': ('static.instructions','static.build-config'),
 'compile': ('compile.windows64',), 'ui-layout': ('compile.windows64','runtime.ui'),
 'ui-navigation': ('compile.windows64','runtime.ui'), 'inventory': ('compile.windows64','runtime.inventory'),
 'trading': ('compile.windows64','services.release','runtime.inventory'),
 'save': ('compile.windows64','services.release'), 'movement': ('compile.windows64','runtime.movement'),
 'map': ('compile.windows64',), 'audio': ('compile.windows64',), 'build': ('static.build-config',),
 'legacy-runtime': ('compile.windows64','runtime.ui','runtime.inventory','runtime.movement'),
}
MISSING = {'trading': ['actual-trading-currency'], 'map': ['map-visual-live'], 'audio': ['audio-events','auditory'], 'build': ['windows-build']}


def csv(value): return tuple(x.strip() for x in value.split(',') if x.strip())

def matches(path, patterns): return any(fnmatch.fnmatchcase(path, p) for p in patterns)


def infer(changes):
    scopes, unknown = set(), []
    for p in changes:
        if p.startswith(('Docs/','Tools/Validation/')) or p in ('AGENTS.md','README.md'):
            scopes.add('docs'); continue
        if p.startswith(('Packages/','ProjectSettings/')):
            scopes.update(('compile','legacy-runtime','save','build')); continue
        if not p.startswith('Assets/'):
            unknown.append(p); continue
        scopes.add('compile'); lower = p.lower()
        if any(x in lower for x in ('persistence','inventory','economy','catalog','input','save','trade')):
            scopes.update(('inventory','save','ui-navigation'))
            if 'trade' in lower or 'economy' in lower: scopes.add('trading')
        elif any(x in lower for x in ('/ui/','settingsmenuui','uitest')):
            scopes.add('ui-layout' if p.endswith(('.prefab','.png','.meta')) else 'ui-navigation')
        elif any(x in lower for x in ('movement','travel','physics','platform')): scopes.add('movement')
        elif any(x in lower for x in ('audio','sound')): scopes.add('audio')
        elif any(x in lower for x in ('map','scene','.unity')): scopes.update(('movement','map'))
        else: unknown.append(p)
    if unknown: scopes.update(('compile','legacy-runtime','save'))
    return scopes or {'docs'}, unknown


def plan(changes, scopes=(), required=()):
    inferred, unknown = infer(changes)
    selected_scopes = set(scopes) if scopes else inferred
    bad = selected_scopes - SCOPES.keys()
    if bad: raise state.Blocked('invalid_configuration', 'Unknown scopes: ' + ', '.join(sorted(bad)))
    selected = set()
    for scope in selected_scopes: selected.update(SCOPES[scope])
    for check in required:
        if check not in CHECKS: raise state.Blocked('invalid_configuration', 'Unknown required check: ' + check)
        selected.add(check)
    # Explicit scopes cannot suppress a known high-risk/unknown impact silently.
    broadened = set()
    if unknown or any(p.startswith(('Packages/','ProjectSettings/')) or any(x in p.lower() for x in ('persistence','inventoryservice','economy','playerinput')) for p in changes):
        for scope in inferred: broadened.update(SCOPES[scope])
        selected.update(broadened)
    manual = set()
    for scope in (selected_scopes|inferred if broadened else selected_scopes):
        manual.update(MISSING.get(scope, ()))
    for check in selected: manual.update(CHECKS[check].requirements)
    return {'selected': sorted(selected), 'scopes': sorted(selected_scopes), 'required': list(required),
            'unknown_dependencies': unknown, 'broadened_for_risk': sorted(broadened),
            'excluded': {k:'Outside selected behavior/dependency scope' for k in CHECKS if k not in selected},
            'required_acceptance': {k:'unverified' for k in sorted(manual)},
            'changes': list(changes), 'note':'A registered synthetic/component check is not visual/native/auditory acceptance.'}


def tool_inputs(source, hashes):
    files = {}
    for pattern in ('*.py','*.txt','*.ps1','*.md','*.json'):
        for path in sorted((source / 'Tools/Validation').glob(pattern)):
            files[path.relative_to(source).as_posix()] = {'sha256':hashes.file(path), 'bytes':path.stat().st_size}
    for name in ('AGENTS.md','Docs/README.md','Docs/Validation.md'):
        p = source / name
        if p.is_file(): files[name] = {'sha256':hashes.file(p),'bytes':p.stat().st_size}
    return files


def context(source, inputs, unity):
    version = re.search(r'^m_EditorVersion:\s*(\S+)', (source/'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8-sig'), re.M)
    if not version: raise state.Blocked('invalid_configuration','Unity version missing')
    settings = {p:v for p,v in inputs.items() if p.startswith(('Packages/','ProjectSettings/'))}
    return {'source':str(source), 'unity':version[1], 'unity_path':str(unity), 'target':'StandaloneWindows64',
            'settings':state.signature(settings), 'isolation':'BatterMapValidation/unique-run-product-v1', 'workspace_schema':state.SCHEMA}


def fingerprint(check, inputs, ctx, hashes):
    relevant = {p:v for p,v in inputs.items() if matches(p, check.dependencies)}
    probes = {p:inputs['Tools/Validation/'+p]['sha256'] for p in check.fixture}
    # The implementation version is content-derived; no mtime/commit-only reuse.
    implementation = {p:v['sha256'] for p,v in inputs.items() if p.startswith('Tools/Validation/') and p.endswith(('.py','.ps1','.json'))}
    return state.signature({'check':asdict(check),'inputs':relevant,'fixture':probes,'implementation':implementation,'context':ctx})


def static_instructions(source):
    for path in ('AGENTS.md','Tools/Validation/README.md','Tools/Validation/Invoke-Validation.ps1','Tools/Validation/validation_workflow.py'):
        if not (source/path).is_file(): raise state.Blocked('invalid_configuration', 'Entry point missing: '+path)
    if 'Invoke-Validation.ps1' not in (source/'AGENTS.md').read_text(encoding='utf-8-sig'):
        raise state.Blocked('assertion_failure','Project AGENTS does not route to the current entry point')
    return {'entry_points':'present','project_default':'Invoke-Validation.ps1','unity_started':False}


def static_build_config(source):
    text = (source/'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')
    scenes = re.findall(r'enabled: 1\s+path: (.+)',text)
    if not scenes: raise state.Blocked('assertion_failure','No enabled build scenes')
    for scene in scenes:
        p = state.relative_file(source, scene)
        if not p.is_file() or not p.with_suffix(p.suffix+'.meta').is_file():
            raise state.Blocked('assertion_failure','Missing enabled scene or metadata: '+scene)
    return {'enabled_scenes':scenes,'player_build_executed':False}


def validate_source(source, unity):
    for name in state.INPUTS:
        if not (source/name).is_dir(): raise state.Blocked('invalid_configuration','Missing '+name)
    if unity is not None and not unity.is_file(): raise state.Blocked('invalid_configuration','Unity executable missing')
    if (source/'Assets/__BatterMapValidation').exists() or (source/'Assets/__BatterMapValidation.meta').exists():
        raise state.Blocked('foreign_workspace','Source already contains reserved probes; existing p is not disposable')
    for path in (source/'Packages').rglob('*.json'):
        data = json.dumps(state.read_json(path))
        if re.search(r'file:|"source"\s*:\s*"local"',data): raise state.Blocked('invalid_configuration','Local packages require a copy policy')


def registered_workspace(root, ctx, mode, requested=None):
    key = state.signature(ctx)[:8] if mode=='reuse' else uuid.uuid4().hex[:8]
    workspace = state.safe_path(requested) if requested else state.safe_path(root/'workspaces'/key/'p')
    if os.name=='nt' and len(str(workspace))>MAX_PROJECT_PREFIX: raise state.Blocked('unsafe_path','Unity workspace prefix exceeds64 chars; choose a shorter run root')
    marker = workspace.parent/'workspace.json'
    existing = state.read_json(marker)
    if existing:
        if existing.get('schema')!=state.SCHEMA or existing.get('context')!=ctx or existing.get('workspace')!=str(workspace):
            raise state.Blocked('incompatible_workspace','Registered workspace compatibility mismatch')
    elif workspace.exists() and any(workspace.iterdir()):
        raise state.Blocked('foreign_workspace','Existing unregistered workspace is preserved; registration/adoption needs explicit review')
    return workspace, marker, existing


def install_probes(workspace, checks, run_id, evidence, source, hashes, workspace_state):
    runtime = any(c.startswith('runtime.') for c in checks)
    desired = {'Editor/CompileProbe.cs': TOOLS/'CompileProbe.cs.txt'}
    if runtime:
        desired.update({'Editor/RuntimeProbe.cs':TOOLS/'RuntimeProbe.cs.txt','Editor/MovementCases.cs':TOOLS/'MovementCases.cs.txt'})
    owned = workspace_state.setdefault('injected',[])
    root = workspace/'Assets/__BatterMapValidation'
    for rel in list(owned):
        if rel not in desired:
            path = state.relative_file(root,rel)
            if path.exists(): path.unlink()
            if path.with_suffix(path.suffix+'.meta').exists(): path.with_suffix(path.suffix+'.meta').unlink()
            owned.remove(rel)
    copied = 0
    for rel,path in desired.items():
        dst = state.relative_file(root,rel); dst.parent.mkdir(parents=True,exist_ok=True)
        if not dst.exists() or hashes.file(dst)!=hashes.file(path): dst.write_bytes(path.read_bytes());copied+=1
        if rel not in owned: owned.append(rel)
    (workspace/'.validation-run-id').write_text(run_id,encoding='utf-8')
    state.write_json(workspace/'.validation-context.json',{'runId':run_id,'project':str(workspace),'marker':str(evidence/'completion.json'),'source':str(source)})
    return copied


def launch_owned(command, timeout, evidence, lock, ledger, environment):
    """A cumulative unavailable-start budget, not a cap on healthy import/build work."""
    import subprocess
    budget = ledger.budget(environment)
    if budget<=0: raise state.Blocked('connection_unavailable','Unchanged environment exhausted its cumulative30-second budget')
    start=time.monotonic();working=False;timed_out=False;connection_failed=False
    log_path=evidence/'logs/unity.log'
    flags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0
    startup=None
    if os.name=='nt':
        startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=subprocess.SW_HIDE
    with (evidence/'logs/process-output.log').open('wb') as output:
        process=subprocess.Popen(command,stdout=output,stderr=subprocess.STDOUT,startupinfo=startup,creationflags=flags)
        born=state.process_identity(process.pid).get('birth')
        lock.update(operation={'pid':process.pid,'birth':born,'run_id':lock.run_id,'owner_token':lock.token})
        try:
            while process.poll() is None:
                elapsed=time.monotonic()-start
                log=log_path.read_text(encoding='utf-8-sig',errors='replace') if log_path.exists() else ''
                if re.search(r'Initialize engine version|Asset Pipeline|Importing|Start importing|MonoManager|Compil|Resolving packages',log,re.I):working=True
                if not working and elapsed>=budget:
                    connection_failed=True;process.kill();break
                if elapsed>=timeout:
                    timed_out=True;process.kill();break
                time.sleep(.5)
            process.wait(timeout=30)
        finally:
            if process.poll() is None:process.kill();process.wait(timeout=30)
            lock.update(operation=None)
    if connection_failed:
        ledger.connection(environment,min(30,time.monotonic()-start))
        raise state.Blocked('connection_unavailable','No usable startup progress within remaining connection budget')
    return process.returncode,timed_out,round(time.monotonic()-start,6)


def required_runtime(checks):
    import runtime_validation as runtime
    result={'no runtime errors or exceptions observed'}
    if 'runtime.ui' in checks: result |= runtime.REQUIRED_CHECKS - {'Movement refactor checks completed','M3 final removal clears slot and restores zero weight','M3 bulk overweight request is atomic'}
    if 'runtime.inventory' in checks:result|={'M3 final removal clears slot and restores zero weight','M3 bulk overweight request is atomic'}
    if 'runtime.movement' in checks:result.add('Movement refactor checks completed')
    return result


def reviewed_normalizations(workspace, inputs, workspace_state, hashes):
    """Only exact reviewed engine derivatives are allowed; never copied back to source."""
    policy=state.read_json(TOOLS/'import_normalizations.json',{})
    if policy.get('unity')!=workspace_state['context']['unity']:return []
    accepted=[]
    for item in policy.get('records',[]):
        rel=item['path'];current=state.relative_file(workspace,rel)
        if inputs.get(rel,{}).get('sha256')!=item['source_sha256'] or not current.is_file():continue
        actual=hashes.file(current)
        if actual==item['normalized_sha256']:
            workspace_state.setdefault('overrides',{})[rel]=actual
            accepted.append({'path':rel,'reason':item['reason'],'source_sha256':item['source_sha256'],'normalized_sha256':actual})
    return accepted


def execute_unity(args, workspace, checks, ctx, evidence, run_id, lock, ledger, hashes, workspace_state):
    import runtime_validation as runtime
    original_settings=(workspace/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig')
    isolated=runtime.isolate_settings(original_settings,run_id)
    (workspace/'ProjectSettings/ProjectSettings.asset').write_text(isolated,encoding='utf-8')
    workspace_state.setdefault('overrides',{})['ProjectSettings/ProjectSettings.asset']=hashes.file(workspace/'ProjectSettings/ProjectSettings.asset')
    count=install_probes(workspace,checks,run_id,evidence,Path(args.project),hashes,workspace_state)
    runtime_scopes=[c.split('.',1)[1] for c in checks if c.startswith('runtime.')]
    command=[args.unity,'-batchmode','-buildTarget','Win64','-projectPath',str(workspace),'-logFile',str(evidence/'logs/unity.log'),'-upmLogFile',str(evidence/'logs/upm.log'),'-executeMethod','RuntimeProbe.Run' if runtime_scopes else 'BatterMap.Validation.CompileProbe.Run', '-validationRunId',run_id,'-validationProject',str(workspace),'-validationMarker',str(evidence/'completion.json'),'-validationVersion',ctx['unity'],'-validationEvidence',str(evidence)]
    if runtime_scopes:command+=['-validationScopes',','.join(runtime_scopes)]
    else:command+=['-nographics','-quit']
    if 'services.release' in checks:command+=['-validationServices','release']
    state.write_json(evidence/'command.json',{'command':command,'injected_copies':count})
    code,timeout,elapsed=launch_owned(command,args.timeout,evidence,lock,ledger,state.signature(ctx))
    log=(evidence/'logs/unity.log').read_text(encoding='utf-8-sig',errors='replace') if (evidence/'logs/unity.log').exists() else ''
    try:marker=state.read_json(evidence/'completion.json')
    except state.Blocked:marker=None
    if runtime_scopes:
        status,reason=runtime.classify(code,timeout,marker,log,run_id,workspace,ctx['unity'],required_checks=required_runtime(checks))
        if marker and (set(marker.get('scopes',[]))!=set(runtime_scopes) or marker.get('target')!='StandaloneWindows64'):status,reason='failed','invalid_scope_completion'
    else:status,reason=legacy.classify(code,timeout,marker,log,run_id,workspace,ctx['unity'])
    if 'services.release' in checks and (not marker or not isinstance(marker.get('serviceChecks'),int) or marker.get('serviceChecks',0)<=0):status,reason='failed','service_completion_missing'
    if status!='passed' and reason=='licensing':ledger.connection(state.signature(ctx),min(30,elapsed))
    return {'status':status,'reason':reason,'exit_code':code,'timed_out':timeout,'seconds':elapsed,'completion':marker}


def run(args):
    source=state.safe_path(args.project);root=state.safe_path(args.run_root)
    if source==root or source in root.parents or root in source.parents:raise state.Blocked('unsafe_path','Run root overlaps source')
    run_id=uuid.uuid4().hex[:12];evidence=root/'runs'/run_id/'evidence';evidence.mkdir(parents=True,exist_ok=False);(evidence/'logs').mkdir()
    timings=state.Timings(evidence/'timings.json');hashes=state.Hashes();start=time.monotonic()
    result={'schema':1,'run_id':run_id,'status':'blocked','checks':{},'evidence':str(evidence),'source':str(source),'phase':'preflight'}
    before=None;ordinary=None;workspace_state=None;marker_path=None;pending_cache=[];all_inputs=None;cache=None
    try:
        with timings.phase('preflight'):
            validate_source(source,None)
        with timings.phase('manifest_change_discovery'):
            before=hashes.snapshot(source);extra=tool_inputs(source,hashes);all_inputs={**before,**extra}
            previous=state.read_json(root/'baselines'/ (state.signature(str(source))[:16]+'.json'),{})
            detected=legacy.differences(previous['inputs'],all_inputs) if previous.get('inputs') else []
            changes=sorted(set(args.changed)|set(detected))
            if not previous.get('inputs') and not args.scope and not changes:changes=['Assets/<unknown-initial-baseline>']
            selected=plan(changes,args.scope,args.require);ctx=context(source,before,Path(args.unity))
            if any(CHECKS[c].cost!='small' for c in selected['selected']) and (not state.safe_path(args.unity).is_file() or Path(args.unity).parent.parent.name!=ctx['unity']):raise state.Blocked('invalid_configuration','Unity installation version mismatch')
            result['plan']=selected;state.write_json(evidence/'plan.json',selected)
        with timings.phase('isolation_preconditions'):
            import runtime_validation as runtime
            company,product=runtime.identity((source/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8-sig'))
            ordinary=runtime.persistence(company,product)
            isolated=runtime.persistence(runtime.COMPANY,'Run_'+run_id)
            if isolated['exists'] or any(v is not None for v in isolated['registry'].values()):raise state.Blocked('isolation_failure','Run persistence namespace already exists')
            state.write_json(evidence/'persistence-before.json',ordinary)
        cache=state.Evidence(root/'evidence-cache');needed=[];fingerprints={}
        for check_id in selected['selected']:
            check=CHECKS[check_id];fp=fingerprint(check,all_inputs,ctx,hashes);fingerprints[check_id]=fp
            record,reason=cache.get(check_id,fp,check.method,hashes) if check.reusable and not args.force_recheck else (None,'forced_or_nonreusable')
            if record:result['checks'][check_id]={'status':'reused','method':check.method,'origin':record['provenance'],'reason':reason}
            else:needed.append(check_id);result['checks'][check_id]={'status':'unverified','reason':reason,'method':check.method}
        for check_id in [c for c in needed if CHECKS[c].cost=='small']:
            with timings.phase('checks.'+check_id):
                detail=globals()[CHECKS[check_id].implementation](source)
                artifact=evidence/(check_id+'.json');state.write_json(artifact,detail)
                result['checks'][check_id]={'status':'passed','method':'static','artifact':str(artifact)}
                pending_cache.append((check_id,fingerprints[check_id],'static',{'run_id':run_id,'executed':True,'completion_verified':True},[artifact]))
        unity_checks=[c for c in needed if CHECKS[c].cost!='small']
        if unity_checks:
            workspace,marker_path,workspace_state=registered_workspace(root,ctx,args.mode,args.workspace)
            with timings.phase('workspace_ownership'), state.WorkspaceLock(workspace.parent/'ownership.lock',workspace,run_id,args.lock_wait) as lock:
                if (workspace/'Temp/UnityLockfile').exists():raise state.Blocked('workspace_busy','Existing Editor lock; no editor adoption or automatic deletion')
                workspace.mkdir(parents=True,exist_ok=True)
                if workspace_state is None:workspace_state={'schema':state.SCHEMA,'workspace':str(workspace),'context':ctx,'files':{},'overrides':{},'injected':[]}
                ledger=state.FailureLedger(root/'failures'/(state.signature(ctx)+'.json'))
                if args.retry_environment_reason:ledger.reopen(state.signature(ctx),args.retry_environment_reason)
                operation=state.signature({c:fingerprints[c] for c in unity_checks})
                cause=args.cause_id or 'unclassified/'+','.join(sorted(unity_checks));ledger.guard(cause,args.approach,operation)
                try:
                    with timings.phase('synchronization'):
                        result['reviewed_import_normalizations']=reviewed_normalizations(workspace,before,workspace_state,hashes)
                        result['sync']=state.synchronize(source,workspace,before,workspace_state,hashes)
                        workspace_state['files']=before
                        result['workspace']={'path':str(workspace),'mode':args.mode,'reconstructed':not bool(workspace_state.get('prepared'))}
                        state.write_json(marker_path,workspace_state)
                    with timings.phase('unity_startup_import_compile_checks_capture_combined'):
                        execution=execute_unity(args,workspace,unity_checks,ctx,evidence,run_id,lock,ledger,hashes,workspace_state)
                    for phase in ('package_resolution_import','compilation','fixture_preparation','actual_checks','capture_evidence'):
                        timings.unavailable(phase,'Legacy probes expose a combined process/marker duration; no invented subphase precision')
                    with timings.phase('candidate_preservation'):
                        result['reviewed_import_normalizations']=reviewed_normalizations(workspace,before,workspace_state,hashes)
                        after=hashes.snapshot(workspace,reject_hardlinks=True)
                        mutations=[p for p,v in before.items() if after.get(p,{}).get('sha256')!=workspace_state['overrides'].get(p,v['sha256'])]
                        if mutations:raise state.Blocked('candidate_mutation','Unexpected original input mutation: '+str(mutations[:12]))
                    if execution['status']!='passed':
                        raise state.Blocked(execution['reason'],str(execution))
                    workspace_state['prepared']=True
                    for c in unity_checks:
                        result['checks'][c]={'status':'passed','method':CHECKS[c].method,'execution':execution['reason'],'service_checks_observed':execution.get('completion',{}).get('serviceChecks',0)}
                        if CHECKS[c].reusable:pending_cache.append((c,fingerprints[c],CHECKS[c].method,{'run_id':run_id,'command':str(evidence/'command.json'),'executed':True,'completion_verified':True,'process_kind':'runtime' if any(x.startswith('runtime.') for x in unity_checks) else 'compile'},[evidence/'completion.json',evidence/'logs/unity.log']))
                except Exception as exc:
                    ledger.failure(cause,args.approach,operation,{'run_id':run_id,'classification':getattr(exc,'code','runner_error'),'evidence':str(evidence)})
                    raise
                finally:state.write_json(marker_path,workspace_state)
        else:
            result['sync']={'copied_files':0,'copied_bytes':0,'deleted_files':0}
            result['workspace']={'mode':args.mode,'reconstructed':False,'unity_started':False}
        result['status']='unverified' if selected['required_acceptance'] else 'passed'
    except state.Blocked as exc:result.update(status='failed' if exc.code in {'compiler_error','asset_import_error','missing_completion','invalid_completion','invalid_scope_completion','missing_or_invalid_completion','service_completion_missing','runtime_assertion_or_error','exception_in_editor_log','editor_exit_without_success','assertion_failure'} else 'blocked',reason=exc.code,detail=str(exc))
    except Exception as exc:result.update(status='failed',reason='runner_error',detail=repr(exc))
    finally:
        try:
            with timings.phase('final_source_and_persistence_preservation'):
                if before is not None:
                    final_inputs={**hashes.snapshot(source),**tool_inputs(source,hashes)}
                    drift=legacy.differences(all_inputs or before,final_inputs);result['source_preserved']=not drift;result['source_drift']=drift
                    if drift:result.update(status='blocked',reason='source_drift')
                if ordinary is not None:
                    import runtime_validation as runtime
                    after_data=runtime.persistence(company,product);state.write_json(evidence/'persistence-after.json',after_data)
                    result['ordinary_persistence_preserved']=ordinary==after_data
                    if ordinary!=after_data:result.update(status='blocked',reason='isolation_failure')
                if result['status'] in ('passed','unverified'):
                    for entry in pending_cache:cache.put(*entry,hashes)
                    state.write_json(root/'baselines'/(state.signature(str(source))[:16]+'.json'),{'inputs':all_inputs,'run_id':run_id})
        except Exception as exc:result.update(status='blocked',reason='preservation_check_failed',detail=repr(exc))
        result['metrics']={'hashed_files':hashes.files,'hashed_bytes':hashes.bytes,'elapsed_seconds':round(time.monotonic()-start,6)}
        state.write_json(evidence/'result.json',result)
        print(json.dumps({'run_id':run_id,'status':result['status'],'reason':result.get('reason'),'checks':result['checks'],'sync':result.get('sync'),'metrics':result['metrics'],'evidence':str(evidence)},ensure_ascii=False),flush=True)
    return 0 if result['status']=='passed' else 3 if result['status']=='unverified' else 2 if result['status']=='blocked' else 1


def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('action',choices=('plan','run'))
    p.add_argument('--project',default=str(ROOT));p.add_argument('--unity',default=UNITY);p.add_argument('--run-root',default=str(DEFAULT_ROOT));p.add_argument('--mode',choices=('reuse','fresh'),default='reuse');p.add_argument('--workspace');p.add_argument('--scope',type=csv,default=());p.add_argument('--require',type=csv,default=());p.add_argument('--changed',action='append',default=[]);p.add_argument('--force-recheck',default='',metavar='REASON');p.add_argument('--retry-environment-reason',default='');p.add_argument('--cause-id',default='');p.add_argument('--approach',default='existing-fixture');p.add_argument('--lock-wait',type=float,default=0);p.add_argument('--timeout',type=int,default=1200)
    a=p.parse_args(argv)
    if not 1<=a.timeout<=7200 or not 0<=a.lock_wait<=30:p.error('timeout1..7200, lock wait0..30')
    if a.action=='plan':
        h=state.Hashes();source=state.safe_path(a.project);inputs=h.snapshot(source);extra=tool_inputs(source,h);all_inputs={**inputs,**extra}
        old=state.read_json(Path(a.run_root)/'baselines'/(state.signature(str(source))[:16]+'.json'),{})
        detected=legacy.differences(old['inputs'],all_inputs) if old.get('inputs') else []
        changes=sorted(set(a.changed)|set(detected))
        if not old.get('inputs') and not a.scope and not changes:changes=['Assets/<unknown-initial-baseline>']
        output=plan(changes,a.scope,a.require);output.update(unity_started=False,environment_mode=a.mode,checks={k:asdict(CHECKS[k]) for k in output['selected']},note_reuse='Run validates dependency fingerprints/artifact provenance before any previous pass is reused.')
        print(json.dumps(output,ensure_ascii=False,indent=2));return 0
    return run(a)

if __name__=='__main__':raise SystemExit(main())
