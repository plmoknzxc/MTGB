"""Presentation-only composition: exercise every battlefield lane, not game rules."""
from pathlib import Path
import json, shutil, hashlib, struct

W=Path(__file__).parent
R=W.parents[1]
source=(W/'scene-data.js').read_text(encoding='utf-8')
D=json.loads(source[source.index('=')+1:].strip().rstrip(';'))
targets={'Sol Ring','Rhystic Study','Jace, the Mind Sculptor','Teferi, Hero of Dominaria','Liliana of the Veil','Invasion of Zendikar','Invasion of Ikoria','Flickerwisp'}
found={}
for line in (R/'Assets/StreamingAssets/CardSearch.jsonl').open(encoding='utf-8'):
    c=json.loads(line)
    if c['name'] not in targets or c['name'] in found: continue
    for prefix in ['CardImages/zh-CN','CardImages']:
        for ext in ['jpg','png','jpeg']:
            p=R/prefix/(c['key']+'.'+ext)
            if p.is_file(): found[c['name']]=(c['key'],p);break
        if c['name'] in found: break
assert set(found)==targets
translations={}
for filename in ['zh-CN.txt','zh-CN.supplement.txt','zh-CN.review.txt']:
    for line in (R/'Assets/StreamingAssets/CardTranslations'/filename).open(encoding='utf-8'):
        p=line.rstrip('\n').split('|',3)
        if len(p)!=4 or p[0] not in targets|{'Lost Mine of Phandelver'}:continue
        old=translations.setdefault(p[0],['','',''])
        for i,v in enumerate(p[1:]):
            if v:old[i]=v

extra={};provenance=[]
for i,(name,(key,path)) in enumerate(found.items()):
    dest=W/'assets'/('full-'+str(i)+path.suffix)
    shutil.copyfile(path,dest)
    t=translations.get(name,[name,'',''])
    extra[name]=dict(name=t[0],english=name,type=t[1],rules=t[2].replace('\\n','\n'),image='assets/'+dest.name,printing=key,cost='',printedCost='',power=None,toughness=None)
    provenance.append(dict(name=name,printing=key,source=path.relative_to(R).as_posix(),asset=dest.relative_to(W).as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
p=R/'CardImages/zh-CN/TAFR/21.png'
shutil.copyfile(p,W/'assets/full-dungeon.png')
t=translations['Lost Mine of Phandelver']
extra['dungeon']=dict(name=t[0],english='Lost Mine of Phandelver',type='地城',rules=t[2].replace('\\n','\n'),image='assets/full-dungeon.png',printing='TAFR/21',cost='',printedCost='',power=None,toughness=None)
provenance.append(dict(name='Lost Mine of Phandelver',printing='TAFR/21',source=p.relative_to(R).as_posix(),asset='assets/full-dungeon.png',sha256=hashlib.sha256(p.read_bytes()).hexdigest(),verification='Viewed TAFR/20,21,22; selected the actual Lost Mine image.'))
shutil.copyfile(R/'Assets/Resources/UI/PlayerCounters/ForgeIcons.png',W/'assets/player-counters.png')
atlas=(W/'assets/player-counters.png').read_bytes()
atlas_size=struct.unpack('>II',atlas[16:24])
icon_index=json.loads((R/'Assets/Resources/UI/PlayerCounters/Index.json').read_text(encoding='utf-8'))['entries']

cards={};lanes={};groups={};uid=0
def make(source,seat,**overrides):
    global uid
    uid+=1
    key='layout-'+str(uid)
    c=dict(extra[source]) if source in extra else {'sourceId':source}
    c.update(id=key,owner=seat,zone='Battlefield',land=False,creature=False,tapped=False)
    c.update(overrides)
    cards[key]=c
    return key
for seat,prefix in [(2,'enemy'),(1,'own')]:
    p=D['players'][seat-1]
    lanes[prefix+'-lands']=[make(i,seat,land=True,tapped=D['cards'][i]['tapped']) for i in p['battlefield'] if D['cards'][i]['land']]
    creature_sources=['103','125','107','97'] if seat==2 else ['19','97','16','125']
    lanes[prefix+'-creatures']=[make(i,seat,creature=True,power=D['cards'][i]['power'],toughness=D['cards'][i]['toughness'],sick=(seat==1 and n==2)) for n,i in enumerate(creature_sources)]
    lanes[prefix+'-support']=[make('Sol Ring',seat),make('Rhystic Study',seat),make('152',seat)]
    lanes[prefix+'-walkers']=[make('Jace, the Mind Sculptor' if seat==2 else 'Liliana of the Veil',seat,loyalty=4 if seat==2 else 3)]
    lanes[prefix+'-battles']=[make('Invasion of Zendikar' if seat==2 else 'Invasion of Ikoria',seat,defense=3 if seat==2 else 6)]
    command=make('Teferi, Hero of Dominaria',seat,zone='Command',type='统帅区 · '+extra['Teferi, Hero of Dominaria']['type'])
    emblem=make('Teferi, Hero of Dominaria',seat,name='徽记 — 泰菲力',type='徽记 · 来源：多明纳里亚英雄泰菲力',cost='',printedCost='',rules='每当你抓一张牌时，放逐目标由对手操控的永久物。')
    delay1=make('Flickerwisp',seat,name='明灭翔灵 — 延迟返场',type='延迟效果',cost='',printedCost='',rules='在下一个结束步骤开始时，将该牌在其拥有者的操控下移回战场。')
    delay2=make('Teferi, Hero of Dominaria',seat,name='泰菲力 — 延迟重置',type='延迟效果',cost='',printedCost='',rules='在下一个结束步骤开始时，重置至多两个地。')
    monarch=make('Teferi, Hero of Dominaria',seat,name='君主',type='持续状态',image=None,cost='',printedCost='',rules='在你的结束步骤开始时，抓一张牌。\n每当一个生物对你造成战斗伤害时，其操控者成为君主。')
    dungeon=make('dungeon',seat,type='地城 · 当前房间：黑暗之池',rules='当前房间：黑暗之池\n\n'+extra['dungeon']['rules'])
    groups[str(seat)]={'command':[command],'emblems':[emblem],'delayed':[delay1,delay2],'states':[monarch],'dungeons':[dungeon]}
F=dict(cards=cards,lanes=lanes,groups=groups,icons=icon_index,atlasSize=atlas_size,resources={
    '2':{'counters':[['POISON','中毒',2],['ENERGY','能量',8],['EXPERIENCE','经验',3],['RAD','辐射',1],['TICKET','票券',4]],'mana':[['W',2],['U',8],['B',5],['R',3],['G',6],['C',4]]},
    '1':{'counters':[['POISON','中毒',1],['ENERGY','能量',6],['EXPERIENCE','经验',2],['RAD','辐射',3],['TICKET','票券',5]],'mana':[['W',12],['U',8],['B',5],['R',23],['G',6],['C',10]]}},
    source='组合展示数据：沿用录像卡图及本地资料，主动组合全部区域；不声称是真实对局或规则效果验证。')
(W/'full-layout-data.js').write_text('window.MTGB_LAYOUT='+json.dumps(F,ensure_ascii=False,separators=(',',':'))+';\nfor(const [id,c] of Object.entries(window.MTGB_LAYOUT.cards)){window.MTGB_DATA.cards[id]={...(c.sourceId?window.MTGB_DATA.cards[c.sourceId]:{}),...c};}\n',encoding='utf-8')
(W/'full-layout-provenance.json').write_text(json.dumps({'source':F['source'],'assets':provenance,'icons':'Assets/Resources/UI/PlayerCounters/ForgeIcons.png + Index.json'},ensure_ascii=False,indent=2),encoding='utf-8')

s=(W/'04-tabbed-sidebar.html').read_text(encoding='utf-8')
s=s.replace('<title>MTGB · 详情与历史</title>','<title>MTGB · 完整区域布局</title>')
s=s.replace('<small>牌桌</small>','<small>全区域演示</small>')
s=s.replace('<script src="scene-data.js"></script>','<script src="scene-data.js"></script><script src="full-layout-data.js"></script>')
css=r'''
/* E: reserved regions for both complete battlefields. Never borrow empty lanes for HUD. */
.arena-world{left:354px;top:70px;width:878px;height:604px}.arena-ground{border-radius:40px 40px 22px 22px}.arena-ground::before{border-radius:31px 31px 15px 15px}
.private-hand{left:1080px;top:15px}.private-hand img{width:29px;height:41px}.private-hand span{font-size:11px}
.player{left:370px!important;width:254px!important;height:72px!important;padding:4px 9px;grid-template-columns:42px minmax(0,1fr) 60px;grid-template-rows:23px 21px 18px;column-gap:8px;row-gap:0;border:0;background:#15232cde!important;box-shadow:inset 0 1px #a9bbc62e,0 4px 8px #0005}
.player.opponent{top:70px}.player.you{top:605px}.player::after{left:9px;right:9px;height:1px}.player .monogram{width:40px;height:40px;font-size:16px}.player .player-name{font-size:16px;line-height:22px}.player .life{font-size:50px;line-height:47px}.player .life::after{display:none}.player .seat-state{font-size:11px;line-height:18px}.player .zones{padding:0;border:0;gap:8px}.player .zone-link{font-size:11px;gap:5px;padding:0}.player .zone-link b{font-size:13px;min-width:13px}
.seat-resources{position:absolute;left:642px;width:574px;height:64px;display:flex;flex-direction:column;justify-content:center;gap:8px}.seat-resources.enemy{top:73px}.seat-resources.own{top:610px}
.resource-row{display:flex;align-items:center;gap:11px;white-space:nowrap}.resource-caption{color:#9babb6;font-size:11px;width:49px;flex:none}.counter-chip{display:flex;align-items:center;gap:4px;font-size:11px;color:#b1bdc4}.counter-chip b{font-family:GameSerif,serif;font-size:17px;color:#ecebe0;font-weight:400}.counter-symbol{display:block;width:20px;flex:none;background-image:url('assets/player-counters.png');background-repeat:no-repeat}
.mana-pool{display:flex;gap:15px}.pool-item{display:flex;align-items:center;gap:5px;width:48px;flex:0 0 48px;font:19px GameSerif,serif;color:#eee9d9}.pool-item img{width:20px;height:20px}.pool-item .generic{display:grid;place-items:center;width:20px;height:20px;border:1px solid #a9ada9;border-radius:50%;font:12px GameSans,sans-serif}
.field-lane{position:absolute;overflow:visible;z-index:1}.field-lane h3{font:12px GameSans,sans-serif;letter-spacing:1px;font-weight:400;color:#b8c2c6;margin:0 0 6px;display:flex;gap:9px;align-items:center;height:14px}.field-lane h3 span{color:#8395a3;font-size:11px;letter-spacing:0}
.lane-track{height:calc(100% - 20px);display:flex;gap:9px;overflow-x:auto;overflow-y:hidden;scrollbar-width:none;padding:1px 2px 0}.lane-track::-webkit-scrollbar{display:none}.lane-next,.lane-previous{position:absolute;right:0;top:-4px;z-index:3;width:19px;height:18px;padding:0;background:#1a303b;border:1px solid #6a879a66;border-radius:3px;font-size:17px;line-height:14px;color:#b5c7d6}.lane-previous{right:24px}.lane-next[hidden],.lane-previous[hidden]{display:none}.lane-next:disabled,.lane-previous:disabled{opacity:.35}
.main-land{left:370px;width:558px;height:70px}.enemy-land-lane{top:149px}.own-land-lane{top:532px}
.main-creatures{left:370px;width:558px;height:102px}.enemy-creature-lane{top:225px}.own-creature-lane{top:423px}
.support-lane{left:960px;width:254px;height:70px}.enemy-support-lane{top:149px}.own-support-lane{top:532px}
.walker-lane{left:960px;width:112px;height:102px}.battle-lane{left:1101px;width:112px;height:102px}.enemy-walker-lane,.enemy-battle-lane{top:225px}.own-walker-lane,.own-battle-lane{top:423px}
.lane-card{position:relative;flex:none;width:86px;height:80px;padding:0;text-align:left;border:0;background:none}.lane-card .tile-face{display:block;width:86px;height:60px;overflow:hidden;border:1px solid #b3c1c66b;border-radius:3px;background:#17242a;box-shadow:0 3px 5px #0008}.lane-card .tile-face img{display:block;width:100%;height:auto;transform:translateY(-11%);filter:brightness(.94)}
.lane-card .tile-name{display:block;font-size:11px;line-height:17px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:#d1d9dc;text-shadow:0 1px #000}.lane-card:hover .tile-face,.lane-card:focus-visible .tile-face{border-color:#d7d9cc}.lane-card.picked .tile-face{outline:1px solid #97b7d2;outline-offset:2px}
.lane-card .tile-value{position:absolute;right:0;top:44px;font:13px/16px GameSerif,serif;padding:0 4px;background:#dddccf;color:#16232a;border-radius:2px;box-shadow:0 2px 3px #0008}.lane-card .sick{position:absolute;left:2px;top:2px;background:#213247e8;color:#cddae9;font-size:10px;padding:1px 4px;border:1px solid #8298b975}
.lane-card.land-tile,.lane-card.support-tile{width:76px;height:51px}.land-tile .tile-face,.support-tile .tile-face{width:76px;height:34px}.land-tile .tile-face img{transform:translateY(-19%)}.land-tile.tapped .tile-face{opacity:.65}.land-tile.tapped::after{content:'↷';position:absolute;top:12px;right:3px;color:#e8e4d4;font-size:20px;text-shadow:0 1px 2px #000}
.special-tile{width:82px}.special-tile .tile-face{width:82px}.special-tile .tile-value{font:11px/17px GameSans,sans-serif;top:43px}
.state-strip{position:absolute;left:370px;width:844px;height:35px;display:flex;gap:7px;z-index:1}.state-strip.enemy{top:338px}.state-strip.own{top:381px}.state-chip{display:flex;align-items:center;gap:7px;min-width:0;flex:1;padding:3px 6px;text-align:left;border:1px solid #748b9a35;background:#16242dd6;border-radius:3px;box-shadow:0 2px 4px #0004}.state-strip.enemy .state-chip{border-top-color:#c878827a}.state-strip.own .state-chip{border-top-color:#75a8d386}.state-chip img{width:21px;height:28px;object-fit:cover;border-radius:2px;flex:none}.state-chip .state-label{min-width:0;display:flex;flex-direction:column;gap:2px}.state-chip b{font-weight:400;color:#dee2dd;font-size:12px;white-space:nowrap}.state-chip small{font-size:9px;color:#93a6b5;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.state-chip:hover{background:#29404f}
.state-owner{width:35px;flex:none;font-size:10px;line-height:16px;color:#e3a2ad;letter-spacing:1px}.state-strip.own .state-owner{color:#9dc7e8}
.center-seam{left:370px;right:386px;top:377px;background:linear-gradient(90deg,transparent,#b9c6b85c 25%,#b9c6b85c 75%,transparent)}
.global-state{position:absolute;left:1261px;top:623px;display:flex;gap:12px}.global-state button{font-size:12px;color:#c5c4af;padding:5px 8px;border:1px solid #8c946c45;background:#252a247d}
.dock{left:354px;bottom:20px;height:200px}.hand-region{left:13px;top:8px;width:calc(100% - 26px);height:187px}.hand-heading{justify-content:flex-start}.hand-heading .hint{margin-left:auto}.hand-line{padding-top:8px;gap:20px;height:174px;overflow-y:hidden}.hand-card{width:111px;flex-basis:111px}.hand-card>img{width:111px;height:155px}.hand-card .mana img{width:17px;height:17px;filter:none;box-shadow:none}.hand-card:hover{transform:translateY(-5px) scale(1.045)}
.reader-photo[hidden]{display:none}.status-standin{display:grid;place-content:center;width:105px;height:147px;border:1px solid #748da344;background:#213648;font:18px GameSerif,serif;color:#d5e0e8;padding:12px;line-height:1.6}
'''
s=s.replace('</style>',css+'\n</style>',1)
start=s.index('<div class="private-hand"')
end=s.index('<aside class="stack-area"',start)
parts=['<div class="private-hand" id="private-hand" aria-label="对手隐藏手牌"></div>']
for prefix,seat in [('enemy',2),('own',1)]:
    side='对手' if seat==2 else '你的'
    parts.append(f'<div class="seat-resources {prefix}" id="resources-{seat}" aria-label="{side}玩家指示物与法术力"></div>')
    for kind,caption,classes in [('lands','地','main-land'),('creatures','生物','main-creatures'),('support','神器 / 结界','support-lane'),('walkers','旅法','walker-lane'),('battles','战役','battle-lane')]:
        cls={'lands':'land','creatures':'creature','support':'support','walkers':'walker','battles':'battle'}[kind]
        track=prefix+'-'+kind
        parts.append(f'<section class="field-lane {classes} {prefix}-{cls}-lane" aria-label="{side}{caption}"><h3>{caption}<span id="{track}-count"></span></h3><div class="lane-track" id="{track}"></div><button class="lane-previous" data-scroll="{track}" data-scroll-direction="-1" aria-label="向左浏览{side}{caption}" hidden>‹</button><button class="lane-next" data-scroll="{track}" data-scroll-direction="1" aria-label="向右浏览{side}{caption}" hidden>›</button></section>')
    parts.append(f'<section class="state-strip {prefix}" id="states-{seat}" aria-label="{side}统帅区、徽记、延迟效果、持续状态和地城"></section>')
parts.append('<div class="center-seam"></div><div class="global-state"><button data-action="global-state">白昼</button></div>')
s=s[:start]+''.join(parts)+'\n'+s[end:]
start=s.index('function renderBoard(){')
end=s.index('renderBoard();',start)+len('renderBoard();')
js=r'''
const L=window.MTGB_LAYOUT;
function refreshLaneArrows(){document.querySelectorAll('.lane-track').forEach(track=>{document.querySelectorAll('[data-scroll="'+track.id+'"]').forEach(b=>{b.hidden=track.scrollWidth<=track.clientWidth+2;b.disabled=Number(b.dataset.scrollDirection)<0?track.scrollLeft<=2:track.scrollLeft>=track.scrollWidth-track.clientWidth-2;});});}
function layoutCard(id,lane){const c=cards[id],land=lane.endsWith('lands'),support=lane.endsWith('support'),special=lane.endsWith('walkers')||lane.endsWith('battles'),value=c.creature?c.power+'/'+c.toughness:c.loyalty!==undefined?'忠诚 '+c.loyalty:c.defense!==undefined?'防御 '+c.defense:'';return `<button class="lane-card ${land?'land-tile':''} ${support?'support-tile':''} ${special?'special-tile':''} ${c.tapped?'tapped':''}" data-card-id="${id}" data-zone="battlefield" data-seat="${c.owner}" aria-label="${c.owner===1?'你的':'对手的'}${esc(c.name)}${value?'，'+value:''}${c.sick?'，召唤失调':''}"><span class="tile-face"><img src="${c.image}" alt="${esc(c.name)}"></span>${value?`<span class="tile-value">${value}</span>`:''}${c.sick?'<span class="sick">失调</span>':''}<span class="tile-name">${esc(c.name)}</span></button>`;}
function renderBoard(){for(const [lane,base] of Object.entries(L.lanes)){const ids=[...base];if(dense)for(let n=1;n<4;n++)for(const id of base){const copy=id+'-dense-'+n;cards[copy]={...cards[id],id:copy};ids.push(copy);}document.getElementById(lane).innerHTML=ids.map(id=>layoutCard(id,lane)).join('');document.getElementById(lane).onscroll=refreshLaneArrows;document.getElementById(lane+'-count').textContent=ids.length;}$('#table').classList.toggle('dense',dense);requestAnimationFrame(refreshLaneArrows);}
function renderResources(seat){const r=L.resources[seat],icon=kind=>{const i=L.icons.find(x=>x.kind===kind);return `<span class="counter-symbol" style="height:${i.height/2}px;background-size:${L.atlasSize[0]/2}px ${L.atlasSize[1]/2}px;background-position:${-i.x/2}px ${-i.y/2}px"></span>`;};document.getElementById('resources-'+seat).innerHTML=`<div class="resource-row" aria-label="玩家指示物"><span class="resource-caption">指示物</span>${r.counters.map(([kind,name,count])=>`<span class="counter-chip" title="${name}指示物 ${count}" aria-label="${name}指示物 ${count}">${icon(kind)}<span>${name}</span><b>${count}</b></span>`).join('')}</div><div class="resource-row" aria-label="法术力池"><span class="resource-caption">法术力</span><div class="mana-pool">${r.mana.map(([symbol,count])=>`<span class="pool-item" aria-label="${symbol}法术力 ${count}">${D.assets.mana[symbol]?`<img src="${D.assets.mana[symbol]}" alt="${symbol}">`:'<span class="generic">C</span>'}<span>${count}</span></span>`).join('')}</div></div>`;}
for(const seat of ['1','2']){renderResources(seat);document.getElementById('states-'+seat).innerHTML=`<span class="state-owner">${seat==='1'?'己方':'对手'}<br>状态</span>`+[['command','统帅区','可查看统帅'],['emblems','徽记','泰菲力'],['delayed','延迟效果','下一结束步骤'],['states','持续状态','君主'],['dungeons','地城','黑暗之池']].map(([group,name,status])=>{const ids=L.groups[seat][group],c=cards[ids[0]];return `<button class="state-chip" data-action="layout-group" data-group="${group}" data-seat="${seat}" aria-label="查看${seat==='1'?'你的':'对手的'}${name}${ids.length}">${c.image?`<img src="${c.image}" alt="">`:''}<span class="state-label"><b>${name} ${ids.length}</b><small>${status}</small></span></button>`;}).join('');}
function openLayoutGroup(seat,group){const ids=L.groups[seat][group],names={command:'统帅区',emblems:'徽记',delayed:'延迟效果',states:'持续状态',dungeons:'地城'};$('#browser').hidden=false;$('#browser-title').textContent=(seat==='1'?'你的':'对手的')+names[group]+' · '+ids.length;$('#browser-list').className='drawer-list';$('#browser-list').innerHTML=ids.map(id=>`<button class="browse-card" data-card-id="${id}" aria-label="查看${esc(cards[id].name)}">${cards[id].image?`<img src="${cards[id].image}" alt="${esc(cards[id].name)}">`:`<span class="status-standin">${esc(cards[id].name)}</span>`}<span>${esc(cards[id].name)}</span></button>`).join('');}
addEventListener('resize',()=>requestAnimationFrame(refreshLaneArrows));
renderBoard();
'''
s=s[:start]+js+s[end:]
s=s.replace("$('#reader-image').src=c.image;$('#reader-image').alt=c.name;", "$('.reader-photo').hidden=!c.image;if(c.image){$('#reader-image').src=c.image;$('#reader-image').alt=c.name;}")
s=s.replace("document.querySelectorAll('.hand-card').forEach(el=>el.classList.toggle('picked',el.dataset.cardId===id));", "document.querySelectorAll('.hand-card,.lane-card').forEach(el=>el.classList.toggle('picked',el.dataset.cardId===id));")
s=s.replace("scrollBy({left:400,behavior:'smooth'})", "scrollBy({left:400*(Number(el.dataset.scrollDirection)||1),behavior:'auto'})")
s=s.replace("else if(action==='graveyard'||action==='exile')", "else if(action==='layout-group')openLayoutGroup(el.dataset.seat,el.dataset.group);\nelse if(action==='global-state'){selectInfoTab('details');if(pinned)togglePin();$('#reader').hidden=false;$('.reader-photo').hidden=true;$('#reader-title').textContent=el.textContent;$('#reader-type').textContent='公共对局状态';$('#reader-cost').innerHTML='';$('#reader-cost-change').style.display='none';$('#reader-rules').textContent=el.textContent==='白昼'?'当前为白昼。':'公共全局效果在这里集中查看。';}\nelse if(action==='graveyard'||action==='exile')")
(W/'05-full-battlefield.html').write_text(s,encoding='utf-8')
print('Built full layout:',len(cards),'display objects,',len(lanes),'battlefield lanes, 2 resource rows and 2 auxiliary state strips.')
