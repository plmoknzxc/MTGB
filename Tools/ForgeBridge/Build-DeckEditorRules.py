"""Export pinned Forge construction facets; validate before atomic publication."""
from pathlib import Path
import hashlib,json,os,subprocess,tempfile

ROOT=Path(__file__).resolve().parents[2]
RUNTIME=ROOT/'ForgeRuntime'
def main():
    output=ROOT/'Assets/StreamingAssets/DeckEditorRules.json'
    with tempfile.TemporaryDirectory(prefix='mtgb-editor-facets-') as directory:
        folder=Path(directory); temporary=folder/'rules.json'
        args=[str(RUNTIME/'jre/bin/java.exe') if os.name=='nt' else 'java','-Xmx1536m','-Djava.awt.headless=true','-Dfile.encoding=UTF-8','-Duser.home='+str(folder),'-cp',str(RUNTIME/'classes')+os.pathsep+str(RUNTIME/'lib/*'),'mtgb.bridge.DeckEditorCatalog',str(RUNTIME/'assets'),str(temporary)]
        env=os.environ.copy();env['APPDATA']=str(folder);env['LOCALAPPDATA']=str(folder)
        result=subprocess.run(args,cwd=RUNTIME/'assets',env=env,capture_output=True,text=True,encoding='utf-8',timeout=180)
        if result.returncode:raise RuntimeError(result.stderr[-6000:])
        data=json.loads(temporary.read_text(encoding='utf-8'))
        names=[row['name'] for row in data['cards']];known=set(names);assert len(names)==len(known) and len(names)>30000
        assert len(data['companions'])==10
        for row in data['cards']:
            assert 0<=row['identity']<=31 and 0<=row['companions']<1024
            assert set(row.get('partners',[]))<=known
        data['forgeRevision']=(ROOT/'Tools/ForgeProof/forge-version.txt').read_text().strip()
        data['catalogHash']=hashlib.sha256((RUNTIME/'catalog.jsonl').read_bytes()).hexdigest()
        staging=output.with_suffix('.json.tmp');staging.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8');staging.replace(output)
        print(f'Editor facets: {len(names)} names, {output.stat().st_size} bytes')
if __name__=='__main__':main()
