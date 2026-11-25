"use strict";
const C=Chess, $=id=>document.getElementById(id), saveKey="mtgb.copper-gate.board-v2";
function fresh(){const s=C.newGame();C.prepare(s);return s}
function restore(){
  try{const s=JSON.parse(localStorage.getItem(saveKey));if(s?.version===2 && s.seats?.length===4 && s.round>=1 && s.round<=6)return s}catch{}
  return fresh();
}
let state=restore(),selected=null,toastHandle,viewFight=null,step=0,lastTrial=null;
const player=()=>state.seats[0],enemy=()=>state.seats[C.opponent(state)],prep=()=>state.stage==="prep";
const sym=k=>`<img class="land-icon" src="art/${k}.png" alt="${C.landNames[k]}">`;
const symbols=str=>`<span class="card-symbols">${[...str].map(sym).join("")}</span>`;
const btn=(name,act,value="",cls="tool",disabled=false)=>`<button data-act="${act}" data-val="${value}" class="${cls}" ${disabled?"disabled":""}>${name}</button>`;
const art=k=>`--art:url('art/${k}.jpg')`;
const signature=k=>Object.values(C.spells).find(spell=>spell.source===k);
const star=u=>"★".repeat(u.star);
const signed=n=>n>0?"+"+n:String(n);
const posName=i=>(i<3?"前排":"后排")+" · "+["左","中","右"][i%3];
const addrPiece=addr=>addr?(addr[0]==="a"?player().board:player().bench)[Number(addr[1])]:null;
const outcome=n=>n>0?"胜利":n<0?"落败":"平局";
function save(){try{localStorage.setItem(saveKey,JSON.stringify(state))}catch{}}
function toast(msg){$("toast").textContent=msg;$("toast").classList.add("visible");clearTimeout(toastHandle);toastHandle=setTimeout(()=>$("toast").classList.remove("visible"),2200)}
function dialog(title,content){$("dialog-content").innerHTML=`<h2 id="dialog-title">${title}</h2>${content}`;if(!$("dialog").open)$("dialog").showModal()}
function close(){if($("dialog").open)$("dialog").close()}
function changed(message){
  const old=player().plans.length;
  C.cleanPlans(player(),enemy());C.prepare(state);
  if(selected&&!addrPiece(selected))selected=null;
  close();save();render();if(message)toast(message+(old>player().plans.length?"；失去目标或费用的预设已取消。":""));
}
function slot(u,i,board){
  const addr=(board?"a":"b")+i,cl=`slot ${board?"board-slot":""} ${u?"held":"free"} ${selected===addr?"selected":selected?"destination":""}`;
  const desc=(board?posName(i):"备战席 "+(i+1))+" "+(u?C.cards[u.key].name+" "+u.star+" 星":"空位");
  let face=board?["Ⅰ","Ⅱ","Ⅲ"][i%3]:String(i+1);
  if(u){
    const c=C.cards[u.key],s=C.stats(u);
    face=`<span class="picture" style="${art(u.key)}"></span><span class="face"><i>${star(u)}</i><strong>${c.name}</strong><small>${c.tribe} · ${s.attack} / ${s.hp}</small></span>`;
  }
  return `<button class="${cl}" data-act="slot" data-val="${addr}" aria-label="${desc}" draggable="${!!u&&prep()}" title="${desc}">${face}</button>`;
}
function board(){
  const p=player(),field=p.board.filter(Boolean);
  $("pop").innerHTML=`人口 <b>${field.length} / ${p.level}</b>`;
  $("battlefield").innerHTML=[0,1].map(row=>`<div class="board-row"><span class="row-label">${row?"后排":"前排"}</span>${p.board.slice(3*row,3*row+3).map((u,j)=>slot(u,3*row+j,true)).join("")}</div>`).join("");
  const synergy=C.synergies(p.board);
  $("traits").innerHTML=`<div class="traitline">${Object.entries(synergy).map(([k,n])=>`<b>${k} ${n}/2</b>${n>=2?" · "+C.tribes[k]:""}`).join("　")||"两种同部族棋子可激活配合。"}　<span>前排优先受击，同排优先对位。</span></div>`;
  $("reserve-count").textContent=`${p.bench.filter(Boolean).length} / 6`;
  $("reserve").innerHTML=p.bench.map((u,i)=>slot(u,i,false)).join("");
  const u=addrPiece(selected);
  if(!u){$("piece-actions").innerHTML='<div class="muted">拖动棋子，或点棋子再点目标格，可上阵、撤回或交换。</div>';return}
  const c=C.cards[u.key],isBoard=selected[0]==="a";
  const unique=signature(u.key);
  $("piece-actions").innerHTML=`<div class="piece-bar"><b>${c.name}</b>${symbols(c.colors)}<span>${c.skill} · ${C.skills[c.skill]}${unique?"。在场可用「"+unique.name+"」。":""}</span>${btn(isBoard?"撤回备战席":"快速上阵","quick","","small-action",isBoard?!p.bench.includes(null):field.length>=p.level)}${btn("出售 · "+C.sellPrice(u)+" 金","sell","","small-action")}${btn("取消选择","unselect","","linkish")}</div>`;
}
function market(){
  const p=player(),stock=C.supply(state);
  $("shop-tools").innerHTML=`<span class="wallet"><i class="coin"></i>${p.gold} 金</span>${btn(p.locked?"已锁店 ✓":"锁店","lock","","tool "+(p.locked?"on":""),!prep())}${btn("刷新 · 2 金","roll","","tool",!prep()||p.gold<2)}`;
  $("offers").innerHTML=p.shop.map((k,i)=>{
    if(!k)return '<div class="shop-card soldout">已招募</div>';
    const c=C.cards[k],count=C.owned(p).filter(u=>u.key===k&&u.star===1).length;
    const ready=count===2,ok=C.canBuy(p,i)&&prep();
    const note=ready?"买入即升星":count===1?"已有 1 / 3":"招募";
    const unique=signature(k);
    return `<button data-act="buy" data-val="${i}" class="shop-card c-${c.colors[0]} ${ok?"":"disabled"}" ${ok?"":"disabled"} title="${c.skill} · ${C.skills[c.skill]}，共享池剩 ${stock[k]} 张"><span class="art" style="${art(k)}"><span class="power">${c.attack}/${c.hp}</span></span><span class="card-body"><strong>${c.name}</strong><small>${c.tribe}${symbols(c.colors)}</small><small>${unique?"在场可用 · "+unique.name:c.skill+" · "+C.skills[c.skill]}</small></span><span class="purchase ${ready?"ready":""}">${note} · ${c.cost} 金</span></button>`;
  }).join("");
}
function lands(){
  const p=player(),free=new Set((C.costLeft(p)||[]).map(l=>l.slot));
  $("sources").innerHTML='<div class="landline">'+p.lands.map((c,i)=>c?`<span class="landtoken ${free.has(i)?"":"spent"}">${sym(c)}${C.landNames[c]} · ${free.has(i)?"可用":prep()?"预留":"已横置"}</span>`:btn("+ 选新地","lands","","tool",!prep())).join("")+'</div>';
  const book=Object.entries(C.spells).filter(([k,s])=>s.source?p.board.some(u=>u?.key===s.source):p.lands.includes(s.cost));
  $("spellbook").innerHTML=book.map(([k,s])=>{
    const reason=C.spellReason(p,k);
    return `<button data-act="spell" data-val="${k}" class="spell-row" ${!prep()||reason?"disabled":""} title="${s.text}"><span><span class="s-cost">${symbols(s.cost)}</span> <b>${s.name}</b>${s.source?" <small>· "+C.cards[s.source].name+"</small>":""}</span><span class="${reason?"":"available"}">${reason?"费用不足":"选目标 →"}</span></button><p class="spell-summary">${s.text}</p>`;
  }).join("");
  $("orders").innerHTML=`<div class="orders">`+(p.plans.length?p.plans.map((plan,i)=>{
    const sp=C.spells[plan.spell],u=(sp.enemy?enemy():p).board.find(v=>v?.id===plan.target);
    return `<div class="order"><span>${sp.name} → ${sp.enemy?"敌方":"我方"}${C.cards[u.key].name}</span>${btn("撤销","unplan",i,"linkish",!prep())}</div>`;
  }).join(""):'<span class="muted">选目标预留法术力，开战时自动横置对应地。</span>')+"</div>";
}
function economy(){
  const p=player(),need=C.xpFor[p.level];
  $("coins").innerHTML=`<span class="wallet">${p.gold} 金币</span>`;
  $("economy").innerHTML=`<div class="econ-line"><b>等级 ${p.level}</b><small>${need?p.xp+" / "+need+" 经验":"已满级"}</small></div><div class="xp-bar"><i style="width:${need?p.xp/need*100:100}%"></i></div><div class="econ-line">${btn("买经验 · 4 金换 4 XP","train","","tool",!prep()||!need||p.gold<4)}<small>每轮 +2 XP</small></div><small>下一轮收入　5 基础 + ${C.interest(p)} 利息（每存 5 金 +1，上限 3）</small>`;
  if(state.round===6)$("economy").lastElementChild.textContent="本轮是决赛，不再结算下一轮收入。";
}
function render(){
  const t=C.teams[C.opponent(state)],e=enemy();
  $("round").innerHTML=`<span class="round-tag">${prep()?"备战":state.stage==="result"?"结算":"完赛"} <b>${String(state.round).padStart(2,"0")}</b> / 06</span>`;
  board();market();lands();economy();
  $("rival").innerHTML=`<div class="kicker">本轮对手 · 电脑牌手</div><div class="enemy-info"><div class="art portrait" style="${art(t.art)}"></div><div><h2>${t.name}</h2><span class="muted">${t.tribe} · ${e.board.filter(Boolean).length} 人出战</span></div>${btn("侦察 ↗","scout","","linkish")}</div>`;
  document.querySelector('[data-act="trial"]').disabled=!prep();
  document.querySelector('.battle-actions [data-act="fight"]').textContent=prep()?"正式交战 →":state.stage==="result"?"本轮战报 →":"最终排名 →";
}
function handleMove(from,to){
  if(!prep())return;
  if(!C.move(player(),from,to)){toast("人口已满时，可直接和场上的棋子交换。");return}
  selected=null;changed("阵容已调整。");
}
function selectSlot(addr){
  if(!prep())return;
  if(selected&&selected!==addr)return handleMove(selected,addr);
  selected=selected===addr?null:addrPiece(addr)?addr:null;board();
}
function editLands(){
  const p=player();
  dialog("调整法术力来源",`<p>一块地每场产一点对应颜色的法术力。首轮可自由选色，以后每轮可换一块已有地。升至 4、6 级各获得一个新地位。换色时会释放无法支付的预设。</p><div class="land-modal">${p.lands.map((color,i)=>`<div class="land-edit"><b>地位 ${i+1} · ${C.landNames[color]||"待选"}</b><div class="b-tools">${Object.entries(C.landNames).map(([k,name])=>btn(sym(k)+name,"setland",i+":"+k,"small-action",!prep()||k===color||!!color&&p.changed&&state.round>1)).join("")}</div></div>`).join("")}</div>`);
}
function spellTargets(k){
  const sp=C.spells[k],p=player(),e=enemy(),targets=(sp.enemy?e:p).board;
  dialog(sp.name+" · 选择"+(sp.enemy?"敌军":"友军"),`<p>${symbols(sp.cost)}　${sp.text}</p><div class="target-list">${targets.map((u,i)=>u?btn(`${posName(i)}　<b>${C.cards[u.key].name}</b>　${star(u)}`,"plan",k+":"+u.id,"",["undying","frost","blast"].includes(k)&&p.plans.some(plan=>plan.spell===k&&plan.target===u.id)):"").join("")||"场上没有可用目标。"}</div>`);
}
function scout(){
  const e=enemy(),t=C.teams[C.opponent(state)],sy=C.synergies(e.board);
  dialog("侦察 · "+t.name,`<p>等级 ${e.level}，持有 ${e.gold} 金。其招募和升星消耗同一个共享牌池。</p><table><tr><th>站位</th><th>棋子</th><th>部族</th></tr>${e.board.map((u,i)=>u?`<tr><td>${posName(i)}</td><td>${C.cards[u.key].name} ${star(u)}</td><td>${C.cards[u.key].tribe}</td></tr>`:"").join("")}</table><p>部族　${Object.entries(sy).map(([t,n])=>t+" "+n+"/2").join("　")}</p><p>预设　${e.plans.map(pl=>C.spells[pl.spell].name).join("、")||"无"}</p>`);
}
function rules(){
  dialog("玩法手册",`<h3>招募与升星</h3><p>开局有 6 金、2 人口，以及一名免费的部族骨干。金币买棋子到备战席，各色棋子都能免费换上场。三张同名同星自动升一星，场上和备战席一起计数；三星封顶。升星使基础攻防翻倍。满备战席仍能买第三张直接合成。出售返还所含棋子的总购价。</p><h3>商店与经济</h3><p>首轮你和洛克的商店固定，方便用同一开局比较站位与施法。每店五格。刷新 2 金；锁店保留剩余格子到下一轮，随后解锁。四席共用牌池，一、二、三费棋子每种分别有 18、12、8 张。未买的展示牌暂时占池，刷新或出售会返池。升级提高高费出现率。</p><p>每个新回合 +5 金，每存 5 金再得 1 利息（最多 3），并获得 2 经验。4 金买 4 经验；由等级 2 升至 3、4、5、6 分别需 4、6、8、10 经验。等级决定人口，等级 4 和 6 各得一块地。</p><h3>布阵与部族</h3><p>前后两排各三格，全员同时攻击。敌方有前排时必须先打前排，同排优先打最近一列，同距选左。相同部族的两种不同棋子即可触发加成。</p><p>${Object.entries(C.tribes).map(([k,v])=>k+" · "+v).join("；")}</p><h3>土地与双色能力</h3><p>基本地按自身颜色产费。每场备战时把法术力分给要施放的法术，选目标，随时可撤销。可把红黑分开施放火花与不息；火药巧匠在场时，也可预留红黑两费给任意友军挂一次殉爆。带 GU、RW 的棋子也有各自的双色能力。开战先结算强化与标记，再结算直伤，之后按次交锋。每场战损还原，地重置。</p><h3>对手与赛程</h3><p>三位本地电脑按部族偏好买棋、合星、升级并施法。与每人交战两次，共六轮。胜 3 分，平 1 分，负 0 分；同分看净胜生物数，仍相同则并列。免费试战使用本轮双方当前阵容和预设，可反复比较，不推进赛程。</p>`);
}
function standings(){
  const r=C.rank(state,0),title=state.stage==="end"?"完赛 · 第 "+r+" 名":"赛程积分";
  dialog(title,`<table><tr><th>名次</th><th>牌手</th><th>积分</th><th>净胜</th></tr>${C.order(state).map(a=>`<tr><td>${C.rank(state,a.id)}</td><td>${C.teams[a.id].name}</td><td>${a.points}</td><td>${signed(a.diff)}</td></tr>`).join("")}</table><h3>你的六轮赛程</h3><p>${Array.from({length:6},(_,i)=>`${i+1} · ${C.teams[i%3+1].short}（${state.results[i]?outcome(state.results[i][0].diff):"待战"}）`).join("　")}</p>`+(state.stage==="end"?btn("再打一局","restart","","primary"):""));
}
function replay(fight,trial){
  viewFight={fight,trial,previous:trial?lastTrial:null};step=0;
  if(trial)lastTrial={round:state.round,diff:fight.diff,plans:player().plans.map(p=>C.spells[p.spell].name).join("＋")||"未施法"};
  showFrame();
}
function showFrame(){
  const {fight,trial,previous}=viewFight,f=fight.frames[step];
  const teams=["我方","敌方"];
  const boards=f.boards.map((side,i)=>`<section><strong>${teams[i]}</strong>${side.map(u=>`<small class="${u.hp<=0?"dead":""}">${posName(u.pos)}　${C.cards[u.key].name} ${star(u)} · ${u.hp<=0?"阵亡":u.hp+" 耐久"+(u.shield?" / "+u.shield+" 盾":"")}</small>`).join("")||"<small>空阵</small>"}</section>`).join("");
  const compare=previous?.round===state.round?`<p>上次试战「${previous.plans}」净胜 ${signed(previous.diff)}，本次 ${signed(fight.diff)}。</p>`:"";
  dialog((trial?"试战":"正式交战")+" · "+outcome(fight.diff),`<p>第 ${state.round} 轮 · 净胜生物 ${signed(fight.diff)}${trial?" · 本次不计积分":" · 本场获得 "+(fight.diff>0?3:fight.diff===0?1:0)+" 分"}</p>${compare}<div class="combat-lines">${boards}</div><div class="frame-nav">${btn("← 上一步","frame",-1,"small-action",step===0)}<b>${f.title}　${step+1}/${fight.frames.length}</b>${btn("下一步 →","frame",1,"small-action",step===fight.frames.length-1)}</div><ul class="round-log">${(f.notes.length?f.notes:["双方没有预设法术。"]).map(n=>"<li>"+n+"</li>").join("")}</ul>${trial?btn("返回备战","close","","primary"):btn(state.round===6?"查看最终排名":"领取收入，进入下一轮","next","","primary")}`);
}
function fight(trial){
  if(!prep()){
    if(state.stage==="end")standings();else replay(state.results.at(-1)[0],false);return;
  }
  if(trial)return replay(Duel.run(player(),enemy()),true);
  if(!player().board.some(Boolean))return dialog("空阵出战？","<p>你可以从备战席免费部署棋子。</p>"+btn("仍然出战","official","","primary"));
  official();
}
function official(){if(C.battle(state)){selected=null;save();render();replay(state.results.at(-1)[0],false)}}
function action(act,val){
  const p=player(),e=enemy(),i=Number(val);
  switch(act){
    case "close":close();return;
    case "rules":rules();return;
    case "lands":editLands();return;
    case "scout":scout();return;
    case "standings":standings();return;
    case "reset":dialog("重新开局？","<p>清空当前新版赛程，从第一轮重新开始。</p>"+btn("确认重新开局","restart","","primary"));return;
    case "restart":state=fresh();selected=null;lastTrial=null;changed("新局已开始。");return;
    case "trial":fight(true);return;
    case "fight":fight(false);return;
    case "official":official();return;
    case "frame":step=Math.max(0,Math.min(viewFight.fight.frames.length-1,step+i));showFrame();return;
    case "next":if(C.next(state)){selected=null;lastTrial=null;close();save();render();if(state.stage==="end")standings()}return;
  }
  if(!prep())return;
  switch(act){
    case "slot":selectSlot(val);break;
    case "unselect":selected=null;board();break;
    case "buy":{const b=C.buy(p,i);if(b)changed(b.stars.length?C.cards[b.key].name+"合成 "+b.stars.at(-1)+" 星！":C.cards[b.key].name+"已加入备战席。");break}
    case "sell":if(C.sell(p,selected)){selected=null;changed("棋子已出售并返还牌池。")}break;
    case "quick":{if(!selected)break;const target=selected[0]==="a"?"b":"a",at=(target==="a"?p.board:p.bench).indexOf(null);if(at>=0)handleMove(selected,target+at);break}
    case "lock":p.locked=!p.locked;changed(p.locked?"下轮保留当前商店。":"商店已解锁。");break;
    case "roll":if(C.roll(state,p))changed("商店已刷新。");break;
    case "train":{const lvl=p.level;if(C.train(p))changed(lvl===p.level?"已购买 4 经验。":"升至等级 "+p.level+"，人口上限增加。");break}
    case "setland":{const [slot,c]=val.split(":");if(C.changeLand(state,p,Number(slot),c))changed("已选择"+C.landNames[c]+"。");break}
    case "spell":spellTargets(val);break;
    case "plan":{const [k,id]=val.split(":");if(C.addPlan(p,e,k,Number(id)))changed("已预设"+C.spells[k].name+"。");break}
    case "unplan":p.plans.splice(i,1);changed("已释放预留法术力。");break;
  }
}
document.addEventListener("click",e=>{const b=e.target.closest("button[data-act]");if(b&&!b.disabled)action(b.dataset.act,b.dataset.val??"")});
document.addEventListener("dragstart",e=>{const b=e.target.closest(".slot.held");if(!b||!prep())return;e.dataTransfer.setData("text/plain",b.dataset.val);e.dataTransfer.effectAllowed="move"});
document.addEventListener("dragover",e=>{if(e.target.closest(".slot")&&prep())e.preventDefault()});
document.addEventListener("drop",e=>{const b=e.target.closest(".slot");if(!b)return;e.preventDefault();handleMove(e.dataTransfer.getData("text/plain"),b.dataset.val)});
render();
