import { playerPoint, bouncePoint } from './court.mjs';

// 只用角色已知的线索决定动画，不能把真实终点编码进预判动画。
export function incomingFlight(v) {
  if(v.phase!=='predict'||!v.ball)return null;
  const x=v.ball.hint.startsWith('左') ? 0.5 : v.ball.hint.startsWith('右') ? 3.5 : 2;
  return {from:playerPoint(v.ball.origin||v.positions[1-v.actor],false),to:{x:bouncePoint({x,y:0},true).x,y:350}};
}
const reduced=()=>window.matchMedia('(prefers-reduced-motion: reduce)').matches;
export async function movePawn(svg,actor,from,to) {
  if(!svg||reduced()||(from.x===to.x&&from.y===to.y))return;
  const pawn=svg.querySelector(`[data-player="${actor?'B':'A'}"]`);
  if(!pawn)return;
  const a=playerPoint(from,true),b=playerPoint(to,true),base={x:Number(pawn.dataset.boardX),y:Number(pawn.dataset.boardY)};
  const transform=p=>`translate(${p.x-base.x}px,${p.y-base.y}px)`;
  const animation=pawn.animate([{transform:transform(a)},{transform:transform({x:b.x,y:a.y})},{transform:transform(b)}],{duration:160,fill:'forwards',easing:'ease-out'});
  await animation.finished.catch(()=>{});
}
export async function flyBall(svg,flight,incoming=false) {
  if(!svg||!flight||reduced())return;
  const node=document.createElementNS('http://www.w3.org/2000/svg','circle');
  node.setAttribute('cx',flight.from.x);node.setAttribute('cy',flight.from.y);node.setAttribute('r','8');node.setAttribute('fill','#fff4ab');node.setAttribute('stroke','#a7760a');node.setAttribute('stroke-width','2');node.dataset.motionBall='true';
  const marker=svg.querySelector('[data-incoming-ball],[data-ball-point]');if(marker)marker.style.opacity='0';
  svg.append(node);
  const animation=node.animate([{transform:'translate(0px,0px)'},{transform:`translate(${flight.to.x-flight.from.x}px,${flight.to.y-flight.from.y}px)`}],{duration:incoming?180:260,fill:'forwards',easing:'ease-out'});
  try{await animation.finished.catch(()=>{});}finally{node.remove();if(marker)marker.style.opacity='';}
}
export async function showMiss(svg,position) {
  if(!svg||reduced())return;
  const p=playerPoint(position,true),node=document.createElementNS('http://www.w3.org/2000/svg','text');
  node.setAttribute('x',p.x);node.setAttribute('y',p.y-34);node.setAttribute('text-anchor','middle');node.setAttribute('fill','#b23620');node.setAttribute('font-size','26');node.textContent='×';svg.append(node);
  try{await node.animate([{opacity:0},{opacity:1},{opacity:0}],{duration:260}).finished.catch(()=>{});}finally{node.remove();}
}
