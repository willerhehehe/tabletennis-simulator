import assert from 'node:assert/strict';
import { allocation,newGame,act,view,botAction,award,serverFor,shotPreview,lateBudget,techniqueOptions,contactGap,scenarioGame,publicReplay,SERVES } from './game.mjs';
import {playerPoint,bouncePoint,courtMarkup,aimSummary} from './court.mjs';
import {receivePlan,actionOrder} from './controls.mjs';
import {incomingFlight} from './motion.mjs';
const builds=[[2,2,2,2,2],[4,1,1,1,3]];
const make=()=>newGame({builds},()=>0);
const ready=(scenario='短下旋接发',hand='反手',wait=false)=>{const g=scenarioGame(scenario);act(g,{pos:{x:2,y:0},hand,guess:g.ball.hintSpin,wait});act(g,{pos:{x:2,y:0},hand,guess:g.receive.read});return g;};
assert.ok(allocation(builds[0]));for(const a of [[10,0,0,0,0],[1,2,2,2,2],[1.5,2.5,2,2,2]])assert.equal(allocation(a),false);
assert.throws(()=>newGame({builds:[[10,0,0,0,0],builds[1]]}));
assert.deepEqual([[0,0],[1,0],[1,1],[2,1],[2,2],[10,10],[11,10],[11,11]].map(s=>serverFor(0,s)),[0,0,1,1,0,0,1,0]);
assert.equal(serverFor(0,[4,4],5),0);assert.equal(serverFor(0,[5,4],5),1);
for(let y=0;y<3;y++)for(let x=0;x<5;x++){const n=playerPoint({x,y},true),f=playerPoint({x,y},false);assert.equal(n.x+f.x,600);assert.equal(n.y+f.y,720);}
assert.ok(bouncePoint({x:0,y:0},false).x>bouncePoint({x:4,y:0},false).x);
const short=ready(),options=()=>techniqueOptions(view(short));
for(const key of ['摆短','劈长','反手拧拉'])assert.ok(options().find(t=>t.key===key).available);
for(const key of ['攻球','前冲弧圈','高吊拉起','挡球','削球','扣杀','正手挑打'])assert.equal(options().find(t=>t.key===key).available,false,key);
assert.throws(()=>act(short,{technique:'前冲弧圈',target:{x:2,y:1}}));
assert.throws(()=>act(short,{technique:'摆短',target:{x:2,y:1}}));
act(short,{technique:'摆短',target:{x:2,y:0}},()=>0);
assert.equal(short.ball.spin,'下旋');assert.equal(short.ball.length,'短');assert.equal(short.ball.height,'低');assert.ok(short.ball.quality>=7);
const flick=ready();act(flick,{technique:'反手拧拉',target:{x:2,y:1}},()=>0);assert.equal(flick.ball.spin,'上旋');assert.equal(flick.ball.side,1);
const long=ready();act(long,{technique:'劈长',target:{x:2,y:0}},()=>0);assert.equal(long.ball.length,'长');assert.equal(long.ball.target.y,0); // 长度不是第一落点的别名
const wait=ready('短下旋接发','反手',true);assert.equal(techniqueOptions(view(wait)).find(t=>t.key==='摆短').available,false);
const earlyRead=scenarioGame('短下旋接发'),patientRead=structuredClone(earlyRead);
for(const g of [earlyRead,patientRead]){g.ball.hintSpin='上旋';g.ball.lateSpin='下旋';}
act(earlyRead,{pos:{x:2,y:0},hand:'反手',guess:'上旋',wait:false});
act(patientRead,{pos:{x:2,y:0},hand:'反手',guess:'上旋',wait:true});
assert.ok(shotPreview(patientRead,'反手拧拉').chance>shotPreview(earlyRead,'反手拧拉').chance); // 慢球且初始线索误导时，观察能挽救判断
assert.equal(lateBudget(patientRead,'反手'),2); // 等待放弃早期移动，不能在看清后补走全部预算
const fastWait=scenarioGame('长上旋近台防守');fastWait.ball.speed=8;fastWait.ball.target.x=0;fastWait.players[0].pos.x=3;
const fastEarly=structuredClone(fastWait);
act(fastWait,{pos:{x:3,y:0},hand:'反手',guess:'上旋',wait:true});
act(fastEarly,{pos:{x:1,y:0},hand:'反手',guess:'上旋',wait:false});
assert.equal(lateBudget(fastWait,'反手'),1);assert.throws(()=>act(fastWait,{pos:{x:1,y:0},hand:'反手'}));
act(fastWait,{pos:{x:2,y:0},hand:'反手'});act(fastEarly,{pos:{x:0,y:0},hand:'反手'});
assert.equal(shotPreview(fastWait,'挡球').chance,0);assert.ok(shotPreview(fastEarly,'挡球').chance>0); // 同一快球，提前押位置可接到，纯等待漏接
const block=ready('长上旋近台防守');assert.equal(contactGap(block,'挡球'),0);assert.ok(techniqueOptions(view(block)).find(t=>t.key==='挡球').available);
const heavy=structuredClone(block);block.ball.strength=1;heavy.ball.strength=6;
assert.ok(shotPreview(heavy,'挡球').chance<shotPreview(block,'挡球').chance);
assert.ok(shotPreview(heavy,'挡球').clean<shotPreview(block,'挡球').clean);
act(block,{technique:'挡球',target:{x:2,y:1}},()=>0);assert.equal(block.ball.spin,'上旋');
const high=ready('抓住冒高机会');assert.ok(techniqueOptions(view(high)).find(t=>t.key==='扣杀').available);
const aim=view(high);
assert.ok(aimSummary(aim,'攻球',{x:4,y:1}).includes('正手侧，横向距离 2 格'));
aim.positions[1].y=2;assert.ok(aimSummary(aim,'摆短',{x:0,y:0}).includes('对方已退台'));
const aimCourt=courtMarkup(aim,{mode:'targets',choice:{x:4,y:1},rows:[1],technique:'攻球'});
assert.equal((aimCourt.match(/data-kind="target"/g)||[]).length,5);assert.ok(aimCourt.includes('data-displacement'));
for(const hand of ['正手','反手']){
  const drive=ready('长上旋近台防守',hand);drive.ball.spin='不转';drive.ball.strength=0;drive.receive.read='不转';drive.receive.initialGuess='不转';
  assert.ok(techniqueOptions(view(drive)).find(t=>t.key==='攻球').available);
  const heavyUnder=structuredClone(drive);heavyUnder.ball.spin='下旋';heavyUnder.ball.strength=6;heavyUnder.receive.read='下旋';heavyUnder.receive.initialGuess='下旋';
  assert.ok(shotPreview(heavyUnder,'高吊拉起').chance>shotPreview(heavyUnder,'攻球').chance);
  assert.ok(shotPreview(drive,'攻球').chance>shotPreview(drive,'前冲弧圈').chance);
  act(drive,{technique:'攻球',target:{x:2,y:1}},()=>0);assert.equal(drive.ball.spin,'上旋');assert.ok(drive.ball.strength<=2);
}
const poor=ready(),expert=ready();poor.players[0].skills=[2,2,2,0,4];expert.players[0].skills=[1,1,1,5,2];
assert.ok(shotPreview(expert,'摆短').clean>shotPreview(poor,'摆短').clean); // 专长不能被成功率上限抹平
const wrong=ready();wrong.ball.spin='不转';wrong.ball.strength=0;act(wrong,{technique:'摆短',target:{x:2,y:0}},()=>0);
assert.equal(wrong.ball.height,'高');assert.equal(wrong.ball.length,'长');assert.equal(wrong.ball.quality,3);
const rescue=ready();rescue.players[0].pos={x:2,y:2};assert.equal(shotPreview(rescue,'救球').chance,0); // 远台不能隔空救短球
const uncertain=ready(),other=structuredClone(uncertain);other.ball.spin='上旋';other.ball.strength=5;
assert.deepEqual(view(uncertain),view(other));assert.deepEqual(techniqueOptions(view(uncertain)),techniqueOptions(view(other)));
assert.deepEqual(botAction(view(uncertain),()=>.4),botAction(view(other),()=>.4));
assert.deepEqual(receivePlan(view(uncertain),'反手拧拉'),receivePlan(view(other),'反手拧拉'));
assert.deepEqual(actionOrder(view(uncertain)),actionOrder(view(other)));
assert.notEqual(shotPreview(uncertain,'反手拧拉').chance,shotPreview(other,'反手拧拉').chance);
const combined=scenarioGame('短下旋接发');act(combined,{pos:{x:2,y:0},hand:'反手',guess:'下旋',wait:false});
const planView=view(combined),unchangedView=structuredClone(planView),plan=receivePlan(planView,'反手拧拉');
assert.deepEqual(planView,unchangedView);assert.ok(plan);assert.ok(Math.abs(plan.pos.x-planView.own.pos.x)+Math.abs(plan.pos.y-planView.own.pos.y)<=lateBudget(planView,plan.hand));
const snapshot=structuredClone(combined);
assert.throws(()=>act(combined,{action:'return',pos:{x:1,y:0},hand:'反手',guess:'下旋',technique:'摆短',target:{x:2,y:1}},()=>0));
assert.deepEqual(combined,snapshot); // 非法组合不能半途提交移动
act(combined,{action:'return',pos:{x:2,y:0},hand:'反手',guess:'下旋',technique:'摆短',target:{x:2,y:0}},()=>0);assert.equal(combined.phase,'predict');
assert.equal(publicReplay(combined).completedPoints.length,0);
award(combined,0,'测试');const exported=JSON.stringify(publicReplay(combined));assert.ok(!exported.includes('技术加成'));assert.ok(!exported.includes('hintSpin'));assert.ok(!exported.includes('skills'));
const hidden=scenarioGame('短下旋接发'),v=view(hidden);assert.equal(v.ball.target,undefined);assert.equal(v.ball.spin,undefined);assert.equal(v.players,undefined);assert.equal(v.pointShots,undefined);
const serveVariant=key=>{const g=make();act(g,{pos:{x:2,y:0}});act(g,{pos:{x:2,y:0}});act(g,{technique:key,target:{x:2,y:0}},()=>0);g.ball.hintSpin='下旋';g.ball.lateSpin='下旋';return g;};
const under=serveVariant('短下旋'),empty=serveVariant('短不转');
assert.deepEqual(view(under),view(empty)); // 历史、状态提示也不能泄露完整发球名称
assert.deepEqual(incomingFlight(view(under)),incomingFlight(view(empty)));
assert.equal(incomingFlight(view(under)).to.y,350); // 只飞到网前，不暴露终点
assert.ok(!JSON.stringify(publicReplay(empty)).includes('短不转'));
const recovery=make();act(recovery,{pos:{x:0,y:0}});act(recovery,{pos:{x:2,y:0}});act(recovery,{technique:'短下旋',target:{x:2,y:0}},()=>0);
assert.deepEqual(recovery.ball.origin,{x:0,y:0});assert.deepEqual(recovery.players[0].pos,{x:1,y:0});
act(recovery,{pos:{x:2,y:0},hand:'正手',guess:'下旋',wait:false});
assert.deepEqual(view(recovery).ball.origin,{x:0,y:0}); // 还原不能篡改球路起点
const exhausted=scenarioGame('长上旋近台防守');exhausted.ball.speed=8;exhausted.players[0].strain=2;
act(exhausted,{pos:{x:1,y:0},hand:'正手',guess:'上旋',wait:false});
assert.equal(lateBudget(exhausted,'反手'),-1);assert.equal(receivePlan(view(exhausted),'攻球','反手'),null);
assert.throws(()=>act(exhausted,{pos:{x:1,y:0},hand:'反手'})); // 零补救余量不能免费换手
// 百分骰逐点穷举：界面阈值必须等于实际正常 / 冒高 / 失误数量。
for(const technique of Object.keys(SERVES))for(const x of [0,2]){
  const base=make();act(base,{pos:{x:2,y:0}});act(base,{pos:{x:2,y:0}});
  const target={x,y:SERVES[technique].length==='短'?0:1},preview=shotPreview(base,technique,target);
  let success=0,clean=0;
  for(let roll=1;roll<=100;roll++){
    const sample=structuredClone(base);act(sample,{technique,target},()=> (roll-.5)/100);
    if(sample.phase==='predict'){success++;if(sample.ball.height!=='高')clean++;}
  }
  assert.equal(success,preview.chance,technique);assert.equal(clean,preview.clean,technique);
}
for(const technique of ['摆短','劈长','反手拧拉','救球']){
  const base=ready(),target={x:2,y:technique==='摆短'?0:1},preview=shotPreview(base,technique,target);
  let success=0,clean=0;
  for(let roll=1;roll<=100;roll++){const sample=structuredClone(base);act(sample,{technique,target},()=> (roll-.5)/100);if(sample.phase==='predict'){success++;if(sample.ball.height!=='高')clean++;}}
  assert.equal(success,preview.chance,technique);assert.equal(clean,preview.clean,technique);
}
assert.ok(courtMarkup(v,{mode:'positions',choice:v.own.pos,max:1}).includes('data-hint-region'));
v.own.skills[0]=99;assert.notEqual(hidden.players[0].skills[0],99);
assert.throws(()=>act(hidden,{pos:{x:0,y:2},hand:'正手',guess:'下旋',wait:false}));
act(hidden,{pos:{x:2,y:0},hand:'正手',guess:'下旋',wait:false});assert.equal(lateBudget(hidden,'反手'),lateBudget(hidden,'正手')-1);
const scoring=make();scoring.score=[10,10];award(scoring,0,'测试');assert.equal(scoring.phase,'pointEnd');act(scoring,{});assert.equal(scoring.server,1);award(scoring,0,'测试');assert.equal(scoring.phase,'setEnd');act(scoring,{});assert.deepEqual(scoring.score,[0,0]);assert.equal(scoring.server,1);scoring.score=[10,0];award(scoring,0,'测试');assert.equal(scoring.phase,'matchEnd');assert.throws(()=>act(scoring,{}));
const camera=make();camera.actor=1;award(camera,0,'对手漏接');assert.equal(view(camera).actor,0);assert.deepEqual(view(camera).own.skills,builds[0]);
let seed=7293;const rng=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/2**32);
let points=0,shots=0,loose=0;const techniques=new Set();
for(let i=0;i<40;i++){
  const g=newGame({builds,bestOf:i%2?3:5,target:i%2?5:11},rng);let steps=0;
  while(g.phase!=='matchEnd'&&steps++<15000){const a=botAction(view(g),rng);act(g,a,rng);if(g.ball){assert.ok(g.ball.speed>=1&&g.ball.strength>=0);assert.ok(g.ball.quality<=10);}}
  assert.equal(g.phase,'matchEnd');for(const p of g.completedPoints){points++;shots+=p.rally;for(const s of p.shots){techniques.add(s.technique);if(s.output?.height==='高')loose++;}}
}
console.log(JSON.stringify({result:'通过技术约束、旋转误判、机会球、信息隔离、坐标、计分与 40 场完整模拟',points,meanRally:+(shots/points).toFixed(2),loose,techniques:[...techniques]},null,2));
