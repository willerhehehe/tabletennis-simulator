from pathlib import Path
import json,hashlib
from fit_response import ROOT
x=json.loads((ROOT/'response-results.json').read_text());rows=x['profiles'];n=sum(r['test_count'] for r in rows)
def mean(k,m):return sum(r[k][m]*r['test_count'] for r in rows)/n
v0,v1=[mean(k,'velocity_mae_m_s') for k in ('constant_test','response_test')]
w0,w1=[mean(k,'spin_mae_rps') for k in ('constant_test','response_test')]
text=f'''# 反胶整拍的速度相关响应 · 2026-09-09

对象是 R01、R02、R03、R04、R09 五种反胶+海绵厚度+底板组合，不是独立面胶材料常数。训练 3,304 条，固定作者测试集 {n} 条。

## 与上一版常数拟合比较

| 测试集平均误差 | 常数拟合 | 速度相关响应 | 相对降低 |
|---|---:|---:|---:|
| x/z 平面速度矢量误差 | {v0:.4f} m/s | {v1:.4f} m/s | {(1-v1/v0)*100:.1f}% |
| y 轴旋转绝对误差 | {w0:.4f} rps | {w1:.4f} rps | {(1-w1/w0)*100:.1f}% |

这是对已有常数校准的增益，不是与最初经验值比较。沿用上一轮固定测试集做配对比较；本轮候选模型选择不使用测试集。不能将这些数字解释为整条三维球路或实战落点精度。

| 配置 | 常数速度误差 | 新速度误差 | 常数旋转误差 | 新旋转误差 |
|---|---:|---:|---:|---:|
'''
for r in rows:
 a,b=r['constant_test'],r['response_test'];text+=f"| R{r['id']:02d} | {a['velocity_mae_m_s']:.4f} | {b['velocity_mae_m_s']:.4f} | {a['spin_mae_rps']:.4f} | {b['spin_mae_rps']:.4f} |\n"
text+='''
## 模型与选择协议

- 固定候选族：常数；仅 e 随法向撞击速度变化；e 随法向速度、β 随法向与切向滑动速度变化。μ 保持该训练数据上常数拟合的值，避免声称从这些数据唯一识别出更复杂摩擦定律。
- 仅在训练集中按 (racket_angle, traj_dir) 分组做固定 5 折交叉验证。分组键经 SHA256 取模，结果可复现。候选以速度平方误差加 (球半径×角速度误差)² 比较；选择距最小值 2% 内最简单的模型。
- 五组均选中法向+滑动速度模型。模型族选定后在完整训练集重新拟合，最后评价作者固定测试集。训练与测试没有共享的角度+轨迹键。
- e 是标准化法向速度的一次函数；β 是标准化法向速度与滑动速度的仿射函数。e 限制在 [0,1]，β 限制在 [-1,1]。没有训练神经网络或复现作者 GP。
- 输入是接触点相对速度：v_rel = v_ball + ω×(-R n) − v_racket。法向速度为 max(0,−v_rel·n)，滑动速度为 |v_rel−(v_rel·n)n|。
- 系数按训练输入范围截断；区间外只取边界系数，球和拍的真实速度不截断。范围提示只是训练矩形覆盖，不代表其内部所有区域都同样有数据。出范围或三维接球均需要额外验证。
- 数据已经在拍面局部坐标，不再次使用 racket_angle 旋转。线速度 m/s、角速度 rad/s；报告转速除以 2π。薄球壳惯量、半径与上一轮相同。

## 程序集成

`InvertedRubberResponse` 只计算等效 e/μ/β；`BallPhysics.Strike` 使用同一冲量求解器。球台 Contact 不受该模型影响。单板、正向发球、接发扰动搜索每次触球均按当次相对速度重新求参数。

目标出球的触球反算采用有界标量求根，再用完整正向模型验证目标。导出保存响应配置 ID，避免把基准 e/μ/β 误当成实际恒定参数。选用速度相关模型时，界面显示实际触球系数及超范围提示；可以切回旧常数拟合。

默认器材和用户已有实验不会自动被覆盖，使用「胶皮参数 → 公开数据标定 → 随触球速度 → 应用」启用。长胶、正胶、防弧继续使用已记录的常数版本。

## 复现与证据

```sh
python3 Calibration/rubber/fit_response.py
python3 Calibration/rubber/generate_response.py
python3 Calibration/rubber/response_report.py
sh scripts/check.sh
python3 scripts/check-reference-data.py
```

拟合阶段只需 NumPy；运行游戏无需 Python。代码与具体系数见 `fit_response.py`、`response-results.json`、`Assets/Scripts/Core/InvertedRubberResponses.Data.cs`。C# 检查逐条重放 827 个测试样本，并覆盖被动能量、坐标旋转/运动球拍不变性、边界截断、正向发球、反算和接发调用链。

[论文和数据索引](../../docs/research/references/README.md)列出数据提交、完整 PDF、许可和背景参考的不同用途。未证明可以从本次数据独立识别面胶、海绵、胶水、底板各层刚度；本轮仍为整拍等效模型。
'''
(ROOT/'RESPONSE_REPORT.md').write_text(text)
manifest={'source_commit':x['source_commit'],'train_only_model_selection':True,'files':{str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [ROOT/'calibrate.py',ROOT/'fit_response.py',ROOT/'response_report.py',ROOT/'generate_response.py',ROOT/'response-results.json',ROOT/'response-test-predictions.csv']}}
(ROOT/'response-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
