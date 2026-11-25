"use strict";
const C=Chess, $=id=>document.getElementById(id), saveKey="mtgb.copper-gate.board-v3";
function fresh(){const s=C.newGame();C.prepare(s);return s}
function restore(){
  try{const s=JSON.parse(localStorage.getItem(saveKey));if(s?.version===3 && s.seats?.length===4 && s.round>=1 && s.round<=6)return s}catch{}
  return fresh();
}
let state=restore(),selected=null,toastHandle,viewFight=null,step=0,lastTrial=null,libraryTier=1,libraryColor="";
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
    face=`<span class="picture" style="${art(u.key)}"></span><span class="face"><i>${star(u)}</i><strong>${c.name}</strong><small>${c.tribe} · ${s.attack} / ${s.hp}</small></span><span class="piece-colors">${symbols(c.colors)}</span>`;
  }
  return `<button class="${cl}" data-act="slot" data-val="${addr}" aria-label="${desc}" draggable="${!!u&&prep()}" title="${desc}">${face}</button>`;
}
function board(){
  const p=player(),field=p.board.filter(Boolean);
  $("pop").innerHTML=`已出战 <b>${field.length} / ${C.boardSize}</b>`;
  $("battlefield").innerHTML=[0,1].map(row=>`<div class="board-row"><span class="row-label">${row?"后排":"前排"}</span>${p.board.slice(3*row,3*row+3).map((u,j)=>slot(u,3*row+j,true)).join("")}</div>`).join("");
  const synergy=C.synergies(p.board);
  $("traits").innerHTML=`<div class="traitline">${Object.entries(synergy).map(([k,n])=>`<b>${k} ${n}/2</b>${n>=2?" · "+C.tribes[k]:""}`).join("　")||"两种同部族棋子可激活配合。"}　<span>前排优先受击，同排优先对位。</span></div>`;
  $("reserve-count").textContent=`${p.bench.filter(Boolean).length} / 6`;
  $("reserve").innerHTML=p.bench.map((u,i)=>slot(u,i,false)).join("");
  const u=addrPiece(selected);
  if(!u||!prep()){$("piece-actions").innerHTML='<div class="muted">拖动棋子，或点棋子再点目标格，可上阵、撤回或交换。</div>';return}
  const c=C.cards[u.key],isBoard=selected[0]==="a";
  const unique=signature(u.key);
  $("piece-actions").innerHTML=`<div class="piece-bar"><b>${c.name}</b>${symbols(c.colors)}<span>${c.skill} · ${C.skills[c.skill]}${unique?"。在场可用「"+unique.name+"」。":""}　${echoLabel(c)}</span>${btn(isBoard?"撤回备战席":"快速上阵","quick","","small-action",isBoard?!p.bench.includes(null):field.length>=C.boardSize)}${btn("出售 · "+C.sellPrice(u)+" 金","sell","","small-action")}${btn("取消选择","unselect","","linkish")}</div>`;
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
    return `<button data-act="buy" data-val="${i}" class="shop-card c-${c.colors[0]} ${ok?"":"disabled"}" ${ok?"":"disabled"} title="${c.skill} · ${C.skills[c.skill]}，共享池剩 ${stock[k]} 张"><span class="art" style="${art(k)}"><span class="power">${c.attack}/${c.hp}</span></span><span class="card-body"><strong>${c.name}</strong><small>${c.cost} 阶 · ${c.tribe}${symbols(c.colors)}</small><small>${unique?"在场可用 · "+unique.name:c.skill+" · "+C.skills[c.skill]}</small></span><span class="purchase ${ready?"ready":""}">${note} · ${c.cost} 金</span></button>`;
  }).join("");
}
function echoLabel(c){return [...c.colors].map(k=>C.echoes[k].color+" · "+C.echoes[k].name).join(" / ")}
function lands(){
  const p=player(),free=new Set((C.costLeft(p)||[]).map(l=>l.slot));
  $("sources").innerHTML='<div class="landline">'+p.lands.map((c,i)=>c?`<span class="landtoken ${free.has(i)?"":"spent"}">${sym(c)}${C.landNames[c]} · ${free.has(i)?"可用":prep()?"预留":"已横置"}</span>`:btn("+ 选新地","lands","","tool",!prep())).join("")+'</div>';
  const book=Object.entries(C.spells).filter(([k,s])=>s.source?p.board.some(u=>u?.key===s.source):p.lands.includes(s.cost));
  $("spellbook").innerHTML=book.map(([k,s])=>{
    const reason=C.spellReason(p,k),planned=C.plannedColors(p);
    const preview=[...s.cost].map(color=>{
      const n=p.board.filter(u=>u&&C.cards[u.key].colors.includes(color)).length;
      return n?C.echoes[color].name+" ×"+n+(planned.includes(color)?"（已预设）":""):"";
    }).filter(Boolean).join("、");
    return `<button data-act="spell" data-val="${k}" class="spell-row" ${!prep()||reason?"disabled":""} title="${s.text}"><span><span class="s-cost">${symbols(s.cost)}</span> <b>${s.name}</b>${s.source?" <small>· "+C.cards[s.source].name+"</small>":""}</span><span class="${reason?"":"available"}">${reason?"费用不足":"选目标 →"}</span></button><p class="spell-summary">${s.text}${preview?'<span class="echo-hint">'+preview+'</span>':""}</p>`;
  }).join("");
  $("orders").innerHTML=`<div class="orders">`+(p.plans.length?p.plans.map((plan,i)=>{
    const sp=C.spells[plan.spell],u=(sp.enemy?enemy():p).board.find(v=>v?.id===plan.target);
    return `<div class="order"><span>${sp.name} → ${sp.enemy?"敌方":"我方"}${C.cards[u.key].name}</span>${btn("撤销","unplan",i,"linkish",!prep())}</div>`;
  }).join(""):'<span class="muted">选目标预留法术力，开战时自动横置对应地。</span>')+"</div>";
}
function economy(){
  const p=player(),need=C.xpFor[p.level],odds=C.grades[p.level];
  $("coins").innerHTML=`<span class="wallet">${p.gold} 金币</span>`;
  $("economy").innerHTML=`<div class="econ-line"><b>营地 ${p.level} 阶</b><small>${need?p.xp+" / "+need+" 经验":"已满阶"}</small></div><div class="xp-bar"><i style="width:${need?p.xp/need*100:100}%"></i></div><div class="econ-line">${btn("买经验 · 4 金换 4 XP","train","","tool",!prep()||!need||p.gold<4)}<small>每轮 +2 XP</small></div><p class="camp-reward">${need?"下一级解锁 "+(p.level+1)+" 阶棋子，并免费三选一。":"五阶棋子已全部解锁。"}</p><p class="muted">抽取率　${odds.map((n,i)=>n?i+1+"阶 "+n+"%":"").filter(Boolean).join(" · ")}</p><small>${state.round===6?"最后一轮，不再发放轮间收入。":"下一轮收入　5 基础 + "+C.interest(p)+" 利息（每存 5 金 +1，上限 3）"}</small>`;
  $("rewards").innerHTML=p.rewards.length?btn("领取 "+p.rewards[0].tier+" 阶招募奖励"+(p.rewards.length>1?" · "+p.rewards.length+" 份":""),"reward","","reward-button",!prep()):"";
}
function previewCard(key,act,value,footer,disabled=false){
  const c=C.cards[key];
  return `<button class="catalog-card c-${c.colors[0]}" data-act="${act}" data-val="${value}" ${disabled?"disabled":""}><span class="art" style="${art(key)}"><span class="power">${c.attack}/${c.hp}</span></span><span class="catalog-body"><strong>${c.name}</strong><small>${c.cost} 阶 ${c.tribe}　${symbols(c.colors)}</small><small>${c.skill} · ${C.skills[c.skill]}</small>${signature(key)?"<small>在场解锁 · "+signature(key).name+"</small>":""}<small class="catalog-echo">${echoLabel(c)}</small></span><span class="catalog-foot">${footer}</span></button>`;
}
function rewardDialog(){
  const p=player(),r=p.rewards[0];if(!r||!prep())return;
  const offered=r.choices.map(k=>{
    const ok=C.canReceive(p,k),merge=C.owned(p).filter(u=>u.key===k&&u.star===1).length===2;
    return previewCard(k,"claim",r.tier+":"+k,ok?(merge?"免费领取并升星":"免费招募"):"备战席已满",!ok);
  }).join("");
  dialog("营地升至 "+r.tier+" 阶",`<p>新阶商店已解锁。以下棋子可免费选一张进入备战席，剩余候选返还共享池。</p><div class="catalog-grid reward-grid">${offered}</div>${!offered?btn("本阶已无余牌，领取 "+r.tier+" 金","claim",r.tier+":gold","tool"):""}<div class="reward-foot"><small>需要腾位置时，奖励会为你保留。</small>${btn("稍后选择","close","","linkish")}</div>`);
}
function library(){
  const p=player(),stock=C.supply(state);
  const shown=Object.entries(C.cards).filter(([,c])=>c.cost===libraryTier && (!libraryColor||c.colors.includes(libraryColor)));
  dialog("棋子图鉴",`<p>四席共享 40 种棋子，每阶 8 种。选择一张查看能力、颜色和余量。</p><div class="filter-row">${C.tiers.map(t=>btn(t+" 阶","filter-tier",t,"tool "+(t===libraryTier?"on":""))).join("")}</div><div class="filter-row">${btn("全部颜色","filter-color","","tool "+(!libraryColor?"on":""))}${Object.keys(C.echoes).map(k=>btn(sym(k)+C.echoes[k].color,"filter-color",k,"tool "+(libraryColor===k?"on":""))).join("")}</div><p>${libraryTier<=p.level?"已解锁":"营地 "+libraryTier+" 阶解锁"} · 本次筛选 ${shown.length} 种</p><div class="catalog-grid">${shown.map(([k])=>previewCard(k,"card-info",k,"共享池剩 "+stock[k]+" 张")).join("")||'<p>本阶没有这个颜色的棋子。</p>'}</div>`);
}
function cardInfo(k){
  const c=C.cards[k];if(!c)return;
  const unique=signature(k);
  dialog(c.name,`<div class="art dialog-art" style="${art(k)}"></div><p>${c.cost} 阶 · ${c.tribe}　${symbols(c.colors)}　基础攻防 ${c.attack}/${c.hp}</p><h3>${c.skill}</h3><p>${C.skills[c.skill]}。</p><h3>施放同色法术时</h3>${[...c.colors].map(color=>`<p>${sym(color)} ${C.echoes[color].name} · ${C.echoes[color].text}。每场一次。</p>`).join("")}${unique?`<h3>在场解锁 · ${unique.name}</h3><p>${symbols(unique.cost)} ${unique.text}</p>`:""}<p>共享池剩 ${C.supply(state)[k]} / ${C.copies[c.cost]} 张。攻防、招募价格和技能均为本模式试玩数值。</p>${btn("返回图鉴","back-library","","primary")}`);
}
function colorGuide(){
  const p=player(),active=C.plannedColors(p);
  dialog("棋子的五色响应",`<p>你实际施放某色法术时，所有带该印色的己方出战棋子触发一次对应效果；一场战斗中同色只触发一次。双色棋子可响应两色。</p>${Object.entries(C.echoes).map(([color,e])=>{
    const matching=p.board.filter(u=>u&&C.cards[u.key].colors.includes(color)).map(u=>C.cards[u.key].name);
    return `<h3>${sym(color)} ${e.color} · ${e.name}</h3><p>${e.text}。${matching.length?(active.includes(color)?"本场已预设响应 · ":"场上可响应 · ")+matching.join("、"):"当前场上无此色棋子。"}</p>`;
  }).join("")}`);
}
function render(){
  const t=C.teams[C.opponent(state)],e=enemy();
  $("round").innerHTML=`<span class="round-tag">${prep()?"备战":state.stage==="result"?"结算":"完赛"} <b>${String(state.round).padStart(2,"0")}</b> / 06</span>`;
  board();market();lands();economy();
  $("pool-entry").innerHTML=`<span>当前可招募 · ${player().level===1?"1 阶":"1 至 "+player().level+" 阶"}</span>${btn("棋子图鉴 · 40 种 ↗","library","","linkish")}`;
  $("traits").insertAdjacentHTML("beforeend",btn("五色响应 ↗","colors","","arena-link"));
  $("rival").innerHTML=`<div class="kicker">本轮对手 · 电脑牌手</div><div class="enemy-info"><div class="art portrait" style="${art(t.art)}"></div><div><h2>${t.name}</h2><span class="muted">${t.tribe} · ${e.board.filter(Boolean).length} 人出战</span></div>${btn("侦察 ↗","scout","","linkish")}</div>`;
  document.querySelector('[data-act="trial"]').disabled=!prep();
  document.querySelector('.battle-actions [data-act="fight"]').textContent=prep()?"正式交战 →":state.stage==="result"?"本轮战报 →":"最终排名 →";
}
function handleMove(from,to){
  if(!prep())return;
  if(!C.move(player(),from,to)){toast("选择一个不同的目标格，即可移动或交换棋子。");return}
  selected=null;changed("阵容已调整。");
}
function selectSlot(addr){
  if(!prep())return;
  if(selected&&selected!==addr)return handleMove(selected,addr);
  selected=selected===addr?null:addrPiece(addr)?addr:null;board();
}
function editLands(){
  const p=player();
  dialog("调整法术力来源",`<p>一块地每场产一点对应颜色的法术力。首轮可自由选色，以后每轮可换一块已有地。进入第 3、5 轮各获得一个新地位。换色时会释放无法支付的预设。</p><div class="land-modal">${p.lands.map((color,i)=>`<div class="land-edit"><b>地位 ${i+1} · ${C.landNames[color]||"待选"}</b><div class="b-tools">${Object.entries(C.landNames).map(([k,name])=>btn(sym(k)+name,"setland",i+":"+k,"small-action",!prep()||k===color||!!color&&p.changed&&state.round>1)).join("")}</div></div>`).join("")}</div>`);
}
function spellTargets(k){
  const sp=C.spells[k],p=player(),e=enemy(),targets=(sp.enemy?e:p).board;
  dialog(sp.name+" · 选择"+(sp.enemy?"敌军":"友军"),`<p>${symbols(sp.cost)}　${sp.text}</p><div class="target-list">${targets.map((u,i)=>u?btn(`${posName(i)}　<b>${C.cards[u.key].name}</b>　${star(u)}`,"plan",k+":"+u.id,"",["undying","frost","blast"].includes(k)&&p.plans.some(plan=>plan.spell===k&&plan.target===u.id)):"").join("")||"场上没有可用目标。"}</div>`);
}
function scout(){
  const e=enemy(),t=C.teams[C.opponent(state)],sy=C.synergies(e.board);
  dialog("侦察 · "+t.name,`<p>营地 ${e.level} 阶，持有 ${e.gold} 金。其招募和升星消耗同一个共享牌池。</p><table><tr><th>站位</th><th>棋子</th><th>部族</th></tr>${e.board.map((u,i)=>u?`<tr><td>${posName(i)}</td><td>${C.cards[u.key].name} ${star(u)}</td><td>${C.cards[u.key].tribe}</td></tr>`:"").join("")}</table><p>部族　${Object.entries(sy).map(([t,n])=>t+" "+n+"/2").join("　")}</p><p>预设　${e.plans.map(pl=>C.spells[pl.spell].name).join("、")||"无"}</p>`);
}
function rules(){
  dialog("玩法手册",`<h3>招募与升星</h3><p>开局有 6 金、六个全部开放的出战格，以及一名免费的二阶部族骨干。金币买棋子到备战席，各色棋子都能免费换上场。三张同名同星自动升一星，场上和备战席一起计数；三星封顶。升星使基础攻防翻倍。满备战席仍能买第三张直接合成。出售返还所含棋子的总购价。</p><h3>商店与经济</h3><p>首轮你和洛克的商店固定，方便用同一开局比较站位与施法。每店五格。刷新 2 金；锁店保留剩余格子到下一轮，随后解锁。四席共用 40 种棋子，每阶 8 种。一至五阶每种分别有 18、15、12、10、9 张。商店和待选奖励都暂时占池。未选奖励、刷走的牌和出售的棋子会返池。</p><p>每个新回合 +5 金，每存 5 金再得 1 利息（最多 3），并获得 2 经验。4 金买 4 经验；营地从一阶升到五阶，每级分别需 4、6、8、10 经验。每次升级解锁新阶商店，并从本阶三种棋子中免费选一张。手动升阶时，未锁定的商店立即免费刷新。奖励可以暂存，满备战席时能直接合星的棋子仍可领。若某阶共享池不足三种则展示实际余牌，完全空池时可领等同该阶的金币。</p><h3>布阵与部族</h3><p>前后两排各三格，全员同时攻击。敌方有前排时必须先打前排，同排优先打最近一列，同距选左。相同部族的两种不同棋子即可触发加成。</p><p>${Object.entries(C.tribes).map(([k,v])=>k+" · "+v).join("；")}</p><h3>五色响应</h3><p>己方施放某色法术时，战场上所有带该印色的己方棋子获得该色效果。每场每种颜色只响应一次，看支付的颜色，与法术目标无关。双色棋子可以获得两色效果。闲置的地和敌方施法都不会替你触发。</p><p>${Object.values(C.echoes).map(e=>e.color+" · "+e.text).join("。")}。</p><h3>土地与双色能力</h3><p>第 3、5 轮分别获得新地位，自选五种基本地之一。基本地按自身颜色产费。每场备战时把法术力分给要施放的法术，选目标，随时可撤销。可把红黑分开施放火花与不息；火药巧匠在场时，也可预留红黑两费给任意友军挂一次殉爆。唤雾使和赤锋军团长也各自解锁一种专属法术。开战先结算强化与标记，再结算直伤，之后按次交锋。每场战损还原，地重置。</p><h3>对手与赛程</h3><p>三位本地电脑按部族偏好买棋、合星、升级并施法。与每人交战两次，共六轮。胜 3 分，平 1 分，负 0 分；同分看净胜生物数，仍相同则并列。免费试战使用本轮双方当前阵容和预设，可反复比较，不推进赛程。</p>`);
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
    case "library":libraryTier=i||player().level;libraryColor="";library();return;
    case "filter-tier":libraryTier=i;library();return;
    case "filter-color":libraryColor=val;library();return;
    case "card-info":cardInfo(val);return;
    case "back-library":library();return;
    case "reward":if(prep())rewardDialog();return;
    case "colors":colorGuide();return;
    case "lands":editLands();return;
    case "scout":scout();return;
    case "standings":standings();return;
    case "reset":dialog("重新开局？","<p>清空当前新版赛程，从第一轮重新开始。</p>"+btn("确认重新开局","restart","","primary"));return;
    case "restart":state=fresh();selected=null;lastTrial=null;changed("新局已开始。");return;
    case "trial":fight(true);return;
    case "fight":fight(false);return;
    case "official":official();return;
    case "frame":step=Math.max(0,Math.min(viewFight.fight.frames.length-1,step+i));showFrame();return;
    case "next":{
      const before=p.level;
      if(C.next(state)){selected=null;lastTrial=null;close();save();render();if(state.stage==="end")standings();else if(p.level>before)rewardDialog()}
      return;
    }
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
    case "train":{
      const before=p.level;if(!C.train(state,p))break;
      changed(before===p.level?"已购买 4 经验。":"营地 "+p.level+" 阶已解锁。");
      if(p.level>before)rewardDialog();break;
    }
    case "setland":{const [slot,c]=val.split(":");if(C.changeLand(state,p,Number(slot),c))changed("已选择"+C.landNames[c]+"。");break}
    case "spell":spellTargets(val);break;
    case "plan":{const [k,id]=val.split(":");if(C.addPlan(p,e,k,Number(id)))changed("已预设"+C.spells[k].name+"。");break}
    case "claim":{
      const [tier,key]=val.split(":");const gained=C.claimReward(p,Number(tier),key);
      if(gained){const stars=gained.stars||[];changed(key==="gold"?"已领取空池补偿。":"免费招募"+C.cards[key].name+(stars.length?"，合成 "+stars.at(-1)+" 星！":"。"));if(p.rewards.length)rewardDialog()}
      break;
    }
    case "unplan":p.plans.splice(i,1);changed("已释放预留法术力。");break;
  }
}
document.addEventListener("click",e=>{const b=e.target.closest("button[data-act]");if(b&&!b.disabled)action(b.dataset.act,b.dataset.val??"")});
document.addEventListener("dragstart",e=>{const b=e.target.closest(".slot.held");if(!b||!prep())return;e.dataTransfer.setData("text/plain",b.dataset.val);e.dataTransfer.effectAllowed="move"});
document.addEventListener("dragover",e=>{if(e.target.closest(".slot")&&prep())e.preventDefault()});
document.addEventListener("drop",e=>{const b=e.target.closest(".slot");if(!b)return;e.preventDefault();handleMove(e.dataTransfer.getData("text/plain"),b.dataset.val)});
render();
