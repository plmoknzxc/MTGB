"""Shared presentation families: references, bounded assertions and explicit gaps."""
import hashlib
import json
from pathlib import Path
import re


def case_id(case):
    return case.get('id', case.get('arg', 'default'))


def input_fingerprint(root, folders):
    digest = hashlib.sha256()
    paths = sorted({p for folder in folders for suffix in ('*.json', '*.jsonl')
                    for p in (root / folder).rglob(suffix)}, key=lambda p: p.relative_to(root).as_posix())
    for path in paths:
        digest.update(path.relative_to(root).as_posix().encode('utf8'))
        digest.update(b'\0')
        digest.update(path.read_bytes())
    return digest.hexdigest()


def job_state(name, results, fresh):
    if not fresh:
        return '需重测' if results else '未运行'
    result = results.get(name)
    return result['status'] if result else '本轮未运行'


def build(root, destination, configuration, manifest, provenance, engine, unity):
    jobs = {job['id']: job for job in manifest['jobs']}
    results = {job['id']: job for job in engine.get('jobs', [])}
    engine_fresh = engine.get('provenance') == provenance
    ui_results = {job['id']: job for job in unity.get('jobs', [])}
    ui_jobs = {job['id']: job for job in configuration['uiJobs']}
    cases = {job['id'] + '/' + case_id(case): case for job in manifest['jobs'] for case in job['cases']}
    for definition in ui_jobs.values():
        definition = dict(definition)
        folders = [jobs[name]['reportDir'] for name in definition['engineJobs']]
        record = ui_results.get(definition['id'], {})
        input_root = root / record['inputEvidence'] if record.get('inputEvidence') else root
        ui_current = (unity.get('fingerprint') == provenance['fingerprint'] and
                      unity.get('forgeRevision') == provenance['revision'] and
                      record.get('inputs') == input_fingerprint(input_root, folders))
        definition['status'] = job_state(definition['id'], ui_results, ui_current)
        definition['evidence'] = ui_results.get(definition['id'])
        ui_jobs[definition['id']] = definition
    rows = []
    invalid_scenarios = []
    for family in configuration['families']:
        row = dict(family)
        row['rules'] = []
        for name in family['rules']:
            if name not in cases:
                invalid_scenarios.append(dict(family=family['id'], scenario=name))
                row['rules'].append(dict(id=name, assertions='登记引用不存在，需修正清单或重新生成。', status='INVALID'))
            else:
                row['rules'].append(dict(id=name, assertions=cases[name]['assertions'], status=job_state(name.split('/')[0], results, engine_fresh)))
        row['ui'] = [dict(id=name, status=ui_jobs[name]['status'], assertions=ui_jobs[name]['assertions']) for name in family['ui']]
        rows.append(row)
    # Flag additions automatically. Mapping a request/API is not proof of its effects.
    mapped = {api for family in rows for api in family['apis']}
    raw = json.loads((destination / 'coverage.json').read_text(encoding='utf8'))
    unmapped = [dict(api=item['name'][4:], cards=item['cards'], cardCount=item['cardCount'])
                for item in raw['entries'] if item['name'].startswith('API:') and item['name'][4:] not in mapped]
    bridge = root / 'Tools/ForgeBridge/java/mtgb/bridge'
    requests = set()
    unsupported = []
    for path in sorted(bridge.glob('*.java')):
        for number, line in enumerate(path.read_text(encoding='utf8').splitlines(), 1):
            if 'new Pending(' in line or 'new ForgeBridge.Pending(' in line:
                # Only constructor's kind argument; other strings are labels.
                match = re.search(r'new (?:ForgeBridge\.)?Pending\([^,]+,\s*([^,]+),', line)
                if match:
                    requests.update(re.findall(r'"([A-Za-z]+)"', match[1]))
            if 'throw new UnsupportedOperationException(' in line:
                unsupported.append(dict(path=path.relative_to(root).as_posix(), line=number, source=line.strip()))
    input_root = root / '.tools/forge-proof/upstream/forge-gui/src/main/java/forge/gamemodes/match/input'
    # Include concrete upstream classes, not just strings already referenced in Unity.
    upstream_inputs = []
    input_sources = {}
    for path in sorted(input_root.glob('*.java')):
        text = path.read_text(encoding='utf8')
        if re.search(r'public\s+(?:final\s+)?class\s+Input\w+[^\n]*?(?:extends\s+Input\w+|implements\s+Input\b)', text):
            upstream_inputs.append(path.stem)
            parent = re.search(r'class\s+' + path.stem + r'(?:<[^\n]+?>)?\s+extends\s+(Input\w+)', text)
            input_sources[path.stem] = parent[1] if parent else None
        elif re.search(r'public\s+abstract\s+class\s+Input', text):
            parent = re.search(r'extends\s+(Input\w+)', text)
            input_sources[path.stem] = parent[1] if parent else None
    source = (bridge / 'ForgeBridge.java').read_text(encoding='utf8')
    supported_body = source.split('static boolean supportedInput(Input input) {', 1)[1].split('\n    }', 1)[0]
    supported_types = set(re.findall(r'instanceof\s+(Input\w+)', supported_body))
    exact_types = set(re.findall(r'getSimpleName\(\)\.equals\("(Input\w+)"\)', supported_body))
    input_support = {}
    for name in upstream_inputs:
        current, visited = name, set()
        while current and current not in visited and current not in supported_types:
            visited.add(current); current = input_sources.get(current)
        input_support[name] = current in supported_types or name in exact_types
    request_map = {name: [f['id'] for f in rows if name in f['requests']]
                   for name in sorted(requests | set(upstream_inputs))}
    payload = dict(provenance=provenance, reference=configuration['reference'], additionalReferences=configuration.get('additionalReferences', []), families=rows,
                   invalidScenarios=invalid_scenarios,
                   uiJobs=list(ui_jobs.values()), unmappedAPIs=unmapped,
                   requestFamilies=request_map, bridgeInputSupport=input_support, bridgeUnsupportedBranches=unsupported,
                   limitations=['Family association is not validation of every mapped API or card.',
                                'Upstream concrete input classes include lock/playback infrastructure; an unassigned class needs review, not automatic rejection.',
                                'Unimplemented, unregistered, failed and stale evidence are different states.',
                                'Visual assertions cover the captured resolution and language; no automatic proof of animation equivalence or all languages.'])
    (destination / 'families.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    lines = ['# 共用交互与结算表现验证', '',
             '按共用交互找缺口：一张牌可能经过多个流程；一个流程对应很多机制。关联不等于整类已经验证。', '',
             '[录像时间点与观察范围](../../../../' + configuration['reference'] + ') · [机制结构清单](README.md)', '',
             '规则断言和 Unity 展示断言分别记录，PASS 只限列出的场景和检查。录像观察不是测试结果。', '',
             '| 共用流程 | 录像时间点 | 本轮规则场景 | Unity 检查 |', '|---|---|---|---|']
    if configuration.get('additionalReferences'):
        lines[6:6] = ['补充录像：' + ' · '.join('[地城、资格和土地变化](../../../../' + ref + ')' for ref in configuration['additionalReferences']), '']
    for row in rows:
        passed = sum(case['status'] == 'PASS' for case in row['rules'])
        ui_text = '；'.join(job['id'] + ': ' + job['status'] for job in row['ui']) or '未登记'
        rule_text = str(passed) + '/' + str(len(row['rules'])) + ' PASS' if row['rules'] else '未登记'
        lines.append('| ' + ' | '.join([row['name'], '、'.join(row['reference']) or '未在本次片段确认', rule_text, ui_text]) + ' |')
    lines += ['', '## 登记完整性', '', '无失效场景引用。' if not invalid_scenarios else '失效场景引用：' + '、'.join(item['family'] + '/' + item['scenario'] for item in invalid_scenarios), '', '## 已知接入限制', '',
              '- 控制另一位玩家：已适配独立操作座位、私有手牌与控制归还；双方座位整回合、仅战斗控制的本轮结算与界面状态见上表。嵌套／相互控制、控制者离场及网络重连仍须专项回归。',
              '- 召集／拼造／截水共用的输入已补适配；曾在真实施放中抛出未适配异常。当前场景与界面检查状态见上表，不能把修复本身当作所有费用边界通过。',
              '- `order(... sideboardingMode=true ...)` 未适配；这项是局间备牌排序，不能误报成所有游戏内排序都未支持。',
              '- 原生 Forge 联机的 `applyDelta/applyYieldUpdate` 未适配；项目自有快照/操作协议须另行验证，不能据此判定项目联机完全不支持。', '',
              '其他异常分支的路径、行号和原文也保存在 families.json，新增分支会进入报告。', '',
              '## 尚缺的边界', '']
    for row in rows:
        lines += ['### ' + row['name'], ''] + ['- ' + gap for gap in row['gaps']] + ['']
    lines += ['## 尚未归入共用流程的引擎操作', '',
              '新增 API 会自动进入此表，避免只从已知关键词列表找缺口。下列引用数量不是机制总数。', '',
              '| 操作 | 直接引用脚本数 | 代表牌 |', '|---|---:|---|']
    lines += ['| ' + item['api'] + ' | ' + str(item['cardCount']) + ' | ' + '、'.join(item['cards'][:2]) + ' |' for item in unmapped]
    lines += ['', '每项规则/界面断言、证据状态、请求族与未适配分支见 [families.json](families.json)。', '']
    (destination / 'families.md').write_text('\n'.join(lines), encoding='utf8')
