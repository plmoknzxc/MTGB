from pathlib import Path
import json
W=Path(__file__).parent
p=W/'index.html'
s=p.read_text(encoding='utf-8')
gallery_ready='data-variant="5"' in s
s=s.replace('data-variant="4" aria-selected="true"','data-variant="4" aria-selected="false"')
s=s.replace('E　完整区域</button></nav>','E　完整区域</button><button data-variant="5" aria-selected="true">F　Unity战场＋新UI</button></nav>')
s=s.replace('href="05-full-battlefield.html"','href="06-native-hybrid.html"').replace('src="05-full-battlefield.html" title="完整区域交互预览"','src="06-native-hybrid.html" title="Unity战场与新UI交互预览"')
s=s.replace('<div class="original">','<button data-preview="5"><span class="tag">F · NATIVE UNITY + NEW UI</span><img src="preview-f.png" alt="Unity原战场与新UI"><strong>Unity战场与新UI</strong><p>实际Unity组合场面截图保留战场、玩家、指示物、法术力、徽记与地城；叠加新横排手牌、阶段操作、堆叠及详情／历史。原生截图区域为静态，新增UI可交互。</p></button><div class="original">',1)
s=s.replace('E 是主动组合全部区域的布局示例。','E 是主动组合全部区域的布局示例，F 用同一组合数据经Unity实际渲染。')
s=s.replace("{file:'05-full-battlefield.html',name:'完整区域'}]","{file:'05-full-battlefield.html',name:'完整区域'},{file:'06-native-hybrid.html',name:'Unity战场与新UI'}]")
s=s.replace("let current=4,","let current=5,")
s=s.replace("document.querySelector('#dense').disabled=next!=='interact';","document.querySelector('#dense').disabled=next!=='interact'||current===5;document.querySelector('#dense').textContent=current===5?'固定Unity底图':'拥挤演示';")
s=s.replace("current===4?'组合展示场面 · 尚未替换客户端':'交互原型，尚未替换客户端'","current===5?'Unity实际截图＋新UI交互预览 · 未替换客户端':current===4?'组合展示场面 · 尚未替换客户端':'交互原型，尚未替换客户端'")
s=s.replace("['1','2','3','4','5']","['1','2','3','4','5','6']").replace("['0','1','2','3','4']","['0','1','2','3','4','5']")
s=s.replace("selectVariant(Number(initial));","selectVariant(Number(initial));else updateMode('interact');")
if not gallery_ready:p.write_text(s,encoding='utf-8')

G=json.loads((W/'native-preserved-geometry.json').read_text(encoding='utf-8-sig'))
def overlaps(a,b):
    return a['x']<b['x']+b['width'] and a['x']+a['width']>b['x'] and a['y']<b['y']+b['height'] and a['y']+a['height']>b['y']
def rect(x,y,w,h):return dict(x=x,y=y,width=w,height=h)
protected=[o for o in G['objects'] if o['kind']=='protected-card' or o['name'] in ['PlayerPlate','OpponentPlate','PlayerPlate/PlayerCounters','OpponentPlate/PlayerCounters']]
mana=next(o['bounds'] for o in G['objects'] if o['name']=='PublicManaPool')
protected.append(dict(name='PublicManaPool-with-navigation',bounds=rect(mana['x']-32,mana['y']-4,mana['width']+64,mana['height']+8)))
own_mana=rect(1190,612,mana['width']+64,mana['height']+8)
new=[('lowered-hand',rect(350,813,875,114)),('stack',rect(1278,18,221,572)),('phase-maximum',rect(1254,694,235,220)),('undo',rect(1393,659,96,32)),('own-mana',own_mana)]
collisions=[(name,o['name']) for name,r in new for o in protected if overlaps(r,o['bounds'])]
left=[]
for prefix,plate in [('Your','PlayerPlate'),('Opponent','OpponentPlate')]:
    info=[o for o in G['objects'] if o['name'] in [plate,plate+'/PlayerCounters'] or o['name'].startswith(prefix+'Emblem:')]
    for o in G['objects']:
        if o['name'].startswith(prefix+'Lands:'):
            left.extend((o['name'],i['name']) for i in info if overlaps(o['bounds'],i['bounds']))
assert not collisions,collisions
assert not left,left
raised_hand=rect(350,690,875,237)
assert not overlaps(raised_hand,own_mana)
for name,r in new:
    if name not in ['own-mana','lowered-hand']:assert not overlaps(r,own_mana),(name,'own mana')
(W/'qa-native-protected-geometry.json').write_text(json.dumps(dict(status='PASS',capture=[G['width'],G['height']],protectedCount=len(protected),newUiOverlaps=collisions,leftInfoLandOverlaps=left,ownManaClearInBothHandStates=True,scope='Fixed composition geometry; raised hand and hover enlargement may cover battlefield by explicit user direction'),ensure_ascii=False,indent=2),encoding='utf-8')
print('Gallery F ready; fixed native composition has no new-UI/protected or land/left-info overlaps')
