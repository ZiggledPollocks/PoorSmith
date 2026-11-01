import json, os, subprocess, sys, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
import validation_state as s
import validation_workflow as w

class StateCases(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.h=s.Hashes()
    def tearDown(self): self.temp.cleanup()
    def file(self,rel,text='data'):
        p=self.root/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text);return p
    def assertBlocked(self,code,action):
        with self.assertRaises(s.Blocked) as cm:action()
        self.assertEqual(code,cm.exception.code)
    def test_manifest_includes_untracked_and_meta_and_same_size_edit(self):
        for name in s.INPUTS:(self.root/name).mkdir()
        f=self.file('Assets/new.png','aaa');self.file('Assets/new.png.meta')
        before=self.h.snapshot(self.root);stamp=f.stat().st_mtime_ns;f.write_text('bbb');os.utime(f,ns=(stamp,stamp))
        after=self.h.snapshot(self.root);self.assertIn('Assets/new.png.meta',after);self.assertNotEqual(before,after)
    def test_manifest_missing_input_blocked(self):self.assertBlocked('invalid_configuration',lambda:self.h.snapshot(self.root))
    def test_manifest_traversal_rejected(self):
        for p in ('../other','/absolute','C:/x','Assets/../other','Assets\\bad'):
            self.assertBlocked('unsafe_path',lambda:s.relative_file(self.root,p))
    def test_hardlink_destination_refused(self):
        src=self.file('source/Assets/a');dst=self.root/'target/Assets/a';dst.parent.mkdir(parents=True);os.link(src,dst)
        self.assertBlocked('unsafe_path',lambda:s.synchronize(self.root/'source',self.root/'target',{'Assets/a':{'sha256':self.h.file(src),'bytes':4}},{},self.h))
    def test_overlap_refused(self):self.assertBlocked('unsafe_path',lambda:s.synchronize(self.root,self.root/'inner',{}, {},self.h))
    def test_incremental_add_rename_delete_meta_and_foreign_preserved(self):
        src=self.root/'src';dst=self.root/'dst';state={};f=self.file('src/Assets/a','a');self.file('src/Assets/a.meta')
        def manifest():return {'Assets/'+p.name:{'sha256':self.h.file(p),'bytes':p.stat().st_size} for p in (src/'Assets').iterdir()}
        first=s.synchronize(src,dst,manifest(),state,self.h);self.assertEqual(first['copied_files'],2)
        self.assertEqual(s.synchronize(src,dst,manifest(),state,self.h)['copied_files'],0)
        self.file('dst/Assets/foreign','keep');f.rename(src/'Assets/b');(src/'Assets/a.meta').rename(src/'Assets/b.meta')
        change=s.synchronize(src,dst,manifest(),state,self.h);self.assertEqual(change['deleted_files'],2);self.assertEqual(change['copied_files'],2)
        self.assertEqual((dst/'Assets/foreign').read_text(),'keep')
    def test_foreign_destination_is_not_overwritten(self):
        f=self.file('src/Assets/a','new');self.file('dst/Assets/a','old')
        self.assertBlocked('foreign_workspace',lambda:s.synchronize(self.root/'src',self.root/'dst',{'Assets/a':{'sha256':self.h.file(f),'bytes':3}},{},self.h))
    def test_cache_content_and_method_and_provenance(self):
        cache=s.Evidence(self.root/'cache');artifact=self.file('marker','ok');prov={'run_id':'run','completion_verified':True}
        cache.put('check','fp','synthetic',prov,[artifact],self.h)
        self.assertIsNotNone(cache.get('check','fp','synthetic',self.h)[0]);self.assertIsNone(cache.get('check','fp','native',self.h)[0])
        artifact.write_text('corrupt');self.assertIsNone(cache.get('check','fp','synthetic',self.h)[0])
    def test_cache_missing_and_malformed_evidence(self):
        cache=s.Evidence(self.root/'cache');artifact=self.file('marker');cache.put('a','fp','static',{},[artifact],self.h)
        self.assertIsNone(cache.get('a','fp','static',self.h)[0]);cache.path('a','fp').write_text('{')
        self.assertEqual(cache.get('a','fp','static',self.h)[1],'corrupt_record')
    def test_cache_missing_artifact(self):
        cache=s.Evidence(self.root/'cache');a=self.file('artifact');cache.put('a','fp','static',{'run_id':'r','completion_verified':True},[a],self.h);a.unlink()
        self.assertIsNone(cache.get('a','fp','static',self.h)[0])
    def test_same_process_identity_and_pid_reuse(self):
        record={'pid':os.getpid(),'birth':s.process_identity(os.getpid())['birth']};self.assertTrue(s.same_live_process(record))
        with patch.object(s,'process_identity',return_value={'alive':True,'birth':'different'}):self.assertFalse(s.same_live_process(record))
    def test_cross_process_lock_busy(self):
        lock=self.root/'ownership';workspace=self.root/'p'
        with s.WorkspaceLock(lock,workspace,'first'):
            code="import sys;from pathlib import Path;import validation_state as s\ntry:\n with s.WorkspaceLock(Path(sys.argv[1]),Path(sys.argv[2]),'second'):sys.exit(9)\nexcept s.Blocked as e:sys.exit(0 if e.code=='workspace_busy' else 8)"
            r=subprocess.run([sys.executable,'-c',code,str(lock),str(workspace)],cwd=Path(__file__).parent,capture_output=True)
            self.assertEqual(r.returncode,0,r.stderr.decode())
        with s.WorkspaceLock(lock,workspace,'next') as owner:self.assertEqual(owner.owner['run_id'],'next')
    def test_orphan_live_operation_protected(self):
        lock=self.root/'lock';workspace=self.root/'p'
        with s.WorkspaceLock(lock,workspace,'first') as owner:owner.update(operation={'pid':os.getpid(),'birth':s.process_identity(os.getpid())['birth']})
        self.assertBlocked('workspace_busy',lambda:s.WorkspaceLock(lock,workspace,'next').__enter__())
    def test_dead_operation_recoverable(self):
        lock=self.file('lock',' '+json.dumps({'run_id':'dead','operation':{'pid':99999,'birth':'old'}}))
        with patch.object(s,'same_live_process',return_value=False):
            with s.WorkspaceLock(lock,self.root/'p','new') as owner:self.assertEqual(owner.owner['recovered_run'],'dead')
    def test_unchanged_failure_and_three_changed_cycles_stop(self):
        ledger=s.FailureLedger(self.root/'fail.json')
        ledger.failure('cause','approach','v1',{'first':'retained'})
        self.assertBlocked('unchanged_failed_operation',lambda:ledger.guard('cause','approach','v1'))
        ledger.guard('cause','approach','v2');ledger.failure('cause','approach','v2',{});ledger.failure('cause','approach','v3',{})
        self.assertBlocked('approach_exhausted',lambda:ledger.guard('cause','approach','v4'))
        self.assertEqual(ledger.load()['issues']['cause|approach']['first_context'],{'first':'retained'})
        ledger.guard('cause','different-approach','v4')
    def test_connection_budget_survives_runner_restart(self):
        path=self.root/'fail.json';s.FailureLedger(path).connection('env',12);s.FailureLedger(path).connection('env',18)
        self.assertEqual(s.FailureLedger(path).budget('env'),0)
        self.assertBlocked('invalid_configuration',lambda:s.FailureLedger(path).reopen('env',''))
        s.FailureLedger(path).reopen('env','license fixed');self.assertEqual(s.FailureLedger(path).budget('env'),30)
    def test_corrupt_ledger_fails_closed(self):
        p=self.file('ledger','{');self.assertBlocked('invalid_record',lambda:s.FailureLedger(p).guard('a','b','c'))
    def test_unregistered_workspace_protected(self):
        self.file('workspace/p/userfile')
        with patch.object(w,'MAX_PROJECT_PREFIX',10000):self.assertBlocked('foreign_workspace',lambda:w.registered_workspace(self.root,{},'reuse',str(self.root/'workspace/p')))
    def test_incompatible_registered_workspace(self):
        p=self.root/'workspace/p';s.write_json(p.parent/'workspace.json',{'schema':1,'workspace':str(p),'context':{'old':True}})
        with patch.object(w,'MAX_PROJECT_PREFIX',10000):self.assertBlocked('incompatible_workspace',lambda:w.registered_workspace(self.root,{},'reuse',str(p)))

    def test_partial_source_change_does_not_publish_copy(self):
        f=self.file('src/Assets/a','old');manifest={'Assets/a':{'sha256':self.h.file(f),'bytes':3}};f.write_text('new')
        self.assertBlocked('source_drift',lambda:s.synchronize(self.root/'src',self.root/'dst',manifest,{},self.h))
        self.assertFalse((self.root/'dst/Assets/a').exists());self.assertEqual(list((self.root/'dst/Assets').iterdir()),[])
    def test_candidate_edit_is_not_overwritten_or_deleted(self):
        f=self.file('src/Assets/a','old');state={};manifest={'Assets/a':{'sha256':self.h.file(f),'bytes':3}}
        s.synchronize(self.root/'src',self.root/'dst',manifest,state,self.h);self.file('dst/Assets/a','user edit');f.write_text('new')
        manifest['Assets/a']={'sha256':self.h.file(f),'bytes':3}
        self.assertBlocked('candidate_drift',lambda:s.synchronize(self.root/'src',self.root/'dst',manifest,state,self.h))
        self.assertBlocked('candidate_drift',lambda:s.synchronize(self.root/'src',self.root/'dst',{},state,self.h))
        self.assertEqual((self.root/'dst/Assets/a').read_text(),'user edit')
    def test_corrupt_ownership_fails_closed_and_unlocks(self):
        lock=self.file('lock',' corrupt')
        self.assertBlocked('invalid_record',lambda:s.WorkspaceLock(lock,self.root/'p','r').__enter__())
        lock.write_text(' ')
        with s.WorkspaceLock(lock,self.root/'p','r'):pass
    def test_dead_owner_os_lock_released_on_process_exit(self):
        lock=self.root/'lock';workspace=self.root/'p'
        code="import os,sys;from pathlib import Path;import validation_state as s;owner=s.WorkspaceLock(Path(sys.argv[1]),Path(sys.argv[2]),'crash');owner.__enter__();os._exit(0)"
        subprocess.run([sys.executable,'-c',code,str(lock),str(workspace)],cwd=Path(__file__).parent,check=True)
        with s.WorkspaceLock(lock,workspace,'after') as owner:self.assertEqual(owner.owner['recovered_run'],'crash')
    def test_healthy_progress_not_cut_by_connection_budget(self):
        evidence=self.root/'evidence';(evidence/'logs').mkdir(parents=True);ledger=s.FailureLedger(self.root/'ledger.json');ledger.connection('env',29)
        code="import pathlib,sys,time;pathlib.Path(sys.argv[1]).write_text('Initialize engine version');time.sleep(1.3)"
        with s.WorkspaceLock(self.root/'lock',self.root/'p','run') as owner:
            result=w.launch_owned([sys.executable,'-c',code,str(evidence/'logs/unity.log')],4,evidence,owner,ledger,'env')
        self.assertEqual(result[0],0);self.assertFalse(result[1]);self.assertEqual(ledger.budget('env'),1)
    def test_exhausted_connection_budget_never_launches(self):
        ledger=s.FailureLedger(self.root/'ledger.json');ledger.connection('env',30)
        with patch.object(w.subprocess,'Popen') as popen:
            self.assertBlocked('connection_unavailable',lambda:w.launch_owned([],3,self.root,None,ledger,'env'))
            popen.assert_not_called()

    def test_reviewed_normalization_requires_exact_source_and_derived(self):
        workspace=self.root/'p';a=self.file('p/Assets/a','derived');source_hash='original';derived=self.h.file(a)
        policy={'unity':'one','records':[{'path':'Assets/a','source_sha256':source_hash,'normalized_sha256':derived,'reason':'reviewed'}]}
        tool=self.root/'tools';tool.mkdir();s.write_json(tool/'import_normalizations.json',policy)
        ws={'context':{'unity':'one'}}
        with patch.object(w,'TOOLS',tool):
            self.assertEqual(len(w.reviewed_normalizations(workspace,{'Assets/a':{'sha256':source_hash}},ws,self.h)),1)
            self.assertEqual(w.reviewed_normalizations(workspace,{'Assets/a':{'sha256':'changed'}},ws,self.h),[])
            a.write_text('unexpected');self.assertEqual(w.reviewed_normalizations(workspace,{'Assets/a':{'sha256':source_hash}},ws,self.h),[])
    def test_reviewed_normalization_rejects_other_unity(self):
        tool=self.root/'tools';tool.mkdir();s.write_json(tool/'import_normalizations.json',{'unity':'one','records':[]})
        with patch.object(w,'TOOLS',tool):self.assertEqual(w.reviewed_normalizations(self.root,{}, {'context':{'unity':'two'}},self.h),[])

class PlannerCases(unittest.TestCase):
    def test_docs_does_not_select_unity(self):self.assertEqual(w.plan(['Docs/README.md'])['selected'],['static.instructions'])
    def test_unknown_does_not_claim_minimal(self):self.assertIn('runtime.inventory',w.plan(['Assets/unknown.asset'],['compile'])['selected'])
    def test_shared_save_expands_even_explicit_minimal(self):self.assertIn('services.release',w.plan(['Assets/JinHo/Scripts/Persistence/Save.cs'],['compile'])['selected'])
    def test_required_checks_cannot_be_removed(self):self.assertIn('runtime.movement',w.plan([],['docs'],['runtime.movement'])['selected'])
    def test_audio_acceptance_stays_unverified(self):self.assertIn('auditory',w.plan([],['audio'])['required_acceptance'])
    def test_invalid_scope_rejected(self):
        with self.assertRaises(s.Blocked):w.plan([],['fake'])
    def test_dependency_fingerprint_content_context_probe_and_method(self):
        inputs={'Assets/A.cs':{'sha256':'a'},'Tools/Validation/CompileProbe.cs.txt':{'sha256':'p'},'Tools/Validation/validation_state.py':{'sha256':'s'},'Tools/Validation/validation_workflow.py':{'sha256':'w'}}
        check=w.CHECKS['compile.windows64'];ctx={'unity':'one'};fp=w.fingerprint(check,inputs,ctx,None)
        extra={**inputs,'Docs/note.md':{'sha256':'x'}};self.assertEqual(fp,w.fingerprint(check,extra,ctx,None))
        extra['Assets/A.cs']={'sha256':'b'};self.assertNotEqual(fp,w.fingerprint(check,extra,ctx,None))
        self.assertNotEqual(fp,w.fingerprint(check,inputs,{'unity':'two'},None))
        extra={**inputs,'Tools/Validation/CompileProbe.cs.txt':{'sha256':'new'}};self.assertNotEqual(fp,w.fingerprint(check,extra,ctx,None))

if __name__=='__main__':unittest.main()
