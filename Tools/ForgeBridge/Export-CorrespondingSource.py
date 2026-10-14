"""Export the GPL source overlay and exact source identities for publication."""
import argparse,hashlib,json,shutil,zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',default='.tools/git-publish-1014')
    parser.add_argument('--date',default='20261014')
    args=parser.parse_args()
    output=(ROOT/args.output).resolve();output.mkdir(parents=True,exist_ok=True)
    source=ROOT/'ForgeRuntime/source';destination=ROOT/'ThirdParty/Forge'
    origins=json.loads((source/'origins.json').read_text('utf8'))
    archive=source/('forge-'+origins['forgeRevision']+'.zip')
    assert sha(archive)==origins['forgeSourceSha256']
    assert sha(ROOT/'Tools/ForgeBridge/NativePatches.json')==origins['nativePatchesSha256']
    destination.mkdir(parents=True,exist_ok=True)
    for row in origins['nativePatchedSources']:
        path=source/'native-patches'/row['path'];assert sha(path)==row['sha256'],row['path']
        target=destination/'native-patches'/row['path'];target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
    shutil.copy2(source/'origins.json',destination/'origins.json')
    release='https://github.com/plmoknzxc/MTGB/releases/tag/forge-source-'+args.date
    overlay=output/('mtgb-forge-source-overlay-'+args.date+'.zip');files=[]
    with zipfile.ZipFile(overlay,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as package:
        for parent in [ROOT/'Tools/ForgeBridge',ROOT/'Tools/ForgeProof',destination]:
            for path in sorted(parent.rglob('*')):
                if not path.is_file() or any(part in ['__pycache__','Reports'] for part in path.parts) or path.suffix in ['.class','.pyc'] or path.name=='source-manifest.json':continue
                relative=path.relative_to(ROOT).as_posix();package.write(path,relative);files.append({'path':relative,'sha256':sha(path)})
        test=ROOT/'Tools/Validation/TestNativePatches.py'
        package.write(test,test.relative_to(ROOT).as_posix());files.append({'path':test.relative_to(ROOT).as_posix(),'sha256':sha(test)})
        package.writestr('SOURCE-README.txt','Full pinned Forge source is the separate '+archive.name+'. This overlay contains GPLv3 Java bridge, actual native modifications, patch definitions, build scripts and fixtures. Modified by MTGB contributors; snapshot '+args.date+'. Preserve original notices and consult the public repository build instructions.\n')
    with zipfile.ZipFile(overlay) as package:
        assert package.testzip() is None
        for row in files:assert hashlib.sha256(package.read(row['path'])).hexdigest()==row['sha256']
    manifest={'date':args.date,'forgeRevision':origins['forgeRevision'],'forgeArchive':archive.name,'forgeArchiveSHA256':sha(archive),'forgeArchiveBytes':archive.stat().st_size,'overlayArchive':overlay.name,'overlaySHA256':sha(overlay),'overlayBytes':overlay.stat().st_size,'nativePatchesSHA256':origins['nativePatchesSha256'],'overlayFiles':files,'release':release,'modificationNotice':'MTGB native fixes and headless Java bridge; original copyright preserved.'}
    (destination/'source-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n','utf8')
    (output/'source-upload.json').write_text(json.dumps({'upstream':str(archive),'overlay':str(overlay),'manifest':manifest},ensure_ascii=False,indent=2)+'\n','utf8')
    print({'upstreamBytes':archive.stat().st_size,'overlayBytes':overlay.stat().st_size,'overlayFiles':len(files),'release':release})

if __name__=='__main__':main()
