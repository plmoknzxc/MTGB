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
    coral:entry("珊瑚骑手","人鱼","U",3,5,7,"突击")
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
  const skills={突击:"首次攻击 +1 伤害",护甲:"开战获得 2 点护盾",接力:"阵亡时，最近的一名友军本场 +1 攻击"};
  const tribes={鬼怪:"鬼怪 +1 攻击",妖精:"妖精 +3 耐久",士兵:"士兵获得 2 点护盾",人鱼:"人鱼首次攻击 +2 伤害"};
  const teams=[{name:"你",tribe:"鬼怪",colors:"RB",captain:"expert"},{name:"煤牙 · 洛克",short:"洛克",art:"ember",tribe:"鬼怪",colors:"RB",captain:"expert"},{name:"青枝 · 芙岚",short:"芙岚",art:"grove",tribe:"妖精",colors:"GB",captain:"archer"},{name:"潮歌 · 伊芮",short:"伊芮",art:"shoal",tribe:"人鱼",colors:"UG",captain:"mist"}];
  const pairings=[[[0,1],[2,3]],[[0,2],[1,3]],[[0,3],[1,2]]];
  const xpFor=[0,0,4,6,8,10];
  const grades=[null,null,[85,15,0],[70,30,0],[55,35,10],[40,40,20],[30,40,30]];
  const blank=n=>Array(n).fill(null);
  const owned=p=>[...p.board,...p.bench].filter(Boolean);
  const stats=u=>({attack:cards[u.key].attack*[1,2,4][u.star-1],hp:cards[u.key].hp*[1,2,4][u.star-1]});
  const interest=p=>Math.min(3,Math.floor(p.gold/5));
  const sellPrice=u=>cards[u.key].cost*3**(u.star-1);
  const active=s=>s.stage==="prep";
  const opponent=s=>pairings[(s.round-1)%3][0][1];
  const pId=(p,k)=>({key:k,id:p.serial++,star:1});
  function seat(t){
    const p={gold:6,level:2,xp:0,board:blank(6),bench:blank(6),shop:blank(5),locked:false,lands:[...t.colors],plans:[],changed:false,serial:1};
    p.board[1]=pId(p,t.captain);return p;
  }
  function supply(s){
    const stock=Object.fromEntries(Object.entries(cards).map(([k,c])=>[k,[0,18,12,8][c.cost]]));
    for(const p of s.seats){
      for(const u of owned(p)) stock[u.key]-=3**(u.star-1);
      for(const k of p.shop) if(k) stock[k]--;
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
      const tiers=[1,2,3].filter(t=>Object.keys(cards).some(k=>cards[k].cost===t&&stock[k]>0));
      const tier=weighted(tiers,t=>odds[t-1],random);if(!tier)return null;
      const k=weighted(Object.keys(cards).filter(k=>cards[k].cost===tier),k=>stock[k],random);
      stock[k]--;return k;
    });
  }
  function newGame(){
    const s={version:2,round:1,stage:"prep",seats:teams.map(seat),results:[],standings:teams.map(()=>({points:0,diff:0}))};
    s.seats[0].shop=["torch","guard","torch","bog","torch"];
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
  function canBuy(p,i){
    const k=p.shop[i];return !!k&&p.gold>=cards[k].cost&&(p.bench.includes(null)||owned(p).filter(u=>u.key===k&&u.star===1).length>=2);
  }
  function buy(p,i){
    if(!canBuy(p,i))return false;
    const key=p.shop[i];p.shop[i]=null;p.gold-=cards[key].cost;
    const slot=p.bench.indexOf(null);p.bench[slot<0?p.bench.length:slot]=pId(p,key);
    const stars=merge(p,key);p.bench.length=6;return {key,stars};
  }
  function move(p,from,to){
    const parse=addr=>/^([ba])([0-5])$/.exec(addr);
    const a=parse(from),b=parse(to);if(!a||!b||from===to)return false;
    const A=a[1]==="a"?p.board:p.bench,B=b[1]==="a"?p.board:p.bench;
    if(!A[a[2]]|| (A===p.bench && B===p.board && !B[b[2]] && B.filter(Boolean).length>=p.level))return false;
    [A[a[2]],B[b[2]]]=[B[b[2]],A[a[2]]];return true;
  }
  function sell(p,addr){
    if(!/^[ab][0-5]$/.test(addr))return false;
    const zone=addr[0]==="a"?p.board:p.bench,i=Number(addr[1]),u=zone[i];if(!u)return false;
    p.gold+=sellPrice(u);zone[i]=null;return true;
  }
  function addXP(p,n){
    if(p.level===6)return;
    p.xp+=n;
    while(p.level<6 && p.xp>=xpFor[p.level]){
      p.xp-=xpFor[p.level];p.level++;
      if(p.level===4||p.level===6)p.lands.push(null);
    }
    if(p.level===6)p.xp=0;
  }
  function train(p){if(p.gold<4 || p.level===6)return false;p.gold-=4;addXP(p,4);return true}
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
    if(s.round>1 && p.gold>=6 && p.board.filter(Boolean).length>=p.level)train(p);
    for(let i=0;i<p.lands.length;i++)if(!p.lands[i])p.lands[i]=t.colors[i%t.colors.length];
    const priority=k=>Number(cards[k].tribe===theme)*4+owned(p).filter(u=>u.key===k&&u.star===1).length*2+cards[k].cost;
    for(let pass=0;pass<2;pass++){
      const shop=p.shop.map((k,i)=>({k,i})).filter(o=>o.k).sort((a,b)=>priority(b.k)-priority(a.k));
      for(const {k,i} of shop){
        if(owned(p).length>=p.level && cards[k].tribe!==theme && !owned(p).some(u=>u.key===k))continue;
        if(s.round===1 && owned(p).length>=2)break;
        buy(p,i);
      }
      if(pass===0 && s.round>1 && p.gold>=5)roll(s,p);else break;
    }
    const strength=u=>stats(u).attack+stats(u).hp+Number(cards[u.key].tribe===theme)*3;
    const group=owned(p).sort((a,b)=>strength(b)-strength(a));
    const field=group.slice(0,p.level).sort((a,b)=>stats(b).hp-stats(a).hp);
    p.board=blank(6);p.bench=blank(6);
    field.forEach((u,i)=>p.board[[1,3,2,5,0,4][i]]=u);
    group.slice(p.level).forEach((u,i)=>p.bench[i]=u);
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
      p.gold+=5+interest(p);addXP(p,2);p.plans=[];p.changed=false;
      if(p.locked)p.locked=false;else draw(s,p);
    }
    for(let id=1;id<4;id++)bot(s,id);
    prepare(s);return true;
  }
  return {cards,teams,landNames,spells,skills,tribes,xpFor,grades,newGame,supply,owned,stats,interest,sellPrice,opponent,buy,canBuy,move,sell,train,roll,costLeft,spellReason,addPlan,cleanPlans,changeLand,synergies,rank,order,prepare,battle,next};
})();
