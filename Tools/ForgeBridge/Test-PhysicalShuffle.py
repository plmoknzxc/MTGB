"""Fast card-agnostic contract for the finite physical shuffle; no game boot."""
import os,pathlib,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[2]
OUT=pathlib.Path(os.environ.get('MTGB_PHYSICAL_SHUFFLE_OUT',str(ROOT/'.tools/cut-insert-1022/contract')))
OUT.mkdir(parents=True,exist_ok=True)
classes=OUT/'classes';classes.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-d',str(classes),str(ROOT/'Tools/ForgeBridge/java/mtgb/bridge/BridgeLibraryShuffle.java'),str(ROOT/'Tools/ForgeBridge/tests/PhysicalShuffleFixture.java')],check=True)
subprocess.run([str(ROOT/'ForgeRuntime/jre/bin/java.exe'),'-Xmx128m','-cp',str(classes),'mtgb.bridge.PhysicalShuffleFixture',str(OUT/'result.json')],check=True)
