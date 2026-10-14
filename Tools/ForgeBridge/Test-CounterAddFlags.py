"""Continue only missing AddOrRemoveCounter branches; retain prior proofs."""
import os,runpy
from pathlib import Path
os.environ['MTGB_COUNTER_FLOW_PLAN']='.tools/counter-addflags-1010'
runpy.run_path(str(Path(__file__).with_name('Test-CounterFlows.py')),run_name='__main__')
