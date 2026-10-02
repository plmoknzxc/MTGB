"""Reviewed printed-image text and exact existing-text identity repairs.

Only manually read, semantically aligned image blocks are imported. Four
printed/current-version differences remain explicit review records; Turn Stones
is resolved using the publisher's Japanese release notes, not its printed image.
"""
import hashlib,importlib.util,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
REPORTS=ROOT/'Tools/ForgeBridge/Reports'
FOLDER=ROOT/'Assets/StreamingAssets/CardTranslations'
CATALOG={r['key']:r for l in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines() if (r:=json.loads(l))}
CONTEXT={(x['gap']['key'],x['gap']['name']):x['englishRules'] for x in json.loads((ROOT/'.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))}
images={
 ('SOC/45',"Vandal's Edit"):('盗人による編集','インスタント','カード２枚を引く。各プレイヤーはそれぞれ２点のライフを失う。','{1}{W}{B}'),
 ('SOC/48',"Maestro's Gift"):('巨匠の贈り物','ソーサリー','あなたがコントロールしているクリーチャー１体を対象とする。それのコピーであるトークン１体を生成する。ターン終了時まで、そのトークンは速攻を得る。','{3}{U}{R}'),
 ('SOC/49','Restore Relic'):('秘宝の修復','ソーサリー','あなたの墓地にありアーティファクトやクリーチャーであるカード１枚を対象とする。それを追放する。それのコピーであるトークン１つを生成する。','{2}{R}{W}'),
 ('SOC/50','Run the Play'):('作戦通り','ソーサリー','クリーチャー最大Ｘ体を対象とする。それらの上にそれぞれ＋１/＋１カウンター１個を置く。ターン終了時まで、それらのクリーチャーは飛行を得る。カード１枚を引く。','{X}{G}{U}'),
 ('PSDG/2',"Ashuza's Breath"):('アシュザーのブレス','ソーサリー','すべてのクリーチャーについて、それぞれ、０から２までの間の数を１つ、無作為に選ぶ。アシュザーのブレスは、そのクリーチャーに選ばれた数に等しいダメージを与える。','{1}{R}'),
 ('PSDG/3','Camato Scout'):('カマローの斥候','クリーチャー — マーフォーク','カマローの斥候が場に出るに際し、基本地形タイプを１つ無作為に選ぶ。\nカマローの斥候は選ばれたタイプの渡りを持つ。（防御側プレイヤーが選ばれたタイプの土地をコントロールしている限り、このクリーチャーはブロックされない。）','{1}{U}{U}'),
 ('PSDG/4',"Hapato's Might"):('ハパトゥの力','インスタント','対象の、１体のクリーチャーはターン終了時まで＋Ｘ/＋０の修正を受ける。Ｘは０から６までの間から無作為に選ばれた数である。','{2}{B}'),
 ('PSDG/5','Lydari Druid'):('レイダルのドルイド','クリーチャー — ドルイド','レイダルのドルイドが場に出たとき、すべての土地について、それぞれ基本地形を１種類、無作為に選ぶ。それらの土地は選ばれた基本地形になる。（この効果はターン終了時に終わらない。）','{2}{G}'),
 ('PSDG/6','Lydari Elephant'):('レイダルの象','クリーチャー — 象','レイダルの象が場に出るに際し、３から７までの間の数を２つ、無作為に選ぶ。\nレイダルの象のパワーは１番目に選ばれた数に等しく、レイダルの象のタフネスは２番目に選ばれた数に等しい。','{4}{G}'),
 ('PSDG/7','Murgish Cemetery'):('マーギッシュの霊園','エンチャント','{3}{B}, あなたの手札からカードを一枚捨てる：Ｘ/Ｘの黒のゾンビ・クリーチャー・トークンを１個場に出す。Ｘは２から６までの間から無作為に選ばれた数である。','{4}{B}{B}'),
}
records=[];rows={}
source=json.loads((ROOT/'.tools/gatherer-ja/remaining-ja-identity-candidates.json').read_text(encoding='utf-8'))
prints={r['key'].upper():r['cards'][0] for r in source['exactPrintingMatches']}
for (key,name),(title,typ,rules,cost) in images.items():
 card=CATALOG[key];face=next(f for f in card['faces'] if f['name']==name)
 assert face['manaCost']==cost
 sc=prints[key]
 assert sc['lang']=='ja' and sc['set'].upper()+'/'+sc['collector_number']==key
 sf=next(f for f in sc.get('card_faces') or [sc] if f['name']==name)
 assert sf['mana_cost']==cost and sf['oracle_text']==CONTEXT[(key,name)]
 for stat in ('power','toughness'):
  if face.get(stat):assert sf[stat]==face[stat]
 image=ROOT/'CardImages/ja-JP'/(key+'.jpg')
 assert image.exists()
 record=dict(key=key,name=name,status='published-Japanese-image-rules-imported',printedName=title,printedType=typ,printedRules=rules,
  source=sc['scryfall_uri'],sourceImage=sc['image_uris']['normal'],imagePath=str(image.relative_to(ROOT)),imageSha256=hashlib.sha256(image.read_bytes()).hexdigest(),
  englishRules=CONTEXT[(key,name)],validation='Exact Japanese printing and English face identity, costs and stats checked; manually read image text and compared every condition, amount, target, timing, destination and token characteristic. Historical wording retained.')
 records.append(record);rows[name]=[name,title,typ,rules]

# The Japanese image omits green. The publisher's Japanese release notes
# explicitly say black and green, matching both current Oracle and Forge.
verification=json.loads((REPORTS/'JapaneseFiveVersionVerification.json').read_text(encoding='utf-8'))
resolved=next(r for r in verification['entries'] if r['name']=='Turn Stones')
assert resolved['conclusion']=='official-Japanese-release-notes-resolve-image-discrepancy'
key='SOC/46';name='Turn Stones'
face=next(f for f in CATALOG[key]['faces'] if f['name']==name)
assert face['text']==resolved['currentEnglishRules'] and face['manaCost']=='{B}{G}'
rules=resolved['publishedJapaneseRules']
rows[name]=[name,'石の裏返し','ソーサリー',rules]
records.append(dict(key=key,name=name,status='official-Japanese-release-notes-rules-imported',
 source=resolved['officialSources'][0],sourceSha256=resolved['officialSourceSha256'],sourcePage=88,
 printedRules=rules,englishRules=face['text'],
 validation='Publisher Japanese release notes specify black and green, matching English release notes and current Forge token script bg_1_1_pest_lifegain. Incorrect black-only image text is not reused.'))

# The whole split-card identity displays its first area, with the second area
# rendered separately. Its already verified front translation was omitted
# only because the root name contains both halves.
existing={r[0]:r for line in (FOLDER/'ja-JP.gatherer-prints.txt').read_text(encoding='utf-8').splitlines() if len(r:=line.split('|',3))==4}
for key in ('WHO/131','WHO/682'):
 card=CATALOG[key];front,back=card['faces']
 assert CONTEXT[(key,card['name'])]==front['text'].replace('\\n','\n')
 a,b=existing[front['name']],existing[back['name']]
 rows[card['name']]=[card['name'],a[1]+' // '+b[1],a[2] or b[2],a[3].replace('\\n','\n')]
 records.append(dict(key=key,name=card['name'],status='verified-existing-face-rule-reused',source='ja-JP.gatherer-prints.txt',
  printedRules=rows[card['name']][3],validation='Whole-card main rule equals first face exactly; reuse verified official first-face rule. Second face remains separate, no combined duplicate rules.'))

key='HBG/A-85';name="A-Tyr's Blesing";source_name="Tyr's Blessing"
plain=next(c for c in CATALOG.values() if any(f['name']==source_name for f in c['faces']))
plain_face=next(f for f in plain['faces'] if f['name']==source_name)
spec=importlib.util.spec_from_file_location('g',Path(__file__).with_name('Import-GathererJapaneseSupplements.py'))
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
assert base.comparable(plain_face['text'].replace('\\n','\n'),source_name)==base.comparable(CONTEXT[(key,name)],name)
published=next(line.split('|',3) for line in (FOLDER/'ja-JP.supplement.txt').read_text(encoding='utf-8').splitlines() if line.startswith(source_name+'|'))
rows[name]=[name,re.sub(r'（[ぁ-ゖ]+）','',published[1]),published[2],published[3].replace('\\n','\n')]
records.append(dict(key=key,name=name,status='verified-existing-face-rule-reused',source='ja-JP.supplement.txt: Tyr\'s Blessing / MTGJSON published Japanese foreign text',
 printedRules=rows[name][3],validation='Local spelling Blesing versus Blessing; same Hippogriff Adventure identity and identical English effect. Rebalanced mana cost remains supplied by canonical engine data; reuse only unchanged published rule/name/type.'))

version_reviews=[dict(key=r['key'],name=r['name'],status=r['conclusion'],reason=r['reason'],officialChangeConfirmed=r['officialChangeConfirmed'])
 for r in verification['entries'] if r['name']!='Turn Stones']
dest=FOLDER/'ja-JP.remaining-reviewed.txt'
dest.write_text('\n'.join('|'.join(s.replace('|','｜').replace('\n','\\n') for s in row) for _,row in sorted(rows.items()))+'\n',encoding='utf-8')
(REPORTS/'RemainingJapaneseReviewedRules.json').write_text(json.dumps(dict(complete=True,entries=records,versionReviews=version_reviews),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Imported',len(records),'reviewed records;',len(version_reviews),'printed/current differences retained for review.')
