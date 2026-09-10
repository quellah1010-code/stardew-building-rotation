# 空牛棚运行流程与 SMAPI 接入准备

2026-09-10。`BuildingRotation.Runtime` 将现有手势、编辑草稿、真实布局和朝向元数据串联；`BuildingRotation.Smapi` 加载／诊断入口现已用上传 DLL 编译成功，但上传的 SMAPI 版本与启动截图不一致，尚无游戏加载结果。**仍没有能游玩的旋转 mod：真实游戏宿主和运行时补丁尚未实现。**

## 最新：本机文件核对与首次入口构建

用户上传的两个 ZIP 均可正常解压，启动截图也已读取。游戏为 Windows 11 上的 Stardew Valley 1.6.15 build 24356，截图显示 SMAPI 4.5.2、Let's Move It 0.6.20。以下为上传文件本身的元数据，不以截图替代二进制检查：

| 上传文件 | 实际版本／内容 | SHA-256 |
| --- | --- | --- |
| `Stardew Valley.dll` | 程序集及文件版本 `1.6.15.24356`，目标 .NET 6 | `7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee` |
| `StardewModdingAPI.dll` | 程序集、文件、产品版本均为 `4.3.2`；`Constants` 初始化中的 API 版本字符串也为 `4.3.2` | `2c03e8d3028977bbe8d128975ff091e1104df31ae2b08724e72888fca702b0a8` |
| `LetsMoveIt.dll` | `0.6.20.0`，引用 SMAPI `4.5.2.0` | `38c746e638cc00bbba245b654dbfa1ee53008c198c8ff8131efbefc9cf7bfb12` |

游戏 runtimeconfig 标记 `net6.0`，随游戏运行框架为 `6.0.32`。此前“没有程序集”的阻塞已部分解除；**现在缺的是与正在运行的 SMAPI 4.5.2 对应的文件，以及实际游戏适配需要的附属引用**。不能把此次构建写成“SMAPI 4.5.2 编译／加载通过”，也不能据此判断用户当前安装损坏。文件为什么不一致尚未确认。

使用 .NET SDK 8.0.424、官方 .NET 6.0.36 targeting packs，对 `BuildingRotation.Smapi` 进行了 Debug 和 Release 标准 MSBuild 构建，均成功，无编译警告／错误。入口增加 `GameLaunched` 时报告，并用公开 `ModRegistry.IsLoaded` 报告 Let's Move It 是否加载、是否已进入存档；不读取其私有选择状态，不改配置，不拦截输入。`IsLoaded` 不等于其 `ModEnabled` 设置开启，更不等于已适配。

```sh
dotnet build src/BuildingRotation.Smapi/BuildingRotation.Smapi.csproj -c Release -p:GamePath="/path/to/matching-game-files"
```

本次只验证加载入口的构建；没有游戏进程、没有执行 `br_status`，没有发出可玩测试包。Core／Runtime 逻辑未改，不重复跑此前 100 项自检；本次不能新增“100 项实机通过”的说法。

### 下一包需要的文件

从**截图中正在启动的那套安装目录**复制以下文件，原安装文件保持原样。不要从另外的备份或旧目录取：

- `StardewModdingAPI.dll`：重新取一次，用于对齐截图的 4.5.2。
- `MonoGame.Framework.dll`、`xTile.dll`、`StardewValley.GameData.dll`：上传游戏 DLL 确实引用了这三项；后续建筑数据、矩形和地图适配要用。
- `smapi-internal` 文件夹：需要其中匹配的 `SMAPI.Toolkit.CoreInterfaces.dll`、`SMAPI.Toolkit.dll` 和 `0Harmony.dll` 等依赖。拿到后核对实际目录及版本，不把它们发布进自己的包。
- 已安装 Let's Move It 的 `config.json`，如存在：Nexus 原始下载 ZIP 没有用户实际按键、复制／多选等设置。

已收到游戏主 DLL、runtimeconfig 和搬动 mod 下载包，不需要重复上传 Content、存档或整个游戏。若目录中找不到上述文件，让用户发目录截图再定位；不要求安装新 SMAPI 或替换其现有文件。

### Let's Move It 0.6.20 的实际接入点

用 ILSpy 9.1.0.7988 读取上传 DLL；反编译内容只留在临时参考目录，没有复制到公开仓库。缺少 MonoGame／xTile 等引用，因此部分图形类型的反编译带未解析标记；本轮只核对明确的选择字段与控制流，不把反编译产物当作可编译源码。DLL 产品版本标记的上游提交为 `00195e510db28e647c518316d1247790343572ff`。

- `LetsMoveIt.ModEntry` 有私有 `SingleTarget`、`MultipleTargets`、`Config`；不能仅根据某个键按下就认定拿起了建筑。
- `OnButtonPressed` 在搬动键按下时调用 `SingleTargetAction`，后者立即检查占用并执行 `CopyTo`／`MoveTo`。后续需要在这次提交发生前协调输入所有权，普通事后输入监听不足以保证长按旋转。
- 拿起通过 `SelectTargetAction`，取消通过 `ClearSelection`。目标 `Target` 提供 `TargetObject`、`TargetLocation`、`TilePosition` 和 `TileOffset`，建筑实际放置用 `tile - TileOffset`；不能把鼠标格直接当左上角。
- 当前 DLL 中的方法是 `Target.MoveBuilding`（上游源码可位于 `Move.cs` 的 partial class 中，并没有 `TargetData.Move` 类型）。同地点移动调用 `buildStructure`，成功后 `performActionOnBuildingPlacement`，并清空 `TargetObject`。
- 跨地点分支会调整室内出口并使用门的 Y 加 1；首版仍只做同一农场的普通空牛棚。具体补丁、候选占地验证及失败后的恢复尚未实现。

后续拿到匹配依赖，先重编译／交付诊断包做本机加载检查，同时继续实现真实宿主和该版本的输入适配。现有独立会话不能被描述为已经接管上述方法。

## 已经可运行验证的部分

`RotationEditor` 接收搬动提供方的当前状态和输入，使用原 `MoveModeGesture`，创建／结束单栋 `RotationSession`。拿起那次点击不放下，长按拖动转一档，松手保持预览，之后短点击提交；失焦或失去输入所有权丢弃未提交草稿。手势阈值和正向拖动对应哪种旋转由适配方传入，没有新增全局启动键。

`RotationSession` 只允许当前受支持的普通空牛棚进入编辑。预览不改宿主；放下前检查建筑是否自拿起后发生变化，再验证放置和门外一格净空，最后交给宿主提交。取消不撤销别的系统随后做的修改，完成的会话不能重复提交。实例身份必须由宿主提供，不能使用坐标或建筑种类作身份。

`FacingData` 为每栋建筑生成 `quellah.BuildingRotation/facing` 数据，取值为 `1:south/east/north/west`，同时保留其他 mod 字段；不重复保存游戏已经管理的位置。缺少本字段视作原始朝向，未知格式明确拒绝读取而不覆盖。这里验证的是字典数据的保存／重建，**尚未接入真实 Building.modData 或游戏存档序列化**。

`RotationQueries` 每次从宿主读取已提交状态，用于建筑阻挡、真实门动作和返回目标。入口要求动作格落在当前门区、人物位于门外且面向门；室内仍使用原房间。返回时核对房间归属，重新按建筑当前位置计算，先取整人物位置，再用对应完整碰撞框检查实际落点。房间后来有摆放可以阻止再次编辑，但不会仅因此阻止玩家返回。返回目标只是待执行数据，不是已经传送。

## 尚缺的游戏边界

`IRotationHost` 是待实现的真实宿主接口。自检中实现的是内存宿主，用于验证调用合同，不能当作原版游戏模拟器。

| 宿主接口 | 实际游戏适配方要完成的工作 |
| --- | --- |
| `Read` | 从单栋真实建筑读取最终运行时数据、位置、modData、房间身份和编辑资格；相关数据变化后修订号也必须变；换存档后旧身份失效 |
| `CanPlace` | 用候选范围替代本建筑旧范围，检查地图、地形、物件、角色、其他建筑及两类附加区域；保留原有放置规则 |
| `CanOccupy` | 检查完整角色／通道矩形与其他世界碰撞，排除本建筑旧几何；Runtime 再合成候选建筑碰撞 |
| `TryApply` | 写入前再核验，并将位置、宽高、门／出口缓存和 modData 一起应用；失败或异常要真正恢复已写内容 |

**接口里写明“原子提交”不等于游戏回滚已经实现。** 内存自检的失败案例发生在写入前；游戏发生半写入后的恢复、其他 mod 的副作用、保存期间的编辑收尾，都仍是宿主适配与实机测试的工作。`CanPlace` 返回 true 也依赖宿主实现正确；本项目没有绕过原版规则的默认“总是允许”宿主。

普通空牛棚是编辑资格范围；其他建筑、动物、升级、联机和搬动 mod 的具体兼容不随这批代码自动获得。查询方法也必须由真实补丁有条件调用，不能替换全世界的碰撞结果。

## SMAPI 入口源码

`src/BuildingRotation.Smapi` 已有工程、manifest 和 `ModEntry`：订阅 `GameLaunched`／`SaveLoaded`／`ReturnedToTitle`，并提供开发控制台命令 `br_status` 报告程序集版本、加载状态与未接通的功能。它目前不接管鼠标，不打 Harmony 补丁，也不写存档。记录的程序集版本不一定等于游戏产品版本；准确版本仍以实际 SMAPI 启动信息和安装文件交叉核对。

工程显式引用本机游戏和 SMAPI DLL，引用设置 `Private=false`，不把游戏程序集打进输出包，也不自动部署。`net6.0` 已与本次游戏文件对齐；`MinimumApiVersion=4.0.0` 仍是参考期起点，**并非完整玩法已核定的支持范围**。实际建筑接入时仍需补齐全部引用，而非造同名桩 DLL。

```sh
dotnet build src/BuildingRotation.Smapi/BuildingRotation.Smapi.csproj -p:GamePath="/path/to/game"
```

可传 `-p:GameTargetFramework=netX.Y` 指定已核验的框架。此前仅运行过缺少 GamePath 的检查；最新已进行真实 DLL 构建，结果及版本差异见本文开头，游戏加载仍未验证。

加载入口参照 [SMAPI Mod 基类](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/Mod.cs) 和 [SaveLoaded 事件定义](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/Events/SaveLoadedEventArgs.cs)。构建路径与后续可选自动配置参考 [SMAPI 构建包文档](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)。本次读取于 2026-09-10，未复制上游实现；版本接入时仍需固定来源。

## 本批验证与真正的下一步

本批五个无游戏依赖工程标准 MSBuild 构建成功，运行标准构建产物得到 **100/100 通过**。自检新增 12 组，覆盖真实 Barn 四向提交与元数据重建、碰撞／门／返回一致性、另一栋建筑不变、非法地块／堵门、外部修改、宿主异常、取整后的碰撞框、重复搬动，以及拾取—旋转—松手—点击放置的完整调用流程。原始上传 ZIP 当前无法由 zipfile 打开，因此使用此前已核验并保存的真实字段摘要，不重报原 ZIP 检查通过。

接下来按本文开头对齐 SMAPI 与附属引用，重编译并加载诊断入口，再实现真实宿主和搬动提供方，随后逐项接碰撞、门、返回、绘制和游戏存读档。可以用方向与入口可辨认的占位图，不等全部室内或马厩方案定稿。
