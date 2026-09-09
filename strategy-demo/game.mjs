// 反胶规则原型；技术关系见 RULES_RESEARCH.md，数值是待真人试玩标定的设计参数。
export const VERSION = '0.5.1';
export const RULES = { maxSkill: 5, skillBonus: 2, stretchCost: 11, wrongSpinCost: 13, cueAccuracy: .7, lateAccuracy: .86 };
export const skills = ['正手进攻', '正手控制', '反手进攻', '反手控制', '移动'];
export const spins = ['下旋', '不转', '上旋'];
export const lengths = ['短', '半出台', '长'];
export const clamp = (n,a,b) => Math.max(a,Math.min(b,n));
export const distance = (a,b) => Math.abs(a.x-b.x)+Math.abs(a.y-b.y);
export const spot = p => `${['左外','左','中','右','右外'][p.x]}·${['近台','中台','远台'][p.y]}`;
export const landing = p => `${['左外','左','中','右','右外'][p.x]}·${p.y===0?'近网落点':'底线落点'}`;
const need=(ok,msg)=>{if(!ok)throw Error(msg);};
const validPoint=(p,rows=3)=>p&&Number.isInteger(p.x)&&p.x>=0&&p.x<5&&Number.isInteger(p.y)&&p.y>=0&&p.y<rows;
export const TECH = {
  '摆短': {spin:'下旋',speed:2,strength:3,length:'短',depths:[0],chance:98,quality:8,control:true,description:'抢早控低控短，限制对方起板；等待后会错过早点。'},
  '劈长': {spin:'下旋',speed:5,strength:4,length:'长',depths:[0,1],chance:98,quality:8,control:true,description:'用深下旋逼退或追身；会给对方拉起长球的空间。'},
  '正手挑打': {spin:'上旋',speed:6,strength:2,length:'长',depths:[0],chance:91,quality:7,hand:'正手',description:'台内抢攻；低而重的下旋需要更高技术，不能当普通攻球。'},
  '反手拧拉': {spin:'上旋',side:1,speed:5,strength:4,length:'长',depths:[0],chance:93,quality:8,hand:'反手',description:'以摩擦把台内球转入侧上旋；跨区接球影响还原。'},
  '攻球': {spin:'上旋',speed:6,strength:1,length:'长',depths:[0,1],chance:96,quality:7,description:'以击打为主处理出台的不转、弱上旋球；回球较快但旋转较弱。低重下旋宜用拉起，台内低短球宜用挑打。'},
  '高吊拉起': {spin:'上旋',speed:4,strength:5,length:'长',depths:[0,1],chance:96,quality:8,strain:1,description:'长下旋的起板选择，弧线与摩擦换稳定性，速度较慢。'},
  '前冲弧圈': {spin:'上旋',speed:8,strength:4,length:'长',depths:[0,1,2],chance:90,quality:9,strain:1,description:'压缩对方移动时间；低重下旋、失位时风险更大。'},
  '挡球': {spin:'上旋',speed:4,strength:2,length:'长',depths:[0],chance:99,quality:7,control:true,description:'近台借力接上旋，保留控制；不能套用到低短下旋。'},
  '快带': {spin:'上旋',speed:7,strength:3,length:'长',depths:[0],chance:94,quality:8,description:'迎前借力抢上升期，回球快；等待容易错过窗口。'},
  '削球': {spin:'下旋',speed:3,strength:5,length:'长',depths:[1,2],chance:98,quality:8,control:true,description:'中远台切削改变旋转节奏；要留出空间，也暴露短球。'},
  '扣杀': {spin:'不转',speed:9,strength:0,length:'长',depths:[0,1],chance:98,quality:9,strain:1,description:'利用高球进攻窗口；只有明显高球才可用。'},
  '救球': {spin:'不转',speed:2,strength:0,length:'长',depths:[0,1,2],chance:97,quality:3,control:true,description:'优先把够得到的球救回，回球较高，给对手进攻机会。'}
};
export const techniques=Object.keys(TECH);
export const SERVES = {
  '短下旋': {spin:'下旋',speed:2,strength:4,length:'短',quality:8},
  '短不转': {spin:'不转',speed:2,strength:0,length:'短',quality:8},
  '侧下短球': {spin:'下旋',side:1,speed:2,strength:3,length:'短',quality:8},
  '半出台下旋': {spin:'下旋',speed:4,strength:4,length:'半出台',quality:8},
  '急长上旋': {spin:'上旋',speed:8,strength:3,length:'长',quality:8},
  '急长不转': {spin:'不转',speed:8,strength:0,length:'长',quality:8}
};
export function allocation(a){return Array.isArray(a)&&a.length===5&&a.every(n=>Number.isInteger(n)&&n>=0&&n<=5)&&a.reduce((s,n)=>s+n,0)===10;}
export function serverFor(first,score,target=11){const deuce=(target-1)*2,total=score[0]+score[1];return (first+(score.every(n=>n>=target-1)?Math.floor(deuce/2)+total-deuce:Math.floor(total/2)))%2;}
export function newGame({builds,bot=true,bestOf=3,target=11,hand='左'},rng=Math.random){
  need(Array.isArray(builds)&&builds.length===2&&builds.every(allocation),'每人恰好 10 点，每项 0–5 点。');
  need([3,5].includes(bestOf)&&[5,11].includes(target)&&['左','右'].includes(hand),'比赛设置无效。');
  const hiddenHand=rng()<.5?'左':'右',first=hiddenHand===hand?0:1;
  return {players:builds.map(a=>({skills:[...a],pos:{x:2,y:0},strain:0})),bot,bestOf,target,first,server:first,actor:first,
    score:[0,0],sets:[0,0],phase:'serverPosition',ball:null,receive:null,rally:0,winner:null,result:'',feedback:null,
    scouting:[{},{}],lastActions:[null,null],pointShots:[],history:[`猜手：藏在${hiddenHand}手，${first?'B':'A'}先发球。`],completedPoints:[],scenario:null};
}
const own=s=>s.players?s.players[s.actor]:s.own;
export function movement(s){return Math.max(1,3+Math.floor(own(s).skills[4]/2)-(s.ball?.speed>=7?1:0)-(own(s).strain||0));}
export function lateBudget(s,hand){
  // 已经飞近的球不能再用完所有剩余步数；提前移动必须有不可替代的作用。
  const window=s.ball.speed>=7?1:2;
  return Math.min(window,movement(s)-s.receive.used)-(hand!==s.receive.hand?1:0);
}
export function contactGap(s,key,pos=own(s).pos){
  const t=TECH[key],b=s.ball;
  const depth=(b.length==='短'?[0]:t.depths).reduce((best,y)=>Math.min(best,Math.abs(pos.y-y)),3);
  return Math.abs(pos.x-b.target.x)+depth;
}
// 技术菜单只看公开长度、球高、自己的准备和自己的旋转判断，不看隐藏真值。
export function techniqueOptions(s){
  const b=s.ball,r=s.receive;
  return techniques.map(key=>{
    const t=TECH[key];let reason='';
    if(t.hand&&t.hand!==r.hand)reason=`需要${t.hand}`;
    else if(['摆短','正手挑打','反手拧拉'].includes(key)&&b.length==='长')reason='球已长出台，使用长球技术';
    else if(['高吊拉起','前冲弧圈','挡球','快带','削球'].includes(key)&&b.length==='短')reason='台内短球没有此动作的空间';
    else if(key==='攻球'&&b.length==='短'&&b.height!=='高')reason='台内低短球请用挑打或拧拉';
    else if(['挡球','快带'].includes(key)&&r.read==='下旋')reason='按当前下旋判断，应选搓、拉或削';
    else if(key==='摆短'&&r.wait)reason='等待已错过抢早摆短窗口';
    else if(key==='快带'&&r.wait)reason='等待已错过快带上升期';
    else if(key==='扣杀'&&b.height!=='高')reason='需要明显冒高的球';
    return {key,...t,available:!reason,reason};
  });
}
export function targetRows(s,key){return (s.ball?TECH[key]:SERVES[key])?.length==='短'?[0]:[0,1];}
export function shotPreview(s,key,target={x:2,y:1}){
  if(!s.ball){
    const serve=SERVES[key],edge=target.x===0||target.x===4;
    return {chance:edge?95:98,clean:edge?88:92,quality:serve?.quality||8,gap:0,wrong:false,
      reasons:[edge?'贴边发球：接回率 -3，正常质量率 -4':'发球也可能失误；擦边档会降低控制余量']};
  }
  const t=TECH[key],p=own(s),r=s.receive,b=s.ball;
  need(t,'未知技术');
  const actual=b.spin??r.read,wrong=actual!==r.read,gap=contactGap(s,key);
  const skill=p.skills[(r.hand==='反手'?2:0)+(t.control?1:0)];
  let penalty=0;const reasons=[];
  if(gap){penalty+=gap*RULES.stretchCost;reasons.push(`到位差 ${gap} 格${gap>1?'，够不到球':'，只能勉强处理'}`);}
  const cross=r.hand==='正手'?b.target.x<p.pos.x:b.target.x>p.pos.x;
  if(cross){penalty+=5;reasons.push('跨身体击球，准备空间受限');}
  if(wrong){penalty+=RULES.wrongSpinCost;reasons.push(`旋转误读：实际${actual}`);}
  if(r.initialGuess!==r.read&&!r.wait){penalty+=3;reasons.push('临时改判，调整动作');}
  if(actual==='下旋'&&['正手挑打','攻球','前冲弧圈','挡球','快带'].includes(key)){
    const cost=b.strength*(['正手挑打','攻球'].includes(key)?3:key==='前冲弧圈'?2:5);penalty+=cost;reasons.push(`处理下旋额外难度 ${cost}`);
  }
  if(actual==='下旋'&&['高吊拉起','反手拧拉'].includes(key)){
    const cost=Math.max(0,b.strength-2)*1.5;penalty+=cost;reasons.push(`重下旋需要更多摩擦提拉 -${cost}`);
  }
  if(actual==='上旋'&&['挡球','快带','攻球','前冲弧圈','削球'].includes(key)){
    // 教练资料支持强旋转需要更精确拍型；系数仅是桌游标定值。
    const cost=Math.max(0,b.strength-2)*(key==='挡球'?2:1.5);
    penalty+=cost;if(cost)reasons.push(`上旋强度 ${b.strength}，拍型控制压力 -${cost}`);
  }
  if(actual==='上旋'&&['摆短','劈长'].includes(key)){penalty+=10;reasons.push('用搓接处理上旋，控低困难');}
  if(b.side){penalty+=Math.max(0,4-skill);reasons.push('侧旋需要调整拍型和方向');}
  const pressure=Math.max(0,b.speed-4)*2+(b.height==='低'?2:0);
  penalty+=pressure+(target.x===0||target.x===4?3:0);
  if(r.wait)reasons.push('等待换取更清楚的线索，但放弃提前移动及抢早窗口');
  if(b.height==='高'&&['正手挑打','反手拧拉','攻球','前冲弧圈','扣杀'].includes(key)){penalty-=10;reasons.push('来球冒高，进攻窗口');}
  const chance=gap>1?0:clamp(Math.round(t.chance+skill*RULES.skillBonus-penalty),8,99);
  const quality=clamp(t.quality-1+Math.floor(skill/2)-gap-(cross?1:0),2,10);
  const forcedHigh=key==='救球'||(r.read==='下旋'&&actual==='不转'&&['摆短','劈长'].includes(key));
  const clean=forcedHigh?0:clamp(Math.round(t.chance-22+skill*3-penalty*1.15),0,chance);
  reasons.push(`技术加成 +${skill*RULES.skillBonus}；不是每次失手都直接丢分`);
  return {chance,clean,quality,gap,wrong,cross,reasons};
}
export const receiveChance=shotPreview;
const cue=(spin,accuracy,rng)=>rng()<accuracy?spin:spins[(spins.indexOf(spin)+1+Math.floor(rng()*2))%3];
function makeBall(profile,target,from,rng){
  return {...profile,target:{...target},from,height:profile.height||'低',side:profile.side||0,
    hintSpin:cue(profile.spin,RULES.cueAccuracy,rng),lateSpin:cue(profile.spin,RULES.lateAccuracy,rng)};
}
export function award(g,winner,reason){
  g.score[winner]++;g.result=`${winner?'B':'A'}得分：${reason}。本分 ${g.rally} 板。`;
  g.history.push(g.result,`比分 ${g.score.join(' : ')}`);g.phase='pointEnd';
  g.completedPoints.push({winner,reason,shots:structuredClone(g.pointShots),rally:g.rally});
  if(g.score[winner]>=g.target&&g.score[winner]-g.score[1-winner]>=2){
    g.sets[winner]++;g.phase='setEnd';g.result+=` 局数 ${g.sets.join(' : ')}。`;
    if(g.sets[winner]>=Math.ceil(g.bestOf/2)){g.phase='matchEnd';g.winner=winner;}
  }
}
export function act(g,a,rng=Math.random){
  // 同一画面完成补救与回球；先在副本验证，非法目标不会消耗移动或改变状态。
  if(g.phase==='adjust'&&a.action==='return'){
    const next=structuredClone(g);act(next,{pos:a.pos,hand:a.hand,guess:a.guess},rng);act(next,{technique:a.technique,target:a.target,recover:a.recover},rng);Object.assign(g,next);return;
  }
  const p=g.players[g.actor];
  switch(g.phase){
    case 'serverPosition':case 'receiverPosition':{
      need(validPoint(a.pos),'无效站位');if(g.phase==='serverPosition')need(a.pos.y===0,'发球请选近台');
      p.pos={...a.pos};g.history.push(`${g.actor?'B':'A'}站位：${spot(a.pos)}`);
      if(g.phase==='serverPosition'){g.actor=1-g.server;g.phase='receiverPosition';}else{g.actor=g.server;g.phase='shot';}break;
    }
    case 'predict':{
      need(validPoint(a.pos)&&['正手','反手'].includes(a.hand)&&spins.includes(a.guess)&&typeof a.wait==='boolean','无效预判');
      const used=distance(p.pos,a.pos);need(a.wait?used===0:used<=Math.ceil(movement(g)/2),'预判移动超预算');
      g.receive={used,wait:a.wait,hand:a.hand,initialGuess:a.guess,read:a.wait?g.ball.lateSpin:a.guess};
      p.pos={...a.pos};g.phase='adjust';
      g.history.push(`${g.actor?'B':'A'}${a.wait?'等待观察':'提前移动 '+used+' 格'}；长度已明确为${g.ball.length}，旋转仍需判断。`);break;
    }
    case 'adjust':{
      need(validPoint(a.pos)&&['正手','反手'].includes(a.hand)&&spins.includes(a.guess??g.receive.read),'无效调整');
      need(distance(p.pos,a.pos)<=lateBudget(g,a.hand),'超过补救预算');
      p.pos={...a.pos};g.receive.hand=a.hand;g.receive.read=a.guess??g.receive.read;g.phase='shot';break;
    }
    case 'shot':{
      const serve=!g.ball,key=a.technique;
      need(validPoint(a.target,2),'无效落点');
      need(serve?Object.hasOwn(SERVES,key):techniqueOptions(view(g)).some(t=>t.key===key&&t.available),'此技术不适用于当前准备或来球条件');
      need(targetRows(g,key).includes(a.target.y),'目标长度与技术不符');
      if(serve&&SERVES[key].length==='短')need(a.target.y===0,'短发球请选择近网落点');
      const e=shotPreview(g,key,a.target),roll=Math.floor(rng()*100)+1;
      const hand=serve?'发球':g.receive.hand;
      if(!serve){const observation=g.scouting[g.actor][hand]??={attempts:0,success:0,loose:0};observation.attempts++;if(roll<=e.chance)observation.success++;}
      const note={actor:g.actor,technique:key,hand,roll,success:roll<=e.chance,reason:e.reasons.join('；')};
      g.pointShots.push(note);
      // 只在接触后公开上一板实际旋转，不把秘密数值放进预测界面。
      if(!serve)g.history.push(`接触后确认上一板${g.ball.technique}：${g.ball.spin}${g.ball.side?'带侧旋':''}，强度 ${g.ball.strength}。`);
      if(roll>e.chance){
        const reason=e.gap>1?'失位漏接':e.wrong&&g.ball?.spin==='下旋'?'低估下旋，回球下网':e.wrong?'旋转误判，回球出界':`${key}执行失误`;
        g.feedback={actor:g.actor,headline:reason,detail:e.reasons.join('；'),roll,chance:e.chance,clean:e.clean};
        g.lastActions[g.actor]={actor:g.actor,headline:reason,roll};
        award(g,1-g.actor,reason);break;
      }
      let profile={...(serve?SERVES[key]:TECH[key])},outputTarget={...a.target};
      if(!serve&&profile.strength){const skill=p.skills[(hand==='反手'?2:0)+(profile.control?1:0)];profile.strength=clamp(profile.strength+Math.floor(skill/2)-1,1,6);}
      const pop=!serve&&g.receive.read==='下旋'&&g.ball.spin==='不转'&&['摆短','劈长'].includes(key);
      const loose=roll>e.clean||pop||key==='救球';
      profile.quality=e.quality;
      if(loose){profile={...profile,height:'高',speed:Math.max(2,profile.speed-2),strength:Math.max(0,profile.strength-2),quality:3,length:'长'};outputTarget.y=1;if(!serve)g.scouting[g.actor][hand].loose++;}
      const origin={...p.pos};
      p.strain=(profile.strain||0)+(e.cross?1:0);
      if(a.recover!==false){if(p.pos.x!==2)p.pos.x+=Math.sign(2-p.pos.x);}
      g.feedback={actor:g.actor,headline:loose?(pop?'把不转当下旋，搓接冒高':'球已上台，但送出高球机会'):'正常回球',detail:e.reasons.join('；'),roll,chance:e.chance,clean:e.clean};
      g.lastActions[g.actor]={actor:g.actor,headline:`${key}：${g.feedback.headline}`,roll};
      g.history.push(`${g.actor?'B':'A'} 已击球，等待接球方判断（骰 ${roll}）。`);
      // 输出只保留球的状态，不能夹带技术模板中的内部配置。
      g.ball=makeBall({origin,spin:profile.spin,side:profile.side||0,speed:profile.speed,strength:profile.strength,length:profile.length,height:profile.height||'低',quality:profile.quality,technique:key},outputTarget,g.actor,rng);
      note.output={...g.ball};delete note.output.hintSpin;delete note.output.lateSpin;
      g.rally++;g.actor=1-g.actor;g.receive=null;g.phase='predict';break;
    }
    case 'pointEnd':case 'setEnd':{
      if(g.phase==='setEnd'){g.score=[0,0];g.first=1-g.first;}
      g.server=serverFor(g.first,g.score,g.target);g.actor=g.server;g.phase='serverPosition';g.ball=null;g.receive=null;g.rally=0;g.pointShots=[];g.feedback=null;g.lastActions=[null,null];
      g.players.forEach(p=>p.strain=0);break;
    }
    default:throw Error('当前不能执行动作');
  }
}
export function view(g){
  // 白名单投影：未完成的一板、对手技能、真实旋转和掷骰阈值均不传给 UI / AI。
  const b=g.ball,r=g.receive;
  const actor=g.bot&&['pointEnd','setEnd','matchEnd'].includes(g.phase)?0:g.actor;
  const ball=!b?null:g.phase==='predict'?{hint:`${b.target.x<2?'左侧两列':b.target.x>2?'右侧两列':'中路'}，长短未明；动作偏${b.hintSpin}`,hintSpin:b.hintSpin,speed:b.speed,origin:b.origin&&{...b.origin}}:
    {target:{...b.target},origin:b.origin&&{...b.origin},length:b.length,height:b.height,speed:b.speed,strength:3,side:b.side,hintSpin:r?.wait?b.lateSpin:b.hintSpin,quality:b.quality};
  const concealed=b&&['predict','adjust','shot'].includes(g.phase);
  const lastActions=g.lastActions.map((a,i)=>a&&{...a,...(concealed&&i!==actor?{headline:'已击球，请根据动作线索判断'}:{})});
  return {bot:g.bot,bestOf:g.bestOf,target:g.target,first:g.first,server:g.server,actor,score:[...g.score],sets:[...g.sets],phase:g.phase,
    own:structuredClone(g.players[actor]),positions:g.players.map(p=>({...p.pos})),receive:r&&{...r},ball,rally:g.rally,winner:g.winner,
    result:g.result,history:[...g.history],scouting:structuredClone(g.scouting),lastActions,scenario:g.scenario,
    feedback:g.feedback&&{actor:g.feedback.actor,headline:concealed&&g.feedback.actor!==actor?'已击球，请根据线索判断':g.feedback.headline,...(g.feedback.actor===actor?g.feedback:{})}};
}
export function publicReplay(g){
  return {version:VERSION,settings:{bot:g.bot,target:g.target,bestOf:g.bestOf},score:[...g.score],sets:[...g.sets],history:[...g.history],
    completedPoints:g.completedPoints.map(p=>({...p,shots:p.shots.map(({reason,...shot})=>structuredClone(shot))})),
    note:'只含公开动作、已结束回合；排除隐藏属性、判定阈值和未完成的秘密击球方案。'};
}
export function botAction(v,rng=Math.random,style='balanced'){
  const choose=xs=>xs[Math.floor(rng()*xs.length)];
  if(v.phase==='serverPosition')return {pos:{x:choose([1,2,3]),y:0}};
  if(v.phase==='receiverPosition')return {pos:{x:2,y:0}};
  if(v.phase==='predict'){
    const wait=style==='wait'||(style!=='early'&&rng()<.25),pos={...v.own.pos};
    const x=v.ball.hint.startsWith('左')?1:v.ball.hint.startsWith('右')?3:2;
    if(!wait)for(let i=0;i<Math.ceil(movement(v)/2);i++)if(pos.x!==x)pos.x+=Math.sign(x-pos.x);else if(pos.y>0)pos.y--;
    return {pos,wait,guess:v.ball.hintSpin,hand:x<pos.x?'反手':choose(['正手','反手'])};
  }
  if(v.phase==='adjust'){
    let best;
    for(const hand of ['正手','反手'])for(let y=0;y<3;y++)for(let x=0;x<5;x++){
      const pos={x,y};if(distance(pos,v.own.pos)>lateBudget(v,hand))continue;
      const trial={...v,own:{...v.own,pos},receive:{...v.receive,hand}};
      const value=Math.max(...techniqueOptions(trial).filter(t=>t.available).map(t=>shotUtility(trial,t.key,style)))-distance(pos,v.own.pos)*.3;
      if(!best||value>best.value)best={value,pos,hand,guess:v.receive.read};
    }
    return best;
  }
  if(v.phase==='shot'){
    const technique=!v.ball?choose(Object.keys(SERVES)):techniqueOptions(v).filter(t=>t.available).map(t=>({key:t.key,value:shotUtility(v,t.key,style)+rng()*5})).sort((a,b)=>b.value-a.value)[0].key;
    const opp=v.positions[1-v.actor],read=v.scouting[1-v.actor];
    const weaknesses=Object.fromEntries(['正手','反手'].map(h=>[h,read[h]?.attempts>=3?(read[h].attempts-read[h].success+read[h].loose*.5)/read[h].attempts:0]));
    const x=rng()<.2?choose([0,1,2,3,4]):[0,1,2,3,4].map(x=>({x,value:Math.abs(x-opp.x)+(x===0||x===4?-.7:0)+weaknesses[x<opp.x?'反手':'正手']*2+rng()})).sort((a,b)=>b.value-a.value)[0].x;
    const profile=v.ball?TECH[technique]:SERVES[technique];
    return {technique,target:{x,y:profile.length==='短'?0:1},recover:true};
  }
  return {};
}
function shotUtility(v,key,style){
  const e=shotPreview(v,key),t=TECH[key];
  const pressure=t.speed*.65+t.strength*.3+(t.length==='短'?3:0)+(v.ball.height==='高'&&key==='扣杀'?4:0);
  return e.chance*.55+e.clean*.2+(style==='safe'?0:pressure*(style==='attack'?2:1.3))- (key==='救球'?10:0);
}
export const SCENARIOS={
  '短下旋接发':{description:'试试摆短、劈长和拧拉：它们怎样改变下一板？',ball:{spin:'下旋',strength:4,speed:2,length:'短',target:{x:2,y:0},height:'低'}},
  '长上旋近台防守':{description:'留在近台可挡、可快带；退到中远台可尝试削球。',ball:{spin:'上旋',strength:4,speed:6,length:'长',target:{x:2,y:1},height:'低'}},
  '抓住冒高机会':{description:'这是一板高球，比较扣杀与普通回球的收益。',ball:{spin:'不转',strength:0,speed:2,length:'长',target:{x:2,y:1},height:'高'}}
};
export function scenarioGame(key){
  need(Object.hasOwn(SCENARIOS,key),'未知练习');const g=newGame({builds:[[2,2,2,2,2],[2,2,2,2,2]]},()=>0);
  g.ball=makeBall({...SCENARIOS[key].ball,quality:8,technique:'练习来球'},SCENARIOS[key].ball.target,1,()=>0);
  g.phase='predict';g.actor=0;g.server=1;g.first=1;g.rally=1;g.scenario=key;g.history=[`练习：${key}。${SCENARIOS[key].description}`];return g;
}
