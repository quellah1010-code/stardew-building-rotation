# 继续开发时从这里接

本记录对应碰撞布局和多格门核心完成后的状态。开始续做时先读取仓库最新提交和工作区修改，再核对本文；不要假定临时工作目录、SDK 或已经运行的进程会跨执行保留。

## 本轮已完成

用户已授权实现路线图第 1–2 项。现已加入不可变阻挡掩码、实例布局、逆变换、框外区域、可选格子锚定、多格／内凹门、按朝向重构门的位置覆盖，以及保留角色碰撞框尺寸的候选落点计算。相关代码在 `src/BuildingRotation.Core`，合同在 [layout-core.md](layout-core.md)。

验证结果为 **63/63 通过**：原有 40 项全部保留，新增 `LayoutTests` 13 项和 `DoorRegionTests` 10 项。17 个核心 C# 文件按 C# 8、netstandard2.1 引用直接编译；自检按 C# 12、net8.0 引用直接编译，nullable 和警告视为错误均开启。没有执行游戏内测试，也没有完成标准 MSBuild 构建。

随后用户已上传 `Content (unpacked).zip`，现已核对其中 25 个建筑定义、主贴图尺寸及相关室内地图元数据。先读 [content-audit.md](content-audit.md) 和 [content-building-facts.json](reference/content-building-facts.json)：普通牛棚 7×4、Shed 7×3、马厩 4×2；农舍存在框外阻挡，附加放置区带有 `OnlyNeedsToBePassable` 语义。此前测试样例的来源不变，不能改称已经使用实机数据测试。包内没有游戏／SMAPI DLL，精确游戏版本和运行时补丁签名仍未核验。

## 接着做什么

1. 从最新上传内容及已提交事实摘要继续，完成真实字段到核心的读取／映射，先验证 Barn、Shed、Stable。不要重复已完成的核心。规范化 `FromRows` 不是游戏解析器；农舍的框外阻挡不能裁掉，额外放置区的检查类型也不能丢失。暂存上传包路径为 `/workspace/scratch/0dfc15d37142/upload/01-Content-unpacked-.zip`；缓存丢失时从本会话可用附件恢复，或先以带来源哈希的摘要推进数据层。
2. 有真实游戏环境后，建立 SMAPI 入口并编译，再适配原版及首个确认兼容的移动流程。移动状态与鼠标输入协调尚未实现，不要因独立手势通过就标记兼容。
3. 缺少程序集时继续数据映射与美术分层。Barn 的 PNG 总尺寸 112×128，但主体只取 112×112；Shed 红屋顶原图也已找到，不能把其 7×3 占地混作 Barn 的数据。用户提供的农场规划器线索只作交互参考，不另造网站或照搬快捷键。记录具体缺口，不假造游戏签名或实机结果。
4. 同步 [roadmap.md](roadmap.md) 与实际提交，说明完成内容、验证方式和剩余依赖。若别的执行已更新仓库，保留并协调其改动，不能强推或覆盖。

首个可玩目标仍是 PC 键鼠、单人、普通空牛棚，可先用占位图；动物出入、NPC 路径、升级、联机和全建筑素材都仍在后续范围。用户不需要替助手写代码或画图。

## 验证环境恢复

正常开发环境优先执行 README 的 `dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj`。

此前临时环境用 .NET SDK 8.0.424、运行时 8.0.30，SDK 在 `/workspace/scratch/0dfc15d37142/tooling/dotnet8`。进程信息接口使标准 CLI / MSBuild 失败，直接调用 `dotnet SDK路径/Roslyn/bincore/csc.dll @响应文件` 可用。`tooling/verification` 下的 `core.rsp` 和 `tests.rsp` 已含当前全部源码引用，随后用 `dotnet BuildingRotation.Core.SelfTest.dll` 执行。

这些路径是可丢失的缓存。如果仍在，先检查响应文件是否包含最新源码；如果不在，重新使用可用的正常 SDK 构建，或重建响应文件：核心引用 `NETStandard.Library.Ref/2.1.0/ref/netstandard2.1`，自检引用 `Microsoft.NETCore.App.Ref/8.0.30/ref/net8.0` 和生成的核心 DLL，补齐项目的隐式 using，生成对应 runtimeconfig。不要把源码直接编译描述成完整工程构建成功。

## 继续保持的设计边界

不新增全局启动快捷键，旋转编辑跟随已适配的建筑移动模式；日常外观、碰撞、门和 Reveal 在退出编辑后仍应工作。室内不转。背面显示的门必须对应真实门；透明分层不改变通行。

通用几何不代替游戏世界检查。`TryGetExit` 成功不是已经传送，也不证明从门到外部路线畅通；游戏实际取整后的碰撞框还需验证。当前类型级数据保持不可变，游戏对象身份、更新、缓存、存档和实际补丁仍由后续接入完成。

公开仓库保存自有源代码和文档；游戏 DLL、解包原版素材、个人存档与含私人信息的日志不提交。自动续做的调度保存在本会话的任务中；本文件是接续记录，本身不会启动任务。
