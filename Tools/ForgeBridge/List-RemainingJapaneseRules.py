"""Export the bounded Japanese rule-source gaps for human inspection."""
import collections
import csv
import io
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
reports=ROOT/'Tools/ForgeBridge/Reports'
review=json.loads((reports/'GathererJapaneseAllPrints.json').read_text(encoding='utf-8'))
statuses={'no-Japanese-record-returned-by-Gatherer-print-index':'有英文身份，日文印次索引未返回记录',
          'canonical-name-not-found-in-Gatherer-search':'搜索未返回准确英文身份'}
current=json.loads((reports/'LocalizedPresentationReviewed.json').read_text(encoding='utf-8'))
remaining={(r['key'],r['name']) for r in current['entries'] if r['language']=='ja-JP' and r['field']=='rules' and r['status']=='translation-unavailable-in-checked-sources'}
rows=[r for r in review['entries'] if (r['key'],r['name']) in remaining]
whisper=json.loads((reports/'WhisperJapaneseRemaining.json').read_text(encoding='utf-8')) if (reports/'WhisperJapaneseRemaining.json').exists() else {'entries':[]}
secondary={(r['key'],r['name']):r for r in whisper['entries']}
manual=json.loads((reports/'RemainingJapaneseReviewedRules.json').read_text(encoding='utf-8')) if (reports/'RemainingJapaneseReviewedRules.json').exists() else {'versionReviews':[]}
versions={(r['key'],r['name']):r for r in manual['versionReviews']}
for r in rows:
    identity=(r['key'],r['name']);other=secondary.get(identity,{})
    r['reviewConclusion']=statuses.get(r['status'],r['status'])
    if identity in versions:r['reviewConclusion']='日文卡图已找到，但与当前英文效果有版本差异：'+versions[identity]['reason']
    elif other.get('httpStatus')==403:r['reviewConclusion']='日文数据库请求返回 403，来源核查未完成；不能判定无译文'
    elif other.get('status')=='source-request-failed':r['reviewConclusion']='日文数据库因拒绝访问停止查询，尚未读取此页；不能判定无译文'
    elif other.get('status')=='exact-bilingual-identity-not-found':r['reviewConclusion']='Gatherer 与本次日文数据库查询均未返回准确记录；仍不能判定无译文'
catalog={r['key']:r for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines() if (r:=json.loads(line))}
sets={r['code']:r for r in json.loads((ROOT/'Assets/Resources/SetBrowser/Catalog.json').read_text(encoding='utf-8'))['entries']}
grouped=collections.defaultdict(list)
output=io.StringIO(newline='')
writer=csv.writer(output)
writer.writerow(['系列代号','系列名称','本地牌号','英文牌名或牌面名','类别','复核结论','搜索链接','备注'])
for r in rows:
    code=r['key'].split('/')[0]
    set_name=sets.get(code,{}).get('zh') or sets.get(code,{}).get('name') or code
    card=catalog.get(r['key'],{})
    faces=[f for f in card.get('faces',[]) if f['name']==r['name']]
    types=' / '.join(dict.fromkeys(t for f in faces for t in f['types']))
    note=''
    if ' // ' in r['name']:
        note='整牌合并名称记录；各半边可能已另有译文，不能按未译单独牌面计数。'
    grouped[code].append((r,types,note))
    writer.writerow([code,set_name,r['key'],r['name'],types,r['reviewConclusion'],r['search'],note])
(reports/'RemainingJapaneseRules.csv').write_text(output.getvalue(),encoding='utf-8-sig')
lines=['# 剩余日文效果来源清单','',
       f'当前清单共 **{len(rows)} 个名称／规则字段记录**，覆盖 **{len(grouped)} 个系列代号**。这是来源缺口，不代表这些牌没有日文版本；同牌不同面、整牌合并名称可能分别占记录。',
       '', 'WHO 两个分割整牌名称及其四个独立牌面已补齐并移出清单。来源访问失败、未返回身份和印刷版本差异分别列出，不统一称为没有译文。',
       '', '## 来源核查状态','']
counts=collections.Counter(r['reviewConclusion'].split('：')[0] for r in rows)
lines+=['| 状态 | 记录数 |','| --- | ---: |']+[f'| {s} | {n} |' for s,n in counts.items()]
lines+=[
       '', '## 按系列汇总','', '| 代号 | 本地系列名称 | 记录数 |','| --- | --- | ---: |']
for code,items in sorted(grouped.items(),key=lambda p:(-len(p[1]),p[0])):
    meta=sets.get(code,{})
    lines.append(f"| {code} | {meta.get('zh') or meta.get('name') or code} | {len(items)} |")
lines+=['','## 逐项清单','']
for code,items in sorted(grouped.items()):
    meta=sets.get(code,{})
    lines+=[f"### {code} · {meta.get('zh') or meta.get('name') or code}（{len(items)}）",'',
            '| 本地牌号 | 英文牌名／牌面名 | 类别 | 检查结论 |','| --- | --- | --- | --- |']
    for r,types,note in items:
        lines.append(f"| {r['key']} | {r['name'].replace('|','&#124;')} | {types} | {r['reviewConclusion']}{('；'+note) if note else ''} |")
    lines.append('')
(ROOT/'Docs/RemainingJapaneseRules.zh-CN.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'Exported {len(rows)} records in {len(grouped)} sets; merged-name caveats preserved.')
