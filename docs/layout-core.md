# 碰撞布局与门区核心接口

这些接口不引用游戏。它们计算几何与候选落点，不注册补丁、写入角色位置、改变门状态或运行寻路。原有核心测试使用通用样例；新增[数据层](content-mapping.md)另有用户上传 Content 的静态字段验证，仍非游戏运行时测试。

## 同一栋建筑读取同一个布局快照

| 类型 | 责任 |
| --- | --- |
| `Footprint` | 基准朝向的占地尺寸；格子／框外偏移／矩形区域的四向变换与逆变换 |
| `CollisionMask` | 不可变的基准逐格阻挡数据，只描述占地范围内此建筑的阻挡 |
| `BuildingLayoutDefinition` | 共享的基准碰撞、以 ID 区分的门、附加净空，以及逐朝向的重构门覆盖 |
| `BuildingLayout` | 一栋建筑某次位置和朝向的不可变快照；提供当前门区、附加区域和碰撞查询 |
| `DoorRegion` | 已定向的多格门及外向方向；门外区域、合并通道、角色出门候选位置 |
| `TileRectangle` / `PixelRectangle` | 明确区分格子与世界像素，采用右／下边界不包含的矩形 |
| `ExitPlacement` | 经过调用方检查的角色候选位置、碰撞框和外向方向，不代表已传送 |
| `PlacementRequirement` | 附加放置区域与 `OnlyNeedsToBePassable`；一起旋转、位移，不自动变成碰撞 |

`BuildingLayoutDefinition` 会复制传入的门字典、覆盖字典和附加区域集合；`CollisionMask` 也复制阻挡集合。公开集合只读，区域本身不可变。不同建筑可共享一份定义，分别持有自己的 `PlacementPose` 和 `BuildingLayout`。调用 `At` 从原始定义生成新快照，旧快照不变；不要把其中一栋的朝向写回共享的游戏类型数据。

接入层仍需按游戏建筑身份保存当前快照，以及处理数据内容变更、升级、缓存重建、存档和联机。核心不负责辨认游戏中的建筑对象。

## 坐标与通行查询

所有变换均以未旋转、入口正向为 South 的源占地为基准。`TransformCell` / `InverseTransformCell` 继续严格检查格子是否在相应占地框内；门前地块等框外格子用 `TransformOffset` / `InverseTransformOffset`。`TransformArea` / `InverseTransformArea` 接受完全在框外或跨越框边的矩形，不做裁剪。矩形边界与格子原点的公式不同，不能只转左上格后照抄原宽高。

`ReorientKeepingCell` 是可选的格子级锚定：调用方指定一个源格，旋转时补偿包围框原点，让该源格的世界位置保持不变。它没有选定最终鼠标抓取策略，也不是屏幕像素、贴图屋檐或摄影机坐标变换。

`CollisionMask.FromRows` 仅接受完整、等宽的规范化 `X` / `O` 行，`X` 阻挡，`O` 表示此建筑不阻挡；不自动删空白、补短行或吞未知字符。该便捷入口不是原版 `CollisionMap` 字符串解析器。也可以显式传入阻挡格集合；没有通行图而需要实心占地时调用 `Solid`。一份空的阻挡集合与实心占地不同，不应因数据缺失就误用空集合。

`BlocksLocal` 反查源碰撞图；`BlocksWorld` 先减当前建筑原点，再反查。框外返回 false 表示此建筑的掩码没有挡住该位置，**不等于该位置在整个世界可走**。`IntersectsWorld` 接受完整角色世界像素碰撞框，检查它覆盖的全部相关阻挡格；恰好贴边不视为重叠。

当前碰撞掩码限于源占地框内，框外阻挡格会被明确拒绝。如果实际建筑数据在 `TilePropertyRadius` 等范围内定义了框外阻挡，需要扩展并验证表示方式后再支持，不能静默裁掉它们。框外“附加放置净空”目前只作为区域数据变换，不直接变成建筑阻挡。

数据中的附加放置检查使用 `SourcePlacementRequirements`／`LocalPlacementRequirements`，不同于纯几何 `SourceClearance`。布尔检查类型保持不变；`AdditionalTilePropertyRadius` 同样从定义保留到布局，实际游戏查询、可建性与可通行性检查仍未执行。Farmhouse 的框外阻挡已由读取层保存，但映射核心时会明确拒绝。

## 多格门、重构入口和门前区域

`DoorRegion` 不再要求门是外边缘上的单个格：可以是多格、内凹或处于框外的门区。它有自己的外向 `Direction`；基准建筑上的侧门旋转后仍从该门正确的一侧向外走。旧 `Doorway` 接口保留，继续用于单格边缘门的简单场景。

- `ApproachArea(depth)` 是门外相邻、与门同宽的净空条带，不包含门本身，深度必须大于零。
- `PassageArea(outwardDepth)` 包含门区及其向外延伸；深度可为零。它可供之后适配原有“门加下方一格”的判断，但当前不修改游戏判断。
- `orientedDoorOverrides` 按建筑朝向和既有门 ID 覆盖门区；覆盖值已经处于目标朝向，不会再次旋转。未覆盖的门仍从基准定义转换。

门区不自动在碰撞掩码中挖洞，也不会自动赋予 NPC／动物通行豁免。门前区域应从解析完覆盖值的 `LocalDoors` 计算；通用 `SourceClearance` 只机械转向，若其某块代表重构前的旧门通道，接入数据时必须更新该定义，不能指望它自动跟随门的位置覆盖。

## 角色落点计算

`TryGetExit` 接收建筑世界格原点、每格世界像素数、角色碰撞框相对角色位置的偏移和尺寸、门外深度、门外间隔，以及完整世界通行检查回调。这些参数都由调用方明确提供，没有硬编码游戏牛棚或玩家尺寸。

| 门的外向 | 候选角色碰撞框的位置 |
| --- | --- |
| South | 上边放在门区下边之外，水平居中 |
| East | 左边放在门区右边之外，垂直居中 |
| North | 下边放在门区上边之外，水平居中 |
| West | 右边放在门区左边之外，垂直居中 |

角色碰撞框宽高保持原样。先放置碰撞框，再减去调用方提供的相对偏移，得到角色 `Position` 候选值；不能把人物立绘左上角、脚部碰撞框左上角和门格当成同一坐标。

候选框必须完整装进门外条带；宽度或深度不足直接返回 false，不缩小人物、不向远处寻找替代位置。随后 `canOccupy` 接收整个世界像素碰撞框，必须综合地图范围、建筑、物件和角色等规则。检查拒绝时输出 null；异常向上抛出，输出也保持 null，不沿用以前成功的落点。函数不写入任何角色或存档。

这只验证一个候选位置，**不证明门前路线整体可达**，也不替动物设置移动状态或回家路线。几何保留小数像素；接入游戏的浮点／整数转换、取整和实际角色边界计算后，需要用最终将采用的碰撞框再确认，不能拿取整前的检查结果替代实际结果。

## 简单调用示例

以下只有独立布局和样例查询，没有真实游戏数据：

```csharp
var definition = new BuildingLayoutDefinition(
    CollisionMask.FromRows("XOOX", "OXOO", "XXOX"),
    new Dictionary<string, DoorRegion>
    {
        ["human"] = new DoorRegion(new TileRectangle(2, 2, 1, 1), Facing.South)
    });
var first = definition.CreateLayout(new PlacementPose(new TilePoint(10, 20), Facing.East));
var second = definition.CreateLayout(new PlacementPose(new TilePoint(30, 20), Facing.South));
bool buildingBlocks = first.BlocksWorld(new TilePoint(10, 20));
TileRectangle approach = first.LocalDoors["human"].WorldApproach(first.Pose.Origin, 1);
// first 与 second 共享定义，各自的朝向和位置互不改变。
```
