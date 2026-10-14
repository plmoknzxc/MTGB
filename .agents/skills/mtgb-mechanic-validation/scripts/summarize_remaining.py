"""Summarize bounded planning work; never run games or change validation PASS states."""
import argparse
import collections
import csv
import datetime
import hashlib
import json
from pathlib import Path

SKILL = Path(__file__).resolve().parents[1]
DEFAULT_ROOT = SKILL.parents[2]
REPORT_PATH = Path('Tools/Validation/Reports/MechanicCoverage')
STRUCTURAL = {'脚本参数', '异能词／待核对文本标签', '其他脚本模式', '持续期限'}


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                   separators=(',', ':')).encode('utf-8')).hexdigest()


def source_snapshot(configuration, coverage, families, contract):
    return {
        'familyContracts': digest([{k: f[k] for k in ('id', 'name', 'requests', 'apis', 'gaps')}
                                   for f in configuration['families']]),
        'unmappedAPIs': digest(sorted(x['api'] for x in families['unmappedAPIs'])),
        'unmappedDeclarations': digest(sorted(coverage['unmappedDeclarations'])),
        'unreviewedTests': digest(sorted(coverage['testsNotReviewedForThisManifest'])),
        'reviewedScripts': digest(coverage.get('reviewedScriptsOutsideEffectManifest', [])),
        'interfaceGaps': digest(contract['knownGaps']),
        'interfaceUnclassified': digest({k: contract.get(k) for k in ('unknown', 'removed', 'missingHandlers')}),
    }


def load_plan():
    with (SKILL / 'references/remaining-packages.tsv').open(encoding='utf-8-sig', newline='') as stream:
        rows = list(csv.DictReader(stream, delimiter='\t'))
    ids = [r['id'] for r in rows]
    if not rows or len(ids) != len(set(ids)):
        raise ValueError('Planning package IDs must be nonempty and unique')
    for row in rows:
        if row['mode'] not in {'reuse', 'separate', 'audit'} or not row['boundary']:
            raise ValueError('Invalid package: ' + str(row))
        row['families'] = [x for x in row['families'].split(',') if x]
        row['status'] = row.get('status') or 'OPEN_PLANNED_NOT_TESTED'
        # Closing work requires a maintained evidence field, not changing a count.
        if row['status'] != 'OPEN_PLANNED_NOT_TESTED':
            raise ValueError('Non-open package requires an audited progress schema; current planner only counts open work: ' + row['id'])
    return rows


def validate_execution_plan(execution, plan):
    """Require one scheduling owner per open boundary; confer no effect proof."""
    if execution.get('schema') != 1:
        raise ValueError('Unknown execution-plan schema')
    batches = execution['batches']
    batch_ids = [b['id'] for b in batches]
    if not batch_ids or len(set(batch_ids)) != len(batch_ids):
        raise ValueError('Execution batch IDs must be nonempty and unique')
    owners = collections.Counter(p for b in batches for p in b['packages'])
    expected = {p['id'] for p in plan}
    if set(owners) != expected or any(n != 1 for n in owners.values()):
        raise ValueError('Execution plan must assign every open package exactly once: '
                         + str({'missing': sorted(expected - set(owners)),
                                'unknown': sorted(set(owners) - expected),
                                'duplicates': sorted(p for p, n in owners.items() if n != 1)}))
    if any(not b['packages'] or not b['title'] or not b['focus'] for b in batches):
        raise ValueError('Empty execution batch')
    checks = execution['sharedChecks']
    check_ids = [c['id'] for c in checks]
    if not check_ids or len(set(check_ids)) != len(check_ids):
        raise ValueError('Shared check IDs must be nonempty and unique')
    for check in checks:
        if (check['ownerBatch'] not in batch_ids or not check['title']
                or not check['reuseWhen'] or not check['newProofWhen']):
            raise ValueError('Shared check needs an owner and conditional proof rules')
    return execution


def load_concrete_work(root, plan, execution):
    """Validate active closure tasks separately from archived validation proof."""
    work = read_json(SKILL / 'references/remaining-work.json')
    if work.get('schema') != 1:
        raise ValueError('Unknown remaining-work schema')
    packages = work['packages']
    ids = [p['id'] for p in packages]
    planned = {p['id']: p for p in plan}
    if len(ids) != len(set(ids)) or set(ids) != set(planned):
        raise ValueError('Concrete work must cover each open package exactly once')
    owners = {p: b['id'] for b in execution['batches'] for p in b['packages']}
    check_ids = {c['id'] for c in execution['sharedChecks']}
    items = []
    for package in packages:
        if (package['batch'] != owners[package['id']] or not package['closeWhen']
                or not package['items']
                or set(package['reuse']['sharedChecks']) - check_ids):
            raise ValueError('Concrete package lacks closure/owner: ' + package['id'])
        for item in package['items']:
            if (not item['id'].startswith(package['id'] + '.')
                    or item['kind'] not in {'audit', 'evidence', 'decision'}
                    or item['status'] not in {'PENDING', 'CLOSED_WITH_EVIDENCE'}
                    or not item['target'] or not item['finish']
                    or item['status'] == 'PENDING' and item['id'] not in planned[package['id']]['boundary']):
                raise ValueError('Concrete item lacks an active boundary/finish: ' + str(item))
            if item['status'] == 'CLOSED_WITH_EVIDENCE' and not item.get('evidence'):
                raise ValueError('Closed item needs evidence: ' + item['id'])
            for proof in item.get('evidence', []):
                if hashlib.sha256((root / proof['path']).read_bytes()).hexdigest() != proof['sha256']:
                    raise ValueError('Concrete closure evidence changed: ' + item['id'])
            items.append(item)
    # Keep original numbered items when a fully completed package leaves the
    # open schedule. Its frozen concrete record must also be in the proof ledger.
    closed = {p['id']: p for p in read_json(SKILL / 'references/verified-progress.json').get('closedPackages', [])}
    archived = work.get('archivedPackages', [])
    if len({p['id'] for p in archived}) != len(archived):
        raise ValueError('Duplicate archived concrete package')
    for package in archived:
        if package['id'] in planned or closed.get(package['id'], {}).get('concretePackage') != package:
            raise ValueError('Archived concrete package lacks matching closed proof: ' + package['id'])
        for item in package['items']:
            if (not item['id'].startswith(package['id'] + '.') or item['status'] != 'CLOSED_WITH_EVIDENCE'
                    or item['kind'] not in {'audit', 'evidence', 'decision'} or not item.get('evidence')
                    or not item.get('target') or not item.get('finish')):
                raise ValueError('Archived concrete item incomplete: ' + item['id'])
            for proof in item['evidence']:
                if hashlib.sha256((root / proof['path']).read_bytes()).hexdigest() != proof['sha256']:
                    raise ValueError('Archived concrete closure evidence changed: ' + item['id'])
            items.append(item)
    if len({i['id'] for i in items}) != len(items):
        raise ValueError('Duplicate concrete item ID')
    frontier = work['auditFrontier']
    if hashlib.sha256((root / frontier['path']).read_bytes()).hexdigest() != frontier['sha256']:
        raise ValueError('Frozen audit frontier changed')
    for field in ('nextAction', 'startEffectTestWhen', 'changes', 'progress'):
        if not work['entryPolicy'].get(field):
            raise ValueError('Missing concrete work entry policy: ' + field)
    exclusion_ref = work.get('schedulingPolicy', {}).get('outsideGoalReference')
    if exclusion_ref:
        exclusion_path = root / exclusion_ref['path']
        if hashlib.sha256(exclusion_path.read_bytes()).hexdigest() != exclusion_ref['sha256']:
            raise ValueError('Current goal exclusion registry changed')
        exclusions = read_json(exclusion_path)
        groups = exclusions.get('excludedGroups', [])
        group_ids = [x['id'] for x in groups]
        if (exclusions.get('status') != 'EXCLUDED_FROM_CURRENT_GOAL_BY_USER_NOT_VERIFIED'
                or len(group_ids) != len(set(group_ids))
                or not {'commander', 'subgame'}.issubset(group_ids)
                or any(not x.get('authority') or not x.get('resumeWhen')
                       for x in groups if x['id'] not in {'commander', 'subgame'})):
            raise ValueError('Current goal exclusions lack explicit unsupported scope')
    return work, dict(collections.Counter(i['kind'] for i in items)), dict(
        collections.Counter(i['kind'] for i in items if i['status'] == 'PENDING'))


def verified_progress(root, manifest, plan):
    """Count archived first-validation targets, never promote runtime PASS."""
    ledger = read_json(SKILL / 'references/verified-progress.json')
    for field in ('baselineEngineEvidence', 'baselineUnityEvidence'):
        proof = ledger[field]
        if hashlib.sha256((root / proof['path']).read_bytes()).hexdigest() != proof['sha256']:
            raise ValueError('Progress baseline changed: ' + proof['path'])
    old_jobs = {j['id'] for j in read_json(root / ledger['baselineEngine'])['jobs']}
    old_ui = {j['id'] for j in read_json(root / ledger['baselineUnity'])['jobs']}
    ui_job_map = {j['id']: j for j in read_json(root / 'Tools/ForgeBridge/PresentationFamilies.json')['uiJobs']}
    ui_jobs = set(ui_job_map)
    case_map = {(j['id'], c['arg']): c for j in manifest['jobs'] for c in j['cases'] if 'arg' in c}
    seen = set()
    for item in ledger['nativeTargets']:
        key = (item['job'], item['case'])
        if key in seen or key not in case_map or item['job'] in old_jobs:
            raise ValueError('Duplicate or unknown completed target: ' + str(key))
        if digest(case_map[key]['assertions']) != item['assertionHash']:
            raise ValueError('Completed target assertions need review: ' + str(key))
        seen.add(key)
    ui_ids = [item['job'] for item in ledger['uiTargets']]
    if len(set(ui_ids)) != len(ui_ids) or set(ui_ids) - ui_jobs or set(ui_ids) & old_ui:
        raise ValueError('Duplicate, unknown or baseline UI progress target')
    for item in ledger['nativeTargets'] + ledger['uiTargets']:
        evidence = root / item['evidence']['path']
        if hashlib.sha256(evidence.read_bytes()).hexdigest() != item['evidence']['sha256']:
            raise ValueError('Progress evidence changed: ' + str(evidence))
        if evidence.suffix == '.json':
            proof = read_json(evidence)
            if proof['case'] != item['case'] or not proof['result'].startswith('PASS'):
                raise ValueError('Invalid native case proof: ' + str(evidence))
        elif not evidence.read_text(encoding='utf-8-sig').startswith('PASS'):
            raise ValueError('Incomplete progress evidence: ' + str(evidence))
    packages = {p['id']: p for p in plan}
    for item in ledger['partialPackages']:
        if item['id'] not in packages or packages[item['id']]['boundary'] != item['remainingBoundary']:
            raise ValueError('Partial-package remaining boundary needs review: ' + item['id'])
        packages[item['id']]['status'] = 'PARTIAL_ARCHIVED_EVIDENCE'
    if len({p['id'] for p in ledger['partialPackages']}) != len(ledger['partialPackages']):
        raise ValueError('Duplicate partial package')
    closed = ledger.get('closedPackages', [])
    if len({p['id'] for p in closed}) != len(closed):
        raise ValueError('Duplicate closed package')
    for item in closed:
        original = item['originalPlanRow']
        if item['id'] in packages or original['id'] != item['id'] or not original['boundary']:
            raise ValueError('Closed package still open or missing original row: ' + item['id'])
        native = [tuple(key.split('/', 1)) for key in item['nativeTargets']]
        # Reused consumers can close a boundary with an audited increment;
        # requiring a new UI first here would count the same consumer twice.
        increments = item.get('uiIncrements', [])
        native_increments = item.get('nativeIncrements', [])
        if (not native and not native_increments or set(native) - seen
                or not item['uiTargets'] and not increments
                or set(item['uiTargets']) - set(ui_ids)):
            raise ValueError('Closed package lacks registered native/UI proof: ' + item['id'])
        increment_jobs = [proof['job'] for proof in increments]
        if len(set(increment_jobs)) != len(increment_jobs):
            raise ValueError('Duplicate closed UI increment: ' + item['id'])
        audited = {(e['path'], e['sha256']) for batch in ledger.get('regressionBatches', [])
                   for e in batch['evidence']}
        # A baseline consumer may need new outcome assertions without becoming
        # another first-validation case. Require its real, audited native result.
        native_keys = [(p['job'], p['evidence']['path']) for p in native_increments]
        if len(set(native_keys)) != len(native_keys):
            raise ValueError('Duplicate closed native increment: ' + item['id'])
        for increment in native_increments:
            evidence = increment['evidence']
            if (increment['job'] not in {j['id'] for j in manifest['jobs']}
                    or (evidence['path'], evidence['sha256']) not in audited):
                raise ValueError('Closed native increment lacks registered audit: ' + item['id'])
            path = root / evidence['path']
            if hashlib.sha256(path.read_bytes()).hexdigest() != evidence['sha256']:
                raise ValueError('Closed native increment changed: ' + item['id'])
            result = read_json(path)
            if (not result.get('status', '').startswith('PASS')
                    or result.get('nativeFlows', 0) <= 0
                    or result.get('actualPaidOperations', 0) <= 0):
                raise ValueError('Closed native increment lacks actual outcome: ' + item['id'])
        for proof in increments:
            evidence = proof['evidence']
            job = ui_job_map.get(proof['job'])
            if (job is None or job['result'] != evidence['path']
                    or (evidence['path'], evidence['sha256']) not in audited):
                raise ValueError('Closed UI increment lacks registered audit: ' + item['id'])
            path = root / evidence['path']
            if (hashlib.sha256(path.read_bytes()).hexdigest() != evidence['sha256']
                    or not path.read_text(encoding='utf-8-sig').startswith('PASS')):
                raise ValueError('Closed UI increment incomplete or changed: ' + item['id'])
        audit = item['audit']
        if hashlib.sha256((root / audit['path']).read_bytes()).hexdigest() != audit['sha256']:
            raise ValueError('Closed package audit changed: ' + item['id'])
    return ledger


def scheduling_summary(work):
    """Keep user-deferred scope pending, while showing its scheduling separately."""
    pending = {i['id']: i for p in work['packages'] for i in p['items']
               if i['status'] == 'PENDING'}
    policy = work.get('schedulingPolicy', {})
    deferred = policy.get('deferredItems', [])
    ids = [i['id'] for i in deferred]
    if len(ids) != len(set(ids)) or set(ids) - pending.keys():
        raise ValueError('Deferred items must be unique existing pending items')
    if any(not i.get('reason') or not i.get('resumeWhen') for i in deferred):
        raise ValueError('Deferred scope needs reason and resumption condition')

    def input_ids(item):
        return {x['id'] for x in item.get('classifiedInputs', [])} | {
            g['id'] for x in item.get('classifiedInputs', []) for g in x.get('groups', [])}

    all_items = {i['id']: i for p in work['packages'] + work.get('archivedPackages', [])
                 for i in p['items']}
    boundaries = policy.get('deferredBoundaries', [])
    for boundary in boundaries:
        owner = all_items.get(boundary['owner'])
        if (owner is None or boundary['inputId'] not in input_ids(owner)
                or not boundary.get('boundary') or not boundary.get('retainActive')):
            raise ValueError('Deferred branch must retain an existing input and active remainder')
        if owner['status'] == 'CLOSED_WITH_EVIDENCE':
            # Completing ordinary scope must not reopen it merely to retain a
            # user-deferred branch, nor turn that branch into effect proof.
            closure = boundary.get('currentScopeClosure')
            if (not boundary.get('excludedFromCurrentGoal')
                    or boundary.get('status') != 'DEFERRED_UNVERIFIED'
                    or not boundary.get('authority') or not boundary.get('resumeWhen')
                    or not closure or closure not in owner.get('evidence', [])):
                raise ValueError('Archived deferred branch needs explicit unverified scope and closure evidence')
    excluded_inputs = policy.get('excludedInputs', [])
    excluded_keys = [(x['owner'], x['inputId']) for x in excluded_inputs]
    if len(excluded_keys) != len(set(excluded_keys)):
        raise ValueError('Duplicate input excluded from current goal')
    for exclusion in excluded_inputs:
        # Completing the retained BO3/Karn remainder must not delete the original
        # subgame exclusion or convert it to PASS. Its identity survives archival.
        owner = all_items.get(exclusion['owner'])
        if (owner is None or exclusion['inputId'] not in input_ids(owner)
                or not exclusion.get('reason') or not exclusion.get('retainActive')):
            raise ValueError('Excluded input needs retained original identity and reason')
        if owner['status'] == 'CLOSED_WITH_EVIDENCE':
            matching = [x for reference in owner.get('classifiedInputs', [])
                        for x in [reference] + reference.get('groups', [])
                        if x['id'] == exclusion['inputId']]
            if not matching or any(x.get('status') != 'EXCLUDED_FROM_CURRENT_GOAL_BY_USER'
                                   for x in matching):
                raise ValueError('Archived exclusion must remain explicitly unverified')
    active = [i for key, i in pending.items() if key not in ids]
    # Preserve the original input/proof inventory after ordinary scope closes.
    # Archived user-deferred inputs must remain explicitly unverified.
    a01 = all_items.get('A01.01', {})
    inputs = a01.get('classifiedInputs', [])
    completed = {key for proof in a01.get('evidenceProgress', [])
                 for key in proof.get('completedInputIds', [])}
    leaves = [leaf for reference in inputs
              for leaf in (reference.get('groups') or [reference])]
    leaf_ids = {leaf['id'] for leaf in leaves}
    if completed - leaf_ids:
        raise ValueError('Completed A01 inputs must exist in the expanded original inventory')
    deferred_a01 = {x['inputId'] for x in excluded_inputs if x['owner'] == 'A01.01'}
    if deferred_a01 & completed:
        raise ValueError('Deferred A01 inputs must not be counted as completed')
    return {
        'currentPendingConcreteWorkItems': len(active),
        'currentPendingConcreteWorkByKind': dict(collections.Counter(i['kind'] for i in active)),
        'userDeferredConcreteWorkItems': len(ids),
        'userDeferredConcreteWorkIds': ids,
        'userDeferredConditionalBoundaries': len(boundaries),
        'userExcludedInputs': len(excluded_inputs),
        'userExcludedInputIds': [x['inputId'] for x in excluded_inputs],
        'a01TopLevelReferenceRecords': len(inputs),
        'a01DistinctTopLevelReferenceIds': len({i['id'] for i in inputs}),
        'a01CompletedBoundedInputIds': sorted(completed),
        'a01ExpandedReferenceRecords': len(leaves),
        'a01DistinctLeafInputs': len(leaf_ids),
        'a01UnclosedLeafInputs': len(leaf_ids - completed),
        'a01DeferredLeafInputIds': sorted(deferred_a01),
        'a01DeferredLeafInputs': len(deferred_a01),
        'a01CurrentUnclosedLeafInputIds': sorted(leaf_ids - completed - deferred_a01),
        'a01CurrentUnclosedLeafInputs': len(leaf_ids - completed - deferred_a01),
    }


def summarize(root):
    report = root / REPORT_PATH
    coverage = read_json(report / 'coverage.json')
    families = read_json(report / 'families.json')
    contract = read_json(report / 'interaction-contract.json')
    config = read_json(root / 'Tools/ForgeBridge/PresentationFamilies.json')
    manifest = read_json(root / 'Tools/ForgeBridge/MechanicCoverage.json')
    plan = load_plan()
    execution = validate_execution_plan(read_json(SKILL / 'references/execution-plan.json'), plan)
    progress = verified_progress(root, manifest, plan)
    concrete, concrete_counts, pending_counts = load_concrete_work(root, plan, execution)
    expected = read_json(SKILL / 'references/plan-provenance.json')['sourceSnapshot']
    actual = source_snapshot(config, coverage, families, contract)
    changed = [k for k in expected.keys() | actual.keys() if expected.get(k) != actual.get(k)]
    family_ids = {f['id'] for f in config['families']}
    used_ids = {f for p in plan for f in p['families']}
    # A closed package retains its original family assignment. Its native/UI
    # proofs were checked by verified_progress above; removal from scheduling
    # must not make those families appear lost or create duplicate open work.
    for package in progress.get('closedPackages', []):
        archived_families = package['originalPlanRow']['families']
        # Older archived rows kept the TSV representation; newer rows use lists.
        if isinstance(archived_families, str):
            archived_families = archived_families.split(',')
        used_ids.update(f for f in archived_families if f)
    if used_ids - family_ids:
        raise ValueError('Unknown family IDs: ' + ','.join(sorted(used_ids - family_ids)))
    if family_ids - used_ids:
        changed.append('unassignedFamilies:' + ','.join(sorted(family_ids - used_ids)))
    entries = coverage['entries']
    states = dict(collections.Counter(e['status'] for e in entries))
    if states != coverage['counts']:
        raise ValueError('Coverage counts do not match raw entries')
    unregistered = [e for e in entries if e['status'] == '未登记效果测试']
    structural = sum(e['category'] in STRUCTURAL for e in unregistered)
    modes = dict(collections.Counter(p['mode'] for p in plan))
    cases = [(j['id'], c) for j in manifest['jobs'] for c in j['cases']]
    adapter = sum(c['assertions'].lower().startswith('adapter') for _, c in cases)
    # Counts are registrations. Current statuses below are read from existing reports.
    summary = {
        'generatedOn': datetime.date.today().isoformat(),
        'planningStatus': 'REVIEW_SOURCE_CHANGES' if changed else 'SOURCE_SNAPSHOT_MATCHES',
        'sourceChanges': sorted(changed),
        'coverageRun': coverage['currentRun'], 'provenance': coverage['provenance'],
        'rawEntries': len(entries), 'rawStatuses': states,
        'unregisteredEntries': len(unregistered), 'structuralUnregistered': structural,
        'executionOrMechanicUnregistered': len(unregistered) - structural,
        'knownPlanningPackages': len(plan), 'planningModes': modes,
        'executionBatches': len(execution['batches']),
        'sharedCheckOwners': len(execution['sharedChecks']),
        'concreteWorkItems': sum(concrete_counts.values()),
        'concreteWorkByKind': concrete_counts,
        'pendingConcreteWorkItems': sum(pending_counts.values()),
        'pendingConcreteWorkByKind': pending_counts,
        'workPhase': concrete['phase'],
        'additionalNativeFirstValidationTargets': len(progress['nativeTargets']),
        'additionalUiFirstValidationSuites': len(progress['uiTargets']),
        'partiallyReconciledPackages': len(progress['partialPackages']),
        'closedPackagesWithArchivedProof': len(progress.get('closedPackages', [])),
        'families': len(families['families']), 'engineJobsRegistered': len(manifest['jobs']),
        'rulesOrOpeningCasesRegistered': len(cases) - adapter,
        'adapterChecksRegistered': adapter,
        'unityJobsRegistered': len(families['uiJobs']),
        'unityStatuses': dict(collections.Counter(j['status'] for j in families['uiJobs'])),
        'unmappedAPIs': len(families['unmappedAPIs']),
        'unmappedDeclarations': len(coverage['unmappedDeclarations']),
        'unreviewedTests': len(coverage['testsNotReviewedForThisManifest']),
        'reviewedScriptsOutsideEffectManifest': len(coverage.get('reviewedScriptsOutsideEffectManifest', [])),
    }
    summary.update(scheduling_summary(concrete))
    workload_ref = concrete.get('workloadReview')
    workload = None
    if workload_ref:
        workload_path = root / workload_ref['path']
        if hashlib.sha256(workload_path.read_bytes()).hexdigest() != workload_ref['sha256']:
            raise ValueError('Workload review changed without registration')
        workload = read_json(workload_path)
        if workload.get('status') != 'WORKLOAD_REVIEW_NOT_EFFECT_PASS':
            raise ValueError('Workload review must not grant effect PASS')
        summary['supplementalRunsConfirmed'] = len(workload['confirmedSupplementalRuns'])
        summary['sharedContractReviewsPending'] = sum(
            r['status'] == 'COMPARE_ARCHIVED_PROOF' for r in workload['contractReviews'])
    payload = {
        'summary': summary, 'sourceSnapshot': actual, 'packages': plan,
        'executionPlan': execution,
        'concreteWork': concrete,
        'archivedProgress': progress,
        'workloadReview': workload,
        'categoryStatuses': {k: dict(v) for k, v in sorted(
            ((category, collections.Counter(e['status'] for e in entries if e['category'] == category))
             for category in {e['category'] for e in entries}))},
        'sourceBoundaries': {f['id']: f['gaps'] for f in config['families']},
        'unmappedAPIs': families['unmappedAPIs'],
        'unmappedDeclarations': coverage['unmappedDeclarations'],
        'unreviewedTests': coverage['testsNotReviewedForThisManifest'],
        'reviewedScriptsOutsideEffectManifest': coverage.get('reviewedScriptsOutsideEffectManifest', []),
        'interfaceGaps': contract['knownGaps'],
        'limitations': [
            'Package counts are a manually reviewed bounded work plan, not all mechanisms or a test count.',
            'Execution batches consolidate scheduling; they neither close boundaries nor measure saved tests or turns.',
            'Source paragraph retention and hashes do not prove each semantic branch has a planned test.',
            'Concrete audit/evidence/decision tasks are not independent tests; source audits must classify the frozen frontier before new effect tests.',
            'A newly confirmed branch changes the plan only with source evidence, unique ownership and an explicit before/after task delta.',
            'Family/API mapping does not confer any rule, UI or language PASS.',
            'Global fingerprint invalidation and selected-run evidence merging are unchanged.',
            'This script does not execute rules/UI tests or certify current source against recorded proof.',
        ],
    }
    return payload, entries


def markdown(payload):
    s = payload['summary']
    modes = s['planningModes']
    lines = [
        '# 机制验证剩余工作与增量回归计划', '',
        f"日期：{s['generatedOn']}。此报告是现有清单的去重规划；本次盘点没有运行规则或 Unity 效果测试。", '',
        '目标范围、验收与用户继续授权/工具状态见 [goal 修订记录](MechanicValidationGoal.zh-CN.md)。接续任务时核对其更新日期；本报告不会修改系统 goal 状态。', '',
        '## 当前逐项工作清单', '',
        f"当前 goal 有 **{s['currentPendingConcreteWorkItems']} 项待办**："
        f"{s['currentPendingConcreteWorkByKind'].get('evidence', 0)} 项待补具体证据、"
        f"{s['currentPendingConcreteWorkByKind'].get('audit', 0)} 项待源码/旧证据审查、"
        f"{s['currentPendingConcreteWorkByKind'].get('decision', 0)} 项待展示/范围收尾。"
        f"另有 {s['userDeferredConcreteWorkItems']} 整项由用户后置，未验证，不计当前待办。"
        f"原 {s['concreteWorkItems']} 项中 {s['concreteWorkItems'] - s['pendingConcreteWorkItems']} 项已按各自范围证据核销；"
        f"{s['knownPlanningPackages']} 个开放包用于归属追踪。", '',
        '**这些是整理后的工作事项，不是测试次数或剩余轮数。** '
        '完整事项编号、待补内容、已有证据和逐项结束条件见 '
        '[当前工作清单](MechanicValidationWorklist.zh-CN.md)，机器源为 skill 的 `references/remaining-work.json`。', '',
        '已完成的冻结来源归类沿用原证明，当前只对齐受影响旧证据并列出缺失结果与UI合同差异。'
        '审查项不能未经分类直接转成新对局。新增可达分支必须记录源码或请求依据、旧证明不足的原因、唯一归属和事项净变化；'
        '数字、标签变体及任意组合不自动扩大目标。', '',
        '## 工作包与排期归属', '',
        f"当前整理为 **{s['knownPlanningPackages']} 个开放工作包**："
        f"{modes.get('reuse', 0)} 个按共用流程补边界，"
        f"{modes.get('separate', 0)} 个需要独立复核的特殊表现/模式，"
        f"{modes.get('audit', 0)} 个清单及工具审查包。", '',
        '审查后的真实缺口按上述变更规则更新具体事项；特殊包先检查复用可能性，'
        '尚未认定都需要新 UI 或用户设计。', '',
        '同一取消恢复、操作玩家、网络时序、隐藏信息和文本边界在多个流程出现时合并排期；'
        '不同项目接入分支及本项目补丁影响保留独立结算断言；未修改Forge内部组合不自动追加验证。'
        '当前活动边界与历史完成范围分开保存，关联来源不代表整牌已验证。', '',
        f"计划来源状态：`{s['planningStatus']}`。",
    ]
    policy = payload['concreteWork'].get('schedulingPolicy', {})
    workload = payload.get('workloadReview')
    if workload:
        closed_scope = (s['currentPendingConcreteWorkItems'] == 0
                        and s['sharedContractReviewsPending'] == 0 and workload.get('closureAudit'))
        lines += ['', '## 实际剩余工作量', '',
                  f"已明确必须新增的运行：**{s['supplementalRunsConfirmed']} 条**；"
                  f"尚待集中核对的共用合同：**{s['sharedContractReviewsPending']} 类**。"
                  + ('现有普通1v1接入范围已按旧真实合同证据收尾；不宣称全部整牌组合通过。'
                   if closed_scope else '零条已确认补测不表示零未知项或 goal 已完成。'), '',
                  '逐卡关联数量仅作证据追溯，不计待办、不用于估时，也不要求归零才能完成。'
                  '先集中比较旧证明；只有明确未覆盖的请求、权限、字段或生命周期分支进入补测表。', '',
                  '| 共用合同核对 | 状态 | 差异或取消依据 |', '|---|---|---|']
        for review in workload['contractReviews']:
            state = {'SHARED_CONTRACT_REUSED': '已复用共同合同',
                     'NO_NEW_INTEGRATION_DELTA': '取消新增验证，复用已有接入证据'}.get(review['status'], '待比较旧结果')
            lines.append('| ' + review['title'] + ' | ' + state + ' | ' + review['difference'].replace('。：', '：') + ' |')
        lines += ['', '旧实际结果及尚未逐卡证明的范围完整保留；本次修正规划，没有新增效果 PASS。']
    if policy:
        lines += ['', '## 当前优先范围与后续记录', '',
                  f"当前排期 **{s['currentPendingConcreteWorkItems']} 项**；"
                  f"排除在当前 goal 外的整项 **{s['userDeferredConcreteWorkItems']} 项**，编号："
                  + '、'.join(s['userDeferredConcreteWorkIds']) + '。'
                  f"另有 {s['userDeferredConditionalBoundaries']} 个模式专属分支及 "
                  f"{s['userExcludedInputs']} 个完整输入排除在当前 goal 外。"
                  '原始记录保留；排除不等于关闭或验证通过。', '',
                  policy['selectionRule'], '', policy['executionRule'], '',
                  f"A01有 {s['a01TopLevelReferenceRecords']} 条顶层引用记录、"
                  f"{s['a01DistinctTopLevelReferenceIds']} 个不同顶层ID；"
                  f"展开为 {s['a01ExpandedReferenceRecords']} 条引用，去重后 {s['a01DistinctLeafInputs']} 个具体输入；"
                  f"已核销 {len(s['a01CompletedBoundedInputIds'])} 个，当前尚需完成 **{s['a01CurrentUnclosedLeafInputs']} 个**，"
                  f"后置且未验证 {s['a01DeferredLeafInputs']} 个；原始未核销合计 {s['a01UnclosedLeafInputs']} 个。"
                  '这是输入账数，包含补证据和支持门槛，不是净新增测试数或剩余轮数。', '',
                  ('以下为历史执行块，当前没有待执行块。' if not s['currentPendingConcreteWorkItems']
                   else '下表保留原执行块，状态按各块完成证明读取；不把已完成块重新排期。'), '',
                  '| 执行块 | 内容 | 原输入／事项 |', '|---|---|---|']
        for block in payload['executionPlan'].get('recommendedBlocks', []):
            identities = block.get('inputIds', block.get('owners', [block.get('owner', '')]))
            lines.append('| ' + ' | '.join([block['id'], block['title'], '、'.join(identities)]) + ' |')
        lines += ['', '推荐块先对齐旧证据；上述记录数不代表需要新运行的数量。']
        if policy.get('outsideGoalReference'):
            lines += ['', '当前排除范围及卡牌提示见 '
                      '[特殊模式与子游戏后续记录](MechanicValidationExcluded.zh-CN.md)。'
                      '游乐设施与装置仍按普通1v1接入；后置模式的入口与专属效果不作为本次完成门槛。']
    if s['sourceChanges']:
        lines += ['', '来源已变化，请复核受影响工作包：' + '、'.join(s['sourceChanges']) + '。']
    lines += ['', '## 合并后的执行批次', '',
              f"{s['knownPlanningPackages']} 条开放边界统一排入 **{s['executionBatches']} 个执行批次**，"
              f"另指定 **{s['sharedCheckOwners']} 类共用检查**的负责批次。每条边界恰有一个排期归属；"
              '批次数不是剩余轮数，合并不关闭任何未验证边界。', '',
              '| 批次 | 内容 | 对应开放包 | 执行重点 |', '|---|---|---|---|']
    for batch in payload['executionPlan']['batches']:
        lines.append('| ' + ' | '.join([batch['id'], batch['title'], '、'.join(batch['packages']), batch['focus']]) + ' |')
    lines += ['', '共用检查按相同消费者/合同引用证据；不同支付资源、控制权、隐藏信息和生命周期继续单独取证。', '',
              '| 共用检查 | 负责批次 | 可引用已有证据的条件 | 需要新增证明的条件 |', '|---|---|---|---|']
    for check in payload['executionPlan']['sharedChecks']:
        lines.append('| ' + ' | '.join([check['title'], check['ownerBatch'], check['reuseWhen'], check['newProofWhen']]) + ' |')
    lines += ['', '## 已完成部分的工作量核销', '',
              f"相较暂停时的完整基线，已存档 **{s['additionalNativeFirstValidationTargets']} 个新增实际规则场景**"
              f"与 **{s['additionalUiFirstValidationSuites']} 套新增 Unity 定向检查**。这些列出的断言不再属于未做首次验证；"
              '后续相关改动仍可能需要回归。规则场景与 UI 套不能相加当成机制数量。', '',
              f"本次在 **{s['partiallyReconciledPackages']} 个开放包**中核销相应边界，保留其余待办；"
              '大包仍开放不表示里面的工作全部没做，包数无法表示包内工作量下降。', '',
              f"另外有 **{s['closedPackagesWithArchivedProof']} 个有界工作包**已保存完整原始行及原生/UI证据后关闭；"
              '从开放队列移除不会删除其证据或扩大为所有卡牌组合通过。', '',
              '原始边界、已完成范围、原生 case ID、证据 SHA-256 和剩余边界保存在 skill 的 '
              '`references/verified-progress.json`，同时输出到 remaining-plan.json。'
              '这是首次验证进度账，不是当前版本依赖账本；不改写需重测或合并全量 PASS。', '',
              '| 包 | 已核销的范围 | 仍需完成 |', '|---|---|---|']
    for item in payload['archivedProgress']['partialPackages']:
        lines.append('| ' + ' | '.join([item['id'], item['completedScope'], item['remainingBoundary']]).replace('\n', ' ') + ' |')
    if payload['archivedProgress'].get('closedPackages'):
        lines += ['', '已关闭的原始有界包：']
        for item in payload['archivedProgress']['closedPackages']:
            lines.append(f"- {item['id']} {item['originalPlanRow']['title']}；审计 `{item['audit']['path']}`。")
    lines += ['', '## 结构清单如何拆分', '',
              f"{s['unregisteredEntries']} 个未登记条目拆为 "
              f"**{s['executionOrMechanicUnregistered']} 个机制/执行维度条目**和 "
              f"**{s['structuralUnregistered']} 个参数、标签、脚本模式及期限条目**。后者是查遗漏的索引，"
              '不能直接删掉或标成通过。分类互相重叠，仍不是独立机制数。', '',
              '| 分类 | 未登记 | 有列出场景证据 | 其他状态 |', '|---|---:|---:|---:|']
    for category, states in payload['categoryStatuses'].items():
        unregistered = states.get('未登记效果测试', 0)
        verified = states.get('已验证列出的场景', 0)
        lines.append(f'| {category} | {unregistered} | {verified} | {sum(states.values())-unregistered-verified} |')
    lines += ['', f"另外保留 **{s['unmappedAPIs']} 个未归族 API、{s['unmappedDeclarations']} 种未归类声明、"
              f"{s['unreviewedTests']} 个旧测试脚本**。它们是审查输入，与结构条目/工作包重叠，不能相加。", '',
              f"另外 **{s['reviewedScriptsOutsideEffectManifest']} 个未登记脚本**已按源码哈希分类为工具、投影适配器、输入合同或历史回归；"
              '完整范围/剩余保留在 JSON，源码变化重新进入复核队列。分类不增加效果场景或 PASS。', '',
              '## 已有验证，按原批次保留', '',
              f"登记：{s['engineJobsRegistered']} 个引擎任务，"
              f"{s['rulesOrOpeningCasesRegistered']} 个规则/起手场景，"
              f"{s['adapterChecksRegistered']} 个适配器检查；"
              f"{s['unityJobsRegistered']} 个 Unity 套，{s['families']} 类共用流程。登记不是自动通过。", '',
              f"清单对应引擎批次：`{s['coverageRun']}`；"
              '当前清单状态：' + '，'.join(f'{k} {v}' for k, v in s['rawStatuses'].items()) + '。',
              'Unity 清单状态：' + '，'.join(f'{k} {v}' for k, v in s['unityStatuses'].items()) + '。', '',
              '最近完整历史基线详见 [验证方法与证据入口](../.agents/skills/mtgb-mechanic-validation/references/verification-method.md)。'
              '691 张截图只属于该批次，不按多次重跑相加，也不算 691 个机制。', '',
              '## 开放工作包索引', '',
              '| ID | 类型 | 优先级 | 工作包 | 当前事项 |', '|---|---|---|---|---|']
    names = {'reuse': '共用流程', 'separate': '独立复核', 'audit': '审查/工具'}
    for p in payload['packages']:
        lines.append('| ' + ' | '.join([p['id'], names[p['mode']], p['priority'], p['title'],
                                      p['boundary'].replace('|', '\\|')]) + ' |')
    lines += ['', '## 避免重复的执行顺序', '',
              '1. 按推荐块合并多个普通1v1输入，沿用完成的来源归类；仅缺失断言新运行，A06随同批消费者集中登记。',
              '2. E02双人真实传输时序与恢复共用一次环境准备；E12已有1v1入口优先，指挥官按明确后置政策保留。',
              '3. 每轮跑新增/变更任务及实际相关消费者；局部 UI 修改沿用未变化原生输入，不重跑无关引擎。',
              '4. 全量留给阶段收尾、核心路由变化、运行包升级或影响无法界定；当前工具总指纹失效状态保持真实。', '',
              '清单归类按消费者/原生分支做一次；每个结构条目保留分类或分支引用，参数/标签数量不转成逐条游戏测试。'
              'UI按真实请求合同选择代表输入；相同控件、提交路径、席位/可见性、身份类型、数量/顺序、确认/取消与生命周期可引用已有点击证明，核对新输入差异。'
              '不为每个原生case重复UI或三语言检查；新合同、文案/资源/布局变化补受影响检查。', '',
              '本报告只刷新盘点，**自动逐任务依赖计算及增量证据合并尚未实现**。'
              '计划要求先判断影响范围，未把现有工具改成自动复用 PASS。', '',
              '完成门槛：选定范围内每个可达交互有准确可操作 UI，实际结算与适用恢复边界有证据，'
              '特殊表现/明确不支持的模式逐项登记；审查队列有结论。有限测试仍不证明全部卡牌组合。', '',
              '机器报告：`Tools/Validation/Reports/MechanicCoverage/remaining-plan.json`；'
              '逐条原始状态与排期方向：`remaining-inventory.csv`。', '']
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=DEFAULT_ROOT)
    parser.add_argument('--output-dir', type=Path, help='Write all generated artifacts here for isolated checks')
    parser.add_argument('--check', action='store_true', help='Validate and print summary without writing')
    args = parser.parse_args()
    payload, entries = summarize(args.root.resolve())
    if not args.check:
        destination = args.output_dir or args.root / REPORT_PATH
        destination.mkdir(parents=True, exist_ok=True)
        (destination / 'remaining-plan.json').write_text(
            json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        doc = destination / 'MechanicValidationRemaining.zh-CN.md' if args.output_dir else args.root / 'Docs/MechanicValidationRemaining.zh-CN.md'
        doc.write_text(markdown(payload), encoding='utf-8')
        with (destination / 'remaining-inventory.csv').open('w', encoding='utf-8-sig', newline='') as stream:
            writer = csv.writer(stream)
            writer.writerow(['原始条目', '分类', '原始状态', '审查方向', '脚本引用卡数', '声明的场景数量'])
            for e in entries:
                direction = '结构归类' if e['category'] in STRUCTURAL else '机制/执行分支复核'
                writer.writerow([e['name'], e['category'], e['status'], direction, e['cardCount'], len(e['scenarios'])])
    print(json.dumps(payload['summary'], ensure_ascii=False, indent=2))
    return 2 if payload['summary']['sourceChanges'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
