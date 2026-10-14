"""A shared renderer never earns rule PASS or hides newly introduced APIs."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('families', ROOT / 'Tools/ForgeBridge/PresentationCoverage.py')
families = importlib.util.module_from_spec(spec)
spec.loader.exec_module(families)


class FamilyEvidenceTests(unittest.TestCase):
    def test_project_ui_job_dependencies_resolve(self):
        configuration = json.loads((ROOT / 'Tools/ForgeBridge/PresentationFamilies.json').read_text(encoding='utf-8-sig'))
        manifest = json.loads((ROOT / 'Tools/ForgeBridge/MechanicCoverage.json').read_text(encoding='utf-8-sig'))
        engine = {job['id'] for job in manifest['jobs']}
        ui_ids = [job['id'] for job in configuration['uiJobs']]
        self.assertEqual(len(ui_ids), len(set(ui_ids)), 'Duplicate UI job identities')
        for job in configuration['uiJobs']:
            self.assertFalse(set(job.get('engineJobs', [])) - engine, job['id'])

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.report = self.root / 'report'; self.report.mkdir()
        bridge = self.root / 'Tools/ForgeBridge/java/mtgb/bridge'; bridge.mkdir(parents=True)
        (bridge / 'ForgeBridge.java').write_text('static boolean supportedInput(Input input) {\n return input instanceof InputConfirm;\n    }')
        (self.report / 'coverage.json').write_text(json.dumps(dict(entries=[
            dict(name='API:NewOperation', cards=['New Card'], cardCount=1)])))
        inputs = self.root / 'inputs'; inputs.mkdir()
        (inputs / 'view.json').write_text('{"power":2}')
        self.provenance = dict(fingerprint='current', revision='forge')
        self.manifest = dict(jobs=[dict(id='engine', reportDir='inputs', cases=[dict(arg='case', assertions='Damage dealt')])])
        self.configuration = dict(reference='ref.md', uiJobs=[dict(id='ui', engineJobs=['engine'], assertions='Legal pointer click')],
            families=[dict(id='targets', name='Targets', requests=['InputConfirm'], apis=[], rules=['engine/case'], ui=['ui'], reference=[], gaps=['Multiple targets'])])
        self.engine = dict(provenance=self.provenance, jobs=[])
        self.unity = dict(fingerprint='current', forgeRevision='forge', jobs=[
            dict(id='ui', status='PASS', inputs=families.input_fingerprint(self.root, ['inputs']))])

    def tearDown(self):
        self.temp.cleanup()

    def build(self):
        families.build(self.root, self.report, self.configuration, self.manifest, self.provenance, self.engine, self.unity)
        return json.loads((self.report / 'families.json').read_text(encoding='utf8'))

    def test_ui_pass_never_proves_untested_rules(self):
        data = self.build()
        self.assertEqual(data['families'][0]['ui'][0]['status'], 'PASS')
        self.assertEqual(data['families'][0]['rules'][0]['status'], '本轮未运行')
        self.assertEqual(data['unmappedAPIs'][0]['api'], 'NewOperation')
        self.assertEqual(data['families'][0]['gaps'], ['Multiple targets'])

    def test_changed_snapshot_invalidates_ui_result(self):
        (self.root / 'inputs/view.json').write_text('{"power":3}')
        self.assertEqual(self.build()['uiJobs'][0]['status'], '需重测')

    def test_archived_inputs_retain_proof_and_detect_archive_changes(self):
        archive = self.root / 'archive'
        shutil.copytree(self.root / 'inputs', archive / 'inputs')
        self.unity['jobs'][0]['inputEvidence'] = 'archive'
        (self.root / 'inputs/view.json').write_text('{"power":3}')
        self.assertEqual(self.build()['uiJobs'][0]['status'], 'PASS')
        (archive / 'inputs/view.json').write_text('{"power":4}')
        self.assertEqual(self.build()['uiJobs'][0]['status'], '需重测')

    def test_changed_code_invalidates_ui_and_engine_independently(self):
        self.unity['fingerprint'] = 'old'
        self.engine = dict(provenance=dict(self.provenance, fingerprint='old'), jobs=[dict(id='engine', status='PASS')])
        data = self.build()['families'][0]
        self.assertEqual(data['ui'][0]['status'], '需重测')
        self.assertEqual(data['rules'][0]['status'], '需重测')

    def test_missing_scenario_stays_visible_without_discarding_the_report(self):
        self.configuration['families'][0]['rules'].append('new/unregistered')
        data = self.build()
        self.assertEqual(data['families'][0]['rules'][-1]['status'], 'INVALID')
        self.assertEqual(data['invalidScenarios'], [dict(family='targets', scenario='new/unregistered')])


if __name__ == '__main__':
    unittest.main()
