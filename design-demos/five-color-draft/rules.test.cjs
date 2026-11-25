const {test}=require("node:test");
const assert=require("node:assert/strict");
const {readFileSync,existsSync}=require("node:fs");
const {join}=require("node:path");
const vm=require("node:vm");
const context=vm.createContext({});
for(const file of ["chess.js","duel.js"])vm.runInContext(readFileSync(join(__dirname,file),"utf8"),context);
const {C,D}=vm.runInContext("({C:Chess,D:Duel})",context);
const clone=v=>JSON.parse(JSON.stringify(v));
const unit=(key,id,star=1)=>({key,id,star});
function seat(...keys){
  const board=Array.from({length:6},(_,pos)=>keys[pos]?unit(keys[pos],pos+1):null);
  return {board,plans:[]};
}
test("Each tier contains eight illustrated pieces and can be recruited only after its camp upgrade",()=>{
  const s=C.newGame(),p=s.seats[0];
  assert.equal(Object.keys(C.cards).length,40);
  for(const k of Object.keys(C.cards))assert.ok(existsSync(join(__dirname,"art",k+".jpg")),k);
  p.gold=500;
  for(const tier of C.tiers){
    assert.equal(tier,p.level);
    assert.equal(Object.values(C.cards).filter(c=>c.cost===tier).length,8);
    assert.equal(C.grades[tier].reduce((sum,n)=>sum+n,0),100);
    assert.ok(C.grades[tier].slice(tier).every(n=>n===0));
    for(let i=0;i<8;i++){assert.ok(C.roll(s,p));assert.ok(p.shop.every(k=>k&&C.cards[k].cost<=tier))}
    if(tier===5){assert.equal(C.train(s,p),false);break}
    while(p.level===tier)assert.ok(C.train(s,p));
    const reward=p.rewards[0];
    assert.equal(reward.tier,tier+1);assert.equal(new Set(reward.choices).size,3);
    assert.ok(reward.choices.every(k=>C.cards[k].cost===tier+1));
    const [pick,...rest]=reward.choices,stock=C.supply(s),gold=p.gold;
    assert.ok(C.claimReward(p,tier+1,pick));assert.equal(p.gold,gold);
    assert.equal(C.supply(s)[pick],stock[pick]);for(const k of rest)assert.equal(C.supply(s)[k],stock[k]+1);
    assert.equal(C.claimReward(p,tier+1,pick),false);
  }
});
test("A level-one camp fields six, returns and swaps pieces for free",()=>{
  const s=C.newGame(),p=s.seats[0];
  for(const [i,pos] of [0,2,3,4,5].entries()){assert.ok(C.buy(p,i));assert.ok(C.move(p,"b0","a"+pos))}
  assert.equal(p.board.filter(Boolean).length,6);assert.equal(p.level,1);assert.equal(p.gold,1);
  assert.ok(C.move(p,"a4","b1"));assert.ok(C.move(p,"b1","a4"));
  assert.ok(C.move(p,"a1","a0"));assert.equal(p.gold,1);
});
test("Natural upgrades preserve the locked shop, award once, and land growth is round-based",()=>{
  const s=C.newGame(),p=s.seats[0];C.prepare(s);
  const shop=clone(p.shop);
  for(let r=1;r<=2;r++){p.locked=true;assert.ok(C.battle(s));assert.ok(C.next(s))}
  assert.equal(s.round,3);assert.equal(p.level,2);assert.equal(p.rewards.length,1);
  assert.equal(p.lands.length,3);assert.deepEqual(clone(p.shop),shop);
  const saved=clone(s),offer=saved.seats[0].rewards[0];
  assert.ok(C.claimReward(saved.seats[0],offer.tier,offer.choices[0]));
  const high=C.newGame();high.seats[0].gold=99;
  while(high.seats[0].level<5)C.train(high,high.seats[0]);
  assert.equal(high.seats[0].lands.length,2);
});
test("A full bench can defer the exact reward or complete a merge without paying",()=>{
  const s=C.newGame(),p=s.seats[0];C.train(s,p);
  const r=p.rewards[0],key=r.choices[0];p.bench=Array.from({length:6},(_,i)=>unit("guard",50+i));
  assert.equal(C.claimReward(p,r.tier,key),false);assert.equal(p.rewards[0],r);
  p.bench[0]=unit(key,90);p.bench[1]=unit(key,91);const gold=p.gold;
  assert.ok(C.claimReward(p,r.tier,key));assert.ok(C.owned(p).some(u=>u.key===key&&u.star===2));assert.equal(gold,p.gold);
});
test("Spending colours affects only friendly pieces with that printed colour, once per colour",()=>{
  const me=seat("torch","bog","expert","guard","leaf","tide"),foe=seat("guard","guard",null,"leaf");
  const noPlans=D.run(me,foe);
  me.plans=[{spell:"spark",target:4},{spell:"spark",target:4},{spell:"undying",target:4}];
  const both=D.run(me,foe);
  assert.equal(both.frames[0].notes.filter(n=>n.includes("红色响应")).length,1);
  assert.match(both.frames[0].notes.find(n=>n.includes("红色响应")),/举炬鬼怪、火药巧匠/);
  assert.match(both.frames[0].notes.find(n=>n.includes("黑色响应")),/泥沼拾荒者、火药巧匠/);
  assert.ok(both.frames[1].notes.some(n=>n.includes("我方举炬鬼怪")&&n.includes("7 点伤害")));
  assert.ok(noPlans.frames[1].notes.some(n=>n.includes("我方举炬鬼怪")&&n.includes("5 点伤害")));
  const fragile=seat("bog");fragile.plans=[{spell:"undying",target:1}];
  const fatal=D.run(fragile,seat("warlord","warlord"));
  assert.equal(fatal.frames.flatMap(f=>f.notes).filter(n=>n.includes("触发黑色遗恨")).length,1);
  me.plans=[{spell:"blast",target:1}];assert.equal(D.run(me,foe).frames[0].notes.filter(n=>n.includes("色响应")).length,2);
  me.plans=[{spell:"ward",target:1},{spell:"growth",target:1},{spell:"frost",target:1}];
  const varied=D.run(me,foe),start=varied.frames[0].boards[0],base=noPlans.frames[0].boards[0];
  assert.equal(start[3].shield,base[3].shield+2);assert.equal(start[4].hp,base[4].hp+3);
  assert.ok(varied.frames[1].notes.some(n=>n.includes("银鳃巡游者使用蓝色潜袭")));
  assert.ok(!varied.frames[0].notes.some(n=>n.startsWith("敌方")&&n.includes("色响应")));
});
test("New tier skills resolve their own effects",()=>{
  const me=seat("banner","warlord",null,"sentry","aegis","guard");
  const foe=seat("skirk","skirk",null,"leaf","leaf");
  const res=D.run(me,foe);
  const start=res.frames[0].boards[0];
  assert.equal(start[0].attack,C.stats(me.board[0]).attack+1+2);
  assert.equal(start.find(u=>u.key==="sentry").shield,7);
  assert.ok(res.frames.flatMap(f=>f.notes).some(n=>n.includes("贯穿至")));
  const reborn=D.run(seat("raider"),seat("warlord","warlord"));
  assert.equal(reborn.frames.flatMap(f=>f.notes).filter(n=>n.includes("以 3 耐久复活")).length,1);
});
test("Four seats finish six rounds without overbooking the shared pool or battlefield",()=>{
  for(let game=0;game<30;game++){
    const s=C.newGame();C.prepare(s);
    for(let round=1;round<=6;round++){
      for(const p of s.seats){assert.equal(p.board.length,6);assert.equal(p.bench.length,6)}
      assert.ok(Object.values(C.supply(s)).every(n=>n>=0));
      assert.ok(C.battle(s));assert.ok(C.next(s));
    }
    assert.equal(s.stage,"end");assert.equal(s.results.length,6);
  }
});
