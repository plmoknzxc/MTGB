"""Real paid regeneration/destroy flows. Declarative expectations never mutate results."""
from pathlib import Path
import json, os, queue, subprocess, tempfile, threading, time, traceback, shutil, sys, hashlib

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'.tools/current-object-flows-1011';BASE.mkdir(exist_ok=True)
RT=ROOT/'ForgeRuntime';runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
case=sys.argv[1];plan=json.loads((BASE/'cases.json').read_text('utf-8'))[case]
CLASSES=BASE/'classes';CLASSES.mkdir(exist_ok=True)
fixtures=[ROOT/'Tools/ForgeBridge/tests/CurrentObjectFixture.java',ROOT/'Tools/ForgeBridge/tests/StateBranchFixture.java']
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,fixtures)],check=True)
folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
checks=[];paid=[];seen=set();baseline=None;last=None;index=0;stage='start';source_id=None;targeted=[];choices=[];operation_before=None
def write(p,value):p.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def check(value,label):assert value,label;checks.append(label)
def observe(rid):
    deadline=time.monotonic()+6
    while time.monotonic()<deadline:
        try:return json.loads((audit/(str(rid)+'.json')).read_text('utf-8'))
        except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing real pending-request observer '+str(rid))
def allcards(n):return [c for p in n['players']for c in p['cards']]
def original(name,owner=1):return next(c for c in allcards(baseline)if c['name']==name and c['owner']==owner)
def current(n,name,owner=1):return next(c for c in allcards(n)if c['id']==original(name,owner)['id'])
def shields(n):return [c for c in allcards(n)if c['zone']=='Command'and c['immutable']and "Regeneration"in c['name']]
def freeze(m,n,label):write(folder/(label+'.json'),m);write(folder/(label+'-native.json'),n)
def result(n,op):
    for expected in op.get('cards',[]):
        c=current(n,expected['name'],expected.get('owner',1))
        for key,value in expected.items():
            if key in ['name','owner']:continue
            if key=='newTimestamp':check((c['timestamp']!=original(c['name'],expected.get('owner',1))['timestamp'])==value,'Original object timestamp transition '+c['name'])
            else:check(c[key]==value,'Exact original '+c['name']+' '+key+'='+str(value))
        if c['zone']=='Battlefield':
            for view in last['views']:
                public=next(x for p in view['players']for x in p['battlefield']if x['id']==c['id'])
                check(public['tapped']==c['tapped'],'Both viewers preserve original tap '+c['name'])
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
    check(not last['views'][0]['stackItems'],'Native resolution returned empty stack')
    check([p['life'] for p in n['players']]==op.get('life',[40,40]),'Exact both life result')
    for item in op.get('tokens',[]):
        ts=[c for c in allcards(n)if c['token']and c['zone']=='Battlefield'and c['controller']==item['owner']]
        check(len(ts)==item['count'],'Exact generated token count')
        check(all(c['power']==item.get('power',4)and c['toughness']==item.get('toughness',4)for c in ts),'Actual token body')
    if op.get('action','cast')in ['cast','activate']:
        src=next(c for c in allcards(n)if c['id']==source_id)
        check(src['zone']==op.get('sourceResult','Graveyard'),'Real source left hand and resolved '+op['source'])
        tapped=lambda v:sum(c['tapped'] for c in v['players'][op.get('seat',1)-1]['cards']if c['zone']=='Battlefield'and c['name']in ['Plains','Island','Swamp','Mountain','Forest'])
        amount=tapped(n)-tapped(operation_before)
        check(amount==op['mana'],'Actual original land payment '+op['source']+' '+str(amount))
        paid.append({'index':index,'name':op['source'],'id':source_id,'seat':op.get('seat',1),'mana':amount})
    check(last['request']['seat'] in [1,2],'Actual next operator seat')

deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
for p,name in [(Path(__file__),'driver.py'),(fixtures[0],'fixture.java'),(fixtures[1],'state-fixture.java'),(RT/'runtime.json','runtime.json'),(BASE/'cases.json','cases.json')]:shutil.copy2(p,folder/name)
write(folder/'start.json',dict(session='current-object-flow',auditDir=str(audit),seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],own=plan['own'],other=plan['other'],loyalty=plan.get('loyalty',{}),attachments=plan.get('attachments',[])))
with tempfile.TemporaryDirectory(prefix='mtgb-regen-')as tmp,(folder/'stderr.log').open('w',encoding='utf-8')as log,(folder/'trace.jsonl').open('w',encoding='utf-8')as trace:
    process=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CurrentObjectFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
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
                if stage=='resolve'and empty or stage in ['attack-result','block-result','probe-result']and empty:
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='block'and op.get('prohibited')and view['phase']=='COMBAT_DECLARE_BLOCKERS':
                    check(not current(n,op['source'],op.get('owner',2))['canBlock'],'Native skipped empty blocker picker because no legal blockers')
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='probeAttack'and view['phase']=='COMBAT_DECLARE_ATTACKERS':
                    check(not current(n,op['source'],op.get('owner',2))['canAttack'],'Native skipped empty attacker picker because no legal attackers')
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='wait'and empty and view['turnSeat']==op['turnSeat']and view['phase']==op['phase']:
                    freeze(message,n,'resolved-'+str(index));result(n,op);index+=1;stage='next';op=plan['ops'][index]if index<len(plan['ops'])else None
                if stage=='next'and empty:
                    if op is None:
                        write(folder/'result.json',dict(status='PASS',case=case,checks=len(checks),assertions=checks,paid=paid,choices=choices,runtimeHash=runtime['runtimeHash'],folder=folder.relative_to(ROOT).as_posix()));print('PASS',case,folder,len(checks),flush=True);break
                    action=op.get('action','cast')
                    if action in ['wait','attack','block','probeAttack']:
                        stage=action;pick='ok'
                    elif request['seat']==op.get('seat',1)and view['phase']in op.get('phases',['MAIN1','MAIN2'])and(not op.get('ownTurn')or view['turnSeat']==op.get('seat',1)):
                        source=next(c for c in n['players'][op.get('seat',1)-1]['cards']if c['name']==op['source']and c['zone']==('Battlefield'if action=='activate'else'Hand'))
                        source_id=source['id'];pick='card:'+str(source_id);targeted=[];operation_before=n;freeze(message,n,'cast-'+str(index));stage='resolve'
                if pick is None:pick='ok'
            elif 'PayMana'in kind:freeze(message,n,'payment-'+str(index)+'-'+str(rid));pick='ok'
            elif kind=='InputSelectTargets':
                if request.get('remainingCount')==0 and'ok'in enabled:pick='ok'
                elif len(targeted)<len(op['targets']):
                    target=op['targets'][len(targeted)];pick=('player:'+str(view['players'][target[0]-1]['targetId']))if target[1]=='player'else'card:'+str(current(n,target[1],target[0])['id']);targeted.append(pick)
                else:raise AssertionError(('Unexpected target request',request))
            elif kind=='InputAttack'and stage=='attack':
                id=current(n,op['source'],op.get('owner',1))['id'];o=next(o for o in request['options']if o['id']in enabled and o.get('attackerId')==id)
                pick='ok'if o['id'].startswith('attack-remove:')else o['id'];freeze(message,n,'attack-'+str(index)+'-'+str(rid))
                if pick=='ok':stage='attack-result'
            elif kind=='InputAttack'and stage=='probeAttack':
                id=current(n,op['source'],op.get('owner',2))['id'];check(not any(o.get('attackerId')==id for o in request['options']if o['id']in enabled),'Actual attack request excludes detained original')
                freeze(message,n,'attack-prohibited-'+str(index)+'-'+str(rid));pick='ok';stage='probe-result'
            elif kind=='InputBlock'and stage=='block':
                blocker=current(n,op['source'],op.get('owner',2))['id'];attacker=current(n,op['attacker'],op.get('attackerOwner',1))['id']
                options=[o for o in request['options']if o['id']in enabled and o.get('defenderId')==blocker and o.get('attackerId')==attacker]
                if op.get('prohibited'):
                    check(not options,'Actual block request excludes detained original');pick='ok';stage='block-result'
                else:
                    check(bool(options),'Original legal blocking relation');o=options[0];pick='ok'if o.get('selected')else o['id']
                    if pick=='ok':stage='block-result'
                freeze(message,n,'block-'+str(index)+'-'+str(rid))
            elif kind in ['InputAttack','InputBlock']:pick='ok'
            elif kind in ['OrderAbilities','OrderCards']:
                end='confirm-order'if kind=='OrderAbilities'else'confirm-cards';prefix='order:'if kind=='OrderAbilities'else'select:'
                pick=end if end in enabled else next(o['id']for o in request['options']if o['id'].startswith(prefix)and not o.get('selected')and not o.get('disabled'))
            else:
                freeze(message,n,'choice-'+str(index)+'-'+str(rid));choices.append({'index':index,'requestId':rid,'kind':kind,'options':request['options']})
                if kind=='dialog'and request.get('message')=='getAbilityToPlay':
                    pick=next(o['id']for o in request['options']if o['id']in enabled and op['abilityContains'].lower()in o['label'].lower())
                elif kind=='ChooseModes':pick='confirm-modes'if'confirm-modes'in enabled else next(o['id']for o in request['options']if o['id']in enabled and op['modeContains'].lower()in o['label'].lower()and not o.get('selected'))
                elif kind=='InputSelectCardsFromList'and op.get('skipOptional'):pick='ok'
                elif kind=='InputConfirm'and'optional'in op:pick='ok'if op['optional']else'cancel'
                elif kind=='ChooseReplacementEffect':pick=next(o['id']for o in request['options']if o['id']in enabled)
                elif kind=='InputConfirm'and any('regen'in str(x).lower()for x in [request.get('message',''),request.get('title','')]):pick='ok'
                else:raise AssertionError(('Unhandled actual request',case,index,kind,request))
            check(pick in enabled,('Actual enabled native option',case,index,stage,kind,pick,enabled))
            response=dict(type='respond',session='current-object-flow',seat=request['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();process.stdin.write(json.dumps(response)+'\n');process.stdin.flush();print(step,case,index,kind,pick,flush=True)
        else:raise AssertionError('Step limit')
    except Exception:
        write(folder/'result.json',dict(status='FAIL',case=case,index=index,stage=stage,checks=len(checks),paid=paid,error=traceback.format_exc(),runtimeHash=runtime['runtimeHash']));print('FAIL',case,folder,traceback.format_exc(),flush=True);raise
    finally:
        if process.poll()is None:process.kill();process.wait()
