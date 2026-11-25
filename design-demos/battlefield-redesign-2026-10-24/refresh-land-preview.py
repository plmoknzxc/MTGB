"""Refresh only the copied scene preview; native snapshots remain unchanged."""
import json,copy
from pathlib import Path
R=Path(__file__).resolve().parents[2]
p=R/'Assets/Scenes/PreviewData/BattlefieldRedesignSample.json'
sample=json.loads(p.read_text('utf-8-sig'))
native=json.loads((R/'.tools/redesign-lands-1026/after-mana.json').read_text('utf8'))['views'][0]
lands=[c for c in native['players'][0]['battlefield'] if c.get('land') and c.get('landStackable')]
for player in sample['players']:
 for card in player['battlefield']:
  if not card.get('land'):continue
  source=next(c for c in lands if c['name']==card['name'])
  for field in ['landPresentationKnown','landStackable','landManaColors']:card[field]=source[field]
 # Keep the existing battlefield and add a few public examples for all four frames.
 additions=['Wastes',"Raffine's Tower"] if player['seat']==1 else ['Hedge Maze']
 for i,name in enumerate(additions):
  card=copy.deepcopy(next((c for c in player['battlefield'] if c['name']==name),next(c for c in lands if c['name']==name)))
  card['id']=63000+player['seat']*100+i;card['ownerSeat']=player['seat']
  player['battlefield']=[c for c in player['battlefield'] if c['id']!=card['id']]+[card]
p.write_text(json.dumps(sample,ensure_ascii=False,separators=(',',':')),encoding='utf8')

