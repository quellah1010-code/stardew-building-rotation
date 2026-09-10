# 空牛棚运行流程与 SMAPI 接入准备

2026-09-10。本批新增 `BuildingRotation.Runtime`，将现有手势、编辑草稿、真实布局和朝向元数据串联；另有 `BuildingRotation.Smapi` 加载／诊断入口源码。**仍没有能游玩的旋转 mod：真实游戏宿主和运行时补丁尚未实现。**

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

`src/BuildingRotation.Smapi` 已有工程、manifest 和 `ModEntry`：准备订阅 `SaveLoaded`／`ReturnedToTitle`，并提供开发控制台命令 `br_status` 报告程序集版本与未接通的功能。它目前不接管鼠标，不打 Harmony 补丁，也不写存档。记录的程序集版本不一定等于游戏产品版本；准确版本仍以实际 SMAPI 启动信息和安装文件为准。

工程显式引用本机游戏和 SMAPI DLL，引用设置 `Private=false`，不把游戏程序集打进输出包，也不自动部署。默认 `net6.0` 与 `MinimumApiVersion=4.0.0` 是 1.6／SMAPI 4 参考期的工程起点，**并非已核定的支持版本**；拿到实际程序集后复核目标框架和全部引用。如本机需要其他游戏依赖，应按实际编译错误补对应引用，而非造同名桩 DLL。

```sh
dotnet build src/BuildingRotation.Smapi/BuildingRotation.Smapi.csproj -p:GamePath="/path/to/game"
```

可传 `-p:GameTargetFramework=netX.Y` 指定已核验的框架。本轮只运行了 `VerifyGameReferences` 目标，按预期指出缺少 GamePath；**没有编译或运行 SMAPI 入口**，不应将 Core／Runtime 自检视为入口构建成功。

加载入口参照 [SMAPI Mod 基类](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/Mod.cs) 和 [SaveLoaded 事件定义](https://github.com/Pathoschild/SMAPI/blob/develop/src/SMAPI/Events/SaveLoadedEventArgs.cs)。构建路径与后续可选自动配置参考 [SMAPI 构建包文档](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)。本次读取于 2026-09-10，未复制上游实现；版本接入时仍需固定来源。

## 本批验证与真正的下一步

本批五个无游戏依赖工程标准 MSBuild 构建成功，运行标准构建产物得到 **100/100 通过**。自检新增 12 组，覆盖真实 Barn 四向提交与元数据重建、碰撞／门／返回一致性、另一栋建筑不变、非法地块／堵门、外部修改、宿主异常、取整后的碰撞框、重复搬动，以及拾取—旋转—松手—点击放置的完整调用流程。原始上传 ZIP 当前无法由 zipfile 打开，因此使用此前已核验并保存的真实字段摘要，不重报原 ZIP 检查通过。

接下来拿到匹配的游戏／SMAPI 程序集与搬动 mod 版本，先编译加载诊断入口，再实现真实宿主和搬动提供方，随后逐项接碰撞、门、返回、绘制和游戏存读档。可以用方向与入口可辨认的占位图，不等全部室内或马厩方案定稿。
