// 运行 node balance.mjs > balance-results.json；这是策略探针，不是真人趣味性证明。
import {newGame,act,view,botAction,VERSION} from './game.mjs';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
const styles=['balanced','safe','attack','early','wait'];
const names={balanced:'混合',safe:'只求稳',attack:'偏进攻',early:'总预判',wait:'总等待'};
const perPair=20, matrix={},usage={},decisionCounts={},rallies=[];let points=0,shots=0,loose=0,maxRally=0,firstWins=0,matches=0;
for(const s of styles){matrix[names[s]]={};usage[names[s]]={};decisionCounts[names[s]]={early:0,wait:0};}
for(let a=0;a<styles.length;a++)for(let b=a+1;b<styles.length;b++){
 let wins=0;
 for(let n=0;n<perPair;n++){
  let seed=19273+n*37+a*1337+b*9001;
  const rng=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/2**32);
  const g=newGame({builds:[[2,2,2,2,2],[2,2,2,2,2]],target:5,bestOf:3},rng),first=g.first;let steps=0;
  while(g.phase!=='matchEnd'&&steps++<20000){
   const style=g.actor===n%2?styles[a]:styles[b],action=botAction(view(g),rng,style);
   if(g.phase==='predict')decisionCounts[names[style]][action.wait?'wait':'early']++;
   if(g.phase==='shot')usage[names[style]][action.technique]=(usage[names[style]][action.technique]||0)+1;
   act(g,action,rng);
  }
  if(g.phase!=='matchEnd')throw Error('对局未结束');
  wins+=g.winner===n%2?1:0;matches++;firstWins+=g.winner===first?1:0;
  for(const p of g.completedPoints){points++;shots+=p.rally;rallies.push(p.rally);maxRally=Math.max(maxRally,p.rally);for(const s of p.shots)if(s.output?.height==='高')loose++;}
 }
 matrix[names[styles[a]]][names[styles[b]]]=`${wins}/${perPair}`;
 matrix[names[styles[b]]][names[styles[a]]]=`${perPair-wins}/${perPair}`;
}
rallies.sort((a,b)=>a-b);
console.log(JSON.stringify({version:VERSION,engineSha256:createHash('sha256').update(readFileSync(new URL('./game.mjs',import.meta.url))).digest('hex'),perPair,matches,points,meanRally:+(shots/points).toFixed(2),medianRally:rallies[Math.floor(rallies.length*.5)],p90Rally:rallies[Math.floor(rallies.length*.9)],over30Rallies:rallies.filter(n=>n>30).length,maxRally,loose,firstServeMatchWins:`${firstWins}/${matches}`,winMatrix:matrix,decisions:decisionCounts,techniqueUsage:usage,limits:'固定参数、均衡属性、五种启发式策略、每组20场；不证明已平衡或真人觉得好玩。'},null,2));
