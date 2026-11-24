# AutoTwin

AutoTwin 是面向 Windows PC 的新能源车数字孪生演示工程。控制台用三维车辆、遥测仪表与告警监控展示 EV-01 概念车的运行状态。

## 在 Unity 中预览

1. 用 Unity **2022.3.55f1c1** 打开工程。项目使用 URP 14.0.11、uGUI、TextMeshPro 3.0.7 和 Unity Input Manager。
2. 打开 `Assets/Scenes/AutoTwinMain.unity`，进入 Play 模式。在 Game 视图选择 **16:9** 或 **1920×1080**。
3. 初始为数字孪生视图，前门、前盖和尾门处于闭合姿态。玻璃和蓝色透视外壳之间可观察独立电池包、动力线路和前轴电驱。打开右上方「工具」并点击「普通展示」，可查看完整车漆、玻璃和轮辋。在视口内用左键旋转、滚轮缩放、中键平移。
4. 在「部件选择」模式单击模型，或在「工具」中打开「部件详情」查看清单。点击视口空白处取消选中。「工具」还收纳视角和车辆复位、自由观察、前门、前盖、四轮旋转及模拟重置。前盖绕原后铰链开启 34°，两扇前门各开启 40°。后门与尾门可选中并查看参数。
5. 点击顶部「开始模拟」推进演示时钟。左侧 SOC 曲线支持悬停查看时间和采样值，右侧故障预警卡提供「模拟电池过热」和「维修恢复」。所有读数均来自本地模拟。

### 模拟与告警

时钟以 12 倍速度推进，每 4 个模拟秒采样一次。SOC 根据电池净能量变化，SOH 表示长期容量健康度。车速、转速、功率、电压、电流和温度来自同一演示工况，充放电能量分别累积。

「模拟电池过热」让电池温度逐步升高。达到 52 °C 时新增一条带部件 ID 的告警，顶部状态、温度曲线、右侧监控和三维电池同时反馈。点击告警可选中电池。「维修恢复」移除热源，冷却至 44 °C 时解除告警并保留本次记录。重置模拟会清空本轮记录并恢复初始历史曲线。当前故障演示为电池温度异常。

### 验证

用 Unity 2022.3.55f1c1 重新生成主场景并完成 Windows x64 构建。独立 Unity Editor 的 Game View 在 1920×1080 和 1600×900 实际取图，并验证了启动、过热、维修恢复、工具抽屉、显示模式切换及车门／前盖／车轮按钮。两个分辨率的 Windows Player 运行检查均通过，覆盖相机、车辆拾取、曲线悬停和完整告警生命周期。

## 工程入口

| 位置 | 用途 |
| --- | --- |
| `Assets/Scenes/AutoTwinMain.unity` | 主场景，已列为第一个构建场景 |
| `Assets/AutoTwin/Generated/EV01 Demo Vehicle.prefab` | EV-01 概念车，包含 13 个可拾取部件绑定 |
| `Assets/AutoTwin/Art/Vehicle/EV01 Concept.fbx` | 由 Blender 生成的分件车身、玻璃、车轮、灯组、电池包和前轴电驱 |
| `Tools/Blender/build_ev01.py` | 概念车建模与 FBX 导出脚本 |
| `Tools/Blender/EV01 Precision.blend` | EV-01 精细版的可编辑 Blender 文件 |
| `Assets/AutoTwin/Generated/EV01 Vehicle Data.asset` | 整车概览、模拟参数、部件类型与稳定 ID |
| `Assets/AutoTwin/Runtime/VehicleViewport.cs` | 鼠标手势、UI 输入隔离、平滑镜头与视口射线 |
| `Assets/AutoTwin/Runtime/VehicleRig.cs`、`VehiclePart.cs` | 选中、局部材质高亮、铰链、轮轴和视图切换 |
| `Assets/AutoTwin/Runtime/TelemetrySimulation.cs`、`TelemetryChart.cs`、`TelemetryRing.cs` | 模拟工况、历史采样、告警、uGUI 曲线与圆环 |
| `Assets/AutoTwin/Runtime/DashboardView.cs`、`VehicleCallouts.cs` | 遥测读数、告警列表、日志和车辆投影标注 |
| `Assets/AutoTwin/Art/VehicleXRay.shader` | URP 透明外壳与车轮光学材质，保留不透明动力部件的深度关系 |
| `Assets/AutoTwin/Editor/AutoTwinBuilder.cs`、`ConsoleLayout.cs` | 场景与仪表台生成器，菜单 `AutoTwin > 重建数字孪生演示场景` 与 `AutoTwin > 刷新所选车辆绑定` |

Unity 工程已经包含场景、布局、材质和 FBX，可以直接运行。概念车由 Blender 5.2.2 LTS 生成并导出。重建菜单会重新生成演示场景与 Prefab，保留已有样车参数资产。若要修改造型，可在 Blender 导入 FBX 检查模型，或运行仓库中的 Python 脚本重新生成。接入其他车型时，可先复制主场景和车辆 Prefab。

EV-01 精细版参考五座 Model Y 的跨界车比例，保留原创车身和灯组。模型车长约 4.786 m，轴距为 2.890 m，包含全景玻璃顶、门内饰、轮拱内衬和悬架。 [Tesla 官方尺寸手册](https://www.tesla.cn/ownersmanual/modely/zh_cn/GUID-1E76B638-7B12-4D9A-8767-94B7F1E92A0E.html)

若需提升至量产车级精度，建议在 Blender 中按实车尺寸调整机盖与保险杠接缝，补足轮拱内衬、底盘和铰链连杆，再用同一部件 ID 替换网格。车辆基本操作沿用本项目的铰链与轮轴绑定。

从项目根目录运行以下 PowerShell 命令可重新导出随工程提供的概念车。按实际 Blender 安装位置调整可执行文件路径。

```powershell
& 'D:/blender/blender.exe' -b --factory-startup --python 'Tools/Blender/build_ev01.py'
```

如需同时输出 Blender 工程，在脚本参数 `--` 后依次传入 FBX 和 `.blend` 的目标路径。

中文字体使用 Noto Sans SC 的项目子集 [AutoTwin Sans](Assets/AutoTwin/Art/Fonts/OFL.txt)。添加超出子集的中文时，为同一个 TMP 字体资产补充字形或设定包含新字形的后备字体。字体上游为 [Noto Sans SC](https://github.com/google/fonts/tree/main/ofl/notosanssc)。

## Blender 模型接入规范

### 层级和稳定 ID

`F` 和 `R` 分别表示前后轴，`L` 和 `R` 指驾驶员面向车头时的车辆左右。下表的名称用于 DCC 协作。Unity 运行时使用 `VehiclePart` 组件绑定的 ID。

```text
VehicleRoot
├── Body       VEH-BODY
├── Wheel_FL   WHEEL-FL
├── Wheel_FR   WHEEL-FR
├── Wheel_RL   WHEEL-RL
├── Wheel_RR   WHEEL-RR
├── Door_FL    DOOR-FL
├── Door_FR    DOOR-FR
├── Door_RL    DOOR-RL
├── Door_RR    DOOR-RR
├── Hood       BODY-HOOD-F
├── Trunk      BODY-TRUNK-R
├── Battery    EV-BAT-HV
└── Motor_F    EV-MOTOR-F
```

建议每个部件用一个空物体作为运动节点，下面挂一个或多个 Mesh 对象。门玻璃、把手、门内饰归各自车门；轮胎、轮辋归各自车轮。车身网格预留车门、前盖和后盖开口。动力电池与电机的网格独立于车身，方便动力视图切换。

### 原点、铰链及轴向

- 模型采用公制，1 Blender 单位对应 Unity 1 米。整车根原点在地面中心，轮胎接地面与地面重合。Blender 车头朝 `+Y`，上方为 `+Z`，车右为 `+X`。按下述设置导入后，Unity 车头朝 `+Z`，上方为 `+Y`，车右为 `+X`。使用 Blender 5.2.2 LTS 导出三个间隔一米的轴向标记，在 Unity 2022.3 验证了这组映射。
- 四个车门的运动原点设在各自门板的前缘铰链，Unity 局部 `Y` 沿铰链。左右前门打开时方向相反。
- 前引擎盖的原点放在靠挡风玻璃一侧的横向铰链上，Unity 局部 `X` 沿铰链。后备箱盖的原点放在靠车顶一侧的横向铰链上，以便后续加入动作。
- 四个车轮的原点放在轮毂中心，Unity 局部 `X` 沿轮轴。清理镜像产生的负缩放，检查车轮和门在闭合姿态下的局部旋转与缩放。

### FBX 导出与 Unity 导入

1. Blender 场景单位设为 `Metric`，`Unit Scale = 1`。将模型置于静止、四门闭合姿态，应用网格旋转和缩放，保留空物体的原点和铰链位置。
2. 导出 FBX 时使用 `Selected Objects`，类型选 `Mesh` 和 `Empty`。设 `Scale = 1`、`Apply Unit`、`Apply Scalings = FBX Units Scale`、`Forward = -Z Forward`、`Up = Y Up`。开启 `Apply Modifiers`，关闭 `Bake Animation` 和实验性的 `Apply Transform`。
3. 在 Unity 的 FBX Model 页启用 `Convert Units` 和 `Bake Axis Conversion`，保持 `Scale Factor = 1`，应用后检查模型尺寸、车头、车轮和门轴。正式材质在 Unity 中换为 URP Lit 材质，供选中时使用基础色和自发光高亮。

导出与导入选项见 [Blender FBX 手册](https://docs.blender.org/manual/en/latest/addons/import_export/scene_fbx.html)及 [Unity Model 导入文档](https://docs.unity3d.com/2022.3/Documentation/Manual/FBXImporter-Model.html)。

### 替换临时车辆

1. 在保留 `VehicleRig` 根对象的 Prefab 副本中，保留 13 个 `VehiclePart` 节点及 ID。把 FBX 中对应网格装到各节点下，以网格子对象调整偏移并保持闭合姿态。正式模型尺寸不同的情况下，移动绑定节点到正式模型的铰链、轮毂位置，并在 Inspector 检查局部轴、开启角和外观／动力可见组。
2. 给可点击部件添加贴合的 `BoxCollider` 或 `MeshCollider`。选择车辆根对象，执行 `AutoTwin > 刷新所选车辆绑定`，验证 ID 并缓存渲染器和碰撞体。工具会把碰撞体对象分配到 `AutoTwinPart` 图层。
3. 用正式车辆 Prefab 替换场景车辆实例。在 `ConsoleView` 的 `Vehicle` 字段中，将引用指向新 `VehicleRig`。若在现有车实例中按节点替换网格，则原有场景引用可直接沿用。之后检查左右前门、缩放视野、可见组和部件清单。
