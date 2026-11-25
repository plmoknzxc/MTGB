"use strict";
const Chess=(()=>{
  const entry=(name,tribe,colors,cost,attack,hp,skill)=>({name,tribe,colors,cost,attack,hp,skill});
  const cards={
    torch:entry("举炬鬼怪","鬼怪","R",1,3,4,"突击"),bog:entry("泥沼拾荒者","鬼怪","B",1,2,5,"接力"),
    leaf:entry("林间斥候","妖精","G",1,2,6,"护甲"),tide:entry("银鳃巡游者","人鱼","U",1,3,4,"突击"),
    guard:entry("铜门新兵","士兵","W",1,2,7,"护甲"),expert:entry("火药巧匠","鬼怪","BR",2,3,4,"接力"),
    archer:entry("荆弓射手","妖精","B",2,4,5,"突击"),smith:entry("铸盾老兵","士兵","W",2,3,8,"护甲"),
    mist:entry("唤雾使","人鱼","GU",2,3,6,"接力"),legion:entry("赤锋军团长","士兵","RW",2,4,5,"突击"),
    chief:entry("战鼓头领","鬼怪","R",3,5,7,"突击"),druid:entry("古林守望者","妖精","G",3,4,10,"护甲"),
    coral:entry("珊瑚骑手","人鱼","U",3,5,7,"突击"),
    skirk:entry("炉渣勘探兵","鬼怪","R",1,3,3,"贯穿"),
    stream:entry("溪岸向导","人鱼","G",1,2,5,"接力"),
    sentry:entry("巡夜哨兵","士兵","B",1,2,6,"护甲"),
    banner:entry("战旗传令兵","鬼怪","R",2,3,6,"号令"),
    seer:entry("林地唤歌者","妖精","G",2,3,6,"守誓"),
    pearl:entry("白浪旗手","人鱼","W",2,3,7,"护甲"),
    raider:entry("深窟突袭者","鬼怪","B",3,5,6,"回生"),
    ranger:entry("晨露游侠","妖精","GW",3,4,8,"守誓"),
    wake:entry("澜锋领航员","人鱼","U",3,4,7,"贯穿"),
    marshal:entry("铜墙指挥官","士兵","W",3,4,9,"号令"),
    pike:entry("灰烬长枪手","士兵","R",3,5,7,"贯穿"),
    squad:entry("火巷伏兵","鬼怪","BR",4,5,9,"贯穿"),
    slinger:entry("熔岩投手","鬼怪","R",4,6,7,"突击"),
    caller:entry("夜林唤灵师","妖精","BG",4,4,10,"回生"),
    warden:entry("青枝护林官","妖精","G",4,5,11,"守誓"),
    surge:entry("巨浪咏者","人鱼","U",4,5,9,"号令"),
    grovefin:entry("浪林卫士","人鱼","GU",4,5,9,"守誓"),
    aegis:entry("银盾教头","士兵","W",4,5,11,"守誓"),
    skyknight:entry("空骑队长","士兵","RW",4,6,8,"贯穿"),
    warlord:entry("煤牙战争领主","鬼怪","R",5,7,10,"号令"),
    revenant:entry("沼火不死王","鬼怪","BR",5,6,11,"回生"),
    speaker:entry("万枝之声","妖精","G",5,6,13,"号令"),
    dusk:entry("暮影族母","妖精","BG",5,6,11,"回生"),
    empress:entry("深潮女皇","人鱼","U",5,6,11,"贯穿"),
    tideguard:entry("珊瑚王庭卫","人鱼","WU",5,6,12,"守誓"),
    suncrest:entry("曙光大元帅","士兵","W",5,6,13,"号令"),
    firemane:entry("烽火统领","士兵","RW",5,7,10,"贯穿"),
  };
  const landNames={W:"平原",U:"海岛",B:"沼泽",R:"山脉",G:"树林"};
  const spells={
    spark:{name:"火花",cost:"R",enemy:true,text:"开战时对指定敌人造成 3 点伤害。"},
    undying:{name:"不息",cost:"B",text:"指定友军首次阵亡后，以 2 点耐久复活。"},
    ward:{name:"护佑",cost:"W",text:"指定友军获得能吸收 4 点伤害的护盾。"},
    frost:{name:"迟滞",cost:"U",enemy:true,text:"指定敌人跳过第一次攻击。"},
    growth:{name:"生长",cost:"G",text:"指定友军本场 +2 攻击、+2 耐久。"},
    blast:{name:"引信殉爆",cost:"BR",source:"expert",text:"指定友军阵亡时，对所有存活敌人造成 3 点伤害。"},
    mistveil:{name:"潮林庇护",cost:"GU",source:"mist",text:"指定友军及其同排友军各获得 4 点护盾。"},
    charge:{name:"并肩冲锋",cost:"RW",source:"legion",text:"指定友军及其同列友军，本场各 +3 攻击。"}
  };
  const skills={突击:"首次攻击 +1 伤害",护甲:"开战获得 2 点护盾",接力:"阵亡时，最近的一名友军本场 +1 攻击",贯穿:"首次攻击额外对目标正后方造成 2 点伤害",守誓:"开战时同排相邻友军各得 3 点护盾",号令:"开战时其他同部族友军各得 2 攻击",回生:"首次阵亡后以 3 耐久复活，与不息取较高值"};
  const echoes={
    W:{color:"白",name:"守护",text:"自己获得 2 点护盾"},
    U:{color:"蓝",name:"潜袭",text:"首次攻击优先瞄准敌方后排"},
    B:{color:"黑",name:"遗恨",text:"首次阵亡时对最近敌人造成 2 点伤害"},
    R:{color:"红",name:"猛攻",text:"自己的首次攻击额外造成 2 点伤害"},
    G:{color:"绿",name:"壮大",text:"自己本场增加 3 点耐久"}
  };
  const plannedColors=p=>[...new Set(p.plans.flatMap(plan=>[...spells[plan.spell].cost]))];
  const tribes={鬼怪:"鬼怪 +1 攻击",妖精:"妖精 +3 耐久",士兵:"士兵获得 2 点护盾",人鱼:"人鱼首次攻击 +2 伤害"};
  const teams=[{name:"你",tribe:"鬼怪",colors:"RB",captain:"expert"},{name:"煤牙 · 洛克",short:"洛克",art:"ember",tribe:"鬼怪",colors:"RB",captain:"expert"},{name:"青枝 · 芙岚",short:"芙岚",art:"grove",tribe:"妖精",colors:"GB",captain:"archer"},{name:"潮歌 · 伊芮",short:"伊芮",art:"shoal",tribe:"人鱼",colors:"UG",captain:"mist"}];
  const pairings=[[[0,1],[2,3]],[[0,2],[1,3]],[[0,3],[1,2]]];
  const xpFor=[0,4,6,8,10];
  const grades=[null,[100,0,0,0,0],[55,45,0,0,0],[36,38,26,0,0],[20,30,30,20,0],[10,20,25,25,20]];
  const tiers=[1,2,3,4,5],copies=[0,18,15,12,10,9];
  const boardSize=6;
  const blank=n=>Array(n).fill(null);
  const owned=p=>[...p.board,...p.bench].filter(Boolean);
  const stats=u=>({attack:cards[u.key].attack*[1,2,4][u.star-1],hp:cards[u.key].hp*[1,2,4][u.star-1]});
  const interest=p=>Math.min(3,Math.floor(p.gold/5));
  const sellPrice=u=>cards[u.key].cost*3**(u.star-1);
  const active=s=>s.stage==="prep";
  const opponent=s=>pairings[(s.round-1)%3][0][1];
  const pId=(p,k)=>({key:k,id:p.serial++,star:1});
  function seat(t){
    const p={gold:6,level:1,xp:0,board:blank(boardSize),bench:blank(6),shop:blank(5),locked:false,lands:[...t.colors],plans:[],changed:false,serial:1,rewards:[]};
    p.board[1]=pId(p,t.captain);return p;
  }
  function supply(s){
    const stock=Object.fromEntries(Object.entries(cards).map(([k,c])=>[k,copies[c.cost]]));
    for(const p of s.seats){
      for(const u of owned(p)) stock[u.key]-=3**(u.star-1);
      for(const k of p.shop) if(k) stock[k]--;
      for(const reward of p.rewards)for(const k of reward.choices)stock[k]--;
    }
    return stock;
  }
  function weighted(choices,weight,random){
    const total=choices.reduce((sum,k)=>sum+weight(k),0);if(!total)return null;
    let n=random()*total;
    for(const k of choices){n-=weight(k);if(n<0)return k}
    return choices.at(-1);
  }
  function draw(s,p,random=Math.random){
    p.shop=blank(5);const stock=supply(s), odds=grades[p.level];
    p.shop=p.shop.map(()=>{
      const unlocked=tiers.filter(t=>t<=p.level && Object.keys(cards).some(k=>cards[k].cost===t&&stock[k]>0));
      const tier=weighted(unlocked,t=>odds[t-1],random);if(!tier)return null;
      const k=weighted(Object.keys(cards).filter(k=>cards[k].cost===tier),k=>stock[k],random);
      stock[k]--;return k;
    });
  }
  function newGame(){
    const s={version:3,round:1,stage:"prep",seats:teams.map(seat),results:[],standings:teams.map(()=>({points:0,diff:0}))};
    s.seats[0].shop=["torch","guard","leaf","tide","bog"];
    // Both first-round shops are fixed so repeated openings can compare tactics.
    s.seats[1].shop=["torch","guard","leaf","tide","bog"];
    bot(s,1);
    for(let i=2;i<4;i++){draw(s,s.seats[i]);bot(s,i)}
    return s;
  }
  function merge(p,key){
    const matches=[];
    for(let star=1;star<3;star++){
      const slots=[...p.board,...p.bench].filter(u=>u && u.key===key && u.star===star);
      if(slots.length<3)break;
      const [keep,...used]=slots.slice(0,3);keep.star++;matches.push(keep.star);
      for(const u of used)for(const zone of [p.board,p.bench]){const i=zone.indexOf(u);if(i>=0)zone[i]=null}
    }
    return matches;
  }
  const canReceive=(p,k)=>p.bench.includes(null)||owned(p).filter(u=>u.key===k&&u.star===1).length>=2;
  function receive(p,key){
    const slot=p.bench.indexOf(null);p.bench[slot<0?p.bench.length:slot]=pId(p,key);
    const stars=merge(p,key);p.bench.length=6;return {key,stars};
  }
  function canBuy(p,i){
    const k=p.shop[i];return !!k&&p.gold>=cards[k].cost&&canReceive(p,k);
  }
  function buy(p,i){
    if(!canBuy(p,i))return false;
    const key=p.shop[i];p.shop[i]=null;p.gold-=cards[key].cost;
    return receive(p,key);
  }
  function move(p,from,to){
    const parse=addr=>/^([ba])([0-5])$/.exec(addr);
    const a=parse(from),b=parse(to);if(!a||!b||from===to)return false;
    const A=a[1]==="a"?p.board:p.bench,B=b[1]==="a"?p.board:p.bench;
    if(!A[a[2]])return false;
    [A[a[2]],B[b[2]]]=[B[b[2]],A[a[2]]];return true;
  }
  function sell(p,addr){
    if(!/^[ab][0-5]$/.test(addr))return false;
    const zone=addr[0]==="a"?p.board:p.bench,i=Number(addr[1]),u=zone[i];if(!u)return false;
    p.gold+=sellPrice(u);zone[i]=null;return true;
  }
  function offerReward(s,p){
    const stock=supply(s),pool=Object.keys(cards).filter(k=>cards[k].cost===p.level && stock[k]>0);
    const choices=[];
    while(pool.length && choices.length<3){
      const k=weighted(pool,k=>stock[k],Math.random);
      choices.push(k);pool.splice(pool.indexOf(k),1);
    }
    p.rewards.push({tier:p.level,choices});
  }
  function claimReward(p,tier,key){
    const reward=p.rewards[0];
    if(!reward || reward.tier!==tier)return false;
    if(!reward.choices.length && key==="gold"){
      p.gold+=tier;p.rewards.shift();return true;
    }
    if(!reward.choices.includes(key)||!canReceive(p,key))return false;
    p.rewards.shift();return receive(p,key);
  }
  function addXP(s,p,n){
    if(p.level===5)return;
    p.xp+=n;
    while(p.level<5 && p.xp>=xpFor[p.level]){
      p.xp-=xpFor[p.level];p.level++;
      offerReward(s,p);
    }
    if(p.level===5)p.xp=0;
  }
  function train(s,p){
    if(!active(s) || p.gold<4 || p.level===5)return false;
    const before=p.level;p.gold-=4;addXP(s,p,4);
    if(p.level>before && !p.locked)draw(s,p);
    return true;
  }
  function roll(s,p){if(p.gold<2)return false;p.gold-=2;draw(s,p);return true}
  function costLeft(p,plans=p.plans){
    const spare=p.lands.map((color,slot)=>({color,slot})).filter(l=>l.color);
    for(const plan of plans){
      for(const color of spells[plan.spell].cost){
        const i=spare.findIndex(l=>l.color===color);if(i<0)return null;spare.splice(i,1);
      }
    }
    return spare;
  }
  function spellReason(p,spell){
    const sp=spells[spell];if(!sp)return "未知法术";
    if(sp.source && !p.board.some(u=>u?.key===sp.source))return "需"+cards[sp.source].name+"在场";
    if(!costLeft(p,[...p.plans,{spell}]))return "可用法术力不足";
    return "";
  }
  function addPlan(p,enemy,spell,target){
    const sp=spells[spell];if(!sp||spellReason(p,spell))return false;
    const army=sp.enemy?enemy.board:p.board;
    if(!army.some(u=>u?.id===target))return false;
    if(["undying","frost","blast"].includes(spell)&&p.plans.some(a=>a.spell===spell&&a.target===target))return false;
    p.plans.push({spell,target});return true;
  }
  function cleanPlans(p,enemy){
    const old=p.plans;p.plans=[];
    for(const plan of old)addPlan(p,enemy,plan.spell,plan.target);
  }
  function changeLand(s,p,i,color){
    if(!Number.isInteger(i)||i<0||i>=p.lands.length||!landNames[color]||color===p.lands[i])return false;
    if(p.lands[i] && s.round>1 && p.changed)return false;
    if(p.lands[i] && s.round>1)p.changed=true;
    p.lands[i]=color;return true;
  }
  function bot(s,id){
    const p=s.seats[id],t=teams[id],theme=t.tribe;
    if(s.round>1 && p.gold>=7 && owned(p).length>=3)train(s,p);
    while(p.rewards.length){
      const reward=p.rewards[0];
      if(!reward.choices.length){claimReward(p,reward.tier,"gold");continue}
      const options=reward.choices.filter(k=>canReceive(p,k));
      if(!options.length)break;
      const preferred=options.find(k=>cards[k].tribe===theme)||options[0];
      claimReward(p,reward.tier,preferred);
    }
    for(let i=0;i<p.lands.length;i++)if(!p.lands[i])p.lands[i]=t.colors[i%t.colors.length];
    const priority=k=>Number(cards[k].tribe===theme)*4+owned(p).filter(u=>u.key===k&&u.star===1).length*2
      +Number(p.lands.some(color=>color&&cards[k].colors.includes(color)))+cards[k].cost;
    for(let pass=0;pass<2;pass++){
      const shop=p.shop.map((k,i)=>({k,i})).filter(o=>o.k).sort((a,b)=>priority(b.k)-priority(a.k));
      for(const {k,i} of shop){
        if(owned(p).length>=boardSize && cards[k].tribe!==theme && !owned(p).some(u=>u.key===k))continue;
        if(s.round===1 && p.gold<=3 && cards[k].tribe!==theme)continue;
        buy(p,i);
      }
      if(pass===0 && s.round>1 && p.gold>=5)roll(s,p);else break;
    }
    const strength=u=>stats(u).attack+stats(u).hp+Number(cards[u.key].tribe===theme)*3;
    const group=owned(p).sort((a,b)=>strength(b)-strength(a));
    const field=group.slice(0,boardSize).sort((a,b)=>stats(b).hp-stats(a).hp);
    p.board=blank(boardSize);p.bench=blank(6);
    field.forEach((u,i)=>p.board[[1,3,2,5,0,4][i]]=u);
    group.slice(boardSize).forEach((u,i)=>p.bench[i]=u);
  }
  function botPlan(p,enemy){
    const me=p.board.find(Boolean),other=enemy.board.find(Boolean);p.plans=[];
    if(!me||!other)return;
    for(const spell of ["blast","mistveil","charge","spark","undying","growth","ward","frost"]){
      const target=spells[spell].enemy?other:me;addPlan(p,enemy,spell,target.id);
    }
  }
  function synergies(board){
    const tribesByKey={};
    for(const u of board)if(u){const t=cards[u.key].tribe;(tribesByKey[t]??=new Set()).add(u.key)}
    return Object.fromEntries(Object.entries(tribesByKey).map(([t,keys])=>[t,keys.size]));
  }
  const order=(s)=>s.standings.map((r,id)=>({...r,id})).sort((a,b)=>b.points-a.points||b.diff-a.diff);
  function rank(s,id){const sorted=order(s),r=s.standings[id];return 1+sorted.findIndex(x=>x.points===r.points&&x.diff===r.diff)}
  function prepare(s){for(const [a,b] of pairings[(s.round-1)%3]){if(a!==0)botPlan(s.seats[a],s.seats[b]);botPlan(s.seats[b],s.seats[a])}}
  function battle(s){
    if(!active(s))return false;
    const scores=pairings[(s.round-1)%3].map(([a,b])=>{
      const fight=Duel.run(s.seats[a],s.seats[b]);
      for(const [id,d] of [[a,fight.diff],[b,-fight.diff]]){
        s.standings[id].points+=d>0?3:d===0?1:0;s.standings[id].diff+=d;
      }
      return {a,b,diff:fight.diff,frames:a===0?fight.frames:[]};
    });
    s.results.push(scores);s.stage="result";return true;
  }
  function next(s){
    if(s.stage!=="result")return false;
    if(s.round===6){s.stage="end";return true}
    s.round++;s.stage="prep";
    for(const p of s.seats){
      p.gold+=5+interest(p);addXP(s,p,2);p.plans=[];p.changed=false;
      if(s.round===3||s.round===5)p.lands.push(null);
      if(p.locked)p.locked=false;else draw(s,p);
    }
    for(let id=1;id<4;id++)bot(s,id);
    prepare(s);return true;
  }
  return {cards,teams,landNames,spells,skills,tribes,echoes,plannedColors,boardSize,tiers,copies,xpFor,grades,newGame,supply,owned,stats,interest,sellPrice,opponent,buy,canBuy,canReceive,claimReward,move,sell,train,roll,costLeft,spellReason,addPlan,cleanPlans,changeLand,synergies,rank,order,prepare,battle,next};
})();
