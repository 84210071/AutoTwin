# AutoTwin

AutoTwin 是面向 Windows PC 的纯电汽车数字孪生展示工程。第一阶段提供三维观察、部件识别和基础机械动作，使用本地样车配置展示模拟数据。

## 在 Unity 中预览

1. 用 Unity **2022.3.55f1c1** 打开工程。项目使用 URP 14.0.11、uGUI、TextMeshPro 3.0.7 和 Unity Input Manager。
2. 打开 `Assets/Scenes/AutoTwinMain.unity`，进入 Play 模式。在 Game 视图选择 **16:9** 或 **1920×1080**。
3. 在中央视口用左键拖动旋转、滚轮缩放、中键拖动平移。默认从车头左前方约 40° 观察。点击「车辆复位」可恢复镜头、外观视图、部件选择模式和车辆动作初始状态。
4. 在「部件选择」模式单击模型，或使用左侧部件清单。点视口空白处取消选中。「自由观察」关闭模型射线拾取，清单仍可用于定位部件。
5. 「打开前车门」联动左右前门。「打开引擎盖」演示前盖铰链。「旋转车轮」同时展示四轮转动。「透视动力总成」切换到内部件视图，可选中动力电池和前驱动电机。

结构拆解、运行仿真和故障诊断已在界面中标为未启用。后门及后备箱盖可单独选中并查看参数。荷电状态、续航和零部件参数来自样车配置。

### 第一阶段验证

使用项目指定的 Unity 版本完成场景生成和 Windows x64 构建。在 Windows Player 中以运行时断言检查 13 个部件绑定、中文字形、UI 射线隔离、镜头限位与复位、模型射线选中与信息更新，以及车门、引擎盖和车轮动作。三个未启用的按钮均为禁用状态。

## 工程入口

| 位置 | 用途 |
| --- | --- |
| `Assets/Scenes/AutoTwinMain.unity` | 主场景，已列为第一个构建场景 |
| `Assets/AutoTwin/Generated/EV01 Demo Vehicle.prefab` | 可交互的临时纯电车，包含 13 个部件绑定 |
| `Assets/AutoTwin/Generated/EV01 Vehicle Data.asset` | 整车概览、模拟参数、部件类型与稳定 ID |
| `Assets/AutoTwin/Runtime/VehicleViewport.cs` | 鼠标手势、UI 输入隔离、平滑镜头与视口射线 |
| `Assets/AutoTwin/Runtime/VehicleRig.cs`、`VehiclePart.cs` | 选中、局部材质高亮、铰链、轮轴和视图切换 |
| `Assets/AutoTwin/Runtime/ConsoleView.cs`、`VehicleCatalog.cs` | UI 状态、部件参数和车型配置 |
| `Assets/AutoTwin/Editor/AutoTwinBuilder.cs` | 菜单 `AutoTwin > 重建第一阶段演示场景` 与 `AutoTwin > 刷新所选车辆绑定` |

场景、布局、材质及临时车辆包含已生成的资源，打开主场景即可运行。重建菜单会覆盖演示场景与临时车辆资源，保留已有样车参数资产。接入正式模型时，可复制主场景和车辆 Prefab 后继续制作。

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
