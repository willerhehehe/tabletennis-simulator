import { TECH, distance, lateBudget, shotPreview } from './game.mjs';

// 仅替玩家规划机械就位，不替玩家选择战术；输入必须是 view() 的角色视图。
export function receivePlan(v, technique, hand = v.receive.hand) {
  hand = TECH[technique].hand || hand;
  if (v.phase !== 'adjust') return hand === v.receive.hand ? { pos: {...v.own.pos}, hand } : null;
  const budget = lateBudget(v, hand);
  let best = null;
  for (let y=0;y<3;y++) for (let x=0;x<5;x++) {
    const pos={x,y}, steps=distance(v.own.pos,pos);
    if(steps>budget)continue;
    const trial={...v,own:{...v.own,pos},receive:{...v.receive,hand}};
    const e=shotPreview(trial,technique), value=e.chance*10+e.clean-steps*.1;
    if(!best||value>best.value)best={pos,hand,value};
  }
  return best&&{pos:best.pos,hand:best.hand};
}

export function actionOrder(v) {
  if(v.ball.height==='高')return ['扣杀','攻球','前冲弧圈','高吊拉起'];
  if(v.ball.length!=='长')return ['摆短','劈长','正手挑打','反手拧拉'];
  if(v.receive.read==='下旋')return ['高吊拉起','劈长','前冲弧圈','削球'];
  return ['攻球','挡球','快带','前冲弧圈'];
}
