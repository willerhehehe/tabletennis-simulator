"""Speed-dependent inverted-rubber response. Model selection uses TRAIN-ONLY group CV.
Frozen author test splits are loaded only after the model family is selected/refit.
"""
from pathlib import Path
import json, hashlib
import numpy as np
from calibrate import ROOT, R, load, fit as fit_constant, predict, metrics
IDS=(1,2,3,4,9)
FAMILIES=('constant','normal-speed','normal-and-slip')

def inputs(a):
 v,w=a[:,8:11],a[:,2:5]
 slip=np.linalg.norm(v[:,:2]+R*np.column_stack((-w[:,1],w[:,0])),axis=1)
 return -v[:,2],slip

def parameters(a,model):
 n,s=inputs(a); n=np.clip(n,*model['normal_range']);s=np.clip(s,*model['slip_range'])
 z=(n-model['normal_center'])/model['normal_scale'];t=(s-model['slip_center'])/model['slip_scale']
 e=np.clip(model['e'][0]+model['e'][1]*z,0,1)
 b=np.clip(model['beta'][0]+model['beta'][1]*z+model['beta'][2]*t,-1,1)
 return e,model['mu'],b

def evaluate(a,model):
 return metrics(a,parameters(a,model))

def fit_model(a,family):
 n,s=inputs(a);base=fit_constant(a)
 model={'family':family,'normal_range':[float(n.min()),float(n.max())],'slip_range':[float(s.min()),float(s.max())],
        'normal_center':float(n.mean()),'normal_scale':max(float(n.std()),.1),
        'slip_center':float(s.mean()),'slip_scale':max(float(s.std()),.1),
        'e':[base[0],0.0],'mu':base[1],'beta':[base[2],0.0,0.0]}
 if family=='constant':return model
 z=(n-model['normal_center'])/model['normal_scale'];t=(s-model['slip_center'])/model['slip_scale']
 # Fit normal outgoing velocity directly, not the noisy ratio v_out/v_in.
 c=np.linalg.lstsq(n[:,None]*np.column_stack((np.ones(len(n)),z)),a[:,13],rcond=None)[0]
 model['e']=c.tolist()
 if family=='normal-speed':return model
 # Coordinate search on beta(n,s), with Coulomb cap. mu stays at its TRAIN-only constant fit.
 best=evaluate(a,model)['weighted_mse']
 for step in [.2,.08,.03,.01,.003,.001]:
  for sweep in range(40):
   improved=False
   for k in range(3):
    original=model['beta'][k]; chosen=original
    for delta in [-step,step]:
     model['beta'][k]=float(np.clip(original+delta,-2,2))
     loss=evaluate(a,model)['weighted_mse']
     if loss<best-1e-12:best=loss;chosen=model['beta'][k];improved=True
    model['beta'][k]=chosen
   if not improved:break
 return model

def folds(a):
 # Keep repeated records of the same angle + trajectory together.
 return np.array([int(hashlib.sha256(f'{row[0]:g}:{row[1]:g}'.encode()).hexdigest()[:8],16)%5 for row in a])

def run():
 out={'protocol':'5-fold training-only grouped CV (angle,traj_dir); choose simplest within 2% of minimum weighted MSE, then refit full train; author test split untouched for selection',
      'families':FAMILIES,'profiles':[],'source_commit':'159c6f040dd7832570b537b3b5fd00a97fc28c15','numpy_version':np.__version__}
 for rid in IDS:
  train=load('train',rid);assignment=folds(train);scores={}
  for family in FAMILIES:
   loss=0;count=0
   for f in range(5):
    tr,va=train[assignment!=f],train[assignment==f]
    m=fit_model(tr,family);loss+=evaluate(va,m)['weighted_mse']*len(va);count+=len(va)
   scores[family]=loss/count
  minimum=min(scores.values());selected=next(f for f in FAMILIES if scores[f]<=minimum*1.02)
  model=fit_model(train,selected)
  test=load('test',rid);constant=fit_constant(train)
  result={'id':rid,'train_count':len(train),'test_count':len(test),'cv_mse':scores,'model':model,
          'constant_test':metrics(test,constant),'response_test':evaluate(test,model)}
  out['profiles'].append(result)
  print(rid,selected,'CV',scores,'TEST',result['constant_test'],'->',result['response_test'],flush=True)
 (ROOT/'response-results.json').write_text(json.dumps(out,indent=2)+'\n')
if __name__=='__main__':run()
