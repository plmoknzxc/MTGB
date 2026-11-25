"""Build a separate tabbed-reader exploration from C; production UI is untouched."""
from pathlib import Path

root = Path(__file__).parent
s = (root / '03-immersive-table.html').read_text(encoding='utf-8')

def replace_once(before, after):
    global s
    if s.count(before) != 1:
        raise RuntimeError(f'Expected one source match: {before[:100]}')
    s = s.replace(before, after, 1)

replace_once('<title>MTGB · 沉浸牌桌</title>', '<title>MTGB · 详情与历史</title>')
replace_once('  假设：横排手牌作为一条牌廊；阅读卡牌与规则放在同一条下方阅读台，', '  用户新增方向：详情与历史共享左侧固定侧栏，用标签页切换；右侧堆叠保留。\n  横排手牌独占底部；历史悬停不抢标签，明确点击卡牌才打开详情。\n  本版从 C 派生，旧 C 保留比较。原探索：')

css = r'''
/* D: shared information rail. The stack retains C's position and presentation. */
.head{left:354px}.tools{left:22px;gap:13px}.tools button{font-size:13px}
.arena-world{left:354px;width:878px}.rail-rule{left:342px}
.enemy-land-section{left:380px}.enemy-section{left:380px;width:800px}
.own-section{left:380px}.own-section .zone-title{left:744px;width:100px}
.center-seam{left:380px}.private-hand{left:1090px;top:31px}
/* Players are primary anchors again, separate from the details/history rail. */
.player{display:grid;grid-template-columns:54px minmax(0,1fr) 70px;grid-template-rows:27px 25px 33px;column-gap:10px;row-gap:8px;align-content:center;align-items:center;isolation:isolate;padding:16px;border:1px solid #8296a45c;border-radius:5px;background:linear-gradient(135deg,#27333fee,#101921f5);box-shadow:inset 0 1px #d5e2e21c,0 8px 18px #0007}
.player::before{display:none}.player::after{content:"";position:absolute;left:16px;right:16px;top:0;height:2px;background:var(--red);opacity:.8;pointer-events:none}.player.you::after{background:var(--blue)}
.player.opponent{left:889px;top:101px;width:330px;height:145px;z-index:2;background:linear-gradient(135deg,#332e39ef,#151a24f5)}
.player.you{left:371px;top:684px;width:282px;height:159px;z-index:2;background:linear-gradient(135deg,#243c52ef,#111f2cf5)}
.player .identity{display:contents}.player .monogram{grid-column:1;grid-row:1/3;width:54px;height:54px;font-size:20px;box-shadow:inset 0 1px #e6d9c83d,0 5px 8px #0008}
.player .player-name{grid-column:2;grid-row:1;font-size:18px;line-height:25px;align-self:end;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:#eedee1}
.player.you .player-name{color:#d8e9f8}.player .seat-state{grid-column:2;grid-row:2;font-size:12px;line-height:20px;align-self:start;color:#ab9ca7}.player.you .seat-state{color:#9dbbd4}
.player .life{grid-column:3;grid-row:1/3;font-size:61px;line-height:59px;letter-spacing:-2px;padding:0;margin:0;border:0;text-align:right;color:#f4f0e4;text-shadow:0 3px 6px #0008}
.player .life::after{content:"生命";display:block;font:11px/18px GameSans,sans-serif;letter-spacing:2px;color:#aeb5bb;padding-top:2px}
.player .zones{grid-column:1/-1;grid-row:3;display:flex;gap:14px;justify-content:space-between;border-top:1px solid #9fadb63b;padding-top:9px}
.player .zone-link{gap:6px;font-size:13px;padding:3px 0;width:auto;color:#acb8c3}
.player .zone-link b{font-size:17px;min-width:17px;color:#e4e8eb}.player .zone-link:disabled{opacity:.78}
.dock{left:354px}.hand-region{left:338px;width:calc(100% - 358px)}.hand-heading{justify-content:flex-end;padding-right:5px}
.hand-heading .hint{margin-left:15px;margin-right:0}.hand-line{justify-content:center;gap:20px;overflow-x:auto;overflow-y:visible;scrollbar-width:thin;scrollbar-color:#526777 transparent;padding-bottom:14px}
#table.dense .hand-line{justify-content:flex-start}
.info-rail{position:absolute;left:22px;top:90px;width:300px;bottom:28px;z-index:6;display:flex;flex-direction:column;overflow:hidden;border:1px solid #71817d55;border-radius:5px;background:linear-gradient(150deg,#1e2b34f5,#0a141df7 65%);box-shadow:inset 0 1px #d3e4e01a,0 12px 26px #0008}
.info-rail[hidden]{display:none}.info-tabs{display:flex;height:48px;flex:none;border-bottom:1px solid #566b7c65;background:#080f17a6}
.info-tabs button{width:50%;font-size:16px;color:#8c9eae;position:relative;letter-spacing:3px}
.info-tabs button[aria-selected=true]{color:#f0e9dc;background:#293b4d66}
.info-tabs button[aria-selected=true]::after{content:"";position:absolute;left:22px;right:22px;bottom:0;height:2px;background:#a5b9cc;pointer-events:none}
.info-tabs button:focus-visible{outline-offset:-5px}.info-panel{position:relative;flex:1;min-height:0;overflow:hidden}
.info-panel[hidden]{display:none}
.reader{position:absolute;inset:0;display:flex;flex-direction:column;align-items:stretch;gap:0;padding:15px 18px 22px;overflow:auto;scrollbar-width:thin;scrollbar-color:#536c80 transparent}
.reader::before{display:none}.reader[hidden]{display:none}.reading{display:contents}
.reader-toolbar{order:0;flex:none;height:23px;margin-bottom:4px}.reader-toolbar button{font-size:12px;color:#a4b2bf}.reader-toolbar .close{font-size:22px}
.reader-title{order:1;flex:none;font-size:24px;line-height:1.4;margin:0 0 15px;overflow-wrap:anywhere}
.reader-photo{order:2;align-self:center;flex:none;width:216px;height:302px;margin-bottom:19px}
.reader-photo img{width:216px;height:302px}.reader-photo::after{font-size:12px;line-height:28px}
.reader-meta{order:3;flex:none;align-items:flex-start;padding:0 0 13px;min-height:0;font-size:13px;color:#a4b1ba;border-color:#6c7e8b45;line-height:1.8}
.reader-meta .mana{padding-top:3px;flex-shrink:0}.reader-meta .mana img{width:18px;height:18px}
.reader-cost-change{order:4;flex:none;font-size:13px;line-height:1.8;color:#c5b894;margin:12px 0 0;flex-wrap:wrap}
.reader-rules{order:5;flex:none;font-size:17px;line-height:1.75;color:#d9dfdf;margin-top:16px;max-height:none;overflow:visible}
.reader-empty{position:absolute;inset:0;display:grid;place-content:center;text-align:center;font-size:14px;color:#8b9ba7;padding:25px;line-height:1.8}
.reader-empty[hidden]{display:none}
.history-panel{padding:0;overflow:auto;scrollbar-width:thin;scrollbar-color:#536c80 transparent}
.history-turn{padding:17px 16px;border-bottom:1px solid #6c7e8b45;display:flex;flex-direction:column;gap:7px;position:sticky;top:0;background:#17242eef;z-index:2;backdrop-filter:blur(10px)}
.history-turn strong{font-size:15px;font-weight:400;color:#eee8da}.history-turn small{font-size:12px;color:#9baab6}
.history-list{padding:0;margin:0;list-style:none}
.history-list .history-event{padding:15px 14px 17px;margin:0;border:0;border-bottom:1px solid #73869736;background:linear-gradient(100deg,#21415c42,transparent 90%);font-size:14px;line-height:1.6;color:#d4e1eb}
.history-list .history-event.enemy{background:linear-gradient(100deg,#59323e47,transparent 90%)}
.event-heading{display:flex;gap:10px;align-items:center;margin-bottom:12px}.event-heading strong{font-weight:400;font-size:15px;color:#e4e7e8}
.event-owner{font-size:11px;line-height:21px;padding:0 7px;color:#a9cff1;border:1px solid #6d9dc869;background:#17385070}
.enemy .event-owner{color:#efafb8;border-color:#ca79886e;background:#4d202f75}
.history-card{display:flex;align-items:center;gap:12px;padding:0;text-align:left;width:100%;border:0;background:none}
.history-card img{width:55px;height:77px;object-fit:cover;border-radius:2px;flex:none;box-shadow:0 3px 6px #0008}
.history-card>span{display:flex;flex-direction:column;gap:6px;min-width:0}.history-card b{font-size:16px;font-weight:400;color:#e9e6dd;line-height:1.45}
.history-card small{font-size:12px;color:#a1b3c2;line-height:1.6;margin:0}.enemy .history-card small{color:#c7a8af}
.history-card:hover b{text-decoration:underline;text-underline-offset:4px}.history-card:focus-visible{outline-offset:4px}
.history-target{margin-top:14px;padding:12px 0 0 13px;border-top:1px solid #a2747d44;border-left:2px solid #ce8a96}
.history-target .history-card img{width:44px;height:62px}.history-target .history-card b{font-size:14px}.history-target small{color:#e4a4af!important}
.history-end{padding:18px;font-size:11px;line-height:1.7;color:#7e919f;text-align:center}
.drawer{left:366px;width:820px;top:111px;background:#13212bf7;border-color:#70869466}.drawer-list{grid-template-columns:repeat(6,1fr)}
@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto!important}}
'''
replace_once('</style>', css + '\n</style>')
replace_once('<button data-action="history" aria-label="打开对局记录">记录</button>', '<button data-action="info-open" aria-label="打开详情与历史">详情 / 历史</button>')
replace_once('<div class="life" id="their-life">20</div>', '<span class="seat-state" id="their-hand-summary">手牌 2</span><div class="life" id="their-life">20</div>')
replace_once('<div class="life" id="our-life">16</div>', '<span class="seat-state">你的回合</span><div class="life" id="our-life">16</div>')
replace_once("$('#hand-count').textContent=own.hand.length;", "$('#hand-count').textContent=own.hand.length;$('#their-hand-summary').textContent='手牌 '+other.handCount;")

start = s.index('<section class="dock"')
end = s.index('<aside class="drawer" id="browser"', start)
reader = s[s.index('<aside class="reader"', start):s.index('</aside>', start)+len('</aside>')]
new = '''<section class="dock" aria-label="你的横排手牌"><div class="hand-region"><div class="hand-heading">手牌 <b id="hand-count">5</b><span class="hint">当前无可施放的牌</span></div><div class="hand-line" id="hand"></div></div></section>
<aside class="info-rail" id="info-rail" aria-label="卡牌详情与对局历史">
  <div class="info-tabs" role="tablist" aria-label="信息侧栏">
    <button id="details-tab" role="tab" aria-selected="true" aria-controls="details-panel" data-action="info-tab" data-tab="details" tabindex="0">详情</button>
    <button id="history-tab" role="tab" aria-selected="false" aria-controls="history" data-action="info-tab" data-tab="history" tabindex="-1">历史</button>
  </div>
  <section class="info-panel" id="details-panel" role="tabpanel" aria-labelledby="details-tab">
    <span class="reader-empty" id="reader-empty" hidden>悬停卡牌查看详情<br>也可以点击卡牌固定</span>
    ''' + reader + '''
  </section>
  <section class="info-panel history-panel" id="history" role="tabpanel" aria-labelledby="history-tab" hidden>
    <div class="history-turn"><strong>第 10 回合</strong><small>你的回合 · 战斗前主要阶段</small></div>
    <ol class="history-list" id="history-list"></ol>
    <div class="history-end">当前记录到对手指定目标</div>
  </section>
</aside>
'''
s = s[:start] + new + s[end:]

replace_once("let activeCard=D.initialInspect,pinned=false,selectedStack=D.stack[0].id,dense=false;", "let activeCard=D.initialInspect,pinned=false,selectedStack=D.stack[0].id,dense=false,activeInfoTab='details';")
replace_once('function togglePin()', '''function selectInfoTab(tab,focus=false){
 activeInfoTab=tab;$('#info-rail').hidden=false;$('#details-panel').hidden=tab!=='details';$('#history').hidden=tab!=='history';
 document.querySelectorAll('[data-action="info-tab"]').forEach(el=>{const selected=el.dataset.tab===tab;el.setAttribute('aria-selected',String(selected));el.tabIndex=selected?0:-1;if(selected&&focus)el.focus();});
}
function togglePin()''')
replace_once("$('#history').hidden=true;$('#browser').hidden=false;", "$('#browser').hidden=false;")
start = s.index("$('#history-list').innerHTML=")
end = s.index("document.addEventListener('pointerover'", start)
s = s[:start] + '''function historyCard(id,note){const c=cards[id];return `<button class="history-card" data-card-id="${id}" aria-label="查看历史中的${esc(c.name)}"><img src="${c.image}" alt=""><span><b>${esc(c.name)}</b><small>${esc(note)}</small></span></button>`;}
const top=D.stack[0],bottom=D.stack[1];
$('#history-list').innerHTML=`<li class="history-event mine"><div class="event-heading"><span class="event-owner">你</span><strong>施放咒语</strong></div>${historyCard(bottom.cardId,'X = 2 · 进入堆叠')}</li><li class="history-event enemy"><div class="event-heading"><span class="event-owner">对手</span><strong>施放咒语</strong></div>${historyCard(top.cardId,'进入堆叠 · 先结算')}</li><li class="history-event enemy"><div class="event-heading"><span class="event-owner">对手</span><strong>指定目标</strong></div>${historyCard(top.cardId,'来源咒语')}<div class="history-target">${historyCard(bottom.cardId,'↳ 成为对手咒语的目标')}</div></li>`;
''' + s[end:]

replace_once("if(action==='history'){$('#history').hidden=!$('#history').hidden;$('#browser').hidden=true;}", "if(action==='info-tab')selectInfoTab(el.dataset.tab);\nelse if(action==='info-open')selectInfoTab(activeInfoTab);\nelse if(action==='history'){selectInfoTab('history');$('#browser').hidden=true;}")
replace_once("$('#menu').hidden=true;$('#history').hidden=true;$('#browser').hidden=true;$('#reader').hidden=true;$('#reader-empty').hidden=false;", "$('#menu').hidden=true;$('#info-rail').hidden=true;$('#browser').hidden=true;")
replace_once("$('#menu').hidden=true;$('#history').hidden=true;$('#browser').hidden=true;$('#zoom').hidden=true;", "$('#menu').hidden=true;selectInfoTab('details');$('#browser').hidden=true;$('#zoom').hidden=true;")
replace_once("else if(action==='history-close')$('#history').hidden=true;", "else if(action==='history-close')selectInfoTab('details');")
replace_once("else if(action==='stack'){selectedStack=el.dataset.stackId;renderStack();showCard(el.dataset.cardId,true);}", "else if(action==='stack'){selectedStack=el.dataset.stackId;renderStack();selectInfoTab('details');showCard(el.dataset.cardId,true);}")
replace_once("else if(el.dataset.cardId){showCard(el.dataset.cardId,true);if(!pinned)togglePin();}", "else if(el.dataset.cardId){selectInfoTab('details');showCard(el.dataset.cardId,true);if(!pinned)togglePin();}")
replace_once("else if(!$('#history').hidden)$('#history').hidden=true;", "else if(!$('#history').hidden)selectInfoTab('details');")
replace_once("document.addEventListener('keydown',e=>{if(e.key==='Escape')", "document.addEventListener('keydown',e=>{if(e.target.closest('[role=tab]')&&['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();selectInfoTab(e.key==='Home'?'details':e.key==='End'?'history':activeInfoTab==='details'?'history':'details',true);return;}if(e.key==='Escape')")

(root / '04-tabbed-sidebar.html').write_text(s, encoding='utf-8')
print('Built 04-tabbed-sidebar.html')
