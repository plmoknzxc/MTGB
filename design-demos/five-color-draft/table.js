"use strict";
const G=League, $=id=>document.getElementById(id);
const saveKey="mtgb.copper-gate.v1";
function loadGame(){
  try {
    const saved=JSON.parse(localStorage.getItem(saveKey));
    if(saved && saved.round>=1 && saved.round<=6 && saved.lands && saved.results && saved.standings?.length===4) return saved;
  } catch {}
  return G.newGame();
}
let state=loadGame(), toastTimer, replayIndex=0, replayTimer;
const art=k=>"--art:url('art/"+k+".jpg')";
const coin='<i class="coin" aria-hidden="true"></i>';
const plain=n=>String(n).padStart(2,"0");
const phase=()=>state.stage==="prep"?"备战中":state.stage==="result"?"本轮结算":"赛事结束";
const outcome=n=>n>0?"胜":n<0?"负":"平";
const signed=n=>(n>0?"+":"")+n;
const manaIcon=k=>'<img src="art/'+k+'.png" alt="'+G.colors[k].color+'色法术力">';
function costHTML(cost){
  return '<span class="mana">'+[...cost].map(k=>G.colors[k]?manaIcon(k):'<span class="generic" title="通用法术力">'+k+'</span>').join("")+'</span>';
}
function button(label,act,value="",cls="subtle",disabled=false){
  return '<button class="'+cls+'" data-act="'+act+'" data-val="'+value+'" '+(disabled?'disabled':'')+'>'+label+'</button>';
}
function save(){
  try {localStorage.setItem(saveKey,JSON.stringify(state))} catch {}
}
function toast(message){
  $("toast").textContent=message;$("toast").classList.add("visible");
  clearTimeout(toastTimer);toastTimer=setTimeout(()=>$("toast").classList.remove("visible"),2700);
}
function modal(title,html){
  stopReplay();
  $("dialog-content").innerHTML='<h2 id="dialog-title">'+title+'</h2>'+html;
  if(!$("dialog").open) $("dialog").showModal();
  $("dialog").scrollTop=0;
}
function closeModal(){if($("dialog").open) $("dialog").close()}
function commit(ok,message){
  if(!ok) return;
  closeModal();save();render();
  if(message) toast(message);
}
function roundHeader(){
  const team=G.teams[G.opponent(state.round)];
  $("round-panel").innerHTML='<div class="round-state">轮次 · '+phase()+'</div><div class="round-number">'+plain(state.round)+' <span>/ 06</span></div><div class="round-track" aria-hidden="true">'+Array.from({length:6},(_,i)=>'<i class="'+(i===state.round-1?"current":i<state.round?"past":"")+'"></i>').join("")+'</div>'+button('侦察'+team.short+' ↗',"scout","","scout-link");
}
function renderLands(){
  if(!state.lands.length) {
    $("lands").innerHTML='<div class="landbar"><div><strong>选一块起始地</strong><small>先看牌，再选色</small></div><div class="land-options">'+Object.entries(G.colors).map(([k,c])=>button(manaIcon(k)+c.land,"land",k,"land-pick")).join("")+'</div></div>';
    return;
  }
  $("lands").innerHTML='<div class="landbar"><div><strong>我的地</strong><small>每块地每轮产 1 费</small></div><div class="land-options">'+state.lands.map(l=>'<span class="land-chip '+(l.tapped?'spent':'')+'">'+manaIcon(l.color)+G.colors[l.color].land+' · '+(l.tapped?'已用':'可用')+'</span>').join("")+'</div></div>';
}
function offer(k,i){
  if(!k) return '<article class="offer sold">本格已售出</article>';
  const c=G.cards[k], full=state.bench.length>=5, poor=state.gold<c.price;
  const label=full?"备战区已满":poor?"金币不足":'购买 '+coin+' '+c.price;
  return '<article class="offer c-'+c.mana.match(/[WUBRG]/)[0]+'"><div class="art card-pic" style="'+art(k)+'"><span class="power" title="攻击 / 耐久">'+c.power+' / '+c.health+'</span></div><div class="card-copy"><h3 class="card-name">'+c.name+'</h3><div class="card-meta"><span>'+c.tribe+'</span>'+costHTML(c.mana)+'</div><p class="card-skill">'+(G.skills[c.skill]||'基础生物，耐久可承受多次交锋')+'</p></div>'+button(label,"buy",i,"buy",full||poor||state.stage!=="prep")+'</article>';
}
function unit(k,i,zone){
  const c=G.cards[k],board=zone==="board",legal=G.payment(state.lands,c.mana);
  const hint=board?"调整队列":!state.lands.length?"先选地":!legal?"等待法术力":state.board.length>=state.level+1?"阵容已满":"点击上阵";
  return button((board?'<i class="count-badge">'+(i+1)+'</i>':'')+'<span class="art" style="'+art(k)+'"></span><span><strong>'+c.name+'</strong><small>'+hint+'</small></span>',"unit",zone+"-"+i,"unit");
}
function collection(){
  for(const [zone,label,cap,empty] of [["bench","备战区",5,"买到的牌会放在这里。点牌，支付法术力即可上阵。"],["board","出战队列",state.level+1,"队首先交锋。已上阵的生物会保留到下一轮。"]]){
    $(zone).innerHTML='<div class="zone"><div class="zone-label"><b>'+label+'</b><em>'+state[zone].length+' / '+cap+'</em></div>'+(state[zone].length?'<div class="slots">'+state[zone].map((k,i)=>unit(k,i,zone)).join("")+'</div>':'<p class="empty">'+empty+'</p>')+'</div>';
  }
  const counts=G.tribeCounts(state.board);
  const notes=Object.entries(counts).map(([t,n])=>t+" "+n+"/2"+(n>=2?" · "+G.tribes[t]:""));
  $("synergies").innerHTML='<div class="synergy">'+notes.join("　")+'</div>';
  $("trick-button").textContent=state.trick?"战术 · "+G.colors[state.trick].spell:"预设战术";
  $("trick-button").disabled=!state.lands.length || state.stage!=="prep";
}
function ranklist(){
  return '<div class="ranklist">'+G.ranked(state).map(r=>'<div class="row-rank '+(r.id===0?"me":"")+'"><i>'+plain(r.rank)+'</i><span>'+G.teams[r.id].name+'</span><span><b>'+r.points+'</b> 分</span></div>').join("")+'</div>';
}
function side(){
  const opp=G.opponent(state.round),team=G.teams[opp];
  const label=state.stage==="prep"?"开始对战":state.stage==="result"?"查看本轮战报":"查看最终排名";
  $("enemy").innerHTML='<div class="side-label">本轮对手</div><div class="enemy-heading"><div class="art portrait" style="'+art(team.art)+'"></div><div><h2>'+team.name+'</h2><small>'+team.theme+' · 电脑牌手</small></div></div><p>'+team.plan+'</p><div class="enemy-army">'+G.army(opp,state.round).map(k=>'<span>'+G.cards[k].name+' '+G.cards[k].power+'/'+G.cards[k].health+'</span>').join("")+'</div>'+button(label+'<span>→</span>',"fight","","primary",!state.lands.length);
  const price=G.levelPrices[state.level];
  $("camp").innerHTML='<div class="camp-head"><h2>营地升级</h2><span class="level">Lv. '+state.level+' / 4</span></div><p>'+(price?'花 '+price+' 金币，增加 1 个出战位并自选 1 块地。'+(state.level===1?'解锁二阶生物。':''):'营地已满级，五个出战位均已解锁。')+'</p>'+button(!state.lands.length?'先选起始地':price?(state.gold<price?'需 '+price+' 金币':'升级营地　'+coin+' '+price):'已满级',"upgrade","","outline",!price||!state.lands.length||state.gold<price||state.stage!=="prep");
  $("ladder").innerHTML='<div class="camp-head"><h2>赛事积分</h2>'+button('赛程 ↗',"schedule","","text-btn")+'</div>'+ranklist();
}
function render(){
  roundHeader();renderLands();
  $("purse").innerHTML='<span class="resource">'+coin+'<strong>'+state.gold+'</strong> 金币</span>'+button('↻ 换牌 · 1 金',"refresh","","subtle",state.stage!=="prep"||!state.lands.length||state.gold<1);
  $("shop").innerHTML=state.shop.map(offer).join("");collection();side();
}
function rules(){
  modal("规则手册",'<p>四席邀请赛。你经营自己的部族阵容，与三位电脑牌手各交手两次，六轮后按积分排名。</p>'+
    '<h3>一轮怎么玩</h3><ol><li>先看商店，免费选择起始地。每局从 6 金币开始，余款可以留到以后。</li><li>用金币买生物到备战区，再点生物，用地支付牌面的费用上阵。备战区有五格。数字费用可用任意颜色支付。</li><li>调整队列，或留一块地预设战术。准备好后按「开始对战」。</li><li>下一轮获得 4 金币，所有地重置，商店免费换牌。战损全部恢复，生物留在队列。</li></ol>'+
    '<h3>商店与营地</h3><p>每批五张，一阶牌 2 金、二阶牌 3 金。手动换牌 1 金；卖出任何生物得 1 金。卖出已上阵生物不会退还本轮法术力。只要牌池足够，商店优先抽最多四张现有地能施放的不同牌，再补一张未来可能用到的牌。</p>'+
    '<table><thead><tr><th>营地</th><th>升级花费</th><th>地 / 出战位</th></tr></thead><tbody><tr><td>Ⅰ</td><td>起始</td><td>1 / 2</td></tr><tr><td>Ⅱ</td><td>4 金</td><td>2 / 3</td></tr><tr><td>Ⅲ</td><td>6 金</td><td>3 / 4</td></tr><tr><td>Ⅳ</td><td>8 金</td><td>4 / 5</td></tr></tbody></table><p>升级时免费刷新商店，新地当轮可用。Ⅱ 级解锁二阶牌。相同的牌可同时出战，分别计入部族。</p>'+
    '<h3>部族与交锋</h3><p>开战时，队列中有两名同部族生物即可触发加成，整场有效。</p><ul>'+Object.entries(G.tribes).map(([t,v])=>'<li>'+t+' · '+v+'</li>').join("")+'</ul><p>双方队首同时造成等于攻击的伤害。耐久归零即退场，下一位补上。幸存者带伤继续交锋。突击与护甲只影响该生物的第一次交锋。某一方无人时战斗结束，双方同时无人为平局。</p>'+
    '<h3>战术、对手与排名</h3><p>你每轮可以从五色预设战术中选一张，两军接战前自动施放并用掉一块对应颜色的地。如果部署生物用掉了最后一块对应地，战术自动取消。三位电脑使用公开的逐轮编队，依靠生物和部族战斗。</p><p>每轮同时结算两场比赛。胜 3 分，平 1 分，负 0 分。积分相同先比六轮累计净胜生物数，再相同就并列。六轮打满，全员参加。</p>'+
    '<details><summary>试玩卡牌与画面</summary><p>生物名称、数值、战术与联赛规则为本网页的试玩设计。插画取自本地万智牌印刷卡图，竞技场取景自《荣耀竞技场》（MH3 / 215，Jorge Jacinto）。地与费用使用万智牌的五色符号。</p></details>');
}
function showSchedule(){
  let rows="";
  for(let r=1;r<=6;r++){
    const opp=G.opponent(r),res=state.results[r-1],others=[1,2,3].filter(k=>k!==opp);
    const otherLabel=res?(res[1].diff===0?"平局":G.teams[res[1].diff>0?res[1].a:res[1].b].short+"胜"):"待战";
    rows+='<tr class="'+(r===state.round?'match-now':'')+'"><td>'+plain(r)+'</td><td>'+G.teams[opp].short+' · '+(res?outcome(res[0].diff):r===state.round?"本轮":"待战")+'</td><td>'+others.map(k=>G.teams[k].short).join(" / ")+' · '+otherLabel+'</td></tr>';
  }
  modal("六轮赛程",'<p>四席双循环。每轮有两场比赛，另外两位牌手的交锋也计入排名。</p><table><thead><tr><th>轮次</th><th>你的对手 / 你的战果</th><th>同轮另一桌</th></tr></thead><tbody>'+rows+'</tbody></table><h3>当前积分</h3>'+ranklist());
}
function upgrade(){
  const price=G.levelPrices[state.level];if(!price || state.stage!=="prep") return;
  modal("选择新地，升级营地",'<p>选地后支付 '+price+' 金币。升至 Lv. '+(state.level+1)+'，开放第 '+(state.level+2)+' 个出战位，并免费刷新商店。'+(state.level===1?'二阶生物现在可以出现。':'')+'</p><div class="option-list">'+Object.entries(G.colors).map(([k,c])=>button(manaIcon(k)+'<b>'+c.land+'</b><small>产 1 点'+c.color+'色法术力</small>',"land",k,"")).join("")+'</div>');
}
function scout(){
  const id=G.opponent(state.round),team=G.teams[id],units=G.fighters(G.army(id,state.round));
  modal("侦察"+team.name,'<p>'+team.theme+' · 电脑牌手。'+team.plan+'</p><table><tr><th>队列</th><th>生物</th><th>攻击 / 耐久</th><th>能力</th></tr>'+units.map((u,i)=>'<tr><td>'+(i+1)+'</td><td>'+u.name+'</td><td>'+u.power+' / '+u.health+'</td><td>'+(u.skill||'基础生物')+'</td></tr>').join("")+'</table><p>数值已计入部族加成，队首首先接战。</p>'+button("回到对战桌","close","","primary"));
}
function tactics(){
  modal("预设本轮战术",'<p>选一张战术，留一块对应颜色的地。两军接战前自动施放。</p><div class="option-list">'+Object.entries(G.colors).map(([k,c])=>{
    const ready=Boolean(G.payment(state.lands,k));
    return button(manaIcon(k)+'<b>'+c.spell+'</b> '+c.text+'<small>'+(state.trick===k?'已预设':ready?'1 费':'缺'+c.color+'费')+'</small>',"spell",k,"",!ready);
  }).join("")+button("本轮保留，不施放","spell","","")+'</div>');
}
function inspectUnit(value){
  const [zone,raw]=value.split("-"),i=Number(raw),k=state[zone][i],c=G.cards[k],board=zone==="board";
  const reason=state.stage!=="prep"?"本轮已锁定阵容":board?"":state.board.length>=state.level+1?"出战位已满，可卖出生物或升级营地":!G.payment(state.lands,c.mana)?"可用法术力不足，需 "+c.mana+"（可选地、升级或等下轮重置）":"";
  const effective=board?G.fighters(state.board)[i]:c;
  modal(c.name,'<div class="art dialog-art" style="'+art(k)+'"></div><p>'+c.tribe+'　·　攻击 / 耐久　<b>'+effective.power+' / '+effective.health+'</b>　·　'+(c.skill?c.skill+'，'+G.skills[c.skill]:'基础生物')+'</p><div class="land-options">部署费用 '+costHTML(c.mana)+'</div><p>两名'+c.tribe+'出战时，'+G.tribes[c.tribe]+'。</p>'+(reason?'<p class="notice">'+reason+'</p>':'')+'<div class="dlg-tools">'+(board?button(i===0?"已在队首":"向前移动一位","advance",i,"outline",i===0||state.stage!=="prep"):button("支付并上阵","deploy",i,"outline",!!reason))+button("卖出 · 返 1 金","sell",value,"subtle",state.stage!=="prep")+'</div>');
}
function stopReplay(){clearInterval(replayTimer);replayTimer=null}
$("dialog").addEventListener("close",stopReplay);
function drawReplay(){
  const frames=state.last?.frames, host=$("replay");
  if(!host || !frames?.length) return;
  const row=frames[replayIndex];
  function fighter(unit,side){
    if(!unit) return '<div class="fighter vacant">'+side+' · 空阵</div>';
    const c=G.cards[unit.key];
    return '<div class="fighter '+(unit.health<=0?'fallen':'')+'"><div class="art" style="'+art(unit.key)+'"></div><div class="fighter-name"><small>'+side+'</small><strong>'+c.name+'</strong><span>'+unit.power+' / '+Math.max(0,unit.health)+(unit.health<=0?' · 退场':'')+'</span></div></div>';
  }
  host.innerHTML='<div class="replay-heading"><b>交锋回放</b><small>'+plain(replayIndex+1)+' / '+plain(frames.length)+'</small></div><div class="fighters">'+fighter(row.left,"我方")+'<i>×</i>'+fighter(row.right,"对手")+'</div><p>'+row.note+'</p><div class="playback">'+button("上一步","step","-1","subtle",replayIndex===0)+button(replayTimer?"暂停":"播放","play","","subtle",frames.length<2)+button("下一步","step","1","subtle",replayIndex+1===frames.length)+'</div>';
}
function playReplay(){
  if(replayTimer){stopReplay();drawReplay();return}
  if(replayIndex+1===state.last.frames.length) replayIndex=0;
  replayTimer=setInterval(()=>{
    replayIndex++;
    if(replayIndex+1>=state.last.frames.length) stopReplay();
    drawReplay();
  },1150);
  drawReplay();
}
function result(){
  const r=state.last,other=state.results[state.round-1][1];
  const label=r.diff>0?"本轮获胜":r.diff<0?"本轮落败":"本轮平局";
  const line=r.diff===0?"双方生物全部退场。":(r.diff>0?"你的队伍":"对方队伍")+"有 "+Math.abs(r.diff)+" 名生物留在场上。";
  const log=r.log.map(v=>'<li>'+v.replaceAll("甲方","我方").replaceAll("乙方","对方")+'</li>').join("");
  modal(label,'<p>第 '+plain(state.round)+' 轮　你 对 '+G.teams[r.b].name+'</p><div class="combat-score">+'+(r.diff>0?3:r.diff===0?1:0)+' <small style="font:14px var(--sans)">积分</small></div><p>'+line+' 本场净胜生物 '+signed(r.diff)+'。</p><div id="replay"></div><p>另一桌 · '+G.teams[other.a].short+' 对 '+G.teams[other.b].short+'，'+(other.diff===0?"平局":G.teams[other.diff>0?other.a:other.b].short+"获胜")+'。</p><details><summary>交锋记录</summary><ol class="log">'+(log||'<li>一方空阵，直接结算。</li>')+'</ol></details>'+button(state.round===6?'查看最终排名 →':'进入下一轮 · 领取 4 金 →',"next","","primary"));
  replayIndex=0;drawReplay();
  if(r.frames?.length>1 && !matchMedia("(prefers-reduced-motion: reduce)").matches) playReplay();
}
function finalRank(){
  const rank=G.ranked(state),me=rank.find(r=>r.id===0);
  modal("邀请赛结束",'<p>六轮打满。你的名次</p><div class="combat-score">'+(rank.filter(r=>r.rank===me.rank).length>1?"并列":"")+'第 '+me.rank+' 名</div><table><tr><th>排名</th><th>牌手</th><th>积分</th><th>净胜生物</th></tr>'+rank.map(r=>'<tr><td>'+r.rank+'</td><td>'+G.teams[r.id].name+'</td><td>'+r.points+'</td><td>'+signed(r.diff)+'</td></tr>').join("")+'</table>'+button("再打一局","restart","","primary"));
}
function startFight(){
  const ok=G.battle(state);if(ok){save();render();result()}
}
function fightClick(){
  if(state.stage==="end") return finalRank();
  if(state.stage==="result") return result();
  if(!state.board.length) return modal("空阵出战？",'<p>这轮还没有生物上阵。你可以回到备战区，点一张已购买的牌部署。</p><div class="dlg-tools">'+button("返回部署","close","","outline")+button("仍然出战","confirm-fight","","subtle")+'</div>');
  startFight();
}
document.addEventListener("click",event=>{
  const el=event.target.closest("button[data-act]");if(!el || el.disabled) return;
  const act=el.dataset.act,v=el.dataset.val || "",i=Number(v);
  switch(act){
    case "desk":closeModal();window.scrollTo({top:0});break;
    case "close":closeModal();break;
    case "rules":rules();break;
    case "schedule":showSchedule();break;
    case "scout":scout();break;
    case "land":commit(G.chooseLand(state,v),"已获得"+G.colors[v].land+"，本轮可以使用。");break;
    case "buy":{const c=G.cards[state.shop[i]];commit(G.buy(state,i),c?.name+"已进入备战区，点牌上阵。");break}
    case "refresh":commit(G.refresh(state),"已花费 1 金币换牌。");break;
    case "upgrade":upgrade();break;
    case "unit":inspectUnit(v);break;
    case "deploy":{const prev=state.trick;const ok=G.deploy(state,i);commit(ok,prev&&!state.trick?"已上阵。对应地已用尽，预设战术已取消。":"部署完成，本轮法术力已支付。");break}
    case "sell":{const [zone,index]=v.split("-");commit(G.sell(state,zone,Number(index)),"已卖出，获得 1 金币。");break}
    case "advance":commit(G.advance(state,i),"队列顺序已调整。");break;
    case "tactics":tactics();break;
    case "spell":commit(G.setTrick(state,v||null),v?"已预设"+G.colors[v].spell+"。":"已取消本轮战术。");break;
    case "fight":fightClick();break;
    case "confirm-fight":startFight();break;
    case "play":playReplay();break;
    case "step":stopReplay();replayIndex=Math.max(0,Math.min(state.last.frames.length-1,replayIndex+i));drawReplay();break;
    case "next":if(G.next(state)){commit(true,state.stage==="end"?"":"新一轮开始，所有地已重置。");if(state.stage==="end")finalRank()}break;
    case "reset":modal("重新开始邀请赛？",'<p>当前六轮赛程和积分将清空，新局从 6 金币和一间初级营地开始。</p><div class="dlg-tools">'+button("继续当前对局","close","","subtle")+button("重新开局","restart","","outline")+'</div>');break;
    case "restart":state=G.newGame();commit(true,"新一局邀请赛已开始。");window.scrollTo({top:0});break;
  }
});
render();
