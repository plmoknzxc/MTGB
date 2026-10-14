"""Import pinned Forge display messages, without changing its English wire protocol.

Run with python -B -X utf8 Tools/Localization/ImportForgeInterface.py.
Unsupported MessageFormat constructs, missing translations and conflicting source
messages are recorded rather than guessing a translation or dropping arguments.
"""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / '.tools/forge-proof/upstream/forge-gui/res/languages'
CODES = ('en-US', 'zh-CN', 'ja-JP')


def properties(path):
    result = {}
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        if not line or line.lstrip().startswith(('#', '!')) or '=' not in line:
            continue
        key, value = line.split('=', 1)
        value = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1], 16)), value)
        value = re.sub(r'\\(.)', lambda m: {'n': '\n', 'r': '\r', 't': '\t'}.get(m[1], m[1]), value)
        result[key.strip()] = value
    return result


def message_format(value):
    # Java MessageFormat always processes apostrophe quoting, even with no args.
    out, quoted, i = [], False, 0
    while i < len(value):
        if value[i] == "'":
            if i + 1 < len(value) and value[i + 1] == "'":
                out.append("'"); i += 2; continue
            quoted = not quoted; i += 1; continue
        if value[i] == '{' and not quoted:
            end = value.find('}', i)
            token = value[i + 1:end]
            if end < 0 or not re.fullmatch(r'\d+', token):
                raise ValueError('complex MessageFormat')
            out.append('{'+token+'}'); i = end + 1; continue
        if value[i] in '{}':
            # Literal mana braces must not become dynamic substitution fields.
            raise ValueError('literal braces')
        out.append(value[i]); i += 1
    return ''.join(out)


def normalize(values):
    values = [message_format(v) for v in values]
    indices = {int(x) for x in re.findall(r'\{(\d+)\}', values[0])}
    start = max(indices, default=-1) + 1
    counts = []
    for i, value in enumerate(values):
        n = [start]
        def replace(match):
            index = int(match[1])-1+start if match[1] else n[0]
            if not match[1]: n[0] += 1
            return '{'+str(index)+'}'
        values[i] = re.sub(r'%(?:(\d+)\$)?[sd]', replace, value)
        counts.append(set(re.findall(r'\{(\d+)\}', values[i])))
    if counts[0] != counts[1] or counts[0] != counts[2]:
        raise ValueError('placeholder mismatch')
    if '%' in ''.join(values) and re.search(r'%[0-9.$]*[a-zA-Z]', ''.join(values)):
        raise ValueError('unsupported printf')
    if any(int(x) > 7 for x in counts[0]): raise ValueError('too many parameters')
    return values


def main():
    catalogs = [properties(SOURCE / (c+'.properties')) for c in CODES]
    candidates, excluded = {}, []
    # Prefer existing project wording over upstream terminology.
    project_keys = set()
    for line in '\n'.join((ROOT/'Assets/Localization'/file).read_text(encoding='utf-8-sig') for file in ('Interface.tsv','RuntimeInterface.tsv')).splitlines():
        parts = line.split('\t')
        if len(parts) in (3, 4): project_keys.add(parts[0].replace('\\n', '\n'))
    for key, en in catalogs[0].items():
        if not key.startswith(('lbl', 'nl', 'tx', 'tit')): continue
        raw = [c.get(key, '') for c in catalogs]
        reason = None
        try:
            if any(not v.strip() for v in raw): raise ValueError('missing locale')
            if not re.search(r'[\u3400-\u9fff]', raw[1]): raise ValueError('untranslated Chinese')
            if not re.search(r'[\u3040-\u30ff\u3400-\u9fff]', raw[2]): raise ValueError('untranslated Japanese')
            values = normalize(raw)
            if re.search(r'\{\d+\}\{\d+\}', values[0]):
                raise ValueError('adjacent dynamic arguments cannot be recovered from wire text')
            # Do not match an arbitrary phrase with a pattern such as "{0}".
            if re.search(r'\{\d+\}', values[0]) and len(re.sub(r'\{\d+\}|\W', '', values[0])) < 12:
                raise ValueError('unbounded template')
            candidates.setdefault(values[0], []).append((key, values))
        except ValueError as e: reason = str(e)
        if reason: excluded.append({'key': key, 'reason': reason})
    rows, origins = [], []
    for en, entries in sorted(candidates.items()):
        if en in project_keys: continue
        alternatives = {(v[1], v[2]) for _, v in entries}
        if len(alternatives) != 1:
            excluded.append({'key': [k for k, _ in entries], 'reason': 'ambiguous English source'}); continue
        values = entries[0][1]
        rows.append([values[0], values[1], values[0], values[2]])
        origins.append({'keys': [k for k, _ in entries], 'source': en})
    def escape(s): return s.replace('\r', '').replace('\n', '\\n').replace('\t', ' ')
    output = ROOT/'Assets/Localization/ForgeInterface.tsv'
    output.write_text(''.join('\t'.join(map(escape, row))+'\n' for row in rows), encoding='utf-8')
    manifest = {'schema': 1, 'locales': list(CODES), 'rows': len(rows), 'sources': {
        str((SOURCE/(c+'.properties')).relative_to(ROOT)): hashlib.sha256((SOURCE/(c+'.properties')).read_bytes()).hexdigest() for c in CODES},
        'outputSha256': hashlib.sha256(output.read_bytes()).hexdigest(), 'origins': origins, 'excluded': excluded}
    (ROOT/'Tools/Localization/ForgeInterface.import.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'rows': len(rows), 'excluded': len(excluded), 'output': str(output)}))


if __name__ == '__main__': main()
