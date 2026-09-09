#!/usr/bin/env python3
"""在隔离临时项目构建 Web，不打断当前 Editor 实验。"""
import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

parser = argparse.ArgumentParser()
parser.add_argument('--version', default='0.2.0')
version = parser.parse_args().version
if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version):
    parser.error('版本号格式应为 0.2.0')
root = Path(__file__).resolve().parents[1]
release = root / 'Builds/Web' / version
output = release / 'site'
if (output / 'index.html').exists():
    raise SystemExit('此版本已构建，请换用新的版本号。')
work = Path(tempfile.mkdtemp(prefix='tabletennis-web-build-'))
shutil.copytree(root / 'Assets', work / 'Assets', ignore=shutil.ignore_patterns('Editor', 'Editor.meta', 'Tests', 'Tests.meta'))
(work / 'Assets/Editor').mkdir()
for name in ['WebBuild.cs', 'WindowsBuild.cs']:
    shutil.copy2(root / 'Assets/Editor' / name, work / 'Assets/Editor' / name)
shutil.copytree(root / 'ProjectSettings', work / 'ProjectSettings')
(work / 'Packages').mkdir()
manifest = json.loads((root / 'Packages/manifest.json').read_text())
manifest['dependencies'] = {k: v for k, v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
(work / 'Packages/manifest.json').write_text(json.dumps(manifest, indent=2))
release.mkdir(parents=True, exist_ok=True)
(release / 'build-project.txt').write_text(str(work))
editor = os.environ.get('UNITY_EDITOR', '/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity')
log = release / 'build.log'
print(f'构建目录：{work}\n日志：{log}', flush=True)
env = dict(os.environ, TABLETENNIS_BUILD_OUTPUT=str(output), TABLETENNIS_BUILD_VERSION=version)
subprocess.run([editor, '-batchmode', '-nographics', '-projectPath', str(work), '-buildTarget', 'WebGL', '-executeMethod', 'WebBuild.Build', '-quit', '-logFile', str(log)], env=env, check=True)
(output / '.nojekyll').write_text('')
for p in (root / 'Assets/Resources').glob('*LICENSE*.txt'):
    shutil.copy2(p, output / p.name)
print(f'Web 构建完成：{output}', flush=True)
