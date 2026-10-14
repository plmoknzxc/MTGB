"""Bounded cost parser/card inventory. Structural samples never grant rule PASS."""
import json,re,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.tools/cost-interactions-1006';OUT.mkdir(parents=True,exist_ok=True)
source=ROOT/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/cost/Cost.java'
text=source.read_text(encoding='utf8')
tokens=re.findall(r'parse.startsWith\("([^"]+)"\)',text)
out={t:dict(count=0,samples=[]) for t in tokens}
for p in (ROOT/'ForgeRuntime/assets/res/cardsfolder').rglob('*.txt'):
    s=p.read_text(encoding='utf8')
    for t,entry in out.items():
        lines=[l for l in s.splitlines() if t in l and not l.startswith(('Oracle:','Name:'))]
        if not lines:continue
        entry['count']+=1
        if len(entry['samples'])<6:entry['samples'].append(dict(path=p.relative_to(ROOT).as_posix(),name=s.splitlines()[0],line=lines[0]))
result=dict(scope='Pinned Cost parser startsWith branches and all 33980 runtime card scripts; token occurrence is structural evidence only.',sourceHash=hashlib.sha256(source.read_bytes()).hexdigest(),tokens=out)
(OUT/'cost-token-samples.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
for token,entry in out.items():print(token,entry['count'],[(s['name'],s['line'][:125]) for s in entry['samples'][:2]])
