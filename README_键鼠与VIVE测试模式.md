# 键鼠与 VIVE 测试模式切换

适用于本项目 Unity **6000.6.1f1**，当前主场景为 `Assets/_LunarEscape/Scenes/11_LunarStation_LifeSupport.unity`。键鼠模式用于编辑器内测试，VIVE 模式用于 Windows + SteamVR + VIVE Pro / Pro Eye 与圆盘手柄。

**先停止 Play，再修改设置。** 场景开关改完按 `Ctrl + S` 保存；在 Play 中修改的场景状态通常会在停止运行后恢复。

## 一、切换时看这张表

| 设置 | 键鼠测试 | VIVE 真机测试 |
| --- | --- | --- |
| Project Settings → XR Plug-in Management → 桌面平台 → **Initialize XR on Startup** | 取消勾选 | 勾选 |
| 同页 Plug-in Providers → **OpenXR** | 可保留勾选，通过上面的启动开关停用真机初始化 | 必须勾选 |
| Hierarchy → **XR Interaction Simulator - Editor Only** → Inspector 顶部对象启用框 | 勾选，启用整个对象 | 取消勾选，关闭整个对象 |
| Project Settings → XR Plug-in Management → XR Interaction Toolkit → **Use XR Interaction Simulator in scenes** | 保持关闭 | 保持关闭 |
| SteamVR | 不需要启动 | 启动并确认设备追踪正常 |

`Initialize XR on Startup` 控制是否在启动时初始化真实 XR 运行环境，属于项目设置。本项目没有另写运行中切换 XR 的初始化流程。相关选项说明见 [Unity XR Plug-in Management 文档](https://docs.unity.com/en-us/engine/6000.5/manual/xr/configuring-project-for/plugin-management)。

`XR Interaction Simulator - Editor Only` 是当前场景里的模拟器，属于场景设置。两项需要配合切换。

## 二、切换为键鼠测试

1. 停止 Play，打开场景 `11_LunarStation_LifeSupport`。
2. 打开 **Edit → Project Settings → XR Plug-in Management**，选择桌面平台页签（Windows / Mac / Linux / Standalone，按界面显示为准），取消勾选 **Initialize XR on Startup**。已经配置好的 OpenXR 可以保留。
3. 在 Hierarchy 搜索 `XR Interaction Simulator - Editor Only`，选中它，勾选 Inspector **最顶部、对象名称左侧**的启用框。这是整个 GameObject 的开关，不是某个组件标题旁的开关。
4. 打开 **Edit → Project Settings → XR Plug-in Management → XR Interaction Toolkit**，在 **Interaction Simulator Settings** 下，确认 **Use XR Interaction Simulator in scenes** 未勾选。
5. 按 `Ctrl + S` 保存场景，点击 Play，再点击 **Game** 画面，让游戏获得键盘焦点。建议切换到英文输入法。

第 4 步保持关闭，是因为场景已经放好了带碰撞行走适配的模拟器。这个项目设置用于自动创建模拟器，不是已有场景对象的启用开关。

### 常用键鼠操作

| 操作 | 按键 |
| --- | --- |
| 地面行走 | `W / A / S / D`；第一人称、头部及手柄操作模式均保留身体碰撞 |
| 切换模拟操作模式 | `Tab`，观察屏幕模拟器提示 |
| 选择头部 / 左手柄 / 右手柄 | `H` / `[` / `]` |
| 第一人称视角控制 | 鼠标右键切换，再移动鼠标观察 |
| 调整手柄高度 | 在手柄操作模式使用 `Q / E`；不会让身体飞起 |
| 点击墙面按钮 | 释放视角控制后，用鼠标左键点击；长按操作按屏幕提示执行 |

抓取及手柄按键以当前模拟器屏幕提示为准。`Tab` 只切换模拟器的操作方式，不会把键鼠模式切换成真实 VIVE 模式。

## 三、切换为 VIVE 真机测试

1. 停止 Play。在 Windows 连接头显、手柄及基站，启动 SteamVR，先确认设备已识别、头部与手柄追踪正常。
2. 在 SteamVR 桌面窗口打开 **设置 → OpenXR**，确认 **Current OpenXR Runtime** 为 **SteamVR**。若不是，点击 **Set SteamVR as OpenXR Runtime**。部分旧界面把入口放在“开发者”页，必要时开启高级设置。参见 [VIVE 官方运行时检查说明](https://www.vive.com/cn/support/vs/category_howto/trouble-with-openxr-titles.html)。
3. Unity 打开当前场景 `11_LunarStation_LifeSupport`。在 Hierarchy 选择 **XR Interaction Simulator - Editor Only**，取消勾选 Inspector 顶部的对象启用框。**关闭整个对象**，让其设备模拟及子对象一起停用。
4. 打开 **Edit → Project Settings → XR Plug-in Management**，选择桌面平台页签，勾选 **OpenXR** 和 **Initialize XR on Startup**。若此前没有启用过桌面 XR，在这里完成配置。
5. 打开其下的 **OpenXR** 设置页，核对本项目的配置：**Render Mode = Multi Pass**；**Enabled Interaction Profiles** 包含 **HTC Vive Controller Profile**。缺少 Profile 时通过列表的 `+` 添加。这里对应标准 VIVE 圆盘手柄。
6. 打开 **XR Interaction Toolkit → Interaction Simulator Settings**，保持 **Use XR Interaction Simulator in scenes** 关闭。
7. 按 `Ctrl + S` 保存场景，点击 Play。确认头显看到基地、头部转动和双手控制器位置正常，再开始任务。

当前主场景的行走方式为：**按住任一手柄圆盘上半区前进、下半区后退**，仅触摸不走，松开按压停止。转身使用真实头部和身体转动；圆盘传送、连续转向与分段转向已关闭。登舱后锁定游戏内身体移动，保留头手追踪。更详细的操作检查见 [VIVE 圆盘移动与追踪检查](Docs/VIVE圆盘移动与追踪检查.md)。

## 四、从 VIVE 切回键鼠

停止 Play → 取消 **Initialize XR on Startup** → 启用场景中的 **XR Interaction Simulator - Editor Only** → 保存场景 → Play → 点击 Game 画面。

OpenXR 的 Profile 和 Multi Pass 配置可以保留；**Use XR Interaction Simulator in scenes** 继续关闭。无需删除 XR Origin、重装包或重新生成场景。

## 五、常见问题

| 现象 | 检查方法 |
| --- | --- |
| 键鼠不响应 | 点击 Game 画面、使用英文输入法，确认整个模拟器对象已启用；检查屏幕显示的当前操作模式。 |
| 键鼠测试时 SteamVR 自动启动或提示找不到头显 | 停止 Play，在桌面平台页取消 Initialize XR on Startup，再运行。 |
| VIVE 有画面但手柄不响应 | 检查 HTC Vive Controller Profile、SteamVR 手柄追踪，以及整个模拟器对象是否关闭。 |
| VIVE 无画面 | 检查桌面平台的 OpenXR 和 Initialize XR on Startup 都已勾选，SteamVR 为当前 OpenXR Runtime，设备已就绪；再查看 Unity Console。 |
| 视角乱转或头手姿态冲突 | 先确认场景模拟器和自动创建模拟器选项都已关闭；退出 Play，在 SteamVR 中对比追踪是否稳定。 |
| 停止 Play 后设置又变回去 | 在编辑状态修改场景对象并保存，不要依赖运行中的临时修改。 |
| 切换另一个场景后模式不一致 | XR 启动设置属于项目，模拟器启用状态属于每个场景；需要检查新场景中的模拟器。 |

## 六、当前工程需要注意的入口

**`Lunar Escape → Configure Vive Press To Walk` 不是日常模式切换按钮。** 当前脚本会打开并修改场景 `10_LunarStation_Docking`，用于配置旧场景输入；测试场景 11 时按本文设置切换即可。

模拟器对象带有 **EditorOnly** 标签，正常构建时会被移除。因此，本文的键鼠方式适用于 Unity 编辑器，不代表构建出的 EXE 已支持键鼠模式。准备 VIVE 构建时应恢复真机设置，并确认构建场景包含场景 11。

本文依据项目输入脚本、场景和安装包设置界面核对，更新于 2026-10-07；本次仅编写说明，未进行新的 VIVE 真机验收。
