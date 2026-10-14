"""Real shared tap-payment flow: convoke, improvise, waterbend, reset/zero/cancel."""
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import tempfile
import threading

root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
classes=root/'.tools/tap-payment/classes';out=root/'Tools/Validation/Reports/TapPayment';out.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/TapPaymentFixture.java')],check=True)

def run(case):
    kind=case.split('-')[0];spell={'convoke':'Venerated Loxodon','improvise':'Herald of Anguish','waterbend':'Ruthless Waterbender'}[kind]
    with tempfile.TemporaryDirectory(prefix='mtgb-tap-payment-') as temp,(out/(case+'.log')).open('w',encoding='utf8') as log,(out/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
        path=Path(temp)/'start.json';path.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])))
        p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,runtime/'classes',runtime/'lib/*'])),'mtgb.bridge.TapPaymentFixture',str(runtime/cfg['assets']),str(path)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue();threading.Thread(target=lambda:[messages.put(json.loads(line)) for line in p.stdout],daemon=True).start()
        selected_ids=[];cast=False;selection=False;steps=0;cancelled=False;initial=None
        def send(message,option,seat=None):
            r=message['request'];p.stdin.write(json.dumps(dict(type='respond',session=case,seat=seat or r['seat'],requestId=r['requestId'],option=option))+'\n');p.stdin.flush()
        def snap(message,label):
            view=dict(message['views'][0],request=message['request'],awaitingInput=True)
            (out/(case+'-'+label+'.json')).write_text(json.dumps(view),encoding='utf8')
        try:
            for _ in range(180):
                m=messages.get(timeout=30);trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','rejected','ended'),m
                if m['type']!='request':continue
                r=m['request'];view=m['views'][0];board=view['players'][0]['battlefield'];request_kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
                if request_kind=='InputPassPriority' and r['seat']==1 and view['phase']=='MAIN1':
                    if not cast:
                        initial=view;source=next(c for c in view['players'][0]['battlefield' if kind=='waterbend' else 'hand'] if c['name']==spell)
                        pick='card:'+str(source['id']);cast=True
                    elif selection and not view['stackItems']:
                        if cancelled:
                            assert any(c['name']==spell for c in view['players'][0]['hand'])
                            assert all(not c['tapped'] for c in board if c['id'] in selected_ids), 'Cancelled payment left permanents tapped'
                        elif kind=='waterbend':
                            source=next(c for c in board if c['name']==spell);assert (source['power'],source['toughness'])==(2,4)
                            assert all(next(c for c in board if c['id']==identity)['tapped'] for identity in selected_ids)
                        else:
                            assert any(c['name']==spell for c in board)
                            assert all(next(c for c in board if c['id']==identity)['tapped'] for identity in selected_ids)
                            if kind=='convoke':
                                assert all(next(c for c in board if c['id']==identity)['power']==2 for identity in selected_ids)
                        if case.endswith('zero'):assert not selected_ids and sum(c['tapped'] for c in board if c['land'])==5
                        snap(m,'resolved');return 'PASS '+case+': real tap-payment selection, costs and final state'
                elif request_kind=='dialog':pick=next(o['id'] for o in r['options'] if o['id']!='cancel')
                elif request_kind=='OrderAbilities':
                    pending=[o for o in r['options'] if o['id'].startswith('order:') and not o.get('selected')]
                    pick=pending[0]['id'] if pending else 'confirm-order'
                elif request_kind=='InputSelectCardsForConvokeOrImprovise':
                    assert r['selectionPurpose']==kind and r['sourceCard']['name']==spell
                    assert r.get('remainingManaCost') is not None
                    assert all(not o['card']['tapped'] for o in r['options'] if o.get('card')), 'Selected card tapped before confirmation'
                    selection=True;cards=[o for o in r['options'] if o.get('card')]
                    if not selected_ids and not case.endswith('zero'):
                        wanted=['Tormod\'s Crypt','Grizzly Bears'] if kind=='waterbend' else ["Tormod's Crypt" if kind=='improvise' else 'Soul Warden']* (2 if case.endswith('cancel') else 5)
                        available=list(cards)
                        for name in wanted:
                            card=next(o for o in available if o['card']['name']==name);selected_ids.append(card['card']['id']);available.remove(card)
                    sequence=[] if case.endswith('zero') else ['card:'+str(selected_ids[0]),'card:'+str(selected_ids[0]),'card:'+str(selected_ids[0]),'reset-cards']+['card:'+str(c) for c in selected_ids]
                    if steps==0:
                        send(m,'card:999999');assert messages.get(timeout=5)['error']=='invalid-option'
                        send(m,'ok',seat=2);assert messages.get(timeout=5)['error']=='wrong-seat'
                    expected_count=[0,1,0,1]+list(range(len(selected_ids)+1)) if sequence else [0]
                    assert r['selectedCount']==expected_count[steps],(case,steps,r['selectedCount'],expected_count)
                    snap(m,'selection-'+str(steps))
                    if steps<len(sequence):pick=sequence[steps];steps+=1
                    else:pick='ok'
                elif 'PayMana' in request_kind:
                    if case.endswith('cancel'):
                        assert 'cancel' in ids;pick='cancel';cancelled=True
                    else:
                        mana=[i for i in ids if i.startswith('mana:')]
                        color='Swamp' if kind=='improvise' else 'Plains'
                        pick='ok' if 'ok' in ids else mana[0] if mana else next(o['id'] for o in r['options'] if o['label'].startswith(color+' ['))
                assert pick in ids,(case,request_kind,pick,ids)
                send(m,pick)
            raise AssertionError('No final resolution')
        finally:
            if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    try:
        for case in sys.argv[1:] or ['convoke','convoke-zero','convoke-cancel','improvise','waterbend','convoke-only','improvise-only','waterbend-only']:
            result=run(case);results.append(result);print(result,flush=True)
    finally:(out/'engine-result.txt').write_text('\n'.join(results),encoding='utf8')
