"""Enumerate pinned native cost identities and keep missing stack evidence visible.

Structural paths and their sample evidence are not an exhaustive card/combinations proof.
Reads the pinned source archive without importing or modifying any card data.
"""
from pathlib import Path
import hashlib,json,re,zipfile

ROOT=Path(__file__).resolve().parents[2]
RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/CastChoicePaths';OUT.mkdir(parents=True,exist_ok=True)
archive=RT/'source'/('forge-'+CFG['revision']+'.zip')
with zipfile.ZipFile(archive) as z:
 def source(suffix):
  matches=[n for n in z.namelist() if n.endswith(suffix)]
  assert len(matches)==1,(suffix,matches)
  return z.read(matches[0]).decode('utf8')
 optional=source('/game/spellability/OptionalCost.java')
 alternative=source('/game/spellability/AlternativeCost.java')
 action=source('/game/GameActionUtil.java')
optional_ids=re.findall(r'^\s+(\w+)\("',optional,re.M)
alternative_body=re.sub(r'//[^\n]*','',alternative.split('{',1)[1].split(';',1)[0])
alternative_ids=[s.strip() for s in alternative_body.split(',') if s.strip()]
assert all(re.fullmatch(r'\w+',s) for s in alternative_ids)
extra=action.split('public static SpellAbility addExtraKeywordCost',1)[1].split('public static Card createETBCountersEffect',1)[0]
keyword_ids=list(dict.fromkeys(re.findall(r'o\.(?:startsWith|equals)\("([^"\n]+)"\)',extra)+['Harmonize']))
samples=[]
for folder in ('ModeChoices','CastChoiceFamilies'):
 for p in sorted((ROOT/'Tools/Validation/Reports'/folder).glob('*-stack*.json')):
  # First-seat artifacts suffice for identity inventory; actual tests separately
  # assert both seats, amounts, payment, effects and priority.
  if p.name.endswith('-seat2.json'):continue
  v=json.loads(p.read_text(encoding='utf8'))
  for e in v.get('stackItems',[]):
   if e['kind']!='spell':continue
   for c in e.get('castingChoices',[]):
    samples.append(dict(kind=c['kind'],count=c['count'],input=p.relative_to(ROOT).as_posix(),inputSha256=hashlib.sha256(p.read_bytes()).hexdigest()))
rows=[]
for group,ids in [('optional',optional_ids),('alternative',alternative_ids),('keyword',keyword_ids)]:
 for kind in ids:
  found=[s for s in samples if s['kind'].casefold()==kind.casefold()]
  rows.append(dict(group=group,kind=kind,samples=found,status='有实际堆叠样本（结算／UI以独立记录为准）' if found else '本次堆叠专项缺少实际样本'))
other=['ordinary/Spree/Escalate/Entwine modal choices','repeatable/X modal choices','X amounts','alternate faces/split/fuse/adventure/omen/MDFC','Prototype','face-down casting','optional costs granted by another source','additional nonmana costs and reductions','casting through another effect/without paying mana cost']
payload=dict(scope='Pinned enum identities and extra-keyword branches only; not all casting meanings, cards, combinations or runtime validation.',revision=CFG['revision'],runtimeHash=CFG['runtimeHash'],sourcesSha256={k:hashlib.sha256(v.encode()).hexdigest() for k,v in dict(optional=optional,alternative=alternative,action=action).items()},rows=rows,otherPaths=other)
(OUT/'paths.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
lines=['# 施放选择路径清单','','固定版本的原生费用身份和追加关键词分支。此表只盘点结构与实际堆叠样本；样本存在不等于该机制全部效果或界面都已验证。','','| 来源 | 身份 | 本次堆叠专项 |','|---|---|---|']
for row in rows:lines.append('| '+row['group']+' | '+row['kind']+' | '+('有样本' if row['samples'] else '**待补实际样本**')+' |')
lines+=['','费用枚举外的路径也必须保留，不能仅查关键词：','']+['- '+s for s in other]
lines+=['','实际断言与三语言展示证据见 [本轮复核](../../../../../Docs/CastChoicePathAudit-2026-10-04.zh-CN.md)。','']
(OUT/'README.md').write_text('\n'.join(lines),encoding='utf8')
print(json.dumps(dict(optional=len(optional_ids),alternative=len(alternative_ids),keyword=len(keyword_ids),withStackSamples=sum(bool(r['samples']) for r in rows),missingSamples=sum(not r['samples'] for r in rows))))
