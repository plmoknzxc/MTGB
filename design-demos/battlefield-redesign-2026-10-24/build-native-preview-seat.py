"""Translate the explicit layout composition to a presentation-only ForgeSeatView."""
from pathlib import Path
import json,re,copy
W=Path(__file__).parent
raw=json.loads((W/'baseline-seat.json').read_text(encoding='utf-8'))
s=(W/'scene-data.js').read_text(encoding='utf-8');D=json.loads(s[s.index('=')+1:].strip().rstrip(';'))
s=(W/'full-layout-data.js').read_text(encoding='utf-8');F=json.loads(s[len('window.MTGB_LAYOUT='):s.index(';\nfor(')])
mapping={key:20000+i for i,key in enumerate(F['cards'])}
def card(key):
    x=F['cards'][key];c={**D['cards'].get(x.get('sourceId'),{}),**x}
    return dict(id=mapping[key],name=c.get('english')or c['name'],printingKey=c.get('printing'),ownerSeat=c['owner'],zone=c.get('zone','Battlefield'),originalName=c.get('english')or c['name'],originalType=c.get('type',''),currentType=c.get('type',''),currentRules=c.get('rules',''),originalRules=c.get('rules',''),manaCost=c.get('cost',''),originalManaCost=c.get('printedCost',''),creature=c.get('creature',False),land=c.get('land',False),tapped=c.get('tapped',False),planeswalker='loyalty'in c,battle='defense'in c,loyalty=c.get('loyalty',0),defense=c.get('defense',0),protectingPlayerSeat=3-c['owner'],power=c.get('power')or 0,toughness=c.get('toughness')or 0,hasSicknessState=True,summoningSick=c.get('sick',False),stackableStateKnown=True,stackablePermanent=False)
for p in raw['players']:
    seat=p['seat'];prefix='own'if seat==1 else'enemy'
    p['battlefield']=[card(key)for name,ids in F['lanes'].items()if name.startswith(prefix+'-')for key in ids]
    for c in p['battlefield']:
        if c['name']=='Food Token':c.update(token=True,tokenArtKey='TWOE/13',printingKey='TWOE/13')
    p['counters']=[dict(kind=kind,label=label,count=count)for kind,label,count in F['resources'][str(seat)]['counters']]
    p['manaPoolEntries']=[dict(id='mana:'+symbol,symbol=symbol,count=count,snow=False,restricted=False)for symbol,count in F['resources'][str(seat)]['mana']]
    p['emblems']=[];p['effectMarkers']=[]
    for group,ids in F['groups'][str(seat)].items():
        for key in ids:
            c=card(key);x=F['cards'][key];c['name']=x.get('name',c['name'])
            if group=='command':
                c['commander']=True;p['commandCards']=[c];p['commanders']=[c];continue
            if group=='emblems':c['emblem']=True;p['emblems'].append(c)
            else:
                c['effectMarker']=True;c['markerKind']='delayed'if group=='delayed'else'dungeon'if group=='dungeons'else'effect'
                if group=='dungeons':c['helperArtName']='Lost Mine of Phandelver';c['dungeonRoom']='Dark Pool';c['markerStatus']='当前房间：黑暗之池'
                if group=='states':c['helperArtName']='The Monarch';c['name']='君主'
                p['effectMarkers'].append(c)
            if group in ['emblems','delayed']:
                c['effectSourceName']=x.get('english');c['effectSourcePrintingKey']=c['printingKey']
raw['notices']=[]
raw['globalEffects']=[dict(id=29990,name='白昼',effectMarker=True,markerKind='game',helperArtName='Day',currentRules='当前为白昼。',ownerSeat=1)]
raw['boardKey']='native-preserved-presentation-only'
(W/'native-full-seat.json').write_text(json.dumps(raw,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
print('Native presentation composition ready; no gameplay generated.')
