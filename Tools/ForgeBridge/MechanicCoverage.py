"""Inventory and bounded real-engine regression. Display help is never effect proof."""
import argparse
import collections
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone

# Also support importlib-based validation without changing the caller's sys.path.
import importlib.util
_declaration_spec = importlib.util.spec_from_file_location(
    'declaration_inventory', Path(__file__).with_name('DeclarationInventory.py'))
declaration_inventory = importlib.util.module_from_spec(_declaration_spec)
_declaration_spec.loader.exec_module(declaration_inventory)

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
REPORT = ROOT / 'Tools/Validation/Reports/MechanicCoverage'
LOCAL = ROOT / '.tools/mechanic-coverage'


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def key(name):
    return re.sub(r'[^a-z0-9]', '', name.casefold())


def category(name):
    return {'API': '引擎操作', 'Trigger': '触发事件', 'Replacement': '替代事件',
            'Static': '持续异能类型', 'Cost': '费用类型', 'Duration': '持续期限',
            'Parameter': '脚本参数', 'ScriptMode': '其他脚本模式',
            'AbilityLabel': '异能词／待核对文本标签'}.get(name.split(':', 1)[0], '机制／效果条目')


def scan_structure(text, card, add):
    """Retain structural branches independently; their presence is not effect proof."""
    for line in text.splitlines():
        if not line.startswith(('A:', 'T:', 'R:', 'S:', 'SVar:', 'K:')):
            continue
        params = {}
        for segment in line.split('|'):
            match = re.match(r'^(?:[\w-]+:)*\s*(\w+)\$\s*(.*)$', segment.strip())
            if match:
                params[match[1]] = match[2]
        mode = params.get('Mode', '').strip()
        if line.startswith('T:') and mode:
            add('Trigger:' + mode, origin='runtime trigger declaration')['cards'].add(card)
        elif line.startswith('R:') and params.get('Event'):
            add('Replacement:' + params['Event'].strip(), origin='runtime replacement declaration')['cards'].add(card)
        elif line.startswith('S:') and mode:
            add('Static:' + mode, origin='runtime static declaration')['cards'].add(card)
        elif mode:
            # A named SVar may be a delayed trigger, static ability or reusable definition.
            # Preserve its mode without guessing which executable definition consumes it.
            add('ScriptMode:' + mode, origin='runtime named definition')['cards'].add(card)
        for field, value in params.items():
            add('Parameter:' + field, origin='runtime script field')['cards'].add(card)
            if field in ('Description', 'TriggerDescription', 'SpellDescription', 'StackDescription'):
                label = re.match(r'^([A-Za-z][A-Za-z -]{0,60}?)\s+[—–]\s+', value)
                if label:
                    # A leading label is a review candidate, not a keyword inferred from prose.
                    add('AbilityLabel:' + label[1].strip(), origin='leading rule-text label; classification unreviewed')['cards'].add(card)
            if field == 'Duration':
                add('Duration:' + value.strip(), origin='runtime duration')['cards'].add(card)
            if field in ('Cost', 'UnlessCost'):
                for part in re.findall(r'(\w+)\s*<', value):
                    add('Cost:' + part, origin='runtime cost declaration')['cards'].add(card)
                for part, label in [('T', 'Tap'), ('Q', 'Untap')]:
                    if re.search(r'(?<![\w<])' + part + r'(?![\w>])', value):
                        add('Cost:' + label, origin='runtime cost declaration')['cards'].add(card)


def fingerprint(paths):
    digest = hashlib.sha256()
    for path in sorted(set(paths)):
        digest.update(path.relative_to(ROOT).as_posix().encode())
        digest.update(b'\0')
        digest.update(path.read_bytes())
    return digest.hexdigest()


def inventory(manifest):
    cfg = read_json(ROOT / 'ForgeRuntime/runtime.json')
    res = ROOT / 'ForgeRuntime' / cfg['assets'] / 'res'
    entries = {}

    def add(name, zh='', origin=''):
        item = entries.setdefault(key(name), dict(name=name, chinese=zh, origins=[], cards=set()))
        if origin and origin not in item['origins']:
            item['origins'].append(origin)
        return item

    for entry in read_json(ROOT / 'Assets/Resources/KeywordGlossary.json')['entries']:
        add(entry['en'], entry.get('zh', ''), 'glossary')
    for heading in read_json(HERE / 'KeywordRulesBaseline.json')['headings']:
        add(heading['name'], origin='CR ' + heading['rule'])
    enum = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/keyword/Keyword.java'
    # The glossary already uses this pinned enum; retain explicit provenance when available.
    catalog = declaration_inventory.keyword_catalog(enum.read_text(encoding='utf8')) if enum.exists() else {}
    split_source = ROOT / '.tools/forge-proof/upstream/forge-core/src/main/java/forge/card/CardSplitType.java'
    split_text = split_source.read_text(encoding='utf8') if split_source.exists() else ''
    split_modes = set(re.findall(r'^\s*(\w+)\(FaceSelectionMethod\.', split_text, re.M))
    if '"DoubleFaced".equals(text)' in split_text:
        split_modes.add('DoubleFaced')
    if enum.exists():
        for match in re.finditer(r'^\s*(\w+)\(("(?:[^"\\]|\\.)*"),', enum.read_text(encoding='utf8'), re.M):
            if match[1] not in ('UNDEFINED', 'MAYFLASHCOST', 'MAYFLASHSAC'):
                add(json.loads(match[2]), origin='Forge keyword enum')
    for job in manifest['jobs']:
        for case in job['cases']:
            for name in case['mechanics']:
                add(name, origin='reviewed scenario')
    for name in manifest['priority']:
        add(name, origin='review queue')

    game = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    enum_sources = []
    for prefix, path in [('Trigger', game / 'trigger/TriggerType.java'),
                         ('Replacement', game / 'replacement/ReplacementType.java'),
                         ('Static', game / 'staticability/StaticAbilityMode.java')]:
        if not path.exists():
            continue
        enum_sources.append(path)
        body = path.read_text(encoding='utf8').split('public enum ', 1)[1].split(';', 1)[0] + ';'
        for name in re.findall(r'^\s*(\w+)\s*(?:\([^\n]*\))?\s*[,;]', body, re.M):
            add(prefix + ':' + name, origin=prefix + ' enum')
    cost_source = game / 'cost/Cost.java'
    if cost_source.exists():
        enum_sources.append(cost_source)
        for name in re.findall(r'parse\.startsWith\("(\w+)<"\)', cost_source.read_text(encoding='utf8')):
            add('Cost:' + name, origin='Forge cost parser')

    scripts = sorted((res / 'cardsfolder').rglob('*.txt'))
    if not scripts:
        raise RuntimeError('No runtime card scripts: inventory would be incomplete.')
    unknown = collections.defaultdict(set)
    declaration_rows = []
    declaration_sources = {}
    entry_names = {identifier: item['name'] for identifier, item in entries.items()}
    for path in scripts:
        text = path.read_text(encoding='utf-8-sig')
        names = re.findall(r'^Name:(.+)$', text, re.M)
        card = names[0].strip() if names else path.stem
        scan_structure(text, card, add)
        # Inspect script structure, never substring-match Oracle text or card names.
        for item in declaration_inventory.declarations(text):
            decision, references, unresolved = declaration_inventory.resolve(
                item, catalog, entry_names, key, split_modes)
            for name in references:
                entries[key(name)]['cards'].add(card)
            value = item['legacyValue']
            if decision == 'unclassified':
                unknown[value].add(card)
            if key(value) not in entries or decision == 'unclassified':
                relative = path.relative_to(ROOT).as_posix()
                declaration_sources[relative] = hashlib.sha256(path.read_bytes()).hexdigest()
                declaration_rows.append(dict(item, path=relative, card=card,
                    classification=decision, references=references, unresolved=unresolved,
                    owner='A04' if decision == 'unclassified' else 'R26' if decision == 'card-face-metadata' else 'A03'))
        api_aliases = {'TakeInitiative': 'Initiative', 'RingTemptsYou': 'The Ring tempts you',
                       'Venture': 'Venture into the dungeon', 'DelayedTrigger': 'Delayed triggers'}
        for api in set(re.findall(r'(?:AB|SP|DB)\$\s*(\w+)', text)):
            add('API:' + api, origin='Forge script API')['cards'].add(card)
            if key(api) in entries:
                entries[key(api)]['cards'].add(card)
            if api in api_aliases:
                entries[key(api_aliases[api])]['cards'].add(card)
        for pattern, name in [(r'Conjure\$\s*True\b', 'Conjure'),
                              (r'Duration\$\s*Perpetual\b', 'Perpetually'),
                              (r'Spellbook\$', 'Spellbook')]:
            if re.search(pattern, text):
                entries[key(name)]['cards'].add(card)
        for cost in re.findall(r'(?:UnlessCost|Cost)\$\s*([^|\r\n]+)', text):
            for name in re.findall(r'(\w+)\s*<', cost):
                if key(name) in entries:
                    entries[key(name)]['cards'].add(card)
    auxiliary_scripts = []
    script_counts = {'cardsfolder': len(scripts)}
    for folder in ('tokenscripts', 'effects'):
        paths = sorted((res / folder).rglob('*.txt'))
        script_counts[folder] = len(paths)
        auxiliary_scripts += paths
        for path in paths:
            text = path.read_text(encoding='utf-8-sig')
            names = re.findall(r'^Name:(.+)$', text, re.M)
            card = '[' + folder + '] ' + (names[0].strip() if names else path.stem)
            scan_structure(text, card, add)
            for api in set(re.findall(r'(?:AB|SP|DB)\$\s*(\w+)', text)):
                add('API:' + api, origin=folder + ' script API')['cards'].add(card)
    files = scripts + auxiliary_scripts + enum_sources
    for folder in ('classes', 'lib'):
        files += [p for p in (ROOT / 'ForgeRuntime' / folder).rglob('*') if p.is_file()]
    files += [ROOT / 'ForgeRuntime/runtime.json', HERE / 'MechanicCoverage.json', Path(__file__), HERE / 'DeclarationInventory.py']
    if split_source.exists():
        files.append(split_source)
    consumer_sources = [p for p in [enum, split_source,
        split_source.with_name('CardRules.java'), game / 'staticability/StaticAbilityContinuous.java'] if p.exists()]
    files += consumer_sources
    files += [p for p in [HERE / 'PresentationFamilies.json', HERE / 'PresentationCoverage.py',
                         HERE / 'InteractionContract.json', HERE / 'Audit-InteractionContract.py',
                         HERE / 'BaseInteractionContract.json', HERE / 'NativePatches.json', HERE / 'Apply-NativePatches.py',
                         ROOT / '.tools/forge-proof/upstream/forge-gui/src/main/java/forge/gui/interfaces/IGuiGame.java'] if p.exists()]
    files += list(HERE.rglob('*.java')) + list(HERE.glob('Test-*.py'))
    # Registered interactive drivers may use Run-* names. Their verification
    # source must participate in provenance just like Test-* drivers.
    files += [ROOT / job['script'] for job in manifest['jobs']]
    base_contract=HERE/'BaseInteractionContract.json'
    if base_contract.exists():files += [ROOT/item['path'] for item in read_json(base_contract)['sourceGuards']]
    files += [ROOT / 'Assets/Resources/KeywordGlossary.json', HERE / 'KeywordRulesBaseline.json']
    if enum.exists():
        files.append(enum)
    files += list((ROOT / 'Assets/Scripts').rglob('*.cs'))
    REPORT.mkdir(parents=True, exist_ok=True)
    review = dict(status='SOURCE_CLASSIFIED_NOT_EFFECT_PASS',
        scope='Previously unknown K/AlternateMode/AddKeyword/Condition/Attributes values and corrected *Condition fields in cardsfolder only. Exact parameters retained; recognized keyword does not prove parameter semantics, reachability, rule results or UI. Token/effect declarations remain outside this bounded declaration review.',
        sourceGuards=[dict(path=p.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                      for p in consumer_sources + [HERE / 'DeclarationInventory.py', Path(__file__)]],
        sources=declaration_sources, rows=declaration_rows,
        classifications=dict(collections.Counter(r['classification'] for r in declaration_rows)),
        remainingValues=sorted(unknown))
    (REPORT / 'declarations.json').write_text(json.dumps(review, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    return entries, unknown, dict(revision=cfg['revision'], fingerprint=fingerprint(files), cardScripts=len(scripts),
                                  scriptRoots=script_counts, structuralSources=[p.relative_to(ROOT).as_posix() for p in enum_sources])


def review_script_inventory(manifest):
    """Classify helper/adapter/legacy scripts without granting effect PASS."""
    registered = {job['script'] for job in manifest['jobs']}
    candidates = {p.relative_to(ROOT).as_posix(): p for p in HERE.glob('Test-*.py')
                  if p.relative_to(ROOT).as_posix() not in registered}
    reviewed, pending = [], []
    seen = set()
    for item in manifest.get('reviewedScripts', []):
        path = item['path']
        if path in seen or path in registered:
            raise ValueError('Duplicate or registered script classification: ' + path)
        seen.add(path)
        if item['kind'] not in {'fixture-service', 'projection-adapter', 'native-input-contract', 'legacy-effect-regression'} or not item['scope'] or not item['remaining']:
            raise ValueError('Incomplete script classification: ' + path)
        guards = [{'path': path, 'sha256': item['sourceHash']}] + item.get('sourceGuards', [])
        valid = path in candidates and all((ROOT / g['path']).is_file() and
                    hashlib.sha256((ROOT / g['path']).read_bytes()).hexdigest() == g['sha256'] for g in guards)
        if valid:
            reviewed.append(item)
        else:
            pending.append(dict(path=path, reason='Classification source missing or changed; review required'))
    unreviewed = sorted(set(candidates) - {item['path'] for item in reviewed} | {item['path'] for item in pending})
    return unreviewed, reviewed, pending


def build_report(manifest, entries, unknown, provenance):
    latest = LOCAL / 'latest.json'
    previous = read_json(latest) if latest.exists() else {}
    fresh = previous.get('provenance') == provenance
    results = {r['id']: r for r in previous.get('jobs', [])}
    statuses = collections.Counter()
    rows = []
    for identifier, item in entries.items():
        scenarios = []
        for job in manifest['jobs']:
            for case in job['cases']:
                if identifier in {key(name) for name in case['mechanics']}:
                    scenarios.append(dict(job=job['id'], case=case.get('id', case.get('arg', 'default')), assertions=case['assertions']))
        job_ids = {s['job'] for s in scenarios}
        if not scenarios:
            status = '未登记效果测试'
        elif not fresh:
            status = '需重测' if previous else '有测试，未运行'
        elif any(results.get(j, {}).get('status') in ('FAIL', 'TIMEOUT', 'ERROR') for j in job_ids):
            status = '失败'
        elif all(results.get(j, {}).get('status') == 'PASS' for j in job_ids):
            status = '已验证列出的场景'
        else:
            status = '有测试，本轮未运行'
        statuses[status] += 1
        rows.append(dict(name=item['name'], category=category(item['name']), chinese=item['chinese'], status=status,
                         priority=0 if identifier in {key(n) for n in manifest['priority']} else 1,
                         cardCount=len(item['cards']), cards=sorted(item['cards'])[:6],
                         origins=item['origins'], scenarios=scenarios,
                         unity='未纳入此清单的自动验证', boundaries='仅验证明确列出的断言；其他边界及卡牌组合未验证'))
    rows.sort(key=lambda r: (r['priority'], r['status'] == '已验证列出的场景', -r['cardCount'], r['name']))
    REPORT.mkdir(parents=True, exist_ok=True)
    unregistered_tests, reviewed_scripts, stale_reviews = review_script_inventory(manifest)
    groups = {}
    for row in rows:
        group = groups.setdefault(row['category'], dict(entries=0, referenced=0, statuses=collections.Counter()))
        group['entries'] += 1
        group['referenced'] += int(row['cardCount'] > 0)
        group['statuses'][row['status']] += 1
    payload = dict(scope=manifest['scope'], provenance=provenance, counts=dict(statuses), groups=groups,
                   currentRun=previous.get('run') if fresh else None, entries=rows,
                   testsNotReviewedForThisManifest=unregistered_tests,
                   reviewedScriptsOutsideEffectManifest=reviewed_scripts,
                   scriptClassificationsNeedingReview=stale_reviews,
                   unmappedDeclarations={k: sorted(v)[:6] for k, v in sorted(unknown.items())},
                   unmappedDeclarationCount=len(unknown),
                   declarationClassification='declarations.json (source classification only; no effect PASS)',
                   limitations=['Categories overlap; do not sum them as unique gameplay mechanics.',
                                'Script fields and enum values are structural inventory, not independent effect semantics.',
                                'No exhaustive inventory of combinations, layers, permissions, timing, targets or multiplayer behavior.'])
    (REPORT / 'coverage.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    with (REPORT / 'coverage.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['类别', '机制/引擎操作', '中文', '状态', '脚本引用卡数', '代表卡', '测试场景', '实际断言', '未验证范围'])
        for row in rows:
            writer.writerow([row['category'], row['name'], row['chinese'], row['status'], row['cardCount'], '; '.join(row['cards']),
                             '; '.join(s['job'] + '/' + s['case'] for s in row['scenarios']),
                             '; '.join(s['assertions'] for s in row['scenarios']), row['boundaries']])
    lines = ['# 机制效果验证清单', '', '这是缺口清单，不是“全部卡牌验证通过”的证明。', '',
             '扫描范围：当前本地 Forge 卡牌、衍生物和辅助效果脚本；固定版本关键词、触发、替代、持续异能枚举、费用解析器；CR 标题和说明词库。',
             '按脚本字段发现引用；没有直接引用的机制仍保留。未识别脚本声明单独留在 JSON，不能当作已覆盖。',
             '引擎事件、参数、费用和期限是结构维度，不是独立的玩家关键词。类别互相重叠，不能相加当作机制总数。引用卡数不等于包含该机制的完整卡数。', '',
             '测试在预置局面中通过实际 ForgeBridge 请求施放/激活、支付和结算；不证明 Unity 点击、显示或联机交互正确。',
             '未登记表示此清单没有经复核的效果断言，不等于确定未实现；其他已有测试可能尚未登记。每个 PASS 只支持该场景列出的断言。', '',
             'Forge 版本：`' + provenance['revision'] + '`；扫描脚本：' + str(provenance['cardScripts']), '',
             '先看 [共用交互与结算表现](families.md)：录像参考、规则断言、Unity 检查与未支持流程分别记录。', '',
             '## 分类范围', '', '| 分类 | 条目 | 有直接脚本引用 |', '|---|---:|---:|']
    lines += ['| ' + name + ' | ' + str(group['entries']) + ' | ' + str(group['referenced']) + ' |'
              for name, group in sorted(groups.items())]
    lines += ['', '未归类声明：' + str(len(unknown)) + ' 种；尚未复核登记的测试脚本：' + str(len(unregistered_tests)) + ' 个。',
              '脚本参数单列用于查遗漏；参数出现不证明整项效果已经测试。异能词／文本标签按规则说明开头的标签提取，仍需核对分类，不能直接当官方关键词。',
              '目标是否合法、替代选择顺序、持续效果层次、权限到期、多方交互等组合仍需专门场景。', '',
              '## 状态汇总（结构条目，不能当机制总数）', '']
    lines += ['- ' + s + '：' + str(n) for s, n in sorted(statuses.items())]
    lines += ['', '## 优先复核', '', '| 机制 | 中文 | 状态 | 代表卡 |', '|---|---|---|---|']
    for row in rows:
        if row['priority'] == 0:
            lines.append('| ' + ' | '.join([row['name'], row['chinese'], row['status'], ', '.join(row['cards'][:2])]) + ' |')
    lines += ['', '完整清单与每项断言见 [coverage.csv](coverage.csv) 和 [coverage.json](coverage.json)。', '',
              '尚未复核登记的测试脚本也列在 JSON 的 testsNotReviewedForThisManifest，不能静默遗漏。', '',
              '本轮日志、请求轨迹和局面快照保存在项目 `.tools/mechanic-coverage/runs/`，不随仓库提交。', '']
    (REPORT / 'README.md').write_text('\n'.join(lines), encoding='utf8')
    family_manifest = HERE / 'PresentationFamilies.json'
    if family_manifest.exists():
        from PresentationCoverage import build
        ui_path = LOCAL / 'unity-latest.json'
        build(ROOT, REPORT, read_json(family_manifest), manifest, provenance, previous,
              read_json(ui_path) if ui_path.exists() else {})
    print(json.dumps(dict(report=str(REPORT), counts=dict(statuses)), ensure_ascii=False), flush=True)


def run_jobs(manifest, provenance, selected, timeout):
    LOCAL.mkdir(parents=True, exist_ok=True)
    # OS releases this file lock even if the runner is terminated.
    with (LOCAL / 'run.lock').open('a+b') as lock:
        lock.write(b'0'); lock.flush(); lock.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        run = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        destination = LOCAL / 'runs' / run
        destination.mkdir(parents=True)
        results = []
        env = dict(os.environ, PYTHONUTF8='1')
        env.pop('PYTHONOPTIMIZE', None)  # Existing suites use assertions as the oracle.
        for job in manifest['jobs']:
            if selected and job['id'] not in selected:
                continue
            started = time.time()
            folder = destination / job['id']; folder.mkdir()
            command = [sys.executable, '-X', 'utf8', str(ROOT / job['script'])]
            command += [case['arg'] for case in job['cases'] if 'arg' in case]
            result = dict(id=job['id'], cases=len(job['cases']), status='ERROR')
            print('RUN ' + job['id'], flush=True)
            with (folder / 'runner.log').open('w', encoding='utf8') as log:
                process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
                try:
                    result['exitCode'] = process.wait(timeout=timeout)
                    result['status'] = 'PASS' if result['exitCode'] == 0 else 'FAIL'
                except subprocess.TimeoutExpired:
                    result['status'] = 'TIMEOUT'
                    if os.name == 'nt':
                        subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                    else:
                        process.kill()
                    process.wait()
            # javac on Windows can emit localized diagnostics in the system codepage.
            # Keep the raw bytes in runner.log, without aborting the entire evidence report.
            output = (folder / 'runner.log').read_text(encoding='utf8', errors='replace')
            markers = [case['pass'] for case in job['cases'] if 'pass' in case]
            if result['status'] == 'PASS' and any(marker not in output for marker in markers):
                result.update(status='FAIL', reason='Missing required per-case PASS evidence')
            source = ROOT / job['reportDir']
            for path in source.rglob('*'):
                if path.is_file() and path.stat().st_mtime >= started - 0.01:
                    target = folder / 'evidence' / path.relative_to(source)
                    target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(path, target)
            if job.get('structuredResult') and result['status'] == 'PASS':
                path = folder / 'evidence' / job['structuredResult']
                record = read_json(path) if path.exists() else {}
                if record.get('status') != 'PASS':
                    result.update(status='FAIL', reason='Missing fresh structured PASS evidence')
            if job['id'] == 'fra' and result['status'] == 'PASS':
                path = folder / 'evidence/results.json'
                records = read_json(path) if path.exists() else []
                expected = {case['arg'] for case in job['cases']}
                if len(records) != len(expected) or {r['case'] for r in records} != expected or any(r['status'] != 'PASS' for r in records):
                    result.update(status='FAIL', reason='FRA per-case result set incomplete')
            result['seconds'] = round(time.time() - started, 2)
            results.append(result)
            payload = dict(run=run, provenance=provenance, jobs=results)
            (destination / 'results.json').write_text(json.dumps(payload, indent=2), encoding='utf8')
            (LOCAL / 'latest.json').write_text(json.dumps(payload, indent=2), encoding='utf8')
            print(result['status'] + ' ' + job['id'], flush=True)
        return any(r['status'] != 'PASS' for r in results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', action='store_true', help='Run all registered real-engine cases before reporting')
    parser.add_argument('--jobs', nargs='+', help='Select registered job IDs (requires --run)')
    parser.add_argument('--timeout', type=int, default=600, help='Timeout seconds per job')
    args = parser.parse_args()
    manifest = read_json(HERE / 'MechanicCoverage.json')
    known = {j['id'] for j in manifest['jobs']}
    if args.jobs and (not args.run or set(args.jobs) - known):
        parser.error('--jobs requires --run and known IDs: ' + ', '.join(sorted(known)))
    if args.timeout <= 0:
        parser.error('--timeout must be positive')
    subprocess.run([sys.executable, '-X', 'utf8', str(HERE / 'Audit-InteractionContract.py')], check=True)
    entries, unknown, provenance = inventory(manifest)
    failed = run_jobs(manifest, provenance, args.jobs, args.timeout) if args.run else False
    # Recompute after execution so a concurrent code/runtime edit cannot retain a green result.
    if args.run:
        entries, unknown, provenance = inventory(manifest)
    build_report(manifest, entries, unknown, provenance)
    return int(failed)


if __name__ == '__main__':
    sys.exit(main())
