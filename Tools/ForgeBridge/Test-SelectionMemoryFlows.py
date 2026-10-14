"""Real token/explore producers and paid spells; declarative checks never mutate results."""
from pathlib import Path
import re, json, os, queue, subprocess, tempfile, threading, time, traceback, shutil, sys, hashlib

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'.tools/selection-memory-flows-1011';BASE.mkdir(exist_ok=True)
RT=ROOT/'ForgeRuntime';runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
case=sys.argv[1];plan=json.loads((BASE/'cases.json').read_text('utf-8'))[case]
CLASSES=BASE/'classes'/case;CLASSES.mkdir(parents=True,exist_ok=True)
fixtures=[ROOT/'Tools/ForgeBridge/tests/SelectionMemoryFixture.java',ROOT/'Tools/ForgeBridge/tests/LifePowerFixture.java',ROOT/'Tools/ForgeBridge/tests/CombatRelationFixture.java',ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,fixtures)],check=True)
folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
checks=[];paid=[];seen=set();baseline=None;last=None;index=0;stage='start';source_id=None;targeted=[];entities=[];numberIndex=0;discardIndex=0;choices=[];operation_before=None;revealed=[]
def write(p,value):p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def check(value,label):assert value,label;checks.append(label)
def observe(rid):
    deadline=time.monotonic()+6
    while time.monotonic()<deadline:
        try:return json.loads((audit/(str(rid)+'.json')).read_text('utf-8'))
        except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing real pending-request observer '+str(rid))
def allcards(n):return list({c['id']:c for p in n['players']for c in p['cards']+p.get('sideboard',[])}.values())
def original(name,owner=1):
    candidates=[c for c in allcards(baseline)if c['name']==name and c['owner']==owner]
    return next((c for c in candidates if c['zone']!='Library'),candidates[0])
def current(n,name,owner=1):return next(c for c in allcards(n)if c['id']==original(name,owner)['id'])
def shields(n):return [c for c in allcards(n)if c['zone']=='Command'and c['immutable']and "Regeneration"in c['name']]
def freeze(m,n,label):write(folder/(label+'.json'),m);write(folder/(label+'-native.json'),n)
def result(n,op):
    if 'parity' in op:
        src=next(c for c in allcards(n)if c['id']==source_id)
        states=[json.loads(p.read_text('utf8')) for p in audit.glob('*.json')]
        check(any(c['id']==source_id and c.get('chosenEvenOdd')==op['parity'] for z in states for c in allcards(z)),'Actual source stored selected EvenOdd enum before real zone reset')
        for c in allcards(operation_before):
            if c['zone']=='Battlefield' and 'Creature' in c['types']:
                after=next(x for x in allcards(n)if x['id']==c['id'])
                exile=(c['manaValue']%2==0)==(op['parity']=='Even')
                check(after['zone']==('Exile' if exile else 'Battlefield'),'Exact parity original identity/destination '+c['name'])
    for name in op.get('cleared',[]):
        sources=[c for c in allcards(n)if c['name']==name and c['owner']==op.get('seat',1) and c['zone']!='Library']
        check(bool(sources) and all(c['remembered']==[] and c['chosenCards']==[]for c in sources),'Actual parent chain clears all original source remembered/chosenCard '+name)
    if 'poolGreen' in op:
        check([p['poolGreen'] for p in n['players']]==op['poolGreen'],'Exact native green mana from revealed originals')
    for seat,names in op.get('knownHand',[]):
        ids={current(n,name,seat)['id'] for name in names}
        for v in last['views']:
            if last['views'].index(v)+1!=seat:
                check({c['id'] for c in v['players'][seat-1]['hand']}==ids,'Exact recipient known hand; unshown originals stay private')
    if 'randomOwner' in op:
        seat=op['randomOwner'];ids={id for row in revealed for id in row['ids']};hand={c['id']for c in operation_before['players'][seat-1]['cards']if c['zone']=='Hand'}
        check(len(ids)==1 and ids<=hand,'Random reveals an actual original hand identity')
        for j,v in enumerate(last['views']):
            if j+1!=seat:check({c['id']for c in v['players'][seat-1]['hand']}==ids,'Exact randomly revealed recipient hand knowledge')
    if 'revealedCount' in op:
        unique={id for row in revealed for id in row['ids']}
        check(len(unique)==op['revealedCount'],'Exact actual RevealCards original-ID count')
    if 'libraryRemoved' in op:
        seat,count=op['libraryRemoved'];old=operation_before['players'][seat-1]['library']
        check(n['players'][seat-1]['library']==old[count:],'Exact actual revealed/drawn top and untouched library order')
    for expected in op.get('cards',[]):
        c=current(n,expected['name'],expected.get('owner',1))
        for key,value in expected.items():
            if key in ['name','owner']:continue
            if key=='newTimestamp':check((c['timestamp']!=original(c['name'],expected.get('owner',1))['timestamp'])==value,'Original object timestamp transition '+c['name'])
            elif key=='mustBlockNames':check(c['mustBlock']==[current(n,t[1],t[0])['id'] for t in value],'Exact forced original attacker IDs')
            elif key=='blockerNames':check(c['blockers']==[current(n,t[1],t[0])['id'] for t in value],'Exact original blocker IDs')
            elif key=='defenderName':check(c['defender']==current(n,value[1],value[0])['id'] and c['defenderKind']=='card','Exact permanent defender ID')
            elif key=='defenderPlayer':check(c['defender']==last['views'][0]['players'][value-1]['targetId'] and c['defenderKind']=='player','Exact player defender ID')
            else:check(c[key]==value,'Exact original '+c['name']+' '+key+'='+str(value))
        if c['zone']=='Battlefield' and not c.get('phased'):
            for view in last['views']:
                public=next(x for p in view['players']for x in p['battlefield']if x['id']==c['id'])
                check(public['tapped']==c['tapped'],'Both viewers preserve original tap '+c['name'])
    if 'phaseState' in op:
        for row in op['phaseState']:
            name,phased,held=row[:3];owner=row[3]if len(row)>3 else 1
            c=current(n,name,owner);check(c['phased']==phased and c['held']==held,'Exact original phase/held '+name)
            check(c['zone']=='Battlefield' and c['timestamp']==original(name,owner)['timestamp'],'Phasing retains original zone/timestamp '+name)
            for v in last['views']:
                present=any(x['id']==c['id']for p in v['players']for x in p['battlefield']);check(present!=phased,'Phased card interaction removed/returned '+name)
            check(not [e for e in n['zoneEvents']if e['id']==c['id']],'No native zone-change event for phased original '+name)
    if 'attachedToName' in op:
        equip=current(n,op['equipment'],op.get('seat',1));want=0 if op['attachedToName'] is None else current(n,op['attachedToName'],op.get('seat',1))['id']
        check(equip['attachedTo']==want,'Exact original equipment recipient')
    if 'noActivation' in op:
        c=current(n,op['noActivation'],op.get('owner',2));check(not c['activatedPlayable'],'Native detained ability cannot activate')
        check('card:'+str(c['id']) not in [o['id']for o in last['request']['options']if not o.get('disabled')],'No enabled detained activated source')
    if 'effects'in op:
        es=shields(n);check(len(es)==op['effects'],'Exact remaining command regeneration effects '+str(op['effects']))
        if op.get('remembered'):
            ids=sorted(original(x[1],x[0])['id']for x in op['remembered'])
            check(sorted(x for e in es for x in e['remembered'])==ids,'Exact command shield remembered IDs')
    if 'scryTop' in op:
        p=op.get('seat',1)-1; old=operation_before['players'][p]['library']; total=len(op['scryTop'])+len(op['scryBottom'])
        check(n['players'][p]['library']==[old[j] for j in op['scryTop']]+old[total:]+[old[j] for j in op['scryBottom']],'Exact branch scry top/bottom and untouched original order')
    if 'effectiveX' in op:
        c=current(n,op['source'],op.get('seat',1))
        if c['zone']=='Graveyard':check(c.get('castX') is None,'Original cast ability reset only after real zone change')
        else:check(c['castX']==op['effectiveX'],'Actual cast ability retains changed effective X')
        check(c['xPaid']==op['effectiveX'],'Actual card X matches changed effective X')
    if 'unlessSpent' in op:
        count=lambda z:sum(c['tapped'] for c in z['players'][1]['cards']if c['zone']=='Battlefield' and c['name']in ['Plains','Island','Swamp','Mountain','Forest'])
        check(count(n)-count(operation_before)==op['unlessSpent'],'Exact extra opponent mana payment')
    if op.get('manifest'):
        chosen,rest=operation_before['players'][0]['library'][:2]
        one=next(c for c in allcards(n)if c['id']==chosen);two=next(c for c in allcards(n)if c['id']==rest)
        check(one['zone']==('Graveyard'if op.get('blockedManifest')else'Exile'if op.get('exileManifest')else'Battlefield'),'Chosen exact original manifest destination')
        check(two['zone']=='Graveyard','Exact original other top enters graveyard')
        check(n['players'][0]['library']==operation_before['players'][0]['library'][2:],'Exact remaining library order')
        if not op.get('exileManifest') and not op.get('blockedManifest'):
            check(one['manifested'] and one['faceDown'] and one['power']==2 and one['toughness']==2,'Actual chosen original face-down2/2')
            for j,v in enumerate(last['views']):
                card=next(c for c in v['players'][0]['battlefield']if c['id']==chosen)
                check(card.get('name')=='Face-down card' and card.get('faceDown') and not card.get('printingKey'),'Both wire viewers expose only face-down identity')
    if 'surveilTop' in op:
        old=operation_before['players'][0]['library'];amount=op['surveilAmount']
        top=[old[i] for i in op['surveilTop']];grave=[old[i] for i in op['surveilGrave']]
        check(n['players'][0]['library']==top+old[amount:],'Exact chosen top order and untouched remaining library')
        check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Graveyard' for i in grave),'Exact chosen graveyard original IDs')
        check(n['players'][0]['surveilled']==op['surveilled'],'Actual first/repeated surveil event count')
    if op.get('emptySurveil'):
        check(not n['players'][0]['library'] and n['players'][0]['surveilled']==1,'Empty library still produces actual first surveil trigger')
    if 'conniveDiscards' in op:
        old=operation_before['players'][0]['library'];count=op.get('conniveDraw',len(op['conniveDiscards']))
        check(n['players'][0]['library']==old[count:],'Exact connive draw order and remaining original library')
        check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Hand' for i in old[:count]),'Exact connive drawn original IDs')
        discarded=[next(c['id'] for c in operation_before['players'][0]['cards']if c['name']==name and c['zone']=='Hand') for name in op['conniveDiscards']]
        check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Graveyard' for i in discarded),'Exact discarded original hand IDs in graveyard')
    if op.get('noConniveDraw'):
        check(n['players'][0]['library']==operation_before['players'][0]['library'],'Cannot draw: exact library remains unchanged')
        check(not [c for c in n['players'][0]['cards']if c['zone']=='Hand'],'Empty hand creates no discard or counter')
    if 'manifestShort' in op:
        old=operation_before['players'][0]['library'];check(len(old)==op['manifestShort'],'Exact short initial library')
        check(not n['players'][0]['library'],'Short manifest exhausts only original available cards')
        if old:
            c=next(c for c in allcards(n)if c['id']==old[0]);check(c['zone']=='Battlefield' and c['faceDown'] and c['manifested'],'Only actual available original is manifested')
        check(len([s for s in n['stack']if s['source']==current(n,'Paranormal Analyst')['id'] and s['trigger']])==1,'Manifest dread watcher triggers even with no graveyard remainder')
    if op.get('manifestPriest'):
        old=operation_before['players'][0]['library'];chosen=old[0]
        check(n['players'][0]['library']==old[2:],'Priest exact remaining library')
        check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Graveyard' for i in old[:2]),'Priest both looked-at originals finally graveyard')
        events=[e for e in n['zoneEvents']if e['id']==chosen and e['mode']=='Added']
        zones=[e['zone'] for e in events]
        check('Exile' in zones and 'Graveyard' in zones and zones.index('Exile')<zones.index('Graveyard'),'Actual replacement exile precedes same-effect graveyard move')
    if op.get('learn'):
        seat=op.get('seat',1);p=n['players'][seat-1];old=operation_before['players'][seat-1]
        lesson=current(n,'Environmental Sciences',seat);shock=current(n,'Shock',seat)
        branch=op['learn']
        check(lesson['zone']==('Hand' if branch=='lesson' else 'Sideboard'),'Exact original Lesson branch zone')
        check(shock['zone']==('Graveyard' if branch=='discard' else 'Hand'),'Exact original discard branch zone')
        draw=branch=='discard';check(p['library']==old['library'][int(draw):],'Exact library order: draw only after real discard')
        if draw:check(next(c for c in allcards(n) if c['id']==old['library'][0])['zone']=='Hand','Exact original Learn draw identity')
        if branch=='replacement':check(current(n,'Retriever Phoenix',seat)['zone']=='Battlefield','Actual Learn replaced by Phoenix return')
        for j,v in enumerate(last['views']):
            visible={c['id'] for c in v['players'][seat-1]['hand']}
            if j+1!=seat and not p['controlled']:
                check(visible==({lesson['id']} if branch=='lesson' else set()),'Only revealed Lesson remains known; other hand/draw private')
    if 'investigated'in op:check([p['investigated']for p in n['players']]==op['investigated'],'Actual investigate events distinct from token count')
    if 'explored'in op:check([p['explored']for p in n['players']]==op['explored'],'Actual explored-this-turn count')
    if 'amassEffects'in op:check(len([c for c in allcards(n)if c['zone']=='Command'and c['name']=='Amass Effect'])==op['amassEffects'],'Exact added-type effect lifetime')
    if 'mill'in op:
        seat,count=op['mill'];ids=operation_before['players'][seat-1]['library'][:count]
        check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Graveyard'for i in ids),'Amass remembered power mills exact original top IDs')
        check(n['players'][seat-1]['library']==operation_before['players'][seat-1]['library'][count:],'Exact post-mill order')
    if op.get('keepTop'):
        check(n['players'][0]['library']==operation_before['players'][0]['library'],'Original source gone nonland kept in exact order')
        check(all(v['players'][0].get('libraryTop',{}).get('id')==n['players'][0]['library'][0]for v in last['views']),'Actual public revealed nonland identity survives keep')
    if op.get('landTop'):
        id=operation_before['players'][0]['library'][0];check(next(c for c in allcards(n)if c['id']==id)['zone']=='Hand','Actual revealed land ID enters hand')
        check(n['players'][0]['library']==operation_before['players'][0]['library'][1:],'Exact land top removed order')
    if op.get('emptyTop'):check(not n['players'][0]['library']and all(not v['players'][0].get('libraryTop')for v in last['views']),'Empty library no invented public top')
    if 'lifeLost' in op:check([p['lifeLost']for p in n['players']]==op['lifeLost'],'Exact actual life lost this turn')
    if 'drawTop' in op:
        seat=op.get('seat',1);ids=operation_before['players'][seat-1]['library'][:op['drawTop']];check(all(next(c for c in allcards(n)if c['id']==i)['zone']=='Hand'for i in ids),'Exact original remembered-loss draw IDs');check(n['players'][seat-1]['library']==operation_before['players'][seat-1]['library'][op['drawTop']:],'Exact post-draw library order')
    check(len(last['views'][0]['stackItems'])==op.get('stackCount',0),'Exact native pending stack count')
    if op.get('queuedTrigger'):
        q=op['queuedTrigger'];rows=[s for s in n['stack'] if s['source']==current(n,q['source'])['id'] and s['trigger']];check(len(rows)==1,'Exact original queued attack trigger');check(rows[0]['OriginalDefender']==last['views'][0]['players'][q['originalPlayer']-1]['targetId'],'Queued original defender retained');check(rows[0]['DefendingPlayer']==last['views'][0]['players'][q['newPlayer']-1]['targetId'],'Queued defending player follows actual new defender')
    if op.get('exiledTop'):
        seat,count=op['exiledTop'];ids=baseline['players'][seat-1]['library'][:count];check(all(next(c for c in allcards(n)if c['id']==id)['zone']=='Exile'for id in ids),'Actual exact original library top exiled');check(n['players'][seat-1]['library']==baseline['players'][seat-1]['library'][count:],'Exact remaining library order')
    check([p['life'] for p in n['players']]==op.get('life',[40,40]),'Exact both life result')
    for item in op.get('tokens',[]):
        ts=[c for c in allcards(n)if c['token']and c['zone']=='Battlefield'and c['controller']==item['owner']and item['type']in c['types']]
        check(len(ts)==item['count'],'Exact generated '+item['type']+' count/controller')
        if 'power'in item:check(all(c['power']==item['power']and c['toughness']==item['toughness']for c in ts),'Actual token body')
        if 'bodies'in item:check(sorted([c['basePower'],c['baseToughness']]for c in ts)==sorted(item['bodies']),'Exact distinct Spirit base bodies')
        if op.get('tokenTypes'):check(all(all(t in c['types']for t in op['tokenTypes'])for c in ts),'Original Army retains types and added type')
    if op.get('action','cast')in ['cast','activate','play']:
        src=next((c for c in allcards(n)if c['id']==source_id),None)
        if op.get('sourceType'):check(src is None or src['zone']!='Battlefield','Real original Clue sacrificed');src=src or dict(zone=op['sourceResult'])
        check(src['zone']==op.get('sourceResult','Graveyard'),'Real source left hand and resolved '+op['source'])
        tapped=lambda v:sum(c['tapped'] for c in v['players'][op.get('seat',1)-1]['cards']if c['zone']=='Battlefield'and c['name']in ['Plains','Island','Swamp','Mountain','Forest'])
        amount=tapped(n)-tapped(operation_before)
        check(amount==op['mana'],'Actual original land payment '+op['source']+' '+str(amount))
        paid.append({'index':index,'name':op['source'],'id':source_id,'seat':op.get('seat',1),'mana':amount})
    check(last['request']['seat'] in [1,2],'Actual next operator seat')

deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
for p,name in [(Path(__file__),'driver.py'),(fixtures[0],'fixture.java'),(fixtures[1],'state-fixture.java'),(RT/'runtime.json','runtime.json'),(BASE/'cases.json','cases.json')]:shutil.copy2(p,folder/name)
for p in fixtures:shutil.copy2(p,folder/p.name)
write(folder/'start.json',dict(session='selection-result-flow',auditDir=str(audit),seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],own=plan['own'],other=plan['other'],loyalty=plan.get('loyalty',{}),attachments=plan.get('attachments',[]),initialLives=plan.get('lives',[40,40]),emptyLibrary=plan.get('emptyLibrary',False),libraryLimit=plan.get('libraryLimit',-1),otherLibraryLimit=plan.get('otherLibraryLimit',-1),faceDown=plan.get('faceDown',[])))
with tempfile.TemporaryDirectory(prefix='mtgb-regen-')as tmp,(folder/'stderr.log').open('w',encoding='utf-8')as log,(folder/'trace.jsonl').open('w',encoding='utf-8')as trace:
    process=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.SelectionMemoryFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
    messages=queue.Queue()
    def reader():
        try:
            for line in process.stdout:messages.put(json.loads(line))
        except Exception as e:messages.put(e)
        finally:messages.put(None)
    threading.Thread(target=reader,daemon=True).start()
    try:
        for step in range(600):
            message=messages.get(timeout=40);check(isinstance(message,dict),'Native live message');trace.write(json.dumps(message)+'\n');trace.flush()
            check(message['type']not in ['fatal','ended','rejected'],'No native terminal or invalid response')
            if message['type']!='request':continue
            request=message['request'];rid=request['requestId'];kind=request['kind'];enabled=[o['id']for o in request['options']if not o.get('disabled')]
            if rid in seen:continue
            seen.add(rid);last=message;view=message['views'][0];n=observe(rid)if'Mulligan'not in kind else None;pick=None
            op=plan['ops'][index]if index<len(plan['ops'])else None
            if 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
            elif kind=='InputPassPriority':
                empty=not view['stackItems']
                if stage=='start'and request['seat']==1 and view['phase']=='MAIN1'and empty:
                    baseline=n;freeze(message,n,'before');stage='next'
                if stage=='resolve'and len(view['stackItems'])==op.get('stackCount',0) and (not op.get('queueSource') or current(n,op['queueSource'])['zone']=='Battlefield'and all(s['source']==current(n,op['queueSource'])['id'] for s in n['stack'])) or stage in ['attack-result','block-result','probe-result']and(empty or op.get('allowAttackStack')):
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='block'and op.get('prohibited')and view['phase']=='COMBAT_DECLARE_BLOCKERS':
                    check(not current(n,op['source'],op.get('owner',2))['canBlock'],'Native skipped empty blocker picker because no legal blockers')
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='probeAttack'and view['phase']=='COMBAT_DECLARE_ATTACKERS':
                    check(not current(n,op['source'],op.get('owner',2))['canAttack'],'Native skipped empty attacker picker because no legal attackers')
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='wait'and empty and view['turnSeat']==op['turnSeat']and view['phase']==op['phase']:
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='next'and(empty or op and op.get('allowStack')):
                    if op is None:
                        write(folder/'result.json',dict(status='PASS',case=case,checks=len(checks),assertions=checks,paid=paid,choices=choices,runtimeHash=runtime['runtimeHash'],folder=folder.relative_to(ROOT).as_posix()));print('PASS',case,folder,len(checks),flush=True);break
                    action=op.get('action','cast')
                    if action in ['wait','attack','block','probeAttack']:
                        stage=action;pick='ok'
                    elif request['seat']==op.get('actorSeat',op.get('seat',1))and view['phase']in op.get('phases',['MAIN1','MAIN2'])and(not op.get('ownTurn')or view['turnSeat']==op.get('seat',1)):
                        source=next(c for c in n['players'][op.get('seat',1)-1]['cards']if (op.get('sourceType')in c['types']if op.get('sourceType')else c['name']==op['source'])and c['zone']==('Battlefield'if action=='activate'else'Hand'))
                        source_id=source['id'];pick='card:'+str(source_id);targeted=[];entities=[];numberIndex=0;discardIndex=0;revealed=[];operation_before=n;freeze(message,n,'cast-'+str(index));stage='resolve'
                if pick is None:pick='ok'
            elif 'PayMana'in kind:
                freeze(message,n,'payment-'+str(index)+'-'+str(rid));pick='cancel' if op.get('unlessPay') is False and request['seat']==2 else 'ok'
            elif kind=='SelectStackTarget':
                seat,name=op['targets'][0];wanted=current(n,name,seat)['id']
                item=next(s for s in view['stackItems'] if s['source']['id']==wanted)
                pick='ok' if request.get('selectedCount',0)==1 and 'ok' in enabled else 'stack:'+str(item['id']);check(pick in enabled,'Original target stack instance matches original source ID and native confirm')
            elif kind=='InputSelectTargets':
                if request.get('remainingCount')==0 and'ok'in enabled:pick='ok'
                elif op.get('tokenTarget') and not targeted:
                    wanted=next(c for c in allcards(n)if c['token']and c['zone']=='Battlefield'and op['tokenTarget']in c['types']);pick='card:'+str(wanted['id']);targeted.append(pick)
                elif len(targeted)<len(op['targets']):
                    target=op['targets'][len(targeted)];pick=('player:'+str(view['players'][target[0]-1]['targetId']))if target[1]=='player'else'card:'+str(current(n,target[1],target[0])['id']);targeted.append(pick)
                else:raise AssertionError(('Unexpected target request',request))
            elif kind=='AssignAmount' and request.get('requiredValue')==0:
                check(all(a['amount']==0 for a in request['allocations']),'Native zero damage allocation unchanged');freeze(message,n,'zero-damage-'+str(index)+'-'+str(rid));pick='confirm-amount'
            elif kind=='InputAttack'and stage=='attack':
                ids=[current(n,name,op.get('owner',1))['id'] for name in op.get('sources',[op.get('source')])]
                missing=[id for id in ids if not any(o.get('attackerId')==id and o['id'].startswith('attack-remove:') for o in request['options'])]
                pick=next(o['id'] for o in request['options'] if o['id']in enabled and o.get('attackerId')==missing[0] and (not op.get('attackPlayer') or o.get('defenderId')==view['players'][op['attackPlayer']-1]['targetId'])) if missing else 'ok';freeze(message,n,'attack-'+str(index)+'-'+str(rid))
                if pick=='ok':stage='attack-result'
            elif kind=='InputAttack'and stage=='probeAttack':
                id=current(n,op['source'],op.get('owner',2))['id'];check(not any(o.get('attackerId')==id for o in request['options']if o['id']in enabled),'Actual attack request excludes detained original')
                freeze(message,n,'attack-prohibited-'+str(index)+'-'+str(rid));pick='ok';stage='probe-result'
            elif kind=='InputBlock'and stage=='block':
                blocker=current(n,op['source'],op.get('owner',2))['id'];attackers=[current(n,name,op.get('attackerOwner',1))['id']for name in op.get('attackers',[op['attacker']])]
                missing=[id for id in attackers if not any(o.get('defenderId')==blocker and o.get('attackerId')==id and o.get('selected') for o in request['options'])];attacker=missing[0]if missing else attackers[0]
                options=[o for o in request['options']if o['id']in enabled and o.get('defenderId')==blocker and o.get('attackerId')==attacker]
                if op.get('testForcedNoBlock') and not op.get('_tested'):
                    freeze(message,n,'forced-confirm-rejected-'+str(index)+'-'+str(rid));check(bool(n['blockError']),'Native validator rejects unmet mandatory block');op['_tested']=True;op['_attemptInvalid']=True
                if op.get('prohibited'):
                    check(not options,'Actual block request excludes detained original');pick='ok';stage='block-result'
                else:
                    check(bool(options),'Original legal blocking relation');o=options[0];pick='ok'if o.get('selected')else o['id']
                    if pick=='ok':stage='block-result'
                    if op.pop('_attemptInvalid',False):pick='ok';stage='block'
                freeze(message,n,'block-'+str(index)+'-'+str(rid))
            elif kind in ['InputAttack','InputBlock']:pick='ok'
            elif kind=='Scry':
                freeze(message,n,'scry-'+str(index)+'-'+str(rid))
                old=operation_before['players'][op.get('seat',1)-1]['library'];selected=request.get('selectedCount',0)
                check(request['seat']==op.get('seat',1) and request['remainingCount']+selected==len(op['scryTop'])+len(op['scryBottom']),'Exact native branch scry amount/operator')
                if 'confirm-scry' in enabled:pick='confirm-scry'
                else:
                    steps=[('top',i) for i in op['scryTop']]+[('bottom',i)for i in op['scryBottom']]
                    destination,offset=steps[selected];pick='scry-'+destination+':'+str(old[offset])
            elif kind=='Surveil':
                freeze(message,n,'surveil-'+str(index)+'-'+str(rid))
                old=operation_before['players'][0]['library'];selected=request.get('selectedCount',0)
                check(request['seat']==1 and request['remainingCount']+selected==op['surveilAmount'],'Exact native surveil amount/operator')
                if 'confirm-scry' in enabled:pick='confirm-scry'
                else:
                    steps=[('top',i) for i in op['surveilTop']]+[('bottom',i)for i in op['surveilGrave']]
                    destination,offset=steps[selected];pick='scry-'+destination+':'+str(old[offset])
                choices.append({'index':index,'requestId':rid,'kind':kind,'options':request['options']})
            elif kind=='RevealCards':
                freeze(message,n,'reveal-'+str(index)+'-'+str(rid));pick='confirm-cards'
                ids=[c['id'] for c in request.get('previewCards',[])]
                revealed.append(dict(requestId=rid,seat=request['seat'],ids=ids))
                write(folder/('reveals-'+str(index)+'.json'),revealed)
                check(request['seat']in [1,2],'Actual public reveal receiver seat')
            elif kind in ['OrderAbilities','OrderCards']:
                end='confirm-order'if kind=='OrderAbilities'else'confirm-cards';prefix='order:'if kind=='OrderAbilities'else'select:'
                pick=end if end in enabled else next(o['id']for o in request['options']if o['id'].startswith(prefix)and not o.get('selected')and not o.get('disabled'))
            else:
                freeze(message,n,'choice-'+str(index)+'-'+str(rid));choices.append({'index':index,'requestId':rid,'kind':kind,'options':request['options']})
                if op.get('declinePlay') and request['seat']==op.get('seat',1) and kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','InputConfirm']:
                    pick='cancel'
                elif 'revealNames' in op and kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList']:
                    seat=op.get('revealSeat',op.get('seat',1));wanted={current(n,name,seat)['id'] for name in op['revealNames']}
                    candidates=[o for o in request['options']if o.get('card',{}).get('id')]
                    legal={o['card']['id']for o in candidates}
                    if 'revealLegal' in op:check(legal=={current(n,name,seat)['id']for name in op['revealLegal']},'Exact native filtered reveal candidate IDs')
                    check(request['seat']==seat,'Actual hand owner selects revealed cards')
                    selected={o['card']['id']for o in candidates if o.get('selected')}
                    if wanted==selected:
                        pick='confirm-cards' if 'confirm-cards'in enabled else 'ok'
                    else:pick=next(o['id']for o in candidates if o['id']in enabled and o['card']['id']in wanted-selected)
                elif 'parity' in op and kind in ['InputConfirm','dialog','ChooseOptions']:
                    wanted=op['parity'].lower();opts=[o for o in request['options']if o['id']in enabled and wanted in o['label'].lower()]
                    check(len(opts)==1,'Actual odd/even option maps selected original enum');pick=opts[0]['id']
                elif 'phaseNames' in op and 'phase' in request.get('message','').splitlines()[-1].lower() and kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList']:
                    wanted={current(n,name)['id']for name in op['phaseNames']};selected={o['card']['id']for o in request['options']if o.get('selected')and o.get('card')}
                    pick='ok'if wanted==selected else next(o['id']for o in request['options']if o['id']in enabled and o.get('card',{}).get('id')in wanted-selected)
                elif 'conniveDiscards' in op and kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList'] and any(o.get('card',{}).get('zone')=='Hand' for o in request['options']) and all(o.get('card',{}).get('zone')=='Hand' for o in request['options']if o.get('card',{}).get('id')):
                    if request.get('remainingCount')==0 and 'ok' in enabled:pick='ok';discardIndex+=1
                    else:
                        wanted=next(c['id'] for c in operation_before['players'][0]['cards']if c['name']==op['conniveDiscards'][discardIndex] and c['zone']=='Hand')
                        pick=next(o['id'] for o in request['options']if o['id']in enabled and o.get('card',{}).get('id')==wanted)
                elif 'conniverFirst' in op and request.get('message','').endswith('Choose creature to connive') and kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','SelectEntity','ChooseEntity']:
                    wanted=current(n,op['conniverFirst'])['id']
                    pick='ok' if request.get('remainingCount')==0 and 'ok' in enabled else next(o['id']for o in request['options']if o['id']in enabled and o.get('card',{}).get('id')==wanted)
                elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList'] and op.get('learn'):
                    opts=[o for o in request['options'] if o.get('card') and o['card'].get('id')]
                    seat=op.get('seat',1);expectedSeat=op.get('actorSeat',seat)
                    check(request['seat']==expectedSeat and request.get('playerSeat',seat)==seat,'Exact physical/logical Learn operator')
                    check(not any(o['card']['zone']=='Sideboard' and 'Lesson' not in o['card'].get('typeLine','Lesson') for o in opts),'Only Lesson sideboard candidates')
                    if n['players'][seat-1]['controlled']:check(not any(o['card']['zone']=='Sideboard' for o in opts),'Controlled Learn cannot access sideboard')
                    else:check(any(o['card']['id']==current(n,'Environmental Sciences',seat)['id'] for o in opts),'Exact own original Lesson candidate')
                    if op['learn']=='decline':pick='cancel'
                    elif request.get('selectedCount',0)>0 and 'ok' in enabled:pick='ok'
                    else:
                        name='Environmental Sciences' if op['learn']=='lesson' else 'Shock';wanted=current(n,name,seat)['id']
                        pick=next(o['id']for o in opts if o['card']['id']==wanted and o['id']in enabled)
                elif kind=='InputSelectEntitiesFromList'and op.get('tokenEntity'):
                    opts=[o for o in request['options']if o['id']in enabled and o.get('card',{}).get('token')and op['tokenEntity']in o['card']['currentType']]
                    check(bool(opts)or request.get('remainingCount')==0,'Actual new Army candidate IDs');pick='ok'if request.get('remainingCount')==0 and 'ok'in enabled else opts[0]['id']
                elif kind=='InputSelectEntitiesFromList' and 'players' in op:
                    wanted=[view['players'][seat-1]['targetId'] for seat in op['players']]
                    pending=[id for id in wanted if id not in entities]
                    if not pending:pick='ok'
                    else:
                        opts=[o for o in request['options'] if o['id']in enabled and (o['id']=='player:'+str(pending[0]) or o.get('playerId')==pending[0])]
                        check(len(opts)==1,'Exact original player entity option');pick=opts[0]['id'];entities.append(pending[0])
                elif 'numbers' in op and kind=='ChooseNumber' and 'confirm-number' in enabled:
                    value=op['numbers'][numberIndex]
                    check(request['minValue']<=value<=request['maxValue'],'Actual X range contains planned value')
                    if request['selectedValue']==value:pick='confirm-number';numberIndex+=1
                    else:pick='number-plus' if request['selectedValue']<value else 'number-minus'
                elif 'numbers'in op and kind in ['ChooseNumber','dialog'] and request.get('message')!='getAbilityToPlay':
                    value=op['numbers'][numberIndex];opts=[o for o in request['options'] if o['id']in enabled and o['label']==str(value)]
                    check(bool(opts),('Exact original numeric option',request,value));pick=opts[0]['id'];numberIndex+=1
                elif kind=='dialog'and request.get('message')=='getAbilityToPlay':
                    pick=next(o['id']for o in request['options']if o['id']in enabled and op['abilityContains'].lower()in o['label'].lower())
                elif kind=='ChooseModes':pick='confirm-modes'if'confirm-modes'in enabled else next(o['id']for o in request['options']if o['id']in enabled and op['modeContains'].lower()in o['label'].lower()and not o.get('selected'))
                elif kind=='InputSelectCardsFromList'and op.get('skipOptional'):pick='ok'
                elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','SelectEntity','ChooseEntity'] and op.get('entity'):
                    seat,name=op['entity'];wanted=current(n,name,seat)['id'];opts=[o for o in request['options']if o['id']in enabled and(o.get('cardId')==wanted or o.get('card',{}).get('id')==wanted or o.get('defenderId')==wanted or o['id'].endswith(':'+str(wanted)))];check(bool(opts),'Original actual entity option');pick='ok' if request.get('remainingCount')==0 and 'ok'in enabled else opts[0]['id']
                elif kind=='InputConfirm'and op.get('optionalByName'):
                    found=[name for name in op['optionalByName']if name in request.get('message','')];check(len(found)==1,'Exact per-attacker optional question');pick='ok'if op['optionalByName'][found[0]]else'cancel'
                elif kind=='InputConfirm'and'optional'in op:pick='ok'if op['optional']else'cancel'
                elif kind=='ChooseReplacementEffect':pick=next(o['id']for o in request['options']if o['id']in enabled)
                elif kind=='InputConfirm'and any('regen'in str(x).lower()for x in [request.get('message',''),request.get('title','')]):pick='ok'
                else:raise AssertionError(('Unhandled actual request',case,index,kind,request))
            check(pick in enabled,('Actual enabled native option',case,index,stage,kind,pick,enabled))
            response=dict(type='respond',session='selection-result-flow',seat=request['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();process.stdin.write(json.dumps(response)+'\n');process.stdin.flush();print(step,case,index,kind,pick,flush=True)
        else:raise AssertionError('Step limit')
    except Exception:
        write(folder/'result.json',dict(status='FAIL',case=case,index=index,stage=stage,checks=len(checks),paid=paid,error=traceback.format_exc(),runtimeHash=runtime['runtimeHash']));print('FAIL',case,folder,traceback.format_exc(),flush=True);raise
    finally:
        if process.poll()is None:process.kill();process.wait()
