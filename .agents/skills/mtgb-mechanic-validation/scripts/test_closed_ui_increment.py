"""Closed boundaries may use audited existing-consumer increments without new UI first."""
import copy,hashlib,importlib.util,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('remaining',Path(__file__).with_name('summarize_remaining.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
ROOT=m.DEFAULT_ROOT
class ClosedIncrementTests(unittest.TestCase):
 def setUp(self):
  self.ledger=m.read_json(m.SKILL/'references/verified-progress.json');self.config=m.read_json(ROOT/'Tools/ForgeBridge/PresentationFamilies.json');self.manifest=m.read_json(ROOT/'Tools/ForgeBridge/MechanicCoverage.json');self.plan=m.load_plan();self.closed=next(p for p in self.ledger['closedPackages'] if p['id']=='R34');self.increment=self.closed['uiIncrements'][0]
 def run_check(self):
  original=m.read_json
  def read(p):
   if p==m.SKILL/'references/verified-progress.json':return copy.deepcopy(self.ledger)
   if p==ROOT/'Tools/ForgeBridge/PresentationFamilies.json':return copy.deepcopy(self.config)
   return original(p)
  with patch.object(m,'read_json',read):return m.verified_progress(ROOT,self.manifest,self.plan)
 def test_increment_closes_without_ui_first(self):
  before=len(self.ledger['uiTargets']);result=self.run_check();self.assertEqual(len(result['uiTargets']),before);self.assertFalse(self.closed['uiTargets']);self.assertNotIn(self.increment['job'],[x['job'] for x in result['uiTargets']])
 def test_missing_audit_rejected(self):
  self.ledger['regressionBatches']=[b for b in self.ledger['regressionBatches'] if b['id']!='dungeon-current-consumers-1006']
  with self.assertRaisesRegex(ValueError,'lacks registered audit'):self.run_check()
 def test_unknown_ui_job_rejected(self):
  self.increment['job']='not-registered'
  with self.assertRaisesRegex(ValueError,'lacks registered audit'):self.run_check()
 def test_duplicate_increment_rejected(self):
  self.closed['uiIncrements'].append(copy.deepcopy(self.increment))
  with self.assertRaisesRegex(ValueError,'Duplicate closed UI increment'):self.run_check()
 def test_changed_hash_rejected_even_if_audit_claims_it(self):
  evidence=self.increment['evidence'];old=evidence['sha256'];evidence['sha256']='0'*64
  for b in self.ledger['regressionBatches']:
   for e in b['evidence']:
    if e['path']==evidence['path'] and e['sha256']==old:e['sha256']=evidence['sha256']
  with self.assertRaisesRegex(ValueError,'incomplete or changed'):self.run_check()
 def test_non_pass_rejected_even_with_matching_hash(self):
  with tempfile.TemporaryDirectory() as folder:
   p=Path(folder)/'result.txt';p.write_text('RUNNING');e=dict(path=p.as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest());self.increment['evidence']=e
   next(j for j in self.config['uiJobs'] if j['id']==self.increment['job'])['result']=e['path'];self.ledger['regressionBatches'].append(dict(id='test',evidence=[e]))
   with self.assertRaisesRegex(ValueError,'incomplete or changed'):self.run_check()
if __name__=='__main__':unittest.main()
