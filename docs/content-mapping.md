# 真实 Content 读取与布局映射

2026-09-09 已实现并验证。处理上传的静态 Content 或已提交的事实摘要，不是游戏运行时最终数据，也不表示某种建筑已获得实机支持。

| 工程 | 职责 |
| --- | --- |
| `BuildingRotation.Core` | 原有几何、碰撞、门和编辑逻辑；新增带检查类型的 `PlacementRequirement` |
| `BuildingRotation.Data`（netstandard2.1） | 不依赖 JSON、PNG 或游戏的不可变快照、原始碰撞文本解析、映射及素材坐标模型 |
| `BuildingRotation.ContentAudit`（net8.0 控制台） | 离线读取 ZIP／事实 JSON、检查 PNG 元数据、输出映射报告与对齐底稿 |
| `BuildingRotation.Core.SelfTest` | 原有 63 项加数据、素材坐标测试；可选直接验证上传 ZIP |

未来 SMAPI 接入应读取当时有效的游戏字段，构造 `BuildingDataSnapshot`，不应让游戏读取仓库中的旧快照，也不必引用 .NET 8 的离线工具。共享定义仍不可变，建筑实例分别持有布局。

## 输入与保守处理

`ContentReader.ReadZip` 寻找唯一的 `Data/Buildings.json`，按 `Texture` 读取 PNG 元数据，不解压任意文件或运行包中内容。本包对象型坐标及 `"x, y"` 像素偏移已验证；这不是所有 Content Patcher 简写的通用解析器。重复 JSON 键、非法纹理路径、缺失或重复资产明确报错。PNG 检查头部、正尺寸及文件哈希，不代替完整图片解码或 CRC 验证。

`ReadFacts` 支持当前 schemaVersion 1，检查建筑数量、碰撞行长度和框外阻挡的冗余记录。来源哈希不是摘要的数字签名。附带原始 ZIP 的测试会重新计算 JSON／主 PNG 哈希，并比较全部 25 个定义的几何和首帧素材字段。

`ContentCollisionMap` 统一换行，裁去整段与各行首尾空白，保留所有 X 格，包括框外格。null 为默认实心；空白文本、未知字符、内部空行明确拒绝。转入核心前要求完整占地行，拒绝框外阻挡，不补短行、不裁宽行。裁空白与默认实心依据[字段文档](https://stardewvalleywiki.com/Modding:Buildings)；其他拒绝行为是本项目的保守边界，不代表游戏所有容错行为。

人物门 `(-1,-1)`、动物门 `(-1,-1,0,0)` 或缺失字段表示没有该门；其他部分负值／零尺寸组合拒绝转换。门 ID 为 `human`／`animal`，可接收按朝向重构的门覆盖。门不自动挖掉实心碰撞，也不赋予动物通行豁免。

附加放置区映射为 `SourcePlacementRequirements`／`LocalPlacementRequirements`，区域和 `OnlyNeedsToBePassable` 一起保留、旋转和位移，不混入只有几何意义的 `SourceClearance`。`AdditionalTilePropertyRadius` 原值保留，但没有修改游戏属性查询。真正的可建／可通行检查仍由接入层执行。

## 本包结果

| 对象 | 结果 |
| --- | --- |
| Barn | 7×4 实心；人物门 (1,3)；动物门 (3,3,2,1)；四向门区有独立坐标预期 |
| Shed | 7×3 实心；仅人物门 (3,2)；不混用牛棚尺寸 |
| Stable | 4×2，缩进解析为 `XXXX/XOOX`；四向阻挡逐格核对 |
| Greenhouse | 框外附加区域四向变换，保留仅通行检查及半径 2 |
| Farmhouse | 保留宽 9、末行宽 10、框外 (9,4) 阻挡及两类放置检查；拒绝核心布局转换 |
| 全部 | 25 项可读，24 项可生成当前几何布局；不是实机支持清单 |

素材读取提供主体及图层**首帧坐标子集**：源裁切、纹理标识、绘制偏移、动物门偏移、前后层及排序。不实现条件、帧播放、皮肤或动态纹理；事实摘要没有保留所有图层的自定义纹理和动画字段，不能直接用于完整渲染器。

## 复现

从仓库根目录运行（.NET 8 SDK）：

```sh
dotnet run --project tests/BuildingRotation.Core.SelfTest
dotnet run --project tests/BuildingRotation.Core.SelfTest -- --content-zip /path/to/Content-unpacked.zip
dotnet run --project tools/BuildingRotation.ContentAudit -- facts docs/reference/content-building-facts.json
dotnet run --project tools/BuildingRotation.ContentAudit -- zip /path/to/Content-unpacked.zip
dotnet run --project tools/BuildingRotation.ContentAudit -- facts docs/reference/content-building-facts.json --templates docs/art/templates
```

无 ZIP 时 88 项；提供本包增加全包交叉检查，合计 89 项。模板命令更新指定目录内三个 SVG 与锚点 JSON，不复制游戏图片。原始素材无需提交。

直接 Roslyn 编译与标准 MSBuild 构建均已成功；标准构建零警告、零错误，`dotnet run --no-build --no-restore` 的 89 项通过。后备命令：`node scripts/verify-direct.mjs /path/to/dotnet-root --content-zip /path/to/Content-unpacked.zip`；脚本重新枚举源码、生成临时响应文件并嵌入事实夹具，不依赖旧响应文件。

剩余：农舍框外碰撞表示；原生分层画稿；游戏／SMAPI 精确版本和程序集；运行时数据刷新、移动状态与输入协调、碰撞／传送／渲染／存档、动物和 NPC。

受限环境补充：`dotnet run` 曾成功，也在复跑时发生 SDK 启动异常。最新标准构建产物用 `dotnet tests/BuildingRotation.Core.SelfTest/bin/Debug/net8.0/BuildingRotation.Core.SelfTest.dll` 直接运行，88／89 项均通过。该方式仍验证标准工程产物，不等于改用手工源码编译；环境包装器的限制仍须保留记录。
