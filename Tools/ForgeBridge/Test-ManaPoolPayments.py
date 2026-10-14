"""Actual pool payment and unchanged wrong-color retry; production bridge."""
import importlib.util,json,sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('control',ROOT/'Tools/ForgeBridge/Test-PlayerControl.py')
control=importlib.util.module_from_spec(spec);spec.loader.exec_module(control)
control.OUT=ROOT/'Tools/Validation/Reports/ManaPoolPayments';control.OUT.mkdir(parents=True,exist_ok=True)
pending=None;tried=False;retried=False;paid=False
def snapshot(message,name):
    view=dict(message['views'][0]);view.update(request=message['request'],awaitingInput=True)
    (control.OUT/(name+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
def observe(message,pick):
    global pending,tried,retried,paid
    r=message['request'];view=message['views'][0];player=view['players'][1]
    if pending is not None:
        assert r['kind']==pending['request']['kind'] and r['contextId']==pending['request']['contextId'],'Payment context changed after unpayable color'
        assert r['requestId']!=pending['request']['requestId'],'No fresh payment request'
        assert view['players']==pending['views'][0]['players'],'Unpayable color consumed mana/changed cards'
        assert r['message']==pending['request']['message'],'Unpayable color changed remaining cost'
        assert [o['id'] for o in r['options']]==[o['id'] for o in pending['request']['options']],'Retry lost offered sources'
        snapshot(message,'wrong-color-retry');pending=None;retried=True
    controlled=r['seat']==1 and r['playerSeat']==2 and player.get('controllingSeat')==1
    if controlled and 'PayMana' in r['kind'] and r['message'].endswith('Pay Mana Cost: {U}') and player['manaPool'][1]==2:
        assert pick=='mana:U','Native available blue not selected'
        snapshot(message,'blue-payment');paid=True
    if controlled and r['kind']=='Scry':
        assert paid and player['manaPool'][1]==1 and view['players'][0]['manaPool'][1]==0,'Valid blue did not spend from correct pool'
        snapshot(message,'blue-paid-scry')
    if controlled and 'PayMana' in r['kind'] and r['message'].endswith('Pay Mana Cost: {R}') and not tried:
        assert any(o['id']=='mana:U' for o in r['options']) and player['manaPool'][1]==1,'Missing unpayable blue option'
        snapshot(message,'wrong-color-before');pending=json.loads(json.dumps(message));tried=True
        return 'mana:U'
    return None

try:
    result=control.run('pool-retry',observe)
    assert tried and retried and paid,'Missing manual payment/retry evidence'
    result='PASS pool-retry: native blue 2 -> 1 from controlled player, unpayable blue for red leaves complete players/remaining cost/options unchanged; fresh same-context request; correct red source then Shock/Opt/control/attack resolve and release'
except Exception as error:result='FAIL pool-retry: '+repr(error)
(control.OUT/'result.txt').write_text(result,encoding='utf8');print(result,flush=True)
sys.exit(result.startswith('FAIL'))
