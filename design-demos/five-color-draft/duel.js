"use strict";
const Duel=(()=>{
  function army(seat){
    const counts=Chess.synergies(seat.board);
    const units=seat.board.flatMap((piece,pos)=>{
      if(!piece)return [];
      const c=Chess.cards[piece.key],s=Chess.stats(piece),group=counts[c.tribe]>=2;
      return [{...piece,...s,pos,shield:(c.skill==="护甲"?2:0)+(group&&c.tribe==="士兵"?2:0),
        hp:s.hp+(group&&c.tribe==="妖精"?3:0),attack:s.attack+(group&&c.tribe==="鬼怪"?1:0),
        opening:(c.skill==="突击"?1:0)+(group&&c.tribe==="人鱼"?2:0),pierce:c.skill==="贯穿",
        skip:false,revive:c.skill==="回生"?3:0,blast:false,ambush:false,grudge:false}];
    });
    for(const source of units){
      const c=Chess.cards[source.key];
      for(const ally of units){
        if(source===ally)continue;
        if(c.skill==="号令"&&Chess.cards[ally.key].tribe===c.tribe)ally.attack+=2;
        if(c.skill==="守誓"&&Math.floor(ally.pos/3)===Math.floor(source.pos/3)&&Math.abs(source.pos-ally.pos)===1)ally.shield+=3;
      }
    }
    return units;
  }
  const living=line=>line.filter(u=>u.hp>0);
  function nearest(candidates,pos){
    return [...candidates].sort((a,b)=>Math.abs(a.pos%3-pos%3)-Math.abs(b.pos%3-pos%3)||a.pos-b.pos)[0];
  }
  function victim(line,pos,backFirst=false){
    const all=living(line),preferred=all.filter(u=>backFirst?u.pos>=3:u.pos<3);
    return nearest(preferred.length?preferred:all,pos);
  }
  function run(left,right){
    const sides=[army(left),army(right)],frames=[];
    const label=(side,u)=>(side===0?"我方":"敌方")+Chess.cards[u.key].name;
    const copy=u=>({id:u.id,key:u.key,star:u.star,pos:u.pos,hp:Math.max(0,u.hp),attack:u.attack,shield:u.shield});
    function frame(title,notes){frames.push({title,notes,boards:sides.map(line=>line.map(copy))})}
    function hit(u,n){const absorb=Math.min(u.shield,n);u.shield-=absorb;u.hp-=n-absorb}
    // Resolve deaths in waves. All simultaneous death triggers get to resolve, even after a mutual knockout.
    function deaths(notes){
      for(;;){
        const dead=sides.map(line=>line.filter(u=>u.hp<=0 && !u.removed));
        if(!dead.some(list=>list.length))return;
        dead.forEach(list=>list.forEach(u=>u.removed=true));
        dead.forEach((list,side)=>list.forEach(u=>{
          notes.push(label(side,u)+"阵亡。");
          if(u.revive){u.hp=u.revive;u.revive=0;u.shield=0;u.removed=false;notes.push(label(side,u)+"以 "+u.hp+" 耐久复活。")}
        }));
        const blasts=[0,0],grudges=[];
        dead.forEach((list,side)=>list.forEach(u=>{
          if(u.blast){u.blast=false;blasts[1-side]++;notes.push(label(side,u)+"触发引信殉爆，对敌军全体造成 3 点伤害。")}
          if(u.grudge){
            u.grudge=false;
            const target=nearest(living(sides[1-side]),u.pos);
            if(target){grudges.push(target);notes.push(label(side,u)+"触发黑色遗恨，对"+label(1-side,target)+"造成 2 点伤害。")}
          }
          if(Chess.cards[u.key].skill==="接力"){
            const ally=nearest(living(sides[side]).filter(v=>v!==u),u.pos);
            if(ally){ally.attack++;notes.push("接力使"+label(side,ally)+"获得 1 点攻击。")}
          }
        }));
        sides.forEach((line,i)=>{if(blasts[i])living(line).forEach(u=>hit(u,3*blasts[i]))});
        grudges.forEach(u=>hit(u,2));
      }
    }
    let notes=[],fire=[];
    for(const [side,seat] of [left,right].entries()){
      const cast=new Set();
      for(const plan of seat.plans){
        const sp=Chess.spells[plan.spell],sideId=sp.enemy?1-side:side;
        const u=sides[sideId].find(a=>a.id===plan.target);if(!u)continue;
        for(const color of sp.cost)cast.add(color);
        notes.push((side?"敌方":"我方")+"横置 "+sp.cost+"，对"+label(sideId,u)+"施放「"+sp.name+"」。");
        switch(plan.spell){
          case "spark":fire.push(u);break;
          case "ward":u.shield+=4;break;
          case "frost":u.skip=true;break;
          case "growth":u.attack+=2;u.hp+=2;break;
          case "undying":u.revive=Math.max(2,u.revive);break;
          case "blast":u.blast=true;break;
          case "mistveil":sides[side].filter(v=>Math.floor(v.pos/3)===Math.floor(u.pos/3)).forEach(v=>v.shield+=4);break;
          case "charge":sides[side].filter(v=>v.pos%3===u.pos%3).forEach(v=>v.attack+=3);break;
        }
      }
      for(const color of cast){
        const matching=sides[side].filter(u=>Chess.cards[u.key].colors.includes(color));
        if(!matching.length)continue;
        for(const u of matching){
          if(color==="W")u.shield+=2;
          if(color==="U")u.ambush=true;
          if(color==="B")u.grudge=true;
          if(color==="R")u.opening+=2;
          if(color==="G")u.hp+=3;
        }
        const effect=Chess.echoes[color];
        notes.push((side?"敌方":"我方")+effect.color+"色响应「"+effect.name+"」 · "+matching.map(u=>Chess.cards[u.key].name).join("、")+"："+effect.text+"。");
      }
    }
    fire.forEach(u=>hit(u,3));deaths(notes);frame("开战施法",notes);
    for(let volley=1;living(sides[0]).length&&living(sides[1]).length;volley++){
      notes=[];const hits=[];
      sides.forEach((line,i)=>living(line).forEach(u=>{
        if(u.skip){u.skip=false;notes.push(label(i,u)+"受迟滞影响，跳过攻击。");return}
        const target=victim(sides[1-i],u.pos,u.ambush),amount=u.attack+u.opening;
        if(u.ambush&&target.pos>=3)notes.push(label(i,u)+"使用蓝色潜袭，绕向敌方后排。");
        u.ambush=false;
        if(u.pierce){
          u.pierce=false;
          const rear=living(sides[1-i]).find(v=>v.pos===target.pos+3);
          if(rear){hits.push({target:rear,amount:2});notes.push(label(i,u)+"贯穿至"+label(1-i,rear)+"，造成 2 点伤害。")}
        }
        u.opening=0;hits.push({target,amount});
        notes.push(label(i,u)+"攻击"+label(1-i,target)+"，造成 "+amount+" 点伤害（护盾优先承受）。");
      }));
      hits.forEach(({target,amount})=>hit(target,amount));deaths(notes);frame("第 "+volley+" 次交锋",notes);
    }
    return {diff:living(sides[0]).length-living(sides[1]).length,frames};
  }
  return {run};
})();
