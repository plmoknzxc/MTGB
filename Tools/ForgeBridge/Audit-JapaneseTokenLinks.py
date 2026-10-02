"""Offline candidate audit of the local Japanese token-link corpus.

This inventories name coverage, not correctness of every translation or every
rendered hover. KeywordReferenceValidation exercises the actual Unity matcher.
"""
import json, re, collections, itertools
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ja={}
for filename in ['ja-JP.txt','ja-JP.supplement.txt']:
    for line in (ROOT/'Assets/StreamingAssets/CardTranslations'/filename).read_text(encoding='utf-8-sig').splitlines():
        f=line.split('|')
        if len(f)>=4:
            previous=ja.get(f[0],['','','',''])
            ja[f[0]]=[f[0]]+[f[i] or previous[i] for i in range(1,4)]
types={e['en']:e['ja'] for e in json.loads((ROOT/'Assets/Resources/TokenTypeAliases.json').read_text(encoding='utf8'))['entries'] if e.get('ja')}
all_aliases={e['en']:e.get('jaAliases',[e['ja']]) for e in json.loads((ROOT/'Assets/Resources/TokenTypeAliases.json').read_text(encoding='utf8'))['entries'] if e.get('ja')}
related=json.loads((ROOT/'Assets/Resources/RelatedTokenArt.json').read_text(encoding='utf8'))['entries']
byname={}
for entry in related:byname.setdefault(entry['name'],entry)
catalog={}
for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines():
    c=json.loads(line)
    if c['name'] in byname:catalog.setdefault(c['name'],c)
missing=[];checked=0;hits=0
def clauses(text):
    start=depth=0;quoted=False
    for i,c in enumerate(text):
        if c in '「『':depth+=1
        elif c in '」』':depth=max(0,depth-1)
        elif c=='"':quoted=not quoted
        if depth==0 and not quoted and c in '。.;；':
            yield text[start:i]
            start=i+1
    yield text[start:]
for name,e in byname.items():
    c=catalog.get(name)
    if not c:continue
    for face in c['faces']:
        rules=ja.get(face['name'],['','','',''])[3].replace('\\n','\n')
        # Release-note PDF line wrapping must not split a token name.
        rules=re.sub(r'(?<=[\u3000-\u9fff])\n(?=[\u3000-\u9fff])','',rules)
        if 'トークン' not in rules:continue
        checked+=1
        for token in e['tokens']:
            n=token['name']
            parts=n.split(' ')
            translation='・'.join(types[p] for p in parts) if all(p in types for p in parts) else n
            aliases={n,translation,ja.get(n,['',n])[1]}
            for token_face in n.split(' // '):
                aliases.update(all_aliases.get(token_face,[]))
                components=token_face.split(' ')
                if all(p in all_aliases for p in components):
                    aliases.update('・'.join(parts) for parts in itertools.product(*(all_aliases[p] for p in components)))
            found=any('トークン' in clause and any(a and a in clause for a in aliases) for clause in clauses(rules))
            if not found and ('以下から' in rules or '選ばれた色とタイプ' in rules):
                found=any(a and a in rules for a in aliases)
            if found:hits+=1
            else:missing.append(dict(card=name,face=face['name'],token=n,alias=translation,key=e['key'],rules=rules,english=face['text']))
for item in missing:
    token=item['token']; rules=item['rules']; english=item['english']
    if token.endswith(' Emblem'):item['reason']='emblem, not a token-name link'
    elif token in {item['card'],item['face']}:item['reason']='copy of source card; uses mechanic link'
    elif token=='Clue' and '調査' in rules:item['reason']='investigate mechanic link'
    elif token=='Copy' and any(k in rules for k in ['無尽','再演','準備済','代わりに','コピーする']):item['reason']='copy mechanic link, no token name'
    elif not re.search(re.escape(token),english,re.I):item['reason']='reference belongs to another face or type description'
    else:item['reason']='review'
out=dict(checkedFaces=checked,matchedReferences=hits,candidates=len(missing),reasons=dict(collections.Counter(x['reason'] for x in missing)),missing=missing)
(ROOT/'Tools/ForgeBridge/Reports/JapaneseTokenLinks.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in out.items() if k!='missing'},ensure_ascii=True))
