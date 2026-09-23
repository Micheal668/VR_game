# Lunar Escape VR — 项目执行文档

月面、上升器与起飞表现的已确认制作边界见 [月面与上升器实现约定](LunarEscapeVR/Docs/月面与上升器实现约定.md)。

## 1. 项目定位

**类型：** VR / 航天模拟 / 科幻叙事 / 生存 / 轻解谜 / 资源管理

**一句话概念：**  
玩家作为三人登月任务中的飞行工程师，从地球出发前往月球科研站。基地发生不可逆事故后，玩家需要处理故障、选择撤离资源、保护队友、驾驶月球上升器逃离，并与月球轨道器对接后返回地球。

**设计原则：**

- 不做完整真实航天模拟，采用 **Guided Simulation（引导式模拟）**
- 玩家负责：**判断 -> 操作 -> 决策 -> 承担后果**
- 系统负责：复杂轨道、导航、长距离飞行等后台自动处理
- 核心不是“看动画”，而是**错误会产生后果**

---

# 2. 核心游戏流程

**地球发射 -> 地球轨道 -> 地月转移 -> 月球轨道 -> 登月 -> 月球科研站 -> 基地事故 -> 紧急撤离 -> 月球起飞 -> 轨道交会对接 -> 返回地球 -> 再入 -> 降落 -> 任务评分**

推荐总时长：**30–60 分钟**

---

# 3. 角色

| 角色 | 定位 | 作用 |
|---|---|---|
| 玩家 | Flight Engineer / Lunar Systems Specialist | 飞行、维修、科研、应急处理 |
| Commander | 月面任务指挥官 | 与玩家共同登月，主要 NPC，可生还/受伤/牺牲 |
| Orbital Pilot | 轨道飞行员 | 留在月球轨道器，等待玩家返回并协助对接 |

---

# 4. 章节设计

## Chapter 1 — 地球发射

**玩法：** 座舱检查 -> 点火 -> 发动机确认 -> 级间分离 -> 逃逸塔处理 -> 入轨

复杂轨迹自动完成，玩家只负责关键节点。

### 失败示例

- 过早分离 -> 速度不足 -> 进入 Abort
- 正确使用逃逸系统 -> 乘员生还，但任务失败
- 严重误操作 -> 火箭失控 -> Mission Failed

> 重点：不要把发射做成纯动画，但也不要做完整火箭空气动力学。

---

## Chapter 2 — 地月转移

采用 **动画 + VR 舱内观察**。

**地球变小 -> 任务日变化 -> 月球变大 -> 进入月球轨道**

时长控制在 **30–90 秒**。

这是明确的降本模块，不做真实三天飞行。

---

## Chapter 3 — 月球轨道与登月

轨道器分离 -> Orbital Pilot 留轨 -> 玩家与 Commander 登月。

### 半自动登月

玩家控制：

- Pitch / Roll / Yaw 小范围修正
- 垂直速度
- 水平速度
- 最终着陆点

主要参数：

`Altitude / Vertical Speed / Horizontal Speed / Fuel / Landing Angle`

### 着陆结果

**Perfect -> Hard Landing -> Damaged Landing -> Crash**

硬着陆造成的损伤应影响后续游戏，例如：

**天线损伤 -> 后续通信故障概率增加**

---

# 5. 月球科研站 — 核心主体

这是游戏最主要的可玩区域，建议占总 Gameplay 的 **30–40%**。

玩家必须先经历正常工作阶段，让基地建立“生活感”，然后再进入事故。

### 科研/维护任务

首版只做 3–4 类：

- 月壤采样与分析
- 能源维护
- 氧气 / 冷却 / 环境系统维护
- EVA 外部维修

---

# 6. 随机故障系统

建立 `Incident Pool`，首版只做 5–6 种：

- 氧气泄漏
- 电力故障
- 冷却异常
- 电池过热
- 通信故障
- 实验室异常

采用 **Weighted Random**：

**当前任务状态 + 玩家表现 + 已发生事件 -> 计算下一事件**

要求：

- 同类故障短时间内不重复
- 玩家濒临失败时不继续堆致命事件
- 每局随机 2–4 个主要事件
- 不允许出现无解组合

---

# 7. 基地最终事故

最终事故 **必然发生**，玩家无法完全阻止基地毁灭。

但前面的操作决定：

**基地损伤 -> 可用资源 -> NPC 状态 -> 撤离时间 -> 后续飞船风险**

例如：

- 表现优秀：撤离窗口 10 分钟
- 一般：4–6 分钟
- 严重失误：约 2 分钟

这样“基地一定爆炸”不会让前期操作失去意义。

---

# 8. 紧急撤离 — 第一核心玩法

**警报 -> 系统失效 -> 部分舱室失压 -> 获取宇航服 -> 选择资源 -> 救援 NPC -> 前往上升器**

这是整个游戏最重要的高潮之一。

## 撤离资源

玩家最多携带 **3 类额外资源**，并受数量和重量限制。

| 资源 | 主要用途 |
|---|---|
| Oxygen Tank | 延长生命保障 |
| Repair Kit | 修复泄漏、线路、RCS 等 |
| Battery Pack | 紧急供电 |
| Medical Kit | 治疗玩家/NPC |
| Data Core | 保存高价值科研数据 |
| Lunar Sample | 科研评分 |
| Cooling Unit | 解决特殊过热故障 |

规则：

- 最多 3 类
- 每类有数量上限
- 总重量有限
- 拿资源消耗撤离时间
- 可在逃跑过程中主动丢弃物资

### 核心抉择

**生存资源 vs 科研成果 vs 撤离速度**

例如：

**氧气 + 维修包 + 医疗包** -> 生存能力强  
**数据核心 + 月壤样本 + 实验数据** -> 科研评分高，但容错率低

---

# 9. NPC 生死与道德选择

NPC 不固定死亡，由以下因素共同决定：

**玩家错误 + 当前资源 + NPC 状态 + 剩余时间 + 随机事故**

可能情况：

1. 两人正常撤离
2. NPC 被困，玩家直接离开
3. 玩家返回救援，可能两人一起逃脱，也可能一起死亡
4. 氧气不足，只够一人使用
5. 玩家主动把生存机会留给 NPC
6. 玩家通过提前携带的资源找到“第三种解法”

## 重要规则

避免简单菜单：

`A. 自己活 / B. NPC 活`

优先做成实际 VR 操作，例如：

**一瓶氧气 -> 两套生命维持接口 -> 玩家自己选择连接对象**

---

# 10. 月球起飞与基地爆炸

玩家进入上升器后：

**系统启动 -> 月面起飞 -> 基地越来越小 -> 基地爆炸 -> 强光/碎片/月尘 -> 舱体震动 -> Master Warning**

这是必须重点制作的视觉高潮。

### 爆炸碎片

命中概率不完全随机，而与撤离时间相关：

**越晚起飞 -> 碎片风险越高**

可能结果：

- 无损
- 轻微损伤
- 严重损伤：漏氧 / RCS 故障 / 电力不足

---

# 11. 上升器资源危机

典型情况：

`O₂ Remaining: 16 min`  
`Docking ETA: 24 min`

玩家可能选择：

- 使用备用氧气
- 修复泄漏
- 使用宇航服氧气
- Emergency Rendezvous
- 在极端情况下决定谁获得生命保障

这部分把“撤离资源系统”真正延续到后半段。

---

# 12. Emergency Rendezvous

两种模式：

| 模式 | 优点 | 风险 |
|---|---|---|
| Normal Rendezvous | 安全、容错高 | 时间长 |
| Emergency Rendezvous | 时间短 | 高油耗、高相对速度、高碰撞风险 |

这提供“资源不足时的第三条路”，避免所有剧情最终都变成“必须牺牲一个人”。

---

# 13. 轨道交会与对接 — 第二核心玩法

这是航天部分最值得真正操作的系统。

玩家控制 RCS：

**前后 / 上下 / 左右 / Roll**

显示：

`Range / Relative Velocity / Alignment / Angle`

结果：

**Perfect Docking -> Normal -> Hard Docking -> Collision -> Fatal Collision**

对接成功后：

**舱门开启 -> 与 Orbital Pilot 汇合 -> 返回地球**

---

# 14. 返回地球与再入

地月返回主体使用动画：

**离开月球 -> 月球变小 -> 地球变大 -> 接近再入窗口**

玩家主要负责再入参数和关键操作。

## 再入角

过浅 -> 可能 Skip-out  
合理 -> 正常再入  
过陡 -> 温度/过载过高 -> 飞船损坏或爆炸

不做真实 CFD，仅用：

**数值判定 + 粒子 + Shader + 音效 + VR 震动**

---

# 15. 降落伞阶段

流程：

**再入完成 -> Apex Cover Jettison -> Drogue -> Main Parachute -> 远景降落动画**

玩家必须根据高度和速度判断释放时机：

- 太早 -> 降落伞受损/撕裂
- 太晚 -> 无法充分减速
- 正确 -> 成功降落

最后低空阶段切第三人称远景，以降低开发成本。

---

# 16. 最重要的系统优先级

## P0 — 必须完成

这些决定项目是否成立：

1. **VR Interaction System**
2. **Mission State Machine**
3. **Failure System**
4. **Resource System**
5. **Crew State**
6. **Inventory / Evacuation Resource**
7. **Lunar Station Emergency**
8. **Orbital Docking**
9. **Scoring System**

---

## P1 — 强烈建议完成

显著提升完成度：

- MissionDirector 随机事故导演
- NPC 分支与牺牲
- Scientific Resource Recovery
- Emergency Rendezvous
- Hard Landing Consequences
- Medal / Achievement System

---

## P2 — 有时间再做

- 植物实验室
- 月球车驾驶
- 大型月面区域
- 更多科研实验
- 高级 NPC 动画
- 更多事故类型
- 高级手势识别
- 大量配音

---

## P3 — 建议直接放弃

第一次做 VR 不建议：

- 完整轨道力学
- N-body Simulation
- 真实火箭空气动力学
- CFD 再入模拟
- 开放世界月球
- 多人联机
- 复杂 NPC AI
- FPS 战斗
- 程序生成月球
- 完整真实飞控计算机

---

# 17. 三个真正需要重点打磨的 Gameplay

## ① 月球基地交互 + 灾难撤离

最高优先级。

## ② 轨道交会与对接

最重要的航天操作玩法。

## ③ 资源 + NPC 生死决策

决定游戏区别于普通 VR Demo。

其他飞行阶段主要负责连接这三个核心体验。

---

# 18. 评分系统

建议基础满分：**10000**

| 模块 | 建议权重 |
|---|---:|
| Crew Survival | 3500 |
| Flight Operations | 2000 |
| Station Operations | 1200 |
| Scientific Recovery | 1200 |
| Emergency Response | 1200 |
| Resource Management | 500 |
| Special Actions | Bonus |

原则：

**人命 > 任务 > 科研成果 > 操作漂亮程度**

### 示例

- 3/3 存活：高分
- 少一名成员：生存分骤减
- 因玩家错误导致 NPC 死亡：额外处罚
- 玩家牺牲自己救 NPC：大量 Heroic Bonus
- 为救人主动丢科研物资：可获得 `Crew First`
- 带回科研数据/样本：提高科研评分

---

# 19. 勋章系统

首版建议 6–8 个：

- **NO ONE LEFT BEHIND** — 全员生还
- **MEDAL OF VALOR** — 玩家牺牲自己救人
- **CREW FIRST** — 放弃高价值资源保护队友
- **PERFECT DOCKING** — 高质量对接
- **SCIENCE MUST GO ON** — 保留全部关键科研数据
- **ACE PILOT** — 多阶段飞行表现优秀
- **FAILURE IS NOT AN OPTION** — 成功解决多个严重故障
- **LUNAR SURVIVOR** — 从重大事故中返回

最终结算分成三层：

**Mission Outcome -> Score / Rank -> Medals & Commendations**

---

# 20. Unity 技术栈

| 模块 | 技术 |
|---|---|
| Engine | Unity 6 LTS |
| Rendering | URP |
| VR Runtime | OpenXR |
| VR Interaction | XR Interaction Toolkit |
| Input | Unity Input System |
| UI | World Space Canvas + TextMeshPro |
| Physics | Unity PhysX |
| Animation | Animator + Timeline |
| Camera | XR Camera + Cinemachine（过场辅助） |
| Audio | Unity Audio Mixer |
| Data Config | ScriptableObject |
| Save | JSON |
| Version Control | Git + Git LFS |

---

# 21. 推荐代码架构

```text
GameManager
├── MissionManager
├── MissionDirector
├── FailureManager
├── ResourceManager
├── CrewManager
├── InventoryManager
├── VehicleManager
├── ScoringManager
├── SaveManager
├── AudioManager
└── UIManager
```

不要把所有逻辑都塞进 `GameManager`。

---

# 22. Mission State Machine

推荐阶段：

```text
Launch
-> EarthOrbit
-> LunarTransfer
-> LunarLanding
-> LunarStation
-> Emergency
-> Evacuation
-> LunarAscent
-> Rendezvous
-> Docking
-> EarthReturn
-> Reentry
-> Landing
-> Completed / Failed
```

这是整个项目最重要的软件结构之一。

---

# 23. 核心数据系统

## ResourceManager

首版只管理：

`Oxygen / Fuel / Power / VehicleIntegrity / StationIntegrity / Time`

## CrewManager

角色：

`Player / Commander / OrbitalPilot`

状态：

`Health / Oxygen / Location / Alive / Injured / Evacuated`

## InventoryManager

物品字段：

`ID / Name / Weight / MaxQuantity / SlotCost / Type / Effects / ScoreValue`

## FailureManager

统一管理：

`OxygenLeak / PowerFailure / RCSFailure / CommunicationFailure / CoolingFailure / StructuralDamage / ParachuteFailure`

---

# 24. 场景划分

建议拆分 Scene：

```text
00_MainMenu
01_Launch
02_LunarOrbit
03_LunarLanding
04_LunarStation
05_Evacuation
06_LunarAscent
07_Docking
08_Reentry
09_Ending
```

地月转移等长距离过程采用 Timeline / Scene Transition，不单独做复杂玩法。

---

# 25. 开发路线

## Phase 1 — Vertical Slice

**月球基地一个房间 -> 氧气故障 -> 玩家维修 -> 最终警报 -> 选择 3 类资源 -> 逃入上升器 -> 基地爆炸 -> 任务评分**

只有这个小 Demo 真正好玩，才继续扩大项目。

---

## Phase 2 — Docking

实现：

**RCS 控制 -> 相对速度 -> 对准 -> 碰撞 -> 成功对接**

---

## Phase 3 — 主流程

把：

**Launch -> Landing -> Station -> Evacuation -> Docking -> Reentry**

通过 Mission State Machine 串起来。

---

## Phase 4 — 系统深化

增加：

**随机事故 -> NPC 分支 -> 资源差异 -> 评分 -> 勋章 -> 多结局**

---

## Phase 5 — Polish

最后再做：

**模型 -> 材质 -> 灯光 -> 爆炸 -> 粒子 -> 音效 -> 配音 -> UI -> 过场**

---

# 26. 开发优先顺序

**VR Interaction -> Mission State -> Station Gameplay -> Emergency -> Inventory -> Crew State -> Docking -> Failure System -> Scoring -> Launch -> Landing -> Reentry -> MissionDirector -> Narrative -> Visual Polish**

不要先花大量时间制作火箭发射动画。

---

# 27. 时间不足时优先砍什么

按顺序砍：

**月球车 -> 植物实验 -> 大型月面 -> 多种科研实验 -> 高级 NPC 动画 -> 高级登月模拟 -> 地月转移互动 -> 多种再入模式 -> 大量随机事件 -> 大量勋章**

尽量不要砍：

- 基地事故
- 撤离资源
- NPC 生死
- 对接
- 评分系统

这些是项目核心差异点。

---

# 28. 推荐最终规模

| 内容 | 推荐范围 |
|---|---|
| 总时长 | 30–60 分钟 |
| NPC | 2 名 |
| 基地主要舱室 | 4–5 个 |
| 科研任务 | 2–3 种 |
| 随机故障 | 5–6 种 |
| 撤离资源 | 6–7 种 |
| 主要结局 | 4–5 个 |
| 勋章 | 6–8 个 |
| 真正手动飞行 | 登月末段 / 月球起飞关键操作 / 对接 / 再入参数 / 降落伞 |

---

# 29. 最终判断标准

项目不追求：

**Simulation Accuracy First**

而追求：

**Believable Interaction First**

玩家不需要知道后台是否进行了完整轨道计算。

玩家需要明确感受到：

> **“我刚才做出的操作，真的改变了后面的任务结果。”**

最终核心循环：

**观察 -> 判断 -> 操作 -> 系统变化 -> 承担后果 -> 再决策**

这就是整个项目最重要的设计基础。
