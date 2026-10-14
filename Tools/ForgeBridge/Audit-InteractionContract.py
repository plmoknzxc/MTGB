"""Audit the pinned GUI boundary. Classification is not effect/UI test evidence."""
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
UPSTREAM = ROOT / '.tools/forge-proof/upstream/forge-gui/src/main/java'
CONTRACT = ROOT / 'Tools/ForgeBridge/InteractionContract.json'
OUT = ROOT / 'Tools/Validation/Reports/MechanicCoverage'


def mask_java(source):
    # Preserve offsets, including strings containing // or braces.
    return re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*',
                  lambda m: ''.join('\n' if c == '\n' else ' ' for c in m[0]), source, flags=re.S)


def declarations(source):
    # Keep only interface-level declarations; exclude calls inside default bodies.
    clean = mask_java(source)
    depth = 0
    visible = []
    for char in clean:
        if char == '}': depth -= 1
        visible.append(char if depth <= 1 else '\n' if char == '\n' else ' ')
        if char == '{': depth += 1
    pattern = r'^\s*(default\s+)?(?:<[^\n]+>\s+)?([\w<>?, .]+?)\s+(\w+)\(([^{};]*?)\)\s*(?:throws\s+[\w.,\s]+)?(;|\{)'
    rows = []
    for match in re.finditer(pattern, ''.join(visible), re.M):
        signature = re.sub(r'\s+', ' ', match[0]).strip().rstrip(';{').strip()
        rows.append(dict(name=match[3], signature=signature, default=bool(match[1]), returnType=match[2].strip()))
    return rows


def direct_calls(source):
    # Match across lines; comments retain line offsets without becoming evidence.
    clean = mask_java(source)
    return [dict(method=m[1], line=clean.count('\n', 0, m.start()) + 1)
            for m in re.finditer(r'getGui\(\)\s*\.\s*(\w+)\s*\(', clean)]


def base_calls(source):
    clean = mask_java(source)
    pattern = r'(GuiBase\s*\.\s*getInterface\(\)\s*\.|SGuiChoose\s*\.|SOptionPane\s*\.)(\w+)\s*\('
    return [dict(helper=re.sub(r'\s+', '', m[1]).rstrip('.'), method=m[2], line=clean.count('\n', 0, m.start()) + 1)
            for m in re.finditer(pattern, clean)]


def audit_base(bridge):
    definition=json.loads((ROOT/'Tools/ForgeBridge/BaseInteractionContract.json').read_text(encoding='utf8'))
    interface=UPSTREAM/'forge/gui/interfaces/IGuiBase.java'
    methods=declarations(interface.read_text(encoding='utf8'))
    roles={r['signature']:r for r in definition['methods']}
    current={r['signature'] for r in methods};expected=set(roles)
    handler=bridge.split('static Object base(',1)[1].split('static final class NoticeContext',1)[0]
    names=set(re.findall(r'n\.equals\("(\w+)"\)',handler))
    for group in ('BASE_FALSE_CALLBACKS','BASE_PRESENTATION_CALLBACKS'):
        names.update(re.findall(r'"(\w+)"',handler.split(group+'=Set.of(',1)[1].split(');',1)[0]))
    missing=[]
    for method in methods:
        method.update({k:v for k,v in roles.get(method['signature'],{}).items() if k!='signature'})
        if not method['default'] and method.get('role')!='unsupported' and method['name'] not in names:
            missing.append(method['signature'])
    sources=[];changed=[]
    for reviewed in definition['sourceGuards']:
        path=ROOT/reviewed['path'];actual=hashlib.sha256(path.read_bytes()).hexdigest()
        sources.append(dict(path=reviewed['path'],sha256=actual,unchanged=actual==reviewed['sha256'],scope=reviewed['scope']))
        if actual!=reviewed['sha256']:changed.append(reviewed['path'])
    calls=[]
    for scope in ('forge/player','forge/gamemodes/match/input','forge/gui/util'):
        for path in sorted((UPSTREAM/scope).rglob('*.java')):
            calls.extend(dict(**call,path=path.relative_to(ROOT).as_posix()) for call in base_calls(path.read_text(encoding='utf8')))
    implicit=bool(re.search(r'method\.getReturnType\(\)\s*==\s*(boolean|void)\.class',handler))
    return dict(methods=methods,unknown=sorted(current-expected),removed=sorted(expected-current),missingHandlers=missing,
                changedSources=changed,sourceGuards=sources,callsites=calls,implicitReturnFallback=implicit,
                knownGaps=definition['knownGaps'],limits=definition['limits'])


def audit():
    definition = json.loads(CONTRACT.read_text(encoding='utf8'))
    interface = UPSTREAM / 'forge/gui/interfaces/IGuiGame.java'
    source = interface.read_text(encoding='utf8')
    source_hash=hashlib.sha256(source.encode()).hexdigest()
    body_changed=source_hash!=definition['reviewedInterfaceSha256']
    methods = declarations(source)
    reviewed = definition['methods']
    current = {row['signature'] for row in methods}
    expected = {row['signature'] for row in reviewed}
    unknown = sorted(current - expected)
    removed = sorted(expected - current)
    roles = {row['signature']: row for row in reviewed}
    bridge_path = ROOT / 'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java'
    bridge = bridge_path.read_text(encoding='utf8')
    base_contract=audit_base(bridge)
    names = set(re.findall(r'n\.equals\("(\w+)"\)', bridge.split('static final class Gui implements InvocationHandler', 1)[1].split('String abilityLabel', 1)[0]))
    whitelist = set(re.findall(r'"(\w+)"', re.split(r'\bPRESENTATION_CALLBACKS=Set.of\(', bridge, maxsplit=1)[1].split(');', 1)[0]))
    missing_handlers = []
    for method in methods:
        method.update({key: value for key, value in roles.get(method['signature'], {}).items() if key != 'signature'})
        if not method['default'] and method.get('role') in ('decision', 'state') and method['name'] not in names:
            # The boolean introspection group is explicitly handled by a set.
            if method['name'] not in ('isGamePaused', 'isUiSetToSkipPhase', 'isSelecting'):
                missing_handlers.append(method['signature'])
        if not method['default'] and method.get('role') == 'snapshot' and method['name'] not in whitelist | names:
            missing_handlers.append(method['signature'])
    callsites = []
    scopes = [UPSTREAM / 'forge/player', UPSTREAM / 'forge/gamemodes/match/input']
    for scope in scopes:
        for path in sorted(scope.rglob('*.java')):
            text=path.read_text(encoding='utf8')
            for call in direct_calls(text):
                callsites.append(dict(**call, path=path.relative_to(ROOT).as_posix()))
    payload = dict(forgeRevision=(ROOT / 'Tools/ForgeProof/forge-version.txt').read_text().strip(),
        interfaceSha256=source_hash, interfaceChanged=body_changed, methods=methods, callsites=callsites,
        unknown=unknown, removed=removed, missingHandlers=sorted(set(missing_handlers)),
        knownGaps=definition['knownGaps'], baseContract=base_contract,
        limits=['Only the pinned IGuiGame boundary and direct getGui() callsites in player/input scopes are audited.',
                'Default forwarding and snapshot callback classifications were reviewed against source; classification is not runtime proof.',
                'IGuiBase now has a separate signature/source guard and bounded helper-call audit; inheritance resolution is recorded separately, not card-effect proof.',
                'Other game modes, indirect/aliased calls and arbitrary mechanism combinations remain outside this audit.'])
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'interaction-contract.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    lines = ['# Forge 交互接口核对', '',
        f'固定版本 `{payload["forgeRevision"]}`：{len(methods)} 个接口声明（含重载），{len(callsites)} 个直接调用位置。', '',
        '**接入分类不等于运行验证。** 规则断言、Unity 点击与继续结算必须分别有证据。', '',
        '完整接口源文件（包括默认方法体）复核哈希：' + ('**已变化，必须重新复核。**' if body_changed else '与已复核版本一致。'), '',
        '| 方法 | 分类 | 共用界面／处理 |', '|---|---|---|']
    for method in methods:
        lines.append('| `' + method['signature'].replace('|', '\\|') + '` | ' + method.get('role', 'UNKNOWN') + ' | ' + method.get('route', '未登记') + ' |')
    lines += ['', '## 必须继续处理的缺口', '']
    for gap in definition['knownGaps']:
        lines.append('- **' + gap['id'] + '**：' + gap['reason'])
    lines += ['', '## 新增／变更声明', ''] + ['- `' + item + '`' for item in unknown + removed + missing_handlers]
    lines += ['', '## 基础界面边界', '', f'{len(base_contract["methods"])} 个 IGuiBase 声明；玩家／输入／静态辅助类范围 {len(base_contract["callsites"])} 个基础界面／辅助调用位置。', '',
              '| 方法 | 分类 | 处理 |', '|---|---|---|']
    for row in base_contract['methods']:
        lines.append('| `'+row['signature'].replace('|','\\|')+'` | '+row.get('role','UNKNOWN')+' | '+row.get('route','未登记')+' |')
    lines += ['', '基础接口、继承源文件或费用辅助源文件的守卫变化会阻止构建；分类与哈希一致仍不代表全部效果验证。', '']
    for gap in base_contract['knownGaps']:lines.append('- **'+gap['id']+'**：'+gap['reason'])
    lines += ['', '范围：不覆盖所有游戏模式、间接／别名调用及任意机制组合；具体限制见 JSON。', '']
    (OUT / 'interaction-contract.md').write_text('\n'.join(lines), encoding='utf8')
    return payload


if __name__ == '__main__':
    result = audit()
    failures = result['unknown'] + result['removed'] + result['missingHandlers']
    base=result['baseContract']
    failures+=base['unknown']+base['removed']+base['missingHandlers']+base['changedSources']
    if base['implicitReturnFallback']:failures.append('IGuiBase has an implicit boolean/void return fallback')
    if result['interfaceChanged']:failures.append('Interface source changed; review default bodies as well as signatures before updating the approved hash.')
    print(('FAIL' if failures else 'PASS') + f': pinned GUI contracts; {len(result["methods"])} game + {len(base["methods"])} base declarations; classification is not runtime evidence')
    for failure in failures: print(failure)
    sys.exit(bool(failures))
