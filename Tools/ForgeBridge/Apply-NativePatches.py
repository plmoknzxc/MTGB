"""Apply reviewed Java and card-script fixes without editing the pinned checkout."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).resolve().parent
BUILD=ROOT/'.tools/native-patches'

def reviewed_java_source(path,definition):
    """Match the pinned source allowing only Git's LF/CRLF checkout conversion."""
    raw=path.read_bytes()
    lf=raw.replace(b'\r\n',b'\n')
    variants=(raw,lf,lf.replace(b'\n',b'\r\n'))
    if not any(hashlib.sha256(data).hexdigest()==definition['sourceSha256'] for data in variants):
        raise ValueError('Pinned native source changed; review '+definition['id'])
    return lf.decode('utf8')

def patched_source(source,definition):
    for change in definition['changes']:
        if source.count(change['before'])!=1:
            raise ValueError('Native patch context must match exactly once: '+definition['id'])
        source=source.replace(change['before'],change['after'],1)
    return source

def reviewed_java_sources(definitions):
    """Compose every reviewed patch for a class, in declaration order."""
    sources={}
    origins={}
    for fix in definitions['patches']:
        path=fix['javaPath']
        origin=(fix['source'],fix['sourceSha256'])
        if path not in sources:
            sources[path]=reviewed_java_source(ROOT/fix['source'],fix)
            origins[path]=origin
        elif origins[path]!=origin:
            raise ValueError('Conflicting native source origins: '+path)
        sources[path]=patched_source(sources[path],fix)
    return sources

def apply_asset_patches(runtime,definitions):
    """Validate every source, destination and context before writing any card script."""
    scope=(runtime/'assets/res/cardsfolder').resolve()
    staged=[]
    for fix in definitions.get('assetPatches',[]):
        original=ROOT/fix['source'];raw=original.read_bytes()
        if hashlib.sha256(raw).hexdigest()!=fix['sourceSha256']:
            raise ValueError('Pinned asset source changed; review '+fix['id'])
        target=(runtime/fix['assetPath']).resolve()
        if not target.is_relative_to(scope) or target.suffix!='.txt':
            raise ValueError('Unsafe card-script patch destination: '+fix['id'])
        content=patched_source(raw.decode('utf8'),fix).encode('utf8')
        if target.exists() and target.read_bytes() not in (raw,content):
            raise ValueError('Runtime asset differs from original and reviewed patch: '+fix['id'])
        staged.append((target,content,dict(id=fix['id'],path=fix['assetPath'],
                      sourceSha256=fix['sourceSha256'],sha256=hashlib.sha256(content).hexdigest())))
    for target,content,_ in staged:
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(content)
    return [record for _,_,record in staged]

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--assets-only',action='store_true')
    args=parser.parse_args()
    definitions=json.loads((HERE/'NativePatches.json').read_text(encoding='utf8'))
    runtime=ROOT/'ForgeRuntime'
    assets=apply_asset_patches(runtime,definitions)
    if args.assets_only:
        BUILD.mkdir(parents=True,exist_ok=True)
        (BUILD/'asset-result.json').write_text(json.dumps(dict(status='PASS',assets=assets),indent=2)+'\n',encoding='utf8')
        print(f'PASS: {len(assets)} reviewed card-script fix')
        return
    sources=[]
    for java_path,text in reviewed_java_sources(definitions).items():
        output=BUILD/'src'/java_path;output.parent.mkdir(parents=True,exist_ok=True)
        output.write_text(text,encoding='utf8')
        sources.append(output)
    classes=(BUILD/'classes').resolve()
    if classes!=ROOT.joinpath('.tools/native-patches/classes').resolve():raise ValueError('Unsafe build output')
    if classes.exists():shutil.rmtree(classes)
    classes.mkdir(parents=True)
    subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'lib/*'),'-d',str(classes),*map(str,sources)],check=True)
    generated=[]
    for path in sorted(classes.rglob('*.class')):
        relative=path.relative_to(classes);target=runtime/'classes'/relative
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
        generated.append(dict(path=relative.as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    (BUILD/'result.json').write_text(json.dumps(dict(status='PASS',patches=[f['id'] for f in definitions['patches']],classes=generated,assets=assets),indent=2)+'\n',encoding='utf8')
    print(f'PASS: {len(definitions["patches"])} reviewed native fix; {len(generated)} compiled override class')

if __name__=='__main__':main()
