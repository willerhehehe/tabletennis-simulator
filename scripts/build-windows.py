#!/usr/bin/env python3
"""Build in a separate checkout directory so the active Editor experiment stays untouched."""
from pathlib import Path
import os
import shutil
import subprocess
import tempfile
import json
import argparse
import re

parser = argparse.ArgumentParser()
parser.add_argument("--version", default="0.1.1")
version = parser.parse_args().version
if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
    parser.error("版本号请使用例如 0.1.1 的格式")

root = Path(__file__).resolve().parents[1]
editor = Path(os.environ.get('UNITY_EDITOR', '/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity'))
release = root / 'Builds/Windows11' / version
output = release / 'TableTennisLab'
if (output / 'TableTennisLab.exe').exists():
    raise SystemExit('该版本已有构建，请使用新的 --version 版本号，保留旧包。')
work = Path(tempfile.mkdtemp(prefix='tabletennis-windows-build-'))
shutil.copytree(root / 'Assets', work / 'Assets', ignore=shutil.ignore_patterns('Editor', 'Editor.meta', 'Tests', 'Tests.meta'))
(work / 'Assets/Editor').mkdir()
shutil.copy2(root / 'Assets/Editor/WindowsBuild.cs', work / 'Assets/Editor/WindowsBuild.cs')
shutil.copytree(root / 'ProjectSettings', work / 'ProjectSettings')
(work / 'Packages').mkdir()
manifest = json.loads((root / 'Packages/manifest.json').read_text())
manifest['dependencies'] = {k: v for k, v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
(work / 'Packages/manifest.json').write_text(json.dumps(manifest, indent=2))
output.parent.mkdir(parents=True, exist_ok=True)
(release / 'build-project.txt').write_text(str(work))
log = release / 'build.log'
env = dict(os.environ, TABLETENNIS_BUILD_OUTPUT=str(output), TABLETENNIS_BUILD_VERSION=version)
print(f'构建目录：{work}\n日志：{log}', flush=True)
subprocess.run([str(editor), '-batchmode', '-nographics', '-projectPath', str(work), '-buildTarget', 'Win64',
                '-executeMethod', 'WindowsBuild.Build', '-quit', '-logFile', str(log)], env=env, check=True)
for license_file in (root / 'Assets/Resources').glob('*LICENSE*.txt'):
    shutil.copy2(license_file, output / license_file.name)
print(f'Windows 构建完成：{output}', flush=True)
