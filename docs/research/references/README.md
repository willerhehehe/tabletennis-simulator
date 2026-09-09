# 论文与参考数据索引

本目录区分“本轮实际使用的数据”与“背景参考”。完整版本、校验值和授权信息见 [manifest.json](manifest.json)。

## gossard2026

Learning Racket-Ball Bounce Dynamics Across Diverse Rubbers for Robotic Table Tennis（2026）。

- 原文：https://arxiv.org/abs/2604.11349v1
- 本项目用途：提供拟合数据及坐标系、模型的参考说明；本项目的响应拟合并未复现该论文的高斯过程基准模型。
- 本地 PDF：[固定版本全文](papers/Gossard-2026-2604.11349v1.pdf)。论文按 CC BY-SA 4.0 保留原署名与链接；PDF 未修改。

## rinaldi2019

Table Tennis Ball Impacting Racket Polymeric Coatings: Experiments and Modeling of Key Performance Metrics（2019）。

- 原文：https://www.mdpi.com/2076-3417/9/1/158
- 本项目用途：用于理解面胶与海绵的变形、应变速率依赖性及层结构；本次拟合未采用该论文中的数值参数。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## karasawa2021

Evaluation of restitution performance and examination of estimate equation of coefficient of restitution of table tennis racket（2021）。

- 原文：https://www.jstage.jst.go.jp/article/transjsme/87/902/87_21-00145/_article/-char/en
- 本项目用途：用于理解底板刚度、固有频率和撞击速度的影响，支持将标定参数视为整拍组合的综合响应。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## cross2010

Impact of a ball on a surface with tangential compliance（2010）。

- 原文：https://physics.usyd.edu.au/~cross/PUBLICATIONS/47.%20MoreSpin.pdf
- 本项目用途：用于理解切向弹性储能与旋转传递；文中的示例并非用于标定乒乓球胶皮的数据集。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## conti2026

Physics Models for Sim-to-Real Transfer in Professional-Level Robot Table Tennis（2026）。

- 原文：https://arxiv.org/html/2606.28805v2
- 本项目用途：提供气动力、球台碰撞及球拍碰撞的建模方法参考；本次反胶拟合未使用其系数或残差网络。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## inaba2017

Effect of Changing Table Tennis Ball Material from Celluloid to Plastic on the Post-Collision Ball Trajectory（2017）。

- 原文：https://pmc.ncbi.nlm.nih.gov/articles/PMC5304273/
- 本项目用途：用于理解球台反弹及球材料的影响；未将其作为球拍标定数据。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## achterhold2023

Black-Box vs. Gray-Box: A Case Study on Learning Table Tennis Ball Trajectory Prediction with Spin and Impacts（2023）。

- 原文：https://github.com/EmbodiedVision/tabletennis-spin-impacts
- 本项目用途：可作为后续飞行轨迹与球台碰撞的独立验证数据来源；尚未下载该数据集，本次拟合也未使用。
- 本地保留书目信息与链接；未将全文拷贝进项目。

## 本轮实际数据

固定提交的训练/测试 CSV、作者坐标处理代码与原 MIT License 均在 `Calibration/rubber/source/`。模型选择仅使用 R01/R02/R03/R04/R09 的训练数据；827 条测试记录仅用于评估。单位和坐标细节见 `Calibration/rubber/RESPONSE_REPORT.md`。研究数据和论文不放在 Assets 中，不随游戏包分发；运行时参数的 MIT 署名保留在 Assets/Resources/RubberCalibration-LICENSE.txt。
