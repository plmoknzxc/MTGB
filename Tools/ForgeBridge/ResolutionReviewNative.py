"""Focused real resolution paths, with private library proofs kept out of production wire."""
import json, os, queue, subprocess, tempfile, threading, time, traceback, sys, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]; RT=ROOT/'ForgeRuntime'
OUT=Path(os.environ.get('MTGB_RESOLUTION_OUTPUT',str(ROOT/'.tools/resolution-ui-1005/native'))); OUT.mkdir(parents=True,exist_ok=True)
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
CLASSES=OUT/'classes'; CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(Path(__file__).with_name('ResolutionReviewFixture.java'))],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
    if (OUT/(case+'.jsonl')).exists():
        archive=OUT/'attempts'/str(time.time_ns());archive.mkdir(parents=True)
        for old in OUT.glob(case+'*'):
            if old.is_file():shutil.copy2(old,archive/old.name)
    with tempfile.TemporaryDirectory(prefix='temp-',dir=OUT) as temp,(OUT/(case+'.log')).open('w',encoding='utf-8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf-8')
        process=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ResolutionReviewFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            except Exception as exc:messages.put(exc)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def save(m,label):
            r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
            while not path.exists() and time.monotonic()<end:time.sleep(.01)
            assert path.exists(),'Missing proof';p=json.loads(path.read_text());assert p['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-proof.json')).write_text(json.dumps(p,ensure_ascii=False,indent=2),encoding='utf-8')
            return [c['id'] for c in p['players'][0]['library']]
        spell='Growth Spiral' if case.startswith('growth') else 'Planar Genesis' if case.startswith('genesis') else 'Brainsurge' if case.startswith('brain') else 'Wrath of the Skies' if case.startswith('wrath') else 'Merfolk Branchwalker'
        cast=False;source=None;before=None;chosen=[];ordered=[];step_counts={};x_chosen=False;follow_draw=False;deadline=time.monotonic()+120
        try:
            for step in range(450):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=35);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];one=v['players'][0];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
                if kind=='InputPassPriority':
                    if not cast and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
                        source=next(c['id'] for c in one['hand'] if c['name']==spell);before=proof(r,'before');save(m,'before');pick='card:'+str(source);cast=True
                    elif cast and not v['stackItems'] and any(c['id']==source for c in (one['graveyardCards'] if not case.startswith('explore') or follow_draw else one['battlefield'])):
                        if case=='explore-draw' and not follow_draw:
                            assert one.get('libraryTop',{}).get('id')==before[0]
                            save(m,'kept');source=next(c['id'] for c in one['hand'] if c['name']=='Opt');pick='card:'+str(source);follow_draw=True
                            process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush();continue
                        after=proof(r,'resolved');save(m,'resolved')
                        hand={c['id'] for c in one['hand']};bf={c['id']:c for c in one['battlefield']};graves={c['id'] for c in one['graveyardCards']}
                        if case.startswith('growth'):
                            assert before[0] in hand and after==before[1:],'Draw incorrect'
                            assert len(chosen)==(1 if case.endswith('yes') else 0)
                            if chosen:assert chosen[0] in bf
                        elif case.startswith('genesis'):
                            assert len(chosen)==1 and chosen[0] in before[:4]
                            assert after[:-3]==before[4:] and set(after[-3:])==set(before[:4])-{chosen[0]},'Bottom selection incorrect'
                            assert (chosen[0] in bf)==case.endswith('yes')
                            if chosen[0] in bf:assert bf[chosen[0]]['tapped']
                            else:assert chosen[0] in hand
                            assert not m['views'][1]['players'][0].get('libraryTop'),'Private look leaked to opponent'
                        elif case.startswith('brain'):
                            assert len(chosen)==2 and set(after[:2])==set(chosen) and after[2:]==before[4:],'Return to top incorrect'
                            if ordered:assert after[:2]==ordered,(after[:2],ordered)
                            assert one.get('libraryTop',{}).get('id')==after[0],'Known return lost'
                            assert not m['views'][1]['players'][0].get('libraryTop'),'Private hand-to-top leaked'
                        elif case.startswith('wrath'):
                            opp=v['players'][1];remaining={c['name'] for c in opp['battlefield']}
                            assert 'Hill Giant' in remaining and 'Jace Beleren' in remaining
                            expected={'Hill Giant','Jace Beleren'} if case.endswith('two') else {'Grizzly Bears','Glorious Anthem','Hill Giant','Jace Beleren'}
                            assert remaining==expected,remaining
                            assert not any(c['name']=='Tormod\'s Crypt' for c in one['battlefield'])
                            energy=sum(c['count'] for c in one['counters'] if c['kind']=='ENERGY');assert energy==(0 if case.endswith('two') else 3),energy
                        elif follow_draw:
                            assert after==before[1:] and before[0] in hand
                            assert one.get('libraryTop',{}).get('id')!=before[0]
                            assert not m['views'][1]['players'][0].get('libraryTop'),'Old public top survived draw'
                        else:
                            creature=bf[source];assert creature['power']==3 and creature['toughness']==2
                            if case.endswith(('keep','shuffle')):
                                assert after==before and one.get('libraryTop',{}).get('id')==before[0]
                                assert m['views'][1]['players'][0].get('libraryTop',{}).get('id')==before[0]
                                if case.endswith('shuffle'):
                                    adapter=json.loads((OUT/(case+'-resolved-proof.json')).read_text(encoding='utf-8'))['shuffleAdapter'];assert adapter['sameTopId'] and adapter['cleared']
                            else:assert before[0] in graves and after==before[1:] and not one.get('libraryTop')
                        assert one['life']==20
                        return {'case':case,'PASS':True,'chosen':chosen,'ordered':ordered,'steps':step_counts,'top':one.get('libraryTop',{}).get('name')}
                elif 'PayMana' in kind:pick='ok'
                elif kind=='InputConfirm':
                    save(m,'confirm-'+str(step_counts.get(kind,0)))
                    pick='cancel' if case.endswith(('no','keep','draw','shuffle')) else 'ok'
                elif kind=='ChooseNumber':
                    save(m,'number-'+str(step_counts.get(kind,0)))
                    wanted='3' if not x_chosen else ('3' if case.endswith('two') else '0')
                    current=r.get('selectedValue',0);wanted=int(wanted)
                    if current<wanted:pick='number-plus'
                    elif current>wanted:pick='number-minus'
                    else:pick='confirm-number';x_chosen=True
                elif kind=='OrderCards':
                    save(m,'order-'+str(step_counts.get(kind,0)))
                    options=[o for o in r['options'] if o['id'].startswith(('order:','select:')) and not o.get('selected')]
                    if options:pick=options[-1]['id'];ordered.append(options[-1]['card']['id'])
                    else:pick='confirm-cards' if 'confirm-cards' in ids else 'confirm-order'
                elif kind=='RevealCards':save(m,'reveal-'+str(step_counts.get(kind,0)));pick='confirm-cards'
                elif kind=='Scry':pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
                elif kind in ('ChooseCards','dialog','InputSelectEntitiesFromList') or kind.startswith('InputSelectCards'):
                    save(m,'select-'+str(step_counts.get(kind,0)))
                    if kind=='dialog' and all(o['label'].isdigit() for o in r['options']):
                        pick=next(o['id'] for o in r['options'] if o['label']==('3' if case.endswith('two') else '0'))
                        step_counts[kind]=step_counts.get(kind,0)+1
                        process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush();continue
                    options=[o for o in r['options'] if o.get('card') and o['id'].startswith(('select:','card:','value:'))]
                    confirm=next((i for i in ('ok','confirm-cards') if i in ids),None)
                    if case.startswith('growth') and case.endswith(('no','cancel')):pick=confirm or 'cancel'
                    elif confirm and (len(chosen)>=(2 if case.startswith('brain') else 1)):pick=confirm
                    else:
                        available=[o for o in options if o['card']['id'] not in chosen]
                        if case.startswith('genesis'):available=[o for o in available if (o['card'].get('land',False))!=(case.endswith('spell'))]
                        option=available[0];pick=option['id'];chosen.append(option['card']['id'])
                elif kind=='OrderAbilities':pick='confirm-order'
                step_counts[kind]=step_counts.get(kind,0)+1
                assert pick in ids,(case,kind,pick,ids,r['message'])
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('Step budget')
        finally:process.kill();process.wait(timeout=5)
results=[]
for case in sys.argv[1:] or ['growth-yes','growth-no','growth-cancel','genesis-yes','genesis-no','genesis-spell','brain-two','wrath-two','wrath-zero','explore-keep','explore-grave','explore-draw','explore-shuffle']:
    try:result=run(case)
    except Exception:result={'case':case,'PASS':False,'error':traceback.format_exc()}
    results.append(result);print(json.dumps(result,ensure_ascii=False),flush=True)
    (OUT/(case+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    (OUT/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    if not result['PASS']:raise SystemExit(1)
