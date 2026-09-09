import { distance, spot, landing, TECH, SERVES } from './game.mjs';

// 当前操作方始终在下方；上方玩家自身的左右必须镜像。
export function playerPoint(p, near) {
  return { x: near ? 100 + p.x * 100 : 500 - p.x * 100, y: near ? 550 + p.y * 60 : 170 - p.y * 60 };
}
export function bouncePoint(p, near) {
  return { x: near ? 172 + p.x * 64 : 428 - p.x * 64, y: near ? 402 + p.y * 72 : 318 - p.y * 72 };
}
const lane=(x,opponentX)=>x<opponentX?'反手侧':x>opponentX?'正手侧':'身前';
export function aimSummary(v, technique, target) {
  const opponent=v.positions[1-v.actor],shot=v.ball?TECH[technique]:SERVES[technique];
  if(!shot)return '';
  return `${shot.length}球打向${lane(target.x,opponent.x)}，横向距离 ${Math.abs(target.x-opponent.x)} 格。`+
    (shot.length==='短'&&opponent.y>0?'对方已退台，短球可以调动前后位置。':shot.speed>=7?'快球会压缩对方移动时间。':'对方有较多移动时间，留意下一板的空当。')+
    (target.x===0||target.x===4?' 贴边落点也增加你的失误风险。':'');
}
export function courtMarkup(v, { mode, choice, targetChoice=choice, max = 0, hand = '正手', rows = [0,1], technique, instant=false } = {}) {
  const own = playerPoint(v.positions[v.actor], true), other = playerPoint(v.positions[1-v.actor], false);
  const label = n => n ? 'B' : 'A';
  let svg = '', buttons = '';
  const circle = (p,r,fill,stroke='none') => `<circle cx="${p.x}" cy="${p.y}" r="${r}" fill="${fill}" stroke="${stroke}" stroke-width="3"/>`;
  const line = (a,b,color,dashed=false) => `<path d="M ${a.x} ${a.y} L ${b.x} ${b.y}" stroke="${color}" stroke-width="3" fill="none" ${dashed?'stroke-dasharray="8 7"':''}/>`;
  for (const near of [false,true]) for(let y=0;y<3;y++) for(let x=0;x<5;x++) {
    const p={x,y}, c=playerPoint(p,near);
    svg+=`<rect x="${c.x-39}" y="${c.y-24}" width="78" height="48" rx="9" fill="${near?'#e0eae3':'#e8e7df'}" stroke="#c6d0c7"/>`;
    if(near && ['positions','return'].includes(mode)) {
      const active=distance(choice,p)===0, valid=distance(v.own.pos,p)<=max && (v.phase!=='serverPosition'||y===0);
      buttons+=`<button class="court-position ${active?'chosen':''}" style="left:${c.x/6}%;top:${c.y/7.2}%" data-kind="position" data-x="${x}" data-y="${y}" ${valid?'':'disabled'} aria-label="移动到${spot(p)}" aria-pressed="${active}" title="${spot(p)}${valid?'':' · 超出移动范围'}">${active?'◎':'·'}</button>`;
    }
  }
  for(const near of [false,true]) for(let y=0;y<3;y++) {
    svg+=`<text x="28" y="${playerPoint({x:0,y},near).y+5}" text-anchor="middle" fill="#66776b" font-size="13">${['近','中','远'][y]}</text>`;
  }
  svg+=`<rect x="138" y="204" width="324" height="312" rx="3" fill="#276b58" stroke="#f9fffa" stroke-width="4"/>
    <path d="M300 204V516" stroke="#e4f1e9" stroke-width="2"/>
    <path d="M138 360H462" stroke="#f7fff9" stroke-width="4"/>
    <rect x="126" y="355" width="348" height="10" fill="url(#net)" stroke="#e4eee7"/>
    <text x="491" y="365" fill="#61796b" font-size="13">球网</text>`;
  if(v.phase==='predict' && v.ball) {
    const xs=v.ball.hint.startsWith('左')?[0,1]:v.ball.hint.startsWith('右')?[3,4]:[2];
    const x=bouncePoint({x:xs[0],y:0},true).x-30;
    svg+=`<rect data-hint-region="true" x="${x}" y="372" width="${xs.length*64-4}" height="132" rx="4" fill="#eec561" fill-opacity=".35" stroke="#ffdc8a" stroke-width="2" stroke-dasharray="6 5"/>
      <text x="${x+(xs.length*64-4)/2}" y="443" text-anchor="middle" fill="#fff6cd" font-size="23">?</text>`;
    svg+=`<circle data-incoming-ball="true" cx="${x+(xs.length*64-4)/2}" cy="350" r="8" fill="#fff4ab" stroke="#a7760a" stroke-width="2"/>`;
  } else if(v.ball?.target && ['adjust','shot'].includes(v.phase)) {
    const bounce=bouncePoint(v.ball.target,true);
    const origin=v.ball.origin?playerPoint(v.ball.origin,false):other;
    svg+=line(origin,bounce,'#ffda82',true)+`<circle data-ball-point="true" cx="${bounce.x}" cy="${bounce.y}" r="8" fill="#fff7c7" stroke="#b67b17" stroke-width="3"/>`;
    if(distance(origin,other))svg+=circle(origin,5,'none','#b67b17');
    const depths=v.ball.length==='短'?[0]:TECH[technique]?.depths||[0,1,2];
    for(const y of depths){const ideal=playerPoint({x:v.ball.target.x,y},true);
      svg+=`<circle data-ideal="true" cx="${ideal.x}" cy="${ideal.y}" r="25" fill="none" stroke="#c68b21" stroke-width="3" stroke-dasharray="5 4"/>`;
    }
    const second={x:bounce.x,y:v.ball.length==='短'?495:v.ball.length==='半出台'?518:537};
    svg+=line(bounce,second,'#ffda82',true)+circle(second,4,'#d19b41');
    svg+=`<text x="${bounce.x+13}" y="${bounce.y+5}" fill="#fff3cf" font-size="14">${v.ball.height==='高'?'高球！':v.ball.length}</text>`;
  }
  if(['targets','return'].includes(mode)) {
    const c=bouncePoint(targetChoice,false),start=mode==='return'?playerPoint(choice,true):own;
    svg+=`<path data-plan="true" d="M${start.x} ${start.y}L${c.x} ${c.y}" stroke="#e5bfff" stroke-width="3" stroke-dasharray="8 7" fill="none"/><circle data-aim="true" cx="${c.x}" cy="${c.y}" r="11" fill="none" stroke="#e5bfff" stroke-width="3"/>`;
    const reach=playerPoint({x:targetChoice.x,y:v.positions[1-v.actor].y},false);
    svg+=`<path data-displacement="true" d="M${other.x} ${other.y-27}H${reach.x}" stroke="#b95535" stroke-width="3" stroke-dasharray="5 4"/>
      <path data-displacement-cap="true" d="M${reach.x} ${other.y-34}v14" stroke="#b95535" stroke-width="3"/>`;
    for(const y of rows) for(let x=0;x<5;x++) {
      const p={x,y}, b=bouncePoint(p,false), active=distance(targetChoice,p)===0;
      buttons+=`<button class="court-target ${!instant&&active?'chosen':''}" style="left:${b.x/6}%;top:${b.y/7.2}%" data-kind="target" data-x="${x}" data-y="${y}" aria-label="打向对方${landing(p)}" ${instant?'':`aria-pressed="${active}"`} title="${instant?'点击用'+technique+'出手 · ':''}对方${landing(p)}">${lane(x,v.positions[1-v.actor].x).replace('侧','')}</button>`;
    }
  }
  if(['positions','return'].includes(mode) && distance(choice,v.own.pos)) {
    const dest=playerPoint(choice,true), elbow={x:dest.x,y:own.y};
    svg+=line(own,elbow,'#aa7320',true)+line(elbow,dest,'#aa7320',true)+circle(dest,20,'#f6dfa0','#aa7320');
    svg+=`<text x="${dest.x}" y="${dest.y+5}" text-anchor="middle" font-size="12" fill="#66480e">预选</text>`;
  }
  for(const [id,p,color] of [[v.actor,own,'#245ac0'],[1-v.actor,other,'#b95535']]) {
    svg+=`<g data-player="${label(id)}" data-board-x="${p.x}" data-board-y="${p.y}">
      <ellipse cx="${p.x}" cy="${p.y+14}" rx="23" ry="9" fill="#233c2b" opacity=".1"/>
      <rect x="${p.x-20}" y="${p.y-9}" width="40" height="30" rx="13" fill="${color}" stroke="white" stroke-width="2"/>
      ${circle({x:p.x,y:p.y-12},10,color,'white')}
      <text x="${p.x}" y="${p.y+13}" text-anchor="middle" fill="white" font-weight="700" font-size="16">${label(id)}</text></g>`;
  }
  if(['predict','adjust','shot'].includes(v.phase)) {
    const x=own.x+(hand==='正手'?31:-31);
    svg+=`<path d="M${x} ${own.y}v16" stroke="#775433" stroke-width="5"/>${circle({x,y:own.y-3},9,'#d15b45','#fff1e8')}`;
  }
  return `<div class="court-title"><strong>俯视球台</strong><span>上方 ${label(1-v.actor)} · 下方 ${label(v.actor)} 当前操作</span></div>
    ${['targets','return'].includes(mode)?`<p class="aim-effect" role="status">${aimSummary(v,technique,targetChoice)}</p>`:''}
    <div class="court-board" aria-label="俯视球台，${label(v.actor)} 在下方，${label(1-v.actor)} 在上方">
    <svg viewBox="0 0 600 720" aria-label="双方实际站位：${label(v.actor)} ${spot(v.own.pos)}；${label(1-v.actor)} ${spot(v.positions[1-v.actor])}" role="img">
    <defs><pattern id="net" width="6" height="5" patternUnits="userSpaceOnUse"><path d="M0 0H6M0 0V5" stroke="#355548" stroke-width="1"/></pattern></defs>
    <rect width="600" height="720" rx="12" fill="#f0f3eb"/>
    <text x="300" y="20" text-anchor="middle" font-size="13" fill="#687265">${label(1-v.actor)} 的后场 · 面向下方</text>
    ${svg}<text x="300" y="708" text-anchor="middle" font-size="13" fill="#47654f">← 你的左侧　　面向球网　　你的右侧 →</text></svg>
    <div class="court-hitareas">${buttons}</div></div>
    <details class="court-legend"><summary>场地图例</summary><p class="court-caption">${mode==='return'?'点下方格子可改就位预览；点对方半台直接击球。紫线是击球计划，红线是横向距离。':mode==='positions'?'深色格可达，人物是实际位置，浅色影子是预览。':mode==='targets'?'选择方向；球长由技术决定。紫线是击球计划，红线标出横向距离。':'人物标记显示双方当前实际站位。'}${v.phase==='predict'?' 黄色区域仅是来球大致范围。':v.ball?.target&&['adjust','shot'].includes(v.phase)?' 黄色虚线示意未接球的第二跳；金圈是技术可用的接球区域。':''}</p></details>`;
}
