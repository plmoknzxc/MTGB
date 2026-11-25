"""F: an unchanged Unity raster plus replaceable hand/stack/controls/reader UI."""
from pathlib import Path
import json
W=Path(__file__).parent
G=json.loads((W/'native-preserved-geometry.json').read_text(encoding='utf-8-sig'))
FW,FH=G['width'],G['height']
(W/'native-preview-geometry.js').write_text('window.MTGB_NATIVE_CAPTURE='+json.dumps(G,ensure_ascii=False,separators=(',',':'))+';',encoding='utf-8')
s=(W/'04-tabbed-sidebar.html').read_text(encoding='utf-8')
s=s.replace('<title>MTGB · 详情与历史</title>','<title>MTGB · Unity战场与新UI</title>')
s=s.replace('<script src="scene-data.js"></script>','<script src="scene-data.js"></script><script src="full-layout-data.js"></script><script src="native-preview-geometry.js"></script>')
s=s.replace('Math.min(innerWidth/1600,innerHeight/900)',f'Math.min(innerWidth/{FW},innerHeight/{FH})')
s=s.replace('<main id="table" aria-label="沉浸牌桌">','<main id="table" aria-label="Unity战场与新UI"><img class="native-base" id="native-base" src="native-full-without-own-mana.png" alt="实际Unity渲染：保留原战场、玩家、指示物、徽记、效果与地城"><div id="native-hit-regions"></div><div id="own-mana-native" class="own-mana-native" role="img" aria-label="自己的法术力池，固定在阶段操作上方"><img src="native-full-preserved.png" alt=""></div>')
css=r'''
/* Native framebuffer is shown unfiltered at its original logical coordinates. */
#fit,#table{width:NATIVE_Wpx;height:NATIVE_Hpx}#table{background:#080c10}#table::before,#table::after{display:none}
.native-base{position:absolute;inset:0;width:100%;height:100%;object-fit:fill;z-index:-1;pointer-events:none}
.arena-world,.player,.seat-resources,.private-hand,.enemy-land-section,.enemy-section,.own-section,.center-seam,.rail-rule,.head,.tools{display:none!important}
.native-hotspot{position:absolute;border:0;padding:0;background:none;opacity:1;z-index:0}.native-hotspot:focus-visible{outline:1px solid #ccd9df;outline-offset:0}
.stack-area{left:1288px;top:32px;width:201px;z-index:3}.stack-area::before{inset:-14px -10px -4px;background:linear-gradient(145deg,#1d2a32ed,#09151ef2);border-color:#758b984d}
.stack-heading{font-size:15px;letter-spacing:1px;margin-bottom:12px}.stack-heading span{font-size:11px}
.stack-item{min-height:105px;padding:9px 0 10px;gap:10px}.stack-thumb{width:54px;height:75px}.stack-name{font-size:18px;line-height:27px;letter-spacing:0}.stack-owner{font-size:10px;line-height:17px}.stack-note{font-size:11px;line-height:17px;margin-top:3px}.target-note{font-size:11px;line-height:17px;margin-top:3px}.stack-item::before{top:10px;bottom:14px}.targeted::after{bottom:4px}
.info-rail{left:1254px;top:293px;bottom:auto;width:235px;height:322px;z-index:4;background:linear-gradient(145deg,#1d2a32ef,#0b151ff2);border-color:#758b984d;box-shadow:0 8px 18px #0006}
.info-tabs{height:34px}.info-tabs button{font-size:13px;letter-spacing:2px}.info-tabs button[aria-selected=true]::after{left:18px;right:18px;height:1px}
.reader{padding:9px 10px 12px;display:grid;grid-template-columns:94px minmax(0,1fr);grid-template-rows:auto auto auto auto auto;gap:6px 10px;align-content:start;align-items:start}
.reading{display:contents}.reader-toolbar{grid-column:2;grid-row:1;height:18px;margin:0}.reader-toolbar button{font-size:10px;line-height:18px}.reader-toolbar .close{font-size:19px;margin-left:5px}
.reader-title{grid-column:2;grid-row:2;font-size:17px;line-height:1.4;margin:0;align-self:center}.reader-photo{grid-column:1;grid-row:1/4;width:94px;height:131px;align-self:start;margin:0}.reader-photo img{width:94px;height:131px}.reader-photo::after{font-size:10px;line-height:22px}.reader-photo[hidden]{display:none}
.reader-meta{grid-column:2;grid-row:3;display:flex;flex-direction:column;align-items:flex-start;gap:5px;font-size:11px;line-height:1.5;padding:0;border:0;min-height:25px}.reader-meta .mana{padding:0}.reader-meta .mana img{width:16px;height:16px}
.reader-cost-change{grid-column:1/-1;grid-row:4;font-size:11px;margin:0;line-height:1.5}.reader-rules{grid-column:1/-1;grid-row:5;margin:3px 0 0;max-height:138px;overflow:auto;font-size:14px;line-height:1.65;color:#d5dcdd}
.history-turn{padding:10px 12px;gap:5px}.history-turn strong{font-size:13px}.history-turn small{font-size:10px}.history-list .history-event{padding:12px 11px;margin:0}.event-heading{margin-bottom:9px;gap:7px}.event-heading strong{font-size:12px}.event-owner{font-size:10px;line-height:18px}.history-card{gap:10px}.history-card img{width:35px;height:49px}.history-card b{font-size:13px}.history-card small{font-size:10px}.history-target{margin-top:10px;padding-top:9px}.history-target .history-card img{width:31px;height:43px}.history-target .history-card b{font-size:12px}.history-end{padding:12px;font-size:10px}
.own-mana-native{position:absolute;left:1190px;top:646px;overflow:hidden;z-index:6;pointer-events:none}.own-mana-native>img{position:absolute;max-width:none}
.priority{left:1254px;top:696px;width:235px;height:164px;z-index:3;display:flex;flex-direction:column;text-align:right;padding:0}
.phase-turn{font-size:12px;color:#a6b4bd;line-height:22px}.phase-label{font-family:GameSerif,serif;font-size:23px;line-height:35px;color:#e8e4d7;margin:0;font-weight:400}
.new-phase-track{display:flex;gap:7px;margin:10px 0 14px}.new-phase-track span{height:3px;flex:1;background:#4d5f6b}.new-phase-track span.current{background:#dbca9e}
.pass-button{width:100%;height:43px;min-height:43px;font-size:17px;border-color:#8199ae;background:linear-gradient(#354f6a,#223b54);color:#edf0e8;padding:0 15px;box-shadow:inset 0 1px #e1e8eb1c,0 3px 7px #0006}.pass-button:hover{background:#3f5c78}.pass-button.pressed{filter:brightness(1.3)}
.new-phase-options{display:flex;justify-content:space-between;align-items:center;margin-top:11px;font-size:12px;color:#a4b5c0}.new-phase-options button{font-size:12px;border:1px solid transparent;padding:4px 7px;border-radius:3px;color:#a8bac8}.new-phase-options button[aria-pressed=true]{color:#ead6a8;border-color:#cfb97b88;background:#8b72351f}
/* The raised hand may cover your field. Explicit field clicks lower it. */
.dock{left:350px;top:690px;right:auto;bottom:auto;width:875px;height:237px;overflow:hidden;z-index:3;border:0;border-top:1px solid #8ea2b92e;border-radius:4px 4px 0 0;background:linear-gradient(#18242bcc,#070d15e9);box-shadow:0 -3px 9px #0003;transition:top .22s ease,height .22s ease}
.dock[data-hand="lowered"]{top:813px;height:114px}
.hand-region{left:28px;top:3px;width:819px;height:234px}.hand-heading{height:15px;font-size:10px;justify-content:flex-start;gap:8px}.hand-heading .hint{font-size:10px;margin-left:auto;color:#9baab5}.hand-line{height:220px;padding-top:2px;gap:20px;overflow-x:auto;overflow-y:hidden;scrollbar-width:none;justify-content:center}.hand-line::-webkit-scrollbar{display:none}
.hand-card{width:132px;flex:0 0 132px;transform:none!important;padding:0;height:205px}.hand-card>img{width:132px;height:185px;margin-top:20px;border-radius:4px;filter:saturate(.94);box-shadow:0 5px 8px #0007}.hand-card .hand-name{position:absolute;top:0;left:0;width:82px;font-size:12px;line-height:19px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:#e0e5e5}
.hand-card .hand-cost{display:flex;left:auto;right:0;top:0;bottom:auto;height:19px;pointer-events:none}.hand-card .mana img{width:15px;height:15px;filter:none;box-shadow:none}.hand-card .mana .generic{height:15px;min-width:15px;font-size:10px;line-height:15px;padding:0 2px}
.hand-card:hover>img{filter:brightness(1.05);outline:1px solid #aabed1}.hand-card:hover,.hand-card:focus-visible{transform:none!important}
.hand-scroll{position:absolute;top:42px;width:21px;height:31px;padding:0;font-size:20px;background:#142532e6;border:1px solid #69899f66;border-radius:3px;z-index:6}.hand-scroll.previous{left:3px}.hand-scroll.next{right:3px}.hand-scroll[hidden]{display:none}
.hand-pop{position:absolute;width:225px;height:315px;z-index:40;pointer-events:none;filter:drop-shadow(0 12px 20px #000c)}.hand-pop[hidden]{display:none}.hand-pop>img{width:225px;height:315px;object-fit:contain;border-radius:7px;outline:1px solid #adc4d57a}.hand-pop .popup-cost{position:absolute;right:2px;top:-22px;padding:3px 6px;background:#182c3df0;border:1px solid #8fa3b36e;border-radius:3px;font-size:11px;color:#cddce8}.popup-cost .mana img{width:17px;height:17px;box-shadow:none}
.drawer{left:380px;top:230px;width:730px;height:480px}.zoom{z-index:60}.zoom>img{width:370px;height:518px}
'''.replace('NATIVE_W',str(FW)).replace('NATIVE_H',str(FH))
s=s.replace('</style>',css+'\n</style>',1)
start=s.index('<section class="priority">');end=s.index('</section>',start)+len('</section>')
s=s[:start]+'''<section class="priority" aria-label="阶段与操作"><span class="phase-turn" id="priority-message">第10回合 · 你的回合</span><h2 class="phase-label">战斗前主要阶段</h2><div class="new-phase-track" aria-label="当前阶段"><span></span><span class="current"></span><span></span><span></span><span></span><span></span></div><button class="pass-button" data-action="pass" id="pass-button">下一步<span>→</span></button><div class="new-phase-options"><button data-action="pause-next" aria-pressed="false">下阶段暂停</button><button data-action="auto" aria-label="切换操作模式">手动</button></div></section>'''+s[end:]
s=s.replace('<div class="hand-line" id="hand"></div></div></section>','<div class="hand-line" id="hand"></div></div><button class="hand-scroll previous" data-action="scroll-hand" data-direction="-1" aria-label="向左浏览手牌" hidden>‹</button><button class="hand-scroll next" data-action="scroll-hand" data-direction="1" aria-label="向右浏览手牌" hidden>›</button></section>')
s=s.replace('</main></div>','<div class="hand-pop" id="hand-pop" hidden><img id="hand-pop-image" alt=""><span class="popup-cost">当前 <span class="mana" id="hand-pop-cost"></span></span></div></main></div>')
s=s.replace("$('#reader-image').src=c.image;$('#reader-image').alt=c.name;", "$('.reader-photo').hidden=!c.image;if(c.image){$('#reader-image').src=c.image;$('#reader-image').alt=c.name;}")
start=s.index("else if(action==='pass'){");end=s.index("else if(el.dataset.scroll)",start)
s=s[:start]+'''else if(action==='pass'){el.classList.add('pressed');setTimeout(()=>el.classList.remove('pressed'),180);}
else if(action==='pause-next'){el.setAttribute('aria-pressed',String(el.getAttribute('aria-pressed')!=='true'));}
else if(action==='auto'){const modes=['手动','自动','略过'];el.textContent=modes[(modes.indexOf(el.textContent)+1)%3];}
else if(action==='scroll-hand')$('#hand').scrollBy({left:Number(el.dataset.direction)*300,behavior:'auto'});
'''+s[end:]
s=s.replace("window.setDense=value=>{dense=!!value;renderBoard();};","window.setDense=()=>{};window.updatePreviewStack=items=>{D.stack=items;selectedStack=items[0]?.id;renderStack();};")
extra=r'''
<script>
(()=>{
const D=window.MTGB_DATA,N=window.MTGB_NATIVE_CAPTURE,esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const ids=Object.keys(window.MTGB_LAYOUT.cards),objects=N.objects.filter(o=>o.kind==='protected-card');
const manaBounds=N.objects.find(o=>o.name==='ManaPoolChips').bounds,manaPanel=document.getElementById('own-mana-native'),manaImage=manaPanel.querySelector('img');manaPanel.style.width=(manaBounds.width+64)+'px';manaPanel.style.height=(manaBounds.height+8)+'px';Object.assign(manaImage.style,{width:N.width+'px',height:N.height+'px',left:-(manaBounds.x-32)+'px',top:-(manaBounds.y-4)+'px'});
document.getElementById('native-hit-regions').innerHTML=objects.map(o=>{const id=ids[o.id-20000],c=id&&D.cards[id],r=o.bounds;if(!c||r.width<=0||r.height<=0)return'';return `<button class="native-hotspot" style="left:${r.x}px;top:${r.y}px;width:${r.width}px;height:${r.height}px" data-card-id="${id}" aria-label="查看Unity战场中的${esc(c.name)}"></button>`;}).join('');
document.querySelectorAll('#hand .hand-card').forEach(el=>{const label=document.createElement('span');label.className='hand-name';label.textContent=D.cards[el.dataset.cardId].name;el.append(label);});
function scrollState(){const h=document.getElementById('hand');document.querySelectorAll('[data-action=scroll-hand]').forEach(b=>{b.hidden=h.scrollWidth<=h.clientWidth+2;b.disabled=Number(b.dataset.direction)<0?h.scrollLeft<=2:h.scrollLeft>=h.scrollWidth-h.clientWidth-2;});}
document.getElementById('hand').addEventListener('scroll',scrollState);addEventListener('resize',scrollState);requestAnimationFrame(scrollState);
const popup=document.getElementById('hand-pop');
const dock=document.querySelector('.dock');function setHand(raised){dock.dataset.hand=raised?'raised':'lowered';dock.setAttribute('aria-label',raised?'你的横排手牌，已展开':'你的横排手牌，已下沉');document.querySelector('.hand-heading .hint').textContent=raised?'点击战场收起 · 当前无可施放的牌':'点击手牌展开 · 当前无可施放的牌';popup.hidden=true;}setHand(true);
document.addEventListener('click',e=>{if(e.target.closest('.dock'))setHand(true);else if(e.target.closest('.native-hotspot')||e.target.id==='table'||e.target.id==='native-hit-regions')setHand(false);});
document.addEventListener('pointerover',e=>{const el=e.target.closest('.hand-card');if(!el||dock.dataset.hand==='lowered')return;const c=D.cards[el.dataset.cardId],r=el.getBoundingClientRect(),root=document.getElementById('table').getBoundingClientRect(),scale=root.width/N.width;document.getElementById('hand-pop-image').src=c.image;document.getElementById('hand-pop-image').alt=c.name;document.getElementById('hand-pop-cost').innerHTML=[...c.cost.matchAll(/\{([^}]+)\}/g)].map(([,v])=>D.assets.mana[v]?`<img src="${D.assets.mana[v]}" alt="${v}">`:`<span class="generic">${esc(v)}</span>`).join('');popup.style.left=Math.max(350,Math.min(940,(r.left-root.left)/scale+r.width/scale*.5-112.5))+'px';popup.style.top=(dock.dataset.hand==='raised'?356:479)+'px';popup.hidden=false;});
document.addEventListener('pointerout',e=>{const el=e.target.closest('.hand-card');if(el&&!el.contains(e.relatedTarget))popup.hidden=true;});
document.addEventListener('click',e=>{if(!e.target.closest('.hand-card'))popup.hidden=true;});
})();
</script>
'''
s=s.replace('</body>',extra+'</body>')
s=s.replace('</head>','<link rel="stylesheet" href="native-actions.css"></head>')
native=json.loads((W/'native-action-ui.json').read_text(encoding='utf-8-sig'))
native['cases']=json.loads((W/'action-ui-manifest.json').read_text(encoding='utf-8-sig'))
(W/'action-ui-data.js').write_text('window.MTGB_UI_STATES='+json.dumps(native,ensure_ascii=False,separators=(',',':'))+';',encoding='utf-8')
s=s.replace('</body>','<script src="action-ui-data.js"></script><script src="native-actions.js"></script></body>')
(W/'06-native-hybrid.html').write_text(s,encoding='utf-8')
print('Built F with unchanged Unity framebuffer:',FW,FH)
