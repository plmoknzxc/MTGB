"""Real native secret ETB/reveal costs. Initial positioning only; strict result gate."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
OUT=ROOT/'.tools/repeat-reveal-names-1011/reveals';OUT.mkdir(parents=True,exist_ok=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
FIXTURES=['RoyalStateFixture.java','DrainManaDeltaFixture.java','RevealChosenPayloadFixture.java']
CASES={
 'killer-type-tax':[
  dict(Hand=['A Killer Among Us','A Killer Among Us'],Battlefield=['Suppression Field']+['Forest']*16),dict(Hand=[],Battlefield=[])],
 'guardian-player-tax':[
  dict(Hand=['Guardian Archon','Shock'],Battlefield=['Suppression Field','Grizzly Bears']+['Plains']*10+['Mountain']*3),dict(Hand=['Shock'],Battlefield=['Mountain']*3)],
 'emissary-payload':[
  dict(Hand=['Emissary of Grudges','Emissary of Grudges','Shock'],Battlefield=['Suppression Field','Grizzly Bears']+['Mountain']*18),dict(Hand=['Shock'],Battlefield=['Mountain']*3)],
 'leonin-payload':[
  dict(Hand=['Stalking Leonin','Stalking Leonin'],Battlefield=['Suppression Field']+['Plains']*12),dict(Hand=[],Battlefield=['Grizzly Bears']),dict(Hand=[],Battlefield=['Hill Giant'])]}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p))
class Flow:
 def __init__(self,case,folder):
  self.case=case;self.folder=folder;self.stage='initial';self.labels=[];self.checks=0;self.violations=[];self.paid=[];self.sources=[];self.target=None;self.before=None;self.after=None;self.castindex=0;self.attempt=0;self.full=os.environ.get('MTGB_REVEAL_REPRO_ONLY')!='1'
 def check(self,ok,label):
  self.checks+=1
  if not ok:self.violations.append(label)
 def snap(self,m,a,label):
  if (self.folder/(label+'-envelope.json')).exists():label=label+'-request'+str(m['request']['requestId'])
  write(self.folder/(label+'-envelope.json'),m);write(self.folder/(label+'-native.json'),a)
  for i,v in enumerate(m['views'],1):write(self.folder/(label+'-seat'+str(i)+'.json'),dict(v,request=m['request'],awaitingInput=True))
  self.labels.append(dict(label=label,requestId=m['request']['requestId'],kind=m['request']['kind'],seat=m['request']['seat']))
 def cards(self,a,seat=1,zone='Battlefield'):return a['players'][seat-1]['zones'][zone]
 def card(self,a,name,seat=1,zone='Battlefield'):return next(c for c in self.cards(a,seat,zone) if c['name']==name)
 def find(self,a,cid):return next((c for p in a['players'] for z in p['zones'].values() for c in z if c['id']==cid),None)
 def opts(self,r):return [o for o in r['options'] if o.get('enabled',True) and not o.get('disabled',False)]
 def ids(self,r):return [o['id'] for o in self.opts(r)]
 def targetpick(self,r,cid=None,player=None,stacksource=None):
  ids=self.ids(r)
  if 'ok' in ids:return 'ok'
  for o in self.opts(r):
   if cid is not None and o.get('card',{}).get('id')==cid:return o['id']
   if cid is not None and o['id']=='card:'+str(cid):return o['id']
   if player is not None and o['id']=='player:'+str(player):return o['id']
   if stacksource is not None and (o.get('card',{}).get('id')==stacksource or o.get('sourceCardId')==stacksource):return o['id']
  raise AssertionError(('Exact requested target absent',cid,player,stacksource,r))
 def castrecord(self,m,a,cid,name,mana,label):
  stack=next(s for s in a['stack'] if s['source']==cid)
  self.check(len(stack['mana'])==mana,'actual '+name+' mana '+str(mana));self.paid.append(dict(source=cid,name=name,mana=mana,requestId=m['request']['requestId'],ability=stack['id']));self.snap(m,a,label)
 def secret(self,a,cid,kind,expected):
  c=self.find(a,cid);self.check(c['chosen'+kind]==expected,'actual ETB secret '+kind);self.check(c['viewChosen'+kind] in ('null','',0),'native secret unpublished before reveal')
 def process(self,m,a):
  r=m['request'];k=r['kind'];ids=self.ids(r)
  if a:
   self.check(a['requestId']==r['requestId'],'observer exact request')
   for seat,v in enumerate(m['views'],1):
    self.check(all(not p.get('hand',[]) for i,p in enumerate(v['players'],1) if i!=seat),'recipient hand privacy')
  if self.case=='killer-type-tax':pick,done=self.killer(m,a)
  elif self.case=='guardian-player-tax':pick,done=self.guardian(m,a)
  elif self.case=='emissary-payload':pick,done=self.emissary(m,a)
  else:pick,done=self.leonin(m,a)
  if pick is not None or done:return pick,done
  if 'PayMana' in k:
   if self.stage=='cancel-mana':self.snap(m,a,'partial-costs');self.check(any(x['part']=='CostPartMana' for x in a['costs']),'tax mana gate reached');self.check(any(x['snapshot'] for x in a['costs']) or self.case!='killer-type-tax','type cancellation snapshot present');self.stage='cancel-submitted';return 'cancel',None
   return 'ok',None
  if k=='OrderCards':
   available=[o for o in self.opts(r) if o['id'].startswith('select:') and not o.get('selected')]
   def rank(o):
    s=o['label'].lower();return 0 if 'sacrifice' in s else 1 if 'reveal' in s else 2
   return min(available,key=rank)['id'] if available else 'confirm-cards',None
  if k=='dialog':
   if r['message']=='getAbilityToPlay':return next(o['id'] for o in self.opts(r) if o['id'] not in ('cancel','ok')),None
   if 'choose' in r['message'].lower():
    if 'type' in r['message'].lower():return next(o['id'] for o in self.opts(r) if o['label']=='Human'),None
    return next(o['id'] for o in self.opts(r) if o.get('player',{}).get('seat')==2 or o['label']=='Player 2'),None
   raise AssertionError(('Unknown dialog',self.stage,r))
  if k=='InputMulligan':return 'keep',None
  if k=='InputSelectEntitiesFromList' and 'choose a player' in r['message'].lower():
   return 'ok' if 'ok' in ids else next(o['id'] for o in self.opts(r) if o['label']=='Player 2'),None
  if k in ('InputConfirmMulligan','InputConfirm','InputPassPriority','InputAttack','InputBlock'):return 'ok',None
  raise AssertionError(('Unhandled request',self.stage,r))
 def killer(self,m,a):
  r=m['request'];k=r['kind'];ids=self.ids(r)
  if k=='InputPassPriority' and a:
   if r['seat']==1 and a['phase']=='MAIN1':
    if self.stage=='initial':
     self.snap(m,a,'opening');self.sources=[c['id'] for c in self.cards(a,zone='Hand') if c['name']=='A Killer Among Us'];self.stage='casting';return 'card:'+str(self.sources[0]),None
    if self.stage=='casting' and any(s['source']==self.sources[0] for s in a['stack']):self.castrecord(m,a,self.sources[0],'A Killer Among Us',5,'paid-source-stack');self.stage='resolve-secret'
    elif self.stage=='resolve-secret' and not a['stack']:
     self.secret(a,self.sources[0],'Type','Human');tokens=[c for c in self.cards(a) if c['token']];self.check(len(tokens)==3,'actual ETB exactly three original tokens');self.target=next(c['id'] for c in tokens if 'Human' in c['type']);self.goblin=next(c['id'] for c in tokens if 'Goblin' in c['type']);self.snap(m,a,'secret-etb');self.stage='wait-attack'
     if self.full:self.stage='second-casting';return 'card:'+str(self.sources[1]),None
    elif self.stage=='second-casting' and any(s['source']==self.sources[1] for s in a['stack']):self.castrecord(m,a,self.sources[1],'A Killer Among Us second original',5,'second-paid-source-stack');self.stage='second-secret'
    elif self.stage=='second-secret' and not a['stack']:
     self.secret(a,self.sources[1],'Type','Human');self.check(len([c for c in self.cards(a) if c['token']])==6,'two actual ETB produce six distinct tokens');self.snap(m,a,'second-secret-etb');self.stage='wait-attack'
   if self.stage=='wait-attack' and r['seat']==1 and a['turn']>=3 and a['phase']=='COMBAT_DECLARE_ATTACKERS' and not a['stack']:
    self.before=a;self.snap(m,a,'before-reveal');self.stage='targeting';return 'card:'+str(self.sources[0]),None
   if self.stage=='cancel-submitted' and r['seat']==1:
    self.snap(m,a,'after-cancel');c=self.find(a,self.sources[0]);self.check(c is not None and c['zone']=='Battlefield','cancel returns exact sacrificed source');self.check(c is not None and c['viewChosenType']=='Human','restored permanent retains legally revealed type');self.check(any(n['message']=='Player 1 reveals Human' for n in m['views'][1]['notices']),'opponent retains exact actual revealed information');self.check(self.find(a,self.target)['power']==1 and not self.find(a,self.target)['deathtouch'],'cancel applies no payload');self.check(not a['stack'],'cancel creates no ability stack')
    if not self.full or self.violations:return None,'Type tax cancellation actual gate and original identity audited'
    self.check([(c['id'],c['tapped'],c['timestamp']) for c in self.cards(a)]==[(c['id'],c['tapped'],c['timestamp']) for c in self.cards(self.before)],'original battlefield IDs/order/tap/timestamps restored');self.check([c['id'] for c in self.cards(a,zone='Library')]==[c['id'] for c in self.cards(self.before,zone='Library')],'entire original library order preserved');self.stage='killer-positive-target';return 'card:'+str(self.sources[0]),None
   if self.stage=='killer-positive-paying' and r['seat']==1 and a['stack']:
    self.castrecord(m,a,self.sources[0],'Killer matching activation',2,'positive-paid-activation-stack');self.check(next(s for s in a['stack'] if s['source']==self.sources[0])['hostChosenType']=='Human','real sacrificed ability host retains original choice');self.stage='killer-positive-resolving'
   elif self.stage=='killer-positive-resolving' and r['seat']==1 and not a['stack']:
    self.snap(m,a,'matching-payload');c=self.find(a,self.target);self.check(c['power']==4 and c['toughness']==4 and c['deathtouch'] and 'P1P1 x 3' in c['counters'],'matching original attacking Human gains3counters/deathtouch');self.stage='killer-negative-target';return 'card:'+str(self.sources[1]),None
   elif self.stage=='killer-negative-paying' and r['seat']==1 and a['stack']:
    self.castrecord(m,a,self.sources[1],'Killer nonmatching activation',2,'negative-paid-activation-stack');self.stage='killer-negative-resolving'
   elif self.stage=='killer-negative-resolving' and r['seat']==1 and not a['stack']:
    self.snap(m,a,'nonmatching-payload');c=self.find(a,self.goblin);self.check(c['power']==1 and c['toughness']==1 and not c['deathtouch'] and c['counters']=='[]','nonmatching original attacking Goblin gains no effects');self.stage='killer-expiry'
   elif self.stage=='killer-expiry' and r['seat']==1 and a['turn']>=5 and a['phase']=='MAIN1':
    self.snap(m,a,'next-own-priority');c=self.find(a,self.target);self.check(c['power']==4 and c['toughness']==4 and not c['deathtouch'],'real EOT removes deathtouch and keeps counters');self.check(all(self.find(a,x)['zone']=='Graveyard' for x in self.sources),'both original sources sacrificed by successful activations');return None,'Type source refund/knowledge retention, real paid retry positive and distinct nonmatching attacking token negative with EOT'
  if k=='InputAttack' and self.stage=='wait-attack' and a and a['turn']>=3:
   targets=[self.target,self.goblin] if self.full else [self.target]
   count=getattr(self,'attackSubmitted',0)
   if count<len(targets):
    pick=next(o['id'] for o in self.opts(r) if o['id'].startswith('attack:'+str(targets[count])+':'));self.attackSubmitted=count+1;return pick,None
   self.stage='wait-attack';return 'ok',None
  if k=='InputSelectTargets' and self.stage=='targeting':
   pick=self.targetpick(r,cid=self.target)
   if pick=='ok':self.stage='cancel-mana'
   return pick,None
  if k=='InputSelectTargets' and self.stage in ('killer-positive-target','killer-negative-target'):
   positive=self.stage=='killer-positive-target';pick=self.targetpick(r,cid=self.target if positive else self.goblin)
   if pick=='ok':self.stage='killer-positive-paying' if positive else 'killer-negative-paying'
   return pick,None
  return None,None
 def guardian(self,m,a):
  r=m['request'];k=r['kind'];seat=r['seat'];ids=self.ids(r)
  if k=='InputPassPriority' and a:
   if self.stage=='initial' and seat==1 and a['phase']=='MAIN1':
    self.snap(m,a,'opening');self.sources=[self.card(a,'Guardian Archon',zone='Hand')['id']];self.target=self.card(a,'Grizzly Bears')['id'];self.ownShock=self.card(a,'Shock',zone='Hand')['id'];self.enemyShock=self.card(a,'Shock',seat=2,zone='Hand')['id'];self.stage='guardian-casting';return 'card:'+str(self.sources[0]),None
   if self.stage=='guardian-casting' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[0],'Guardian Archon',6,'paid-source-stack');self.stage='guardian-secret'
   elif self.stage=='guardian-secret' and not a['stack'] and seat==1:
    self.secret(a,self.sources[0],'Player',2);self.snap(m,a,'secret-etb');self.before=a;self.stage='guardian-target-cancel';return 'card:'+str(self.sources[0]),None
   elif self.stage=='cancel-submitted' and seat==1:
    self.snap(m,a,'after-cancel');c=self.find(a,self.sources[0]);ab=next(s for s in c['abilities'] if s['api']=='Pump' and not s['spell']);self.check(c['zone']=='Battlefield' and c['viewChosenPlayer']==2,'cancel preserves original source and public player');self.check(ab['activationGame']==0,'cancel does not consume once-per-object activation');self.check(not a['stack'],'cancel creates no stack entry');self.check(a['players'][0]['mana']==self.before['players'][0]['mana'],'cancel pool exact');self.check(not any(x.startswith('Protection:') for x in self.find(a,self.target)['keywords']),'cancel no premature permanent protection');self.check(not any(x.startswith('Protection:') for x in a['players'][0]['keywords']),'cancel no premature player protection');self.stage='guardian-target-retry';return 'card:'+str(self.sources[0]),None
   elif self.stage=='guardian-paying' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[0],'Guardian Archon activation',2,'paid-activation-stack');self.check(next(s for s in a['stack'] if s['source']==self.sources[0])['activationGame']==1,'successful activation once count1');self.stage='guardian-resolving'
   elif self.stage=='guardian-resolving' and not a['stack'] and seat==1:
    self.snap(m,a,'protected-payload');self.check(any(x.startswith('Protection:Player.PlayerUID_1') for x in self.find(a,self.target)['keywords']),'original permanent protection chosen native player UID1');self.check(any(x.startswith('Protection:Player.PlayerUID_1') for x in a['players'][0]['keywords']),'original player protection chosen native player UID1');self.stage='own-shock-target';return 'card:'+str(self.ownShock),None
   elif self.stage=='own-shock-paying' and seat==1 and a['stack']:
    self.castrecord(m,a,self.ownShock,'nonchosen controller Shock',1,'own-shock-stack');self.stage='own-shock-resolving'
   elif self.stage=='own-shock-resolving' and not a['stack'] and seat==1:
    self.snap(m,a,'nonchosen-shock-result');self.check(a['players'][0]['life']==28,'nonchosen own Shock actually damages protected player2');self.stage='enemy-shock-casting'
   elif self.stage=='enemy-shock-casting' and seat==2:
    self.stage='enemy-shock-target';return 'card:'+str(self.enemyShock),None
   elif self.stage=='enemy-shock-paying' and seat==2 and any(s['source']==self.enemyShock for s in a['stack']):
    self.castrecord(m,a,self.enemyShock,'chosen controller Shock legal other target',1,'enemy-shock-stack');self.stage='enemy-shock-resolving'
   elif self.stage=='enemy-shock-resolving' and not a['stack']:
    self.snap(m,a,'chosen-shock-result');self.check(a['players'][0]['life']==28 and self.find(a,self.target)['damage']==0,'chosen controller cannot hit protected player/permanent');self.check(self.find(a,self.sources[0])['damage']==2,'actual chosen Shock hits legal unprotected Archon');self.stage='guardian-expiry'
   elif self.stage=='guardian-expiry' and seat==1 and a['turn']>=3 and a['phase']=='MAIN1':
    self.snap(m,a,'next-own-priority');c=self.find(a,self.sources[0]);ab=next(s for s in c['abilities'] if s['api']=='Pump' and not s['spell']);self.check(ab['activationGame']==1,'activation once limit persists across turn');self.check('card:'+str(self.sources[0]) not in ids,'once-per-object source cannot reactivate next turn');self.check(not any(x.startswith('Protection:') for x in self.find(a,self.target)['keywords']) and not any(x.startswith('Protection:') for x in a['players'][0]['keywords']),'protection expires at actual EOT');return None,'Guardian secret reveal tax cancel/retry, two-seat chosen/nonchosen protection and once limit actual outcomes'
  if k=='InputSelectTargets':
   if self.stage in ('guardian-target-cancel','guardian-target-retry'):
    pick=self.targetpick(r,cid=self.target)
    if pick=='ok':self.stage='cancel-mana' if self.stage.endswith('cancel') else 'guardian-paying'
    return pick,None
   if self.stage=='own-shock-target':
    pick=self.targetpick(r,player=m['views'][0]['players'][0]['targetId'])
    if pick=='ok':self.stage='own-shock-paying'
    return pick,None
   if self.stage=='enemy-shock-target':
    if not getattr(self,'negativeCaptured',False):
     self.snap(m,a,'chosen-controller-target-exclusions');self.check('player:'+str(m['views'][0]['players'][0]['targetId']) not in ids,'chosen controller cannot target original protected player');self.check('card:'+str(self.target) not in ids,'chosen controller cannot target original protected permanent');self.negativeCaptured=True
    pick=self.targetpick(r,cid=self.sources[0])
    if pick=='ok':self.stage='enemy-shock-paying'
    return pick,None
  return None,None
 def emissary(self,m,a):
  r=m['request'];k=r['kind'];seat=r['seat'];ids=self.ids(r)
  if k=='InputPassPriority' and a:
   if self.stage=='initial' and seat==1 and a['phase']=='MAIN1':
    self.snap(m,a,'opening');self.sources=[c['id'] for c in self.cards(a,zone='Hand') if c['name']=='Emissary of Grudges'];self.ownShock=self.card(a,'Shock',zone='Hand')['id'];self.enemyShock=self.card(a,'Shock',seat=2,zone='Hand')['id'];self.stage='emissary-first-casting';return 'card:'+str(self.sources[0]),None
   if self.stage=='emissary-first-casting' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[0],'Emissary first original',6,'first-paid-source-stack');self.stage='emissary-first-secret'
   elif self.stage=='emissary-first-secret' and seat==1 and not a['stack']:
    self.secret(a,self.sources[0],'Player',2);self.snap(m,a,'first-secret-etb');self.stage='emissary-second-casting';return 'card:'+str(self.sources[1]),None
   elif self.stage=='emissary-second-casting' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[1],'Emissary second original',6,'second-paid-source-stack');self.stage='emissary-second-secret'
   elif self.stage=='emissary-second-secret' and seat==1 and not a['stack']:
    self.secret(a,self.sources[1],'Player',2);self.snap(m,a,'second-secret-etb');self.stage='emissary-own-shock-target';return 'card:'+str(self.ownShock),None
   elif self.stage=='emissary-own-shock-paying' and seat==1 and any(s['source']==self.ownShock for s in a['stack']):
    self.castrecord(m,a,self.ownShock,'nonchosen pending Shock',1,'nonchosen-pending-spell');self.stage='emissary-negative-stack-target';return 'card:'+str(self.sources[0]),None
   elif self.stage=='emissary-negative-paying' and seat==1 and len(a['stack'])==2:
    self.castrecord(m,a,self.sources[0],'Emissary nonchosen controller activation',2,'negative-paid-activation-stack');self.stage='emissary-negative-resolving'
   elif self.stage=='emissary-negative-resolving' and len(a['stack'])==1 and a['stack'][0]['source']==self.ownShock:
    self.snap(m,a,'nonchosen-controller-no-retarget');self.check(a['stack'][0]['actor']==1 and a['stack'][0]['targetPlayers']==[1],'nonchosen spell original controller/target remains');self.check(next(s for s in self.find(a,self.sources[0])['abilities'] if s['api']=='ChangeTargets')['activationGame']==1,'actual nonmatching activation consumes once limit');self.stage='emissary-own-shock-resolving'
   elif self.stage=='emissary-own-shock-resolving' and not a['stack']:
    self.snap(m,a,'nonchosen-controller-result');self.check(a['players'][0]['life']==28 and a['players'][1]['life']==30,'nonchosen original Shock hits own player unchanged');self.stage='emissary-enemy-shock-casting'
   elif self.stage=='emissary-enemy-shock-casting' and seat==2:
    self.stage='emissary-enemy-shock-target';return 'card:'+str(self.enemyShock),None
   elif self.stage=='emissary-enemy-shock-paying' and seat==1 and any(s['source']==self.enemyShock for s in a['stack']):
    self.castrecord(m,a,self.enemyShock,'chosen controller pending Shock',1,'chosen-pending-spell');self.stage='emissary-positive-stack-target';return 'card:'+str(self.sources[1]),None
   elif self.stage=='emissary-positive-paying' and seat==1 and len(a['stack'])==2:
    self.castrecord(m,a,self.sources[1],'Emissary chosen controller activation',2,'positive-paid-activation-stack');self.stage='emissary-positive-resolving'
   elif self.stage=='emissary-positive-retargeted' and len(a['stack'])==1 and a['stack'][0]['source']==self.enemyShock:
    self.snap(m,a,'chosen-controller-retargeted');self.check(a['stack'][0]['actor']==2 and a['stack'][0]['targetPlayers']==[2],'original enemy spell retains controller2 and receives exact new player2 target');self.stage='emissary-enemy-shock-resolving'
   elif self.stage=='emissary-enemy-shock-resolving' and not a['stack']:
    self.snap(m,a,'chosen-controller-result');self.check(a['players'][0]['life']==28 and a['players'][1]['life']==28,'chosen original enemy Shock hits actual new target2');self.check(all(next(s for s in self.find(a,cid)['abilities'] if s['api']=='ChangeTargets')['activationGame']==1 for cid in self.sources),'each original source once count1');return None,'Emissary independent actual chosen/nonchosen controller conditions, original pending spell retarget and exact damage outcomes'
  if k=='InputSelectTargets':
   if self.stage in ('emissary-own-shock-target','emissary-enemy-shock-target'):
    own=self.stage=='emissary-own-shock-target';pick=self.targetpick(r,player=m['views'][0]['players'][0]['targetId'])
    if pick=='ok':self.stage='emissary-own-shock-paying' if own else 'emissary-enemy-shock-paying'
    return pick,None
   if self.stage=='emissary-positive-resolving':
    self.snap(m,a,'actual-retarget-picker');pick=self.targetpick(r,player=m['views'][0]['players'][1]['targetId'])
    if pick=='ok':self.stage='emissary-positive-retargeted'
    return pick,None
  if k=='SelectStackTarget' and self.stage in ('emissary-negative-stack-target','emissary-positive-stack-target'):
   positive=self.stage=='emissary-positive-stack-target';source=self.enemyShock if positive else self.ownShock;self.snap(m,a,('positive' if positive else 'negative')+'-actual-stack-target');pick='ok' if 'ok' in ids else 'stack:'+str(next(s['instance'] for s in a['stack'] if s['source']==source))
   if pick=='ok':self.stage='emissary-positive-paying' if positive else 'emissary-negative-paying'
   return pick,None
  return None,None
 def leonin(self,m,a):
  r=m['request'];k=r['kind'];seat=r['seat'];ids=self.ids(r)
  if k=='InputPassPriority' and a:
   if self.stage=='initial' and seat==1 and a['phase']=='MAIN1':
    self.snap(m,a,'opening');self.sources=[c['id'] for c in self.cards(a,zone='Hand') if c['name']=='Stalking Leonin'];self.victim2=self.card(a,'Grizzly Bears',seat=2)['id'];self.victim3=self.card(a,'Hill Giant',seat=3)['id'];self.stage='leonin-first-casting';return 'card:'+str(self.sources[0]),None
   if self.stage=='leonin-first-casting' and seat==1 and any(s['source']==self.sources[0] and s['spell'] for s in a['stack']):
    self.castrecord(m,a,self.sources[0],'Leonin first original',3,'first-paid-source-stack');self.stage='leonin-first-secret'
   elif self.stage=='leonin-first-secret' and seat==1 and not a['stack']:
    self.secret(a,self.sources[0],'Player',2);self.snap(m,a,'first-secret-etb');self.stage='leonin-second-casting';return 'card:'+str(self.sources[1]),None
   elif self.stage=='leonin-second-casting' and seat==1 and any(s['source']==self.sources[1] and s['spell'] for s in a['stack']):
    self.castrecord(m,a,self.sources[1],'Leonin second original',3,'second-paid-source-stack');self.stage='leonin-second-secret'
   elif self.stage=='leonin-second-secret' and seat==1 and not a['stack']:
    self.secret(a,self.sources[1],'Player',2);self.snap(m,a,'second-secret-etb');self.stage='leonin-wait-positive-attack'
   elif self.stage=='leonin-wait-positive-attack' and seat==1 and a['phase']=='COMBAT_DECLARE_ATTACKERS' and any(c['id']==self.victim2 and c['defenderSeat']==1 for c in a['attackers']):
    self.snap(m,a,'chosen-controller-actual-attack');self.stage='leonin-positive-target';return 'card:'+str(self.sources[0]),None
   elif self.stage=='leonin-positive-paying' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[0],'Leonin chosen-controller activation',2,'positive-paid-activation-stack');self.stage='leonin-positive-resolving'
   elif self.stage=='leonin-positive-resolving' and not a['stack']:
    self.snap(m,a,'chosen-controller-exile');c=self.find(a,self.victim2);self.check(c['zone']=='Exile' and c['owner']==2 and c['controller']==2,'chosen original attacking creature2 exiled');self.check(a['players'][0]['life']==30,'chosen attacker dealt no combat damage');self.stage='leonin-wait-negative-attack'
   elif self.stage=='leonin-wait-negative-attack' and seat==1 and a['phase']=='COMBAT_DECLARE_ATTACKERS' and any(c['id']==self.victim3 and c['defenderSeat']==1 for c in a['attackers']):
    self.snap(m,a,'nonchosen-controller-actual-attack');self.stage='leonin-negative-target';return 'card:'+str(self.sources[1]),None
   elif self.stage=='leonin-negative-paying' and seat==1 and a['stack']:
    self.castrecord(m,a,self.sources[1],'Leonin nonchosen-controller activation',2,'negative-paid-activation-stack');self.stage='leonin-negative-resolving'
   elif self.stage=='leonin-negative-resolving' and not a['stack']:
    self.snap(m,a,'nonchosen-controller-no-exile');c=self.find(a,self.victim3);self.check(c['zone']=='Battlefield' and c['owner']==3 and c['controller']==3,'nonchosen original attacker3 remains battlefield');self.check(any(c['id']==self.victim3 and c['defenderSeat']==1 for c in a['attackers']),'nonchosen legal attack remains');self.check(next(s for s in self.find(a,self.sources[1])['abilities'] if s['api']=='ChangeZone')['activationGame']==1,'nonmatching actual activation consumes once limit');self.stage='leonin-negative-combat'
   elif self.stage=='leonin-negative-combat' and seat==1 and a['turnSeat']==3 and a['phase']=='MAIN2':
    self.snap(m,a,'next-priority-after-negative-combat');self.check(a['players'][0]['life']==27,'nonchosen actual attacker deals exactly3combat damage');self.check(all(next(s for s in self.find(a,cid)['abilities'] if s['api']=='ChangeZone')['activationGame']==1 for cid in self.sources),'two independent original once counters1');return None,'Leonin actual chosen attacker exiled and distinct nonchosen legal native attacker retained, original once counters and natural next priority'
  if k=='InputAttack' and a:
   if (self.stage=='leonin-wait-positive-attack' and seat==2) or (self.stage=='leonin-wait-negative-attack' and seat==3):
    cid=self.victim2 if seat==2 else self.victim3
    if not any(x['id']==cid for x in a['attackers']):return next(o['id'] for o in self.opts(r) if o['id'].startswith('attack:'+str(cid)+':') and o.get('defenderId')==m['views'][0]['players'][0]['targetId']),None
    return 'ok',None
  if k=='InputSelectTargets' and self.stage in ('leonin-positive-target','leonin-negative-target'):
   positive=self.stage=='leonin-positive-target';pick=self.targetpick(r,cid=self.victim2 if positive else self.victim3)
   if pick=='ok':self.stage='leonin-positive-paying' if positive else 'leonin-negative-paying'
   return pick,None
  return None,None
def run(case):
 cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));folder=OUT/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir();shutil.copyfile(__file__,folder/'driver.py')
 for n in FIXTURES:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 write(folder/'cases.json',CASES);shutil.copyfile(RT/'runtime.json',folder/'runtime.json')
 originals=['.tools/repeat-reveal-names-1011/plan.json','.tools/repeat-reveal-names-1011/graph-fallback.json','.tools/scope-classification-1008/classification.json','.tools/payment-snapshot-review-1008/classification.json']
 sourcepaths=['Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java','ForgeRuntime/source/native-patches/forge/game/cost/CostPayment.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/cost/CostRevealChosen.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/ability/effects/ChoosePlayerEffect.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/ability/effects/ChooseTypeEffect.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/ability/effects/ChangeTargetsEffect.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/spellability/SpellAbilityCondition.java','.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/spellability/SpellAbilityRestriction.java','ForgeRuntime/assets/res/cardsfolder/a/a_killer_among_us.txt','ForgeRuntime/assets/res/cardsfolder/e/emissary_of_grudges.txt','ForgeRuntime/assets/res/cardsfolder/g/guardian_archon.txt','ForgeRuntime/assets/res/cardsfolder/s/stalking_leonin.txt','ForgeRuntime/assets/res/cardsfolder/s/suppression_field.txt']
 sources=[]
 for name in originals+sourcepaths:
  src=ROOT/name;dst=folder/'frozen-source'/name;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst);sources.append(dict(origin=name,frozen=ref(dst)))
 write(folder/'source-identities.json',sources)
 nativebase=[]
 for name in ['forge/game/cost/CostPayment.class','forge/game/cost/CostRevealChosen.class','forge/game/ability/effects/ChooseTypeEffect.class','forge/game/ability/effects/ChoosePlayerEffect.class','forge/game/ability/effects/ChangeTargetsEffect.class','forge/game/spellability/SpellAbilityCondition.class','forge/game/spellability/SpellAbilityRestriction.class','forge/game/card/Card.class','forge/game/card/CardView.class','mtgb/bridge/ForgeBridge.class']:
  dst=folder/'native-classbase'/name;dst.parent.mkdir(parents=True,exist_ok=True);live=RT/'classes'/name
  if live.exists():dst.write_bytes(live.read_bytes());origin=live.relative_to(ROOT).as_posix()
  else:
   matches=[]
   for jar in (RT/'lib').glob('*.jar'):
    with zipfile.ZipFile(jar) as z:
     if name in z.namelist():matches.append((jar,z.read(name)))
   assert len(matches)==1,(name,[str(x[0]) for x in matches]);dst.write_bytes(matches[0][1]);origin=matches[0][0].relative_to(ROOT).as_posix()+'!/'+name
  nativebase.append(dict(origin=origin,frozen=ref(dst)))
 write(folder/'native-classbase.json',nativebase)
 override=Path(os.environ['MTGB_REVEAL_OVERRIDE_DIR']).resolve() if os.environ.get('MTGB_REVEAL_OVERRIDE_DIR') else None;tested=[]
 if override:
  assert override.is_relative_to(OUT.resolve()) and override.is_dir(),override
  for x in sorted(override.rglob('*.class')):
   dst=folder/'tested-overrides'/x.relative_to(override);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(x,dst);tested.append(dict(classPath=x.relative_to(override).as_posix(),frozen=ref(dst)))
  for x in sorted((OUT/'fix/source').rglob('*.java')):
   dst=folder/'tested-source'/x.relative_to(OUT/'fix/source');dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(x,dst)
 flow=Flow(case,folder);p=None;result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=cfg['runtimeHash'],status='RUNNING',testedNativeOverrides=tested,testedOverrideSources=[ref(x) for x in sorted((folder/'tested-source').rglob('*.java'))],sources=[ref(folder/'driver.py'),*[ref(folder/n) for n in FIXTURES],ref(folder/'cases.json'),ref(folder/'runtime.json'),ref(folder/'source-identities.json'),ref(folder/'native-classbase.json')]);write(folder/'result.json',result)
 def finish(status,why):
  result.update(status=status,result=why,checks=flow.checks,violations=flow.violations,labels=flow.labels,paid=flow.paid,stage=flow.stage);return result
 try:
  subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in FIXTURES]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-reveal-tail-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   config=dict(session=case,seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK]*len(CASES[case]),auditDir=str(audit),positions=CASES[case]);write(folder/'start.json',config)
   p=subprocess.Popen([str(RT/cfg['java']),'-Xmx640m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,*([override] if override else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.RevealChosenPayloadFixture',str(RT/cfg['assets']),str(folder/'start.json')],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start();responded=set();deadline=time.monotonic()+300
   for step in range(900):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40)
    if isinstance(m,Exception):raise m
    assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in responded:continue
    a=None
    if m['views'][0]['phase'] not in (None,'null'):
     end=time.monotonic()+5
     while time.monotonic()<end:
      try:a=json.loads((audit/(str(rid)+'.json')).read_text('utf8'));break
      except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
     assert a is not None,('Missing observer',rid)
    pick,done=flow.process(m,a)
    if done:finish('PASS_BOUNDED' if not flow.violations else 'FAIL_BOUNDED_REPRO',done);break
    assert pick in flow.ids(r),(flow.stage,r,pick);print(case,step,rid,r['kind'],pick,flush=True);response=dict(type='respond',session=case,seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();responded.add(rid);p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception as e:finish('FAIL_DRIVER_OR_NATIVE',repr(e));print(traceback.format_exc(),flush=True)
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
  result['frozenEvidence']=[ref(x) for x in sorted(folder.rglob('*')) if x.is_file() and x.name not in ('result.json','evidence.json')];write(folder/'result.json',result);write(OUT/(case+'-latest.json'),dict(case=case,ref=ref(folder/'result.json'),status=result['status']))
 print(result['status'],case,folder.relative_to(ROOT).as_posix(),result['result'],flush=True)
 return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['killer-type-tax']:
  assert case in CASES,case
  result=run(case)
  if result['status']!='PASS_BOUNDED' or result.get('violations'):raise SystemExit(1)
  print('PASS_BOUNDED '+case,flush=True)
