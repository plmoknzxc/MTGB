"""Guard text-process boundaries against reintroducing Windows default encodings."""
from pathlib import Path
import re

root=Path(__file__).resolve().parents[2]

def violations(text):
    errors=[]
    if 'Encoding.Default' in text:
        errors.append('system-default encoding')
    if 'Utf8ProcessIO.Configure(' not in text:
        for stream in ('Input','Output','Error'):
            if re.search(r'RedirectStandard'+stream+r'\s*=\s*true',text) and not re.search(r'Standard'+stream+r'Encoding\s*=',text):
                errors.append('implicit standard '+stream+' encoding')
    return errors

assert violations('RedirectStandardOutput = true')
assert violations('RedirectStandardInput = true, StandardOutputEncoding = Encoding.UTF8')
assert not violations('RedirectStandardOutput = true; Utf8ProcessIO.Configure(info);')
failures=[]
for base in (root/'Assets/Scripts',root/'Tools/ForgeProof/UnityProject/Assets'):
    for path in base.rglob('*.cs'):
        for error in violations(path.read_text(encoding='utf-8-sig')):
            failures.append(f'{path.relative_to(root)}: {error}')
if failures:
    raise SystemExit('\n'.join(failures))
print('PASS: redirected C# text pipes declare encodings; no Encoding.Default in project scripts; guard catches missing declarations.')
