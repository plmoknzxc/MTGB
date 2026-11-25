from pathlib import Path
import json,base64,mimetypes,sys,hashlib
R=Path(__file__).resolve().parents[2];W=Path(__file__).parent
def uri(p):return 'data:'+ (mimetypes.guess_type(p.name)[0]or'application/octet-stream')+';base64,'+base64.b64encode(p.read_bytes()).decode()
raw=json.loads((R/'.tools/hand-disappear-1022/replay.json').read_text('utf8'))['frames'][280]['views'][0]
translations={}
for filename in ['zh-CN.txt','zh-CN.supplement.txt','zh-CN.review.txt']:
 for line in (R/'Assets/StreamingAssets/CardTranslations'/filename).read_text('utf8').splitlines():
  parts=line.split('|',3)
  if len(parts)!=4:continue
  old=translations.setdefault(parts[0],['','',''])
  for i,value in enumerate(parts[1:]):
   if value:old[i]=value
cards={};provenance=[]
def card(c):
 cid=str(c['id'])
 if cid in cards:return cid
 name,typ,rules=translations.get(c['name'],['','',''])
 if c['name']=='Food Token':name='食品';typ='衍生神器～食品';rules='{2}，{T}，牺牲此神器：你得到3点生命。'
 key=c.get('printingKey')or c.get('tokenArtKey');image=None;chosen=None
 if key:
  s,n=key.split('/',1)
  for prefix in ['CardImages/zh-CN','CardImages']:
   for ext in ['jpg','png','jpeg']:
    p=R/prefix/s/(n+'.'+ext)
    if p.exists():chosen=p;image=uri(p);break
   if image:break
 if not image and c['name']=='Food Token':
  chosen=R/'CardImages/TWOE/13.jpg';image=uri(chosen)
 assert image,(c['name'],key)
 cards[cid]=dict(id=cid,name=name or c['name'],english=c['name'],image=image,printing=key,owner=c.get('ownerSeat',1),tapped=bool(c.get('tapped')),land=bool(c.get('land')),creature=bool(c.get('creature')),type=typ or c.get('currentType',''),rules=(rules or c.get('currentRules','')).replace('\\n','\n'),cost=c.get('previewManaCost')or c.get('manaCost',''),printedCost=c.get('originalManaCost')or c.get('manaCost',''),power=c.get('power'),toughness=c.get('toughness'),zone=c.get('zone',''))
 provenance.append(dict(card=cid,name=c['name'],path=chosen.relative_to(R).as_posix(),sha256=hashlib.sha256(chosen.read_bytes()).hexdigest()))
 return cid
players=[]
for p in raw['players']:
 players.append(dict(seat=p['seat'],name='你'if p['seat']==1 else'对手',life=p['life'],handCount=p['handCount'],library=p['libraryCount'],battlefield=[card(c)for c in p['battlefield']],hand=[card(c)for c in p.get('hand',[])],graveyard=[card(c)for c in p.get('graveyardCards',[])],exile=[card(c)for c in p.get('exileCards',[])],mana=p['manaPool']))
stack=[]
for item in raw['stackItems']:
 cid=card(item['source']);stack.append(dict(id=str(item['id']),cardId=cid,owner=item['controllerSeat'],kind='咒语',name=cards[cid]['name'],targets=[str(t['stackId'])for t in item['targets']if t.get('kind')=='stack'],note='X = 2'if item['id']==46 else'反击目标非生物咒语'))
assets=dict(back=uri(R/'Assets/Resources/UI/CardBack.png'),background=uri(R/'Assets/Art/Battlefield/ObsidianSanctum.png'),mana={})
for p in (R/'Assets/Resources/UI/ManaSymbols').glob('Mana_*.png'):assets['mana'][p.stem.split('_')[1]]=uri(p)
data=dict(cards=cards,players=players,stack=stack,assets=assets,turn=raw['turn'],phase='战斗前主要阶段',priority=1,initialInspect=stack[0]['cardId'],history=['你施放'+cards[stack[1]['cardId']]['name']+'，X=2','对手施放'+cards[stack[0]['cardId']]['name'],'目标：'+cards[stack[1]['cardId']]['name']],source='用户录像第280帧；独立界面设计样本')
(W/'scene-data.js').write_text('window.MTGB_DATA='+json.dumps(data,ensure_ascii=False,separators=(',',':'))+';\n','utf8')
(W/'scene-summary.json').write_text(json.dumps({**data,'cards':{k:{a:b for a,b in v.items()if a!='image'}for k,v in cards.items()},'assets':list(assets)},ensure_ascii=False,indent=2),'utf8')
(W/'asset-provenance.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),'utf8')
(W/'baseline-seat.json').write_text(json.dumps(raw,ensure_ascii=False),'utf8')
# Retain approved OFL fonts, with a small local glyph subset for these prototypes.
sys.path.insert(0,str(R/'.tools/release-1021/python-deps'))
from fontTools import subset
fontDir=R/'Assets/Resources/UI/CardTemplates/Fonts';outFonts=W/'assets';outFonts.mkdir(exist_ok=True)
text=(W/'spec.md').read_text('utf8')+json.dumps(data,ensure_ascii=False)+''.join(chr(i)for i in range(32,127))+'战场详情手牌坟墓场放逐统帅区查看固定取消关闭轮到你响应让过优先权历史记录目标先结算后结算密集稀疏还原当前印刷费用蓝红自己对手力量防御力原始状态已横置阵列横排上方下方收起展开暂无释放箭头暗光衬线灰白金手'
for filename,out in [('RulesCN.otf','sans.otf'),('NameCN.otf','serif.otf')]:
 opts=subset.Options();font=subset.load_font(str(fontDir/filename),opts);sub=subset.Subsetter(options=opts);sub.populate(text=text);sub.subset(font);subset.save_font(font,str(outFonts/out),opts)
for filename in ['SourceHanSans-OFL.txt','SourceHanSerif-OFL.txt']:(outFonts/filename).write_bytes((fontDir/filename).read_bytes())
(W/'fonts.css').write_text("@font-face{font-family:GameSans;src:url('assets/sans.otf')}@font-face{font-family:GameSerif;src:url('assets/serif.otf')}html,body,button{font-family:GameSans,'Microsoft YaHei',sans-serif}*{box-sizing:border-box}", 'utf8')
print('Assets ready:',len(cards),'physical cards,',len(set(x['path']for x in provenance)),'unique original card images; data bytes', (W/'scene-data.js').stat().st_size)
