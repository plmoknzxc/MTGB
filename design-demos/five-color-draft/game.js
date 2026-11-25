// Standalone playtest rules. Card names, costs, combat and the league are specific to this prototype.
"use strict";
const League = (() => {
  const colors = {
    W: {land:"平原", color:"白", spell:"护身", text:"队首本场 +0/+3"},
    U: {land:"海岛", color:"蓝", spell:"退潮", text:"敌方队首移至队尾"},
    B: {land:"沼泽", color:"黑", spell:"汲取", text:"敌首失去 1 耐久，己首 +0/+1"},
    R: {land:"山脉", color:"红", spell:"火花", text:"对敌方队首造成 2 点伤害"},
    G: {land:"树林", color:"绿", spell:"生长", text:"队首本场 +2/+2"}
  };
  const card = (name, tribe, mana, power, health, skill, tier = 1) =>
    ({name, tribe, mana, power, health, skill, tier, price:tier + 1});
  const cards = {
    torch:card("举炬鬼怪","鬼怪","R",2,1,"突击"),
    bog:card("泥沼拾荒者","鬼怪","B",1,2,"接力"),
    leaf:card("林间斥候","妖精","G",2,2,""),
    tide:card("银鳃巡游者","人鱼","U",2,1,"护甲"),
    guard:card("铜门新兵","士兵","W",1,3,"护甲"),
    chief:card("战鼓头领","鬼怪","RR",3,3,"突击",2),
    expert:card("火药巧匠","鬼怪","BR",3,3,"接力",2),
    archer:card("荆弓射手","妖精","1B",2,3,"突击",2),
    druid:card("古林守望者","妖精","GG",3,4,"护甲",2),
    mist:card("唤雾使","人鱼","GU",3,3,"护甲",2),
    coral:card("珊瑚骑手","人鱼","UU",3,3,"突击",2),
    smith:card("铸盾老兵","士兵","1W",2,4,"接力",2),
    legion:card("赤锋军团长","士兵","RW",4,3,"突击",2)
  };
  const skills = {突击:"首次交锋 +1 攻击",护甲:"首次交锋少受 1 点伤害",接力:"倒下时下一位友军 +1 攻击"};
  const tribes = {鬼怪:"所有鬼怪 +1 攻击",妖精:"所有妖精 +2 耐久",人鱼:"第二位起的人鱼 +1/+1",士兵:"最靠前的士兵 +1/+2"};
  const teams = [
    {name:"你", short:"你", theme:"自由构筑"},
    {name:"煤牙 · 洛克",short:"洛克", theme:"红黑鬼怪",art:"ember",plan:"前排打出突击，后排接力收尾。", builds:[
      ["torch"],["bog","torch"],["torch","expert"],["chief","bog","torch"],["chief","expert","bog"],["chief","expert","bog","torch"]]},
    {name:"青枝 · 芙岚",short:"芙岚",theme:"绿黑妖精",art:"grove",plan:"妖精成群时更耐打，擅长拖长交锋。",builds:[
      ["leaf"],["leaf","leaf"],["archer","leaf"],["druid","leaf","archer"],["archer","druid","leaf"],["archer","druid","leaf","leaf"]]},
    {name:"潮歌 · 伊芮",short:"伊芮",theme:"蓝白守军",art:"shoal",plan:"护甲守住队首，让后排人鱼接战。",builds:[
      ["tide"],["guard","tide"],["tide","coral"],["guard","tide","coral"],["smith","tide","coral"],["smith","guard","tide","coral"]]}
  ];
  const pairings = [[[0,1],[2,3]],[[0,2],[1,3]],[[0,3],[1,2]]];
  const levelPrices = [0,4,6,8];
  const opening = ["torch","leaf","tide","bog","guard"];
  const newGame = () => ({
    round:1, gold:6, level:1, lands:[], shop:[...opening], bench:[], board:[],
    trick:null, stage:"prep", results:[], standings:teams.map(()=>({points:0, diff:0})), last:null
  });
  const opponent = round => pairings[(round-1)%3][0][1];
  const army = (team,round) => teams[team].builds[round-1];
  function payment(lands,cost,reserveColor=null) {
    const used = [];
    for (const symbol of cost.replace(/[0-9]/g,"")) {
      const index = lands.findIndex((land,i)=>land.color===symbol && !land.tapped && !used.includes(i));
      if (index < 0) return null;
      used.push(index);
    }
    const generic = Number(cost.match(/\d+/)?.[0] || 0);
    const free = lands.map((_,i)=>i).filter(i=>!lands[i].tapped && !used.includes(i));
    if (free.length < generic) return null;
    if(reserveColor) free.sort((a,b)=>Number(lands[a].color===reserveColor)-Number(lands[b].color===reserveColor));
    return used.concat(free.slice(0,generic));
  }
  function shuffled(items,random) {
    const copy=[...items];
    for(let i=copy.length-1;i>0;i--) {
      const j=Math.floor(random()*(i+1));
      [copy[i],copy[j]]=[copy[j],copy[i]];
    }
    return copy;
  }
  function drawShop(s,random=Math.random) {
    if (!s.lands.length) return [...opening];
    const available = shuffled(Object.keys(cards).filter(k=>cards[k].tier<=s.level),random);
    const fresh=s.lands.map(l=>({...l,tapped:false}));
    const playable=available.filter(k=>payment(fresh,cards[k].mana));
    const other=available.filter(k=>!playable.includes(k));
    const picks=[...playable.slice(0,4),...other.slice(0,1)];
    return shuffled([...picks,...available.filter(k=>!picks.includes(k))].slice(0,5),random);
  }
  function chooseLand(s,color) {
    if(s.stage!=="prep" || !colors[color]) return false;
    if(s.lands.length) {
      const price=levelPrices[s.level];
      if(!price || s.gold<price) return false;
      s.level++; s.gold-=price;
    }
    s.lands.push({color,tapped:false});
    if(s.level>1) s.shop=drawShop(s);
    return true;
  }
  function buy(s,index) {
    const key=s.shop[index];
    if(s.stage!=="prep" || !key || s.bench.length>=5 || s.gold<cards[key].price) return false;
    s.gold-=cards[key].price; s.bench.push(key); s.shop[index]=null;
    return true;
  }
  function deploy(s,index) {
    const key=s.bench[index];
    if(s.stage!=="prep" || !key || s.board.length>=s.level+1) return false;
    const paid=payment(s.lands,cards[key].mana,s.trick);
    if(!paid) return false;
    paid.forEach(i=>s.lands[i].tapped=true);
    s.board.push(key); s.bench.splice(index,1);
    if(s.trick && !payment(s.lands,s.trick)) s.trick=null;
    return true;
  }
  function refresh(s) {
    if(s.stage!=="prep" || !s.lands.length || s.gold<1) return false;
    s.gold--; s.shop=drawShop(s); return true;
  }
  function sell(s,zone,index) {
    if(s.stage!=="prep" || !["board","bench"].includes(zone) || !s[zone][index]) return false;
    s[zone].splice(index,1); s.gold++; return true;
  }
  function advance(s,index) {
    if(s.stage!=="prep" || index<1 || !s.board[index]) return false;
    [s.board[index-1],s.board[index]]=[s.board[index],s.board[index-1]]; return true;
  }
  function setTrick(s,color) {
    if(s.stage!=="prep" || (color!==null && (!colors[color] || !payment(s.lands,color)))) return false;
    s.trick=color; return true;
  }
  const tribeCounts = keys=>keys.reduce((m,k)=>{const t=cards[k].tribe;m[t]=(m[t]||0)+1;return m},{});
  function fighters(keys) {
    const counts=tribeCounts(keys), seen={};
    return keys.map(k=>{
      const unit={...cards[k],key:k,fresh:true};
      const t=unit.tribe;
      if(counts[t]>=2) {
        if(t==="鬼怪") unit.power++;
        if(t==="妖精") unit.health+=2;
        if(t==="人鱼" && seen[t]) {unit.power++;unit.health++}
        if(t==="士兵" && !seen[t]) {unit.power++;unit.health+=2}
      }
      seen[t]=true;
      return unit;
    });
  }
  function fight(left,right,trick=null) {
    const a=fighters(left), b=fighters(right), log=[], frames=[];
    const snapshot=u=>u?{key:u.key,power:u.power,health:u.health}:null;
    function frame(x,y,note) {frames.push({left:snapshot(x),right:snapshot(y),note})}
    frame(a[0],b[0],"双方队首就位，部族加成已计入。");
    function clear(line,label) {
      if(line.length && line[0].health<=0) {
        const dead=line.shift();
        log.push(label+"的"+dead.name+"倒下。");
        if(dead.skill==="接力" && line.length) {
          line[0].power++; log.push("接力使"+line[0].name+"获得 1 点攻击。");
        }
      }
    }
    if(a.length && b.length && trick) {
      log.push(colors[trick].spell+"发动 · "+colors[trick].text+"。");
      if(trick==="W") a[0].health+=3;
      if(trick==="U") b.push(b.shift());
      if(trick==="B") {b[0].health--;a[0].health++}
      if(trick==="R") b[0].health-=2;
      if(trick==="G") {a[0].health+=2;a[0].power+=2}
      frame(a[0],b[0],colors[trick].spell+"发动 · "+colors[trick].text+"。");
      clear(b,"乙方");
    }
    while(a.length && b.length) {
      const x=a[0], y=b[0];
      const damage=(h,v)=>Math.max(0,h.power+Number(h.fresh && h.skill==="突击")-Number(v.fresh && v.skill==="护甲"));
      const hit=damage(x,y), back=damage(y,x);
      x.health-=back; y.health-=hit;
      frame(x,y,"相互交锋 · 我方造成 "+hit+" 点伤害，对方造成 "+back+" 点。");
      log.push(x.name+" ⇄ "+y.name+"，分别造成 "+hit+" / "+back+" 点伤害。");
      x.fresh=false;y.fresh=false;clear(a,"甲方");clear(b,"乙方");
    }
    return {diff:a.length-b.length, leftAlive:a.length,rightAlive:b.length,log,frames};
  }
  function battle(s) {
    if(s.stage!=="prep" || !s.lands.length) return false;
    const pairs=pairings[(s.round-1)%3], scores=[];
    for(const [a,b] of pairs) {
      const cost=a===0 && s.trick ? payment(s.lands,s.trick) : null;
      const result=fight(a===0?s.board:army(a,s.round),army(b,s.round),cost?s.trick:null);
      if(cost) cost.forEach(i=>s.lands[i].tapped=true);
      for(const [id,d] of [[a,result.diff],[b,-result.diff]]) {
        s.standings[id].points+=d>0?3:d===0?1:0;
        s.standings[id].diff+=d;
      }
      scores.push({a,b,...result});
    }
    s.results.push(scores); s.last=scores[0]; s.stage="result"; return true;
  }
  function next(s) {
    if(s.stage!=="result") return false;
    if(s.round===6) {s.stage="end";return true}
    s.round++;s.stage="prep";s.gold+=4;s.trick=null;
    s.lands.forEach(l=>l.tapped=false);s.shop=drawShop(s);return true;
  }
  function ranked(s) {
    const sorted=s.standings.map((row,id)=>({...row,id})).sort((a,b)=>b.points-a.points||b.diff-a.diff);
    sorted.forEach((r,i)=>r.rank=i && r.points===sorted[i-1].points && r.diff===sorted[i-1].diff ? sorted[i-1].rank : i+1);
    return sorted;
  }
  return {colors,cards,skills,tribes,teams,levelPrices,newGame,opponent,army,payment,drawShop,chooseLand,buy,deploy,refresh,sell,advance,setTrick,tribeCounts,fighters,fight,battle,next,ranked};
})();
