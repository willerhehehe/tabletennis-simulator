"""Fit the existing e/mu/beta impulse model on fixed training CSVs; test once.
Requires NumPy. No author model code is executed. Units: m/s and rad/s.
"""
from pathlib import Path
import json, hashlib, csv
import numpy as np

ROOT = Path(__file__).resolve().parent
R = .02
BASELINE = (.8, .8, .15)

def load(split, rid):
    path = ROOT / 'source/data' / split / f'racket_{rid:02d}.csv'
    a = np.genfromtxt(path, delimiter=',', skip_header=1)
    assert a.shape[1] == 14 and np.isfinite(a).all()
    assert (a[:, 10] < 0).all(), 'CSV must already be in racket frame, approaching +Z normal'
    return a

def predict(a, params):
    e, mu, beta = params
    v, w = a[:, 8:11], a[:, 2:5]
    # n=(0,0,1), contact arm=(0,0,-R); source preprocessing applies no extra racket_angle rotation.
    tangent = np.column_stack((v[:,0] - R*w[:,1], v[:,1] + R*w[:,0]))
    slip = np.linalg.norm(tangent, axis=1)
    jn_per_mass = -(1+e)*v[:,2]
    jt_per_mass = np.minimum((1+beta)*slip/2.5, mu*jn_per_mass)
    dv = -tangent * (jt_per_mass/np.maximum(slip,1e-12))[:,None]
    out_v = v.copy(); out_v[:,:2] += dv; out_v[:,2] = -e*v[:,2]
    out_w = w.copy(); out_w[:,0] += 1.5/R*dv[:,1]; out_w[:,1] -= 1.5/R*dv[:,0]
    return out_v, out_w

def loss(a, p):
    v, w = predict(a, p)
    return float(np.mean(np.sum((v[:,[0,2]]-a[:,[11,13]])**2,axis=1) + (R*(w[:,1]-a[:,6]))**2))

def fit(a):
    e = float(np.clip(-np.dot(a[:,10],a[:,13])/np.dot(a[:,10],a[:,10]),0,1))
    mu, beta, best = .8, .15, float('inf')
    low_mu, high_mu, low_b, high_b = 0, 1.5, -1, 1
    for _ in range(4):
        for candidate_mu in np.linspace(low_mu, high_mu, 25):
            for candidate_b in np.linspace(low_b, high_b, 25):
                value = loss(a, (e,candidate_mu,candidate_b))
                if value < best: mu,beta,best = float(candidate_mu),float(candidate_b),value
        dm, db = (high_mu-low_mu)/24, (high_b-low_b)/24
        low_mu,high_mu=max(0,mu-dm),min(1.5,mu+dm)
        low_b,high_b=max(-1,beta-db),min(1,beta+db)
    return (e,mu,beta)

def metrics(a,p):
    v,w=predict(a,p)
    ev=np.linalg.norm(v[:,[0,2]]-a[:,[11,13]],axis=1)
    ew=np.abs(w[:,1]-a[:,6])/(2*np.pi)
    return {'velocity_mae_m_s':float(ev.mean()),'spin_mae_rps':float(ew.mean()),'weighted_mse':loss(a,p)}

def run():
    rows=[]
    for rid in range(1,11):
        train,test=load('train',rid),load('test',rid)
        params=fit(train)  # Test data never enters fit or parameter selection.
        before,after=metrics(test,BASELINE),metrics(test,params)
        t=train[:,8:10]+R*np.column_stack((-train[:,3],train[:,2]))
        slip=np.linalg.norm(t,axis=1)
        fraction=float(np.mean(params[1]*(1+params[0])*(-train[:,10]) < (1+params[2])*slip/2.5))
        row={'id':rid,'train_count':len(train),'test_count':len(test),'e':params[0],'mu':params[1],'beta':params[2],
             'train':metrics(train,params),'baseline_test':before,'fitted_test':after,
             'normal_speed_range_m_s':[float((-train[:,10]).min()),float((-train[:,10]).max())],
             'surface_slip_range_m_s':[float(slip.min()),float(slip.max())],
             'sliding_fraction_train':fraction,
             'velocity_improvement_pct':100*(1-after['velocity_mae_m_s']/before['velocity_mae_m_s']),
             'spin_improvement_pct':100*(1-after['spin_mae_rps']/before['spin_mae_rps'])}
        rows.append(row)
        print(rid, 'params',np.round(params,4),'test v',round(before['velocity_mae_m_s'],3),'->',round(after['velocity_mae_m_s'],3),
              'spin',round(before['spin_mae_rps'],3),'->',round(after['spin_mae_rps'],3), 'sliding',round(fraction,3),flush=True)
    output={'source_commit':'159c6f040dd7832570b537b3b5fd00a97fc28c15','model':'constant e/mu/beta; bounds e[0,1],mu[0,1.5],beta[-1,1]',
            'objective':'mean squared xz velocity error + squared (R * wy error); training only',
            'baseline':BASELINE,'metrics':'source benchmark: mean xz vector error and mean absolute wy spin error; spin reported in rps',
            'numpy_version':np.__version__,'profiles':rows,'sha256':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((ROOT/'source/data').rglob('*.csv'))}}
    (ROOT/'results.json').write_text(json.dumps(output,indent=2)+'\n')
if __name__=='__main__':run()
