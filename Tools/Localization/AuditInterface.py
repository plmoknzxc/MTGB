"""Check source tables and inventory archived native UI contracts (no engine runs)."""
import collections
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def main():
    rows, failures, duplicates = {}, [], []
    for name in ('Interface.tsv', 'RuntimeInterface.tsv', 'ForgeInterface.tsv'):
        path = ROOT/'Assets/Localization'/name
        for n, line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(), 1):
            if not line.strip(): continue
            parts = line.split('\t')
            if len(parts) not in (3, 4):
                failures.append(f'{name}:{n}: invalid columns'); continue
            source = parts[0].replace('\\n', '\n')
            values = (parts if len(parts) == 3 else parts[1:])
            values = [x.replace('\\n', '\n') for x in values]
            if any(not x for x in values): failures.append(f'{name}:{n}: empty locale')
            fields = set(re.findall(r'\{[0-7]\}', source))
            if any(set(re.findall(r'\{[0-7]\}', v)) != fields for v in values):
                failures.append(f'{name}:{n}: parameter mismatch')
            if source in rows:
                duplicates.append({'source': source, 'same': rows[source] == values})
                if rows[source] != values: failures.append(f'{name}:{n}: conflicting duplicate')
            rows[source] = values
    patterns = []
    for key in rows:
        seen, at, parts = set(), 0, ['^']
        for token in re.finditer(r'\{([0-7])\}', key):
            parts.append(re.escape(key[at:token.start()])); group = 'p'+token[1]
            parts.append(f'(?P={group})' if group in seen else f'(?P<{group}>.+?)')
            seen.add(group); at = token.end()
        parts += [re.escape(key[at:]), '$']
        if seen: patterns.append(re.compile(''.join(parts), re.S))
    messages, files, parse_failures = collections.Counter(), [], []
    def record(value):
        if isinstance(value, list):
            for item in value: record(item)
        elif isinstance(value, dict):
            request = value.get('request')
            if isinstance(request, dict):
                if isinstance(request.get('message'), str): messages[request['message']] += 1
                for option in request.get('options', []):
                    if isinstance(option, dict) and isinstance(option.get('label'), str): messages[option['label']] += 1
            for key, item in value.items():
                if key in ('views', 'forge', 'inputs', 'requests', 'snapshots'): record(item)
    for group in (ROOT/'.tools').iterdir():
        if not group.is_dir(): continue
        candidates = list(group.glob('ui-inputs/*.json'))+list(group.glob('ui-inputs.json'))
        for path in candidates:
            if path.stat().st_size > 10_000_000: continue
            try: record(json.loads(path.read_text(encoding='utf-8-sig')))
            except (ValueError, UnicodeError) as e: parse_failures.append({'path': str(path.relative_to(ROOT)), 'error': str(e)}); continue
            files.append({'path': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    unknown = []
    for message, count in messages.most_common():
        # Context-specific production translators are audited separately in Unity.
        for line in message.splitlines():
            stripped = re.sub(r'^(?:one|oneOrNone): ', '', line)
            if re.search(r'[A-Za-z]+(?: +[A-Za-z]+){3,}', stripped) and stripped not in rows and not any(p.fullmatch(stripped) for p in patterns):
                unknown.append({'text': stripped, 'occurrences': count})
    result = {'status': 'PASS' if not failures else 'FAIL', 'keys': len(rows), 'failures': failures,
              'duplicates': duplicates, 'corpusFiles': files, 'parseFailures': parse_failures,
              'nativeMessages': len(messages), 'genericUnmatched': unknown,
              'scope': 'Generic table inventory; unmatched can have contextual production translators. Not rules, complete card art, or live network proof.'}
    output = ROOT/'.tools/language-unification-1010'; output.mkdir(exist_ok=True)
    (output/'table-audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'status': result['status'], 'keys': len(rows), 'failures': failures[:12], 'corpusFiles': len(files), 'messages': len(messages), 'unmatched': len(unknown)}, ensure_ascii=False))
    print(json.dumps(unknown[:35], ensure_ascii=False, indent=2))
    if failures: raise SystemExit(1)


if __name__ == '__main__': main()
