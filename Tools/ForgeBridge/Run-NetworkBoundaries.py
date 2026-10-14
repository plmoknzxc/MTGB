"""Real current standalone clients/server; only opening state uses fixture.

Command mailbox invokes production service/UI submission and actual UDP transport.
No effect/phase/permission mutation. All states and wire envelopes stay on disk.
"""
import argparse, json, os, subprocess, time, pathlib, datetime, hashlib
ROOT=pathlib.Path(__file__).resolve().parents[2]
B=ROOT/'.tools/network-boundaries-1010'

def read(p):
    try:return json.loads(pathlib.Path(p).read_text(encoding='utf-8-sig'))
    except (OSError,ValueError):return None

def save(p,x):
    p=pathlib.Path(p);q=p.with_suffix('.write.tmp');q.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
    for _ in range(150):
        try:os.replace(q,p);return
        except PermissionError:time.sleep(.01)
    raise OSError('Mailbox held open too long: '+str(p))

def brief(p):
    if not p:return None
    seat,r=p
    return {'seat':seat,**{k:r.get(k) for k in ['requestId','contextId','kind','message','playerSeat','remainingManaCost','selectedCount','remainingCount']},'options':[{'id':o['id'],'label':o['label'],'disabled':o.get('disabled'),'selected':o.get('selected'),'cardId':(o.get('card') or {}).get('id')} for o in r.get('options',[])]}

class Run:
    def __init__(self,path):self.d=pathlib.Path(path)
    def state(self,role):
        p=self.d/(role+'-state.json')
        if not p.exists():return {}
        for _ in range(150):
            value=read(p)
            if value is not None:return value
            time.sleep(.01)
        raise OSError('Live snapshot temporarily unreadable: '+str(p))
    def wait(self,predicate,timeout=30):
        end=time.monotonic()+timeout
        while time.monotonic()<end:
            for f in self.d.glob('*-fatal.txt'):raise AssertionError(f.read_text(encoding='utf-8-sig'))
            x=predicate()
            if x:return x
            time.sleep(.1)
        raise TimeoutError('Network predicate timeout; inspect frozen snapshots/wire')
    def cmd(self,role,typ,**kw):
        ids=[int(p.stem.rsplit('-',1)[1]) for p in self.d.glob(role+'-result-*.json')]
        old=read(self.d/(role+'-command.json')) or {}; n=max(ids+[old.get('id',0)])+1
        save(self.d/(role+'-command.json'),dict(id=n,type=typ,**kw))
        result=self.wait(lambda:read(self.d/(role+'-result-'+str(n)+'.json')),15)
        assert result['ok'],result
        return result
    def pending(self):
        s=self.state('server')
        for seat,key in [(1,'first'),(2,'second')]:
            f=(s.get(key) or {}).get('forge') or {};r=f.get('request') or {}
            if f.get('awaitingInput') and r.get('requestId'):
                return seat,r
        return None
    def ready(self,timeout=30):
        def check():
            p=self.pending()
            if not p:return None
            seat,r=p;c=self.state('client'+str(seat));f=(c.get('view') or {}).get('forge') or {}
            cr=f.get('request') or {}
            if c.get('state')=='Idle' and c.get('inMatch') and f.get('awaitingInput') and cr.get('requestId')==r['requestId']:return seat,r
        return self.wait(check,timeout)
    def choose(self,option,seat=None,typ='choose',**kw):
        s,r=self.ready()
        if seat is not None:assert s==seat,(s,r)
        assert any(o['id']==option and not o.get('disabled') for o in r['options']),(option,r)
        before=self.state('server'); result=self.cmd('client'+str(s),typ,nativeId=r['requestId'],optionId=option,**kw)
        self.wait(lambda:((p:=self.pending()) and p[1]['requestId']!=r['requestId']) or self.state('server').get('series',{}).get('finished'),30)
        self.record('submit',{'seat':s,'request':r,'option':option,'before':before,'result':result,'after':self.state('server')})
        return self.ready()
    def record(self,label,value):
        with (self.d/'checks.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps({'label':label,'value':value},ensure_ascii=False)+'\n')
    def sync(self):
        def check():
            s=self.state('server');m=s.get('members') or []
            if len(m)!=2 or not all(x['connected'] and x['ready'] for x in m):return False
            for seat,key in [(1,'first'),(2,'second')]:
                c=self.state('client'+str(seat));v=c.get('view') or {};sv=s.get(key) or {}
                if c.get('state')!='Idle' or not v.get('viewHash') or v['viewHash']!=sv.get('viewHash'):return False
            return s
        s=self.wait(check,30);self.record('both-private-views-synchronized',s);return s
    def disconnect_restore(self,seat,label):
        self.sync();before=self.state('server');v=before['first' if seat==1 else 'second'];role='client'+str(seat)
        self.cmd(role,'disconnect',hold=True)
        disconnected=self.wait(lambda:next((m for m in self.state('server').get('members',[]) if m['seat']==seat and not m['connected']),None))
        self.cmd(role,'reconnect');after=self.sync()
        assert after['nativePid']==before['nativePid'],(before['nativePid'],after['nativePid'])
        assert after['first']['viewHash']==before['first']['viewHash'] and after['second']['viewHash']==before['second']['viewHash']
        self.record(label,{'before':before,'disconnected':disconnected,'after':after,'actualDisconnectSeconds':after['time']-before['time']})
        return after

def start():
    status=(B/'build-status.txt').read_text(encoding='utf-8-sig');assert status.startswith('PASS'),status
    stamp=datetime.datetime.now().strftime('%Y%m%dT%H%M%S');d=B/('run-'+stamp);d.mkdir();save(B/'current.json',{'path':str(d)})
    exe=ROOT/'Builds/ForgeValidation/MTGB.exe';pids={};port=17897
    for role in ['server','client1','client2']:
        args=[str(exe),'-batchmode','-nographics','-forge-boundaries',role,'-forge-boundary-root',str(d),'-forge-boundary-classes',str(B/'classes'),'-forge-port',str(port),'-forge-room','n'+stamp[-4:],'-logFile',str(d/(role+'-unity.log'))]
        p=subprocess.Popen(args,cwd=exe.parent,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));pids[role]=p.pid;save(d/'processes.json',pids)
        run=Run(d)
        run.wait(lambda:run.state(role) if role=='server' else run.state(role).get('connected'),90)
        if role!='server':run.wait(lambda:run.state(role).get('seat')==int(role[-1]),30)
    run=Run(d)
    for _ in range(12):
        seat,r=run.ready(90)
        if 'Mulligan' not in r['kind']:break
        option=next((o['id'] for o in r['options'] if o['label']=='Keep'),None);assert option,r
        run.choose(option)
    run.sync();run.record('opening-complete',{'path':str(d),'buildExeSHA256':hashlib.sha256(exe.read_bytes()).hexdigest()});print(str(d))
    print(json.dumps(brief(run.pending()),ensure_ascii=False))

def main():
    p=argparse.ArgumentParser();p.add_argument('action',choices=['start','status','choose','command','stop']);p.add_argument('value',nargs='?');p.add_argument('--seat',type=int);p.add_argument('--type',default='choose');p.add_argument('--data',default='{}');a=p.parse_args()
    if a.action=='start':return start()
    run=Run(read(B/'current.json')['path'])
    if a.action=='status':
        s=run.state('server');print(json.dumps({'path':str(run.d),'pid':s.get('nativePid'),'members':s.get('members'),'request':brief(run.pending())},ensure_ascii=False))
    elif a.action=='choose':print(json.dumps(brief(run.choose(a.value,a.seat,a.type,**json.loads(a.data))),ensure_ascii=False))
    elif a.action=='command':print(json.dumps(run.cmd('client'+str(a.seat),a.value,**json.loads(a.data)),ensure_ascii=False))
    else:
        for role in ['client1','client2','server']:
            try:run.cmd(role,'quit')
            except Exception as e:print(role,str(e))

if __name__=='__main__':main()
