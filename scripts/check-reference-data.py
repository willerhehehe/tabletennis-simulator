from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parents[1]
def verify(base,files):
 for name,expected in files.items():
  p=base/name
  assert p.is_file() and hashlib.sha256(p.read_bytes()).hexdigest()==expected, f'参考文件缺失或校验不符：{p}'
r=json.loads((root/'docs/research/references/manifest.json').read_text())
verify(root,r['files'])
for p in r['papers']:
 if p.get('pdf'):verify(root,{p['pdf']:p['sha256']})
a=root/'Calibration/rubber';m=json.loads((a/'response-manifest.json').read_text());verify(a,m['files'])
assert m['source_commit']==r['data']['commit']
print('PASS: archived paper PDF, 30 source/data files, and response fit manifests match recorded SHA256.')
