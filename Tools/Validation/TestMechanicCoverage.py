"""Guard against unearned PASS: stale inputs, partial runs and missing assertions."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('coverage_runner', ROOT / 'Tools/ForgeBridge/MechanicCoverage.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class CoverageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.original = runner.ROOT, runner.HERE, runner.LOCAL, runner.REPORT
        runner.ROOT = Path(self.temp.name)
        runner.HERE = runner.ROOT
        runner.LOCAL = runner.ROOT / 'local'
        runner.REPORT = runner.ROOT / 'report'
        self.provenance = dict(revision='fixture', fingerprint='current', cardScripts=1)
        self.manifest = dict(scope='test', priority=[], jobs=[
            dict(id='first', script='first.py', cases=[dict(mechanics=['Flying'], assertions='Blocks grounded attacker')]),
            dict(id='second', script='second.py', cases=[dict(mechanics=['Storm'], assertions='Correct copied damage')])])
        self.entries = {'flying': dict(name='Flying', chinese='飞行', cards=set(), origins=[]),
                        'storm': dict(name='Storm', chinese='风暴', cards=set(), origins=[])}

    def tearDown(self):
        runner.ROOT, runner.HERE, runner.LOCAL, runner.REPORT = self.original
        self.temp.cleanup()

    def report(self, provenance, results):
        runner.LOCAL.mkdir(exist_ok=True)
        (runner.LOCAL / 'latest.json').write_text(json.dumps(dict(run='test', provenance=provenance, jobs=results)))
        with contextlib.redirect_stdout(io.StringIO()):
            runner.build_report(self.manifest, self.entries, {}, self.provenance)
        return {r['name']: r['status'] for r in json.loads((runner.REPORT / 'coverage.json').read_text(encoding='utf8'))['entries']}

    def test_changed_input_invalidates_all_historical_passes(self):
        stale = dict(self.provenance, fingerprint='old')
        statuses = self.report(stale, [dict(id='first', status='PASS'), dict(id='second', status='PASS')])
        self.assertEqual(set(statuses.values()), {'需重测'})

    def test_partial_run_does_not_claim_untested_job(self):
        statuses = self.report(self.provenance, [dict(id='first', status='PASS')])
        self.assertEqual(statuses['Flying'], '已验证列出的场景')
        self.assertEqual(statuses['Storm'], '有测试，本轮未运行')

    def test_failure_stays_visible(self):
        statuses = self.report(self.provenance, [dict(id='first', status='FAIL')])
        self.assertEqual(statuses['Flying'], '失败')

    def test_success_exit_without_all_case_evidence_is_failure(self):
        script = runner.ROOT / 'fake.py'
        script.write_text("print('PASS first')\n", encoding='utf8')
        manifest = dict(jobs=[dict(id='fake', script='fake.py', reportDir='fake-reports', cases=[
            dict(arg='first', **{'pass': 'PASS first'}), dict(arg='second', **{'pass': 'PASS second'})])])
        with contextlib.redirect_stdout(io.StringIO()):
            failed = runner.run_jobs(manifest, self.provenance, None, 5)
        self.assertTrue(failed)
        result = json.loads((runner.LOCAL / 'latest.json').read_text())['jobs'][0]
        self.assertEqual(result['exitCode'], 0)
        self.assertEqual(result['status'], 'FAIL')

    def test_content_change_changes_fingerprint(self):
        path = runner.ROOT / 'rules.txt'
        path.write_text('old effect')
        before = runner.fingerprint([path])
        path.write_text('new effect')
        self.assertNotEqual(before, runner.fingerprint([path]))

    def test_classified_helper_is_not_effect_pass_and_changes_reopen_review(self):
        import hashlib
        path = runner.ROOT / 'Test-Helper.py'
        path.write_text('helper service')
        self.manifest['reviewedScripts'] = [dict(path=path.name,
            sourceHash=hashlib.sha256(path.read_bytes()).hexdigest(),
            kind='fixture-service', scope='Local HTTP fixture only',
            remaining='No card effect assertions or current runtime proof')]
        unreviewed, reviewed, stale = runner.review_script_inventory(self.manifest)
        self.assertEqual(unreviewed, [])
        self.assertEqual(len(reviewed), 1)
        self.assertEqual(stale, [])
        self.assertEqual(set(self.report(self.provenance, []).values()), {'有测试，本轮未运行'})
        path.write_text('changed service')
        unreviewed, reviewed, stale = runner.review_script_inventory(self.manifest)
        self.assertEqual(unreviewed, [path.name])
        self.assertEqual(reviewed, [])
        self.assertEqual(len(stale), 1)

    def test_adapter_fixture_change_reopens_classification(self):
        import hashlib
        path = runner.ROOT / 'Test-Adapter.py'
        fixture = runner.ROOT / 'Fixture.java'
        path.write_text('projection driver')
        fixture.write_text('seed metadata')
        self.manifest['reviewedScripts'] = [dict(path=path.name,
            sourceHash=hashlib.sha256(path.read_bytes()).hexdigest(),
            kind='projection-adapter', scope='Direct metadata projection',
            remaining='No real spell cast', sourceGuards=[dict(path=fixture.name,
                sha256=hashlib.sha256(fixture.read_bytes()).hexdigest())])]
        self.assertEqual(runner.review_script_inventory(self.manifest)[0], [])
        fixture.write_text('now real casting')
        self.assertEqual(runner.review_script_inventory(self.manifest)[0], [path.name])

    def test_non_keyword_effects_are_retained_without_oracle_false_matches(self):
        found = {}

        def add(name, origin=''):
            return found.setdefault(name, dict(cards=set()))

        runner.scan_structure('''Name:Storm
Oracle:Storm creates a delayed trigger; PayLife<5>.
R:Event$ BeginPhase | Phase$ Draw | Skip$ True
T:Mode$ Discarded | Execute$ ExileCard | TriggerDescription$ Landfall — Whenever a land enters, do something.
S:Mode$ Continuous | AddPower$ 2
A:AB$ ChangeZone | Cost$ PayLife<1> T | SpellDescription$ Example:Mode$ FakeMode
SVar:Later:DB$ DelayedTrigger | Mode$ Phase | Duration$ UntilEndOfTurn
''', 'Necropotence', add)
        for name in ['Replacement:BeginPhase', 'Trigger:Discarded', 'Static:Continuous',
                     'Cost:PayLife', 'Cost:Tap', 'ScriptMode:Phase', 'Duration:UntilEndOfTurn', 'Parameter:AddPower', 'AbilityLabel:Landfall']:
            self.assertEqual(found[name]['cards'], {'Necropotence'})
        self.assertNotIn('Storm', found)
        self.assertNotIn('ScriptMode:FakeMode', found)


class DeclarationClassificationTests(unittest.TestCase):
    def setUp(self):
        self.parser = runner.declaration_inventory
        self.catalog = self.parser.keyword_catalog('''
    UNDEFINED("", Simple.class, false, ""),
    FLYING("Flying", Simple.class, false, ""),
    FIRST_STRIKE("First strike", Simple.class, false, ""),
    WARD("Ward", Ward.class, false, ""),
    PROTECTION("Protection", Protection.class, false, ""),
''')
        self.entries = {runner.key(v): v for v in self.catalog.values()}

    def resolve(self, field, value):
        return self.parser.resolve(dict(field=field, value=value), self.catalog,
                                   self.entries, runner.key, {'DoubleFaced', 'Modal'})

    def test_parameter_and_multiword_resolution(self):
        self.assertEqual(self.resolve('AddKeyword', 'Ward:PayLife<2> & First strike'),
                         ('keyword-variant', ['Ward', 'First strike'], []))
        self.assertEqual(self.parser.keyword_name('Protection from black', self.catalog), 'Protection')

    def test_unknown_component_is_retained(self):
        self.assertEqual(self.resolve('AddKeyword', 'Flying & Unreviewed:3'),
                         ('unclassified', ['Flying'], ['Unreviewed:3']))
        # K is not an AddKeyword list: native parsing recognizes only Flying,
        # retaining the rest as details, without proving those details valid.
        self.assertEqual(self.resolve('K', 'Flying & First strike'), ('keyword-variant', ['Flying'], []))

    def test_exact_fields_and_source_locations(self):
        rows = list(self.parser.declarations('''Oracle:Condition$ True
SVar:Ability:DB$ Pump | LifeCondition$ True | Condition$ Kicked | AddKeyword$ Ward:3
ALTERNATE
K:Unknown:Exact:Parameters
'''))
        self.assertEqual([(r['field'], r['line'], r['face']) for r in rows],
                         [('LifeCondition', 2, 0), ('Condition', 2, 0), ('AddKeyword', 2, 0), ('K', 4, 1)])
        self.assertEqual(rows[-1]['value'], 'Unknown:Exact:Parameters')
        self.assertEqual(self.resolve('LifeCondition', 'True')[0], 'exact-field-parameter')
        self.assertEqual(self.resolve('Condition', 'True')[0], 'unclassified')

    def test_metadata_is_not_keyword_or_effect_pass(self):
        self.assertEqual(self.resolve('AlternateMode', 'DoubleFaced'), ('card-face-metadata', [], []))
        self.assertEqual(self.resolve('AlternateMode', 'FutureMode')[0], 'unclassified')
        self.assertEqual(self.resolve('Attributes', 'Flying')[0], 'existing-entry-reference')

    def test_keyword_prefix_never_guesses_unknown_name(self):
        self.assertIsNone(self.parser.keyword_name('Flight:3', self.catalog))
        self.assertIsNone(self.parser.keyword_name('FlyingExtra', self.catalog))


if __name__ == '__main__':
    unittest.main()
