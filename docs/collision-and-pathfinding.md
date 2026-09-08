# 建筑碰撞与人物寻路：接入调查

记录日期：2026-09-08。结论是：贴图、建筑通行格、人物碰撞框与寻路有关联，但不是同一个东西。旋转必须同步建筑的通行布局，不能只旋转 sprite 或交换放置框的宽高。

## 证据与版本边界

已取得公开的建筑/地图字段文档，并阅读一份游戏反编译快照。后者是第三方托管的公开实现，不是官方开源仓库，也不是用户本机程序集；固定提交为 `5225ef409e42a6159a82cf81200bf6eb315c9961`，程序集版本标记为 **1.6.8.24119**。以下涉及具体方法的观察仅针对该快照，实际接入前要在用户版本复核。[版本标记](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/Properties/AssemblyInfo.cs)

公开资料可以确认数据结构与参考实现。后续用户已上传解包 Content，已核验该快照内牛棚的尺寸、门位置、默认通行配置和贴图尺寸，见 [Content 核对记录](content-audit.md)。用户的可执行游戏环境、存档及其他 mod 修改后的最终运行时数据仍不可访问，精确游戏版本也未由该包确认。

## 原版如何区分这些信息

| 信息 | 作用 | 对旋转的影响 |
| --- | --- | --- |
| `Texture`、`SourceRect`、绘制层 | 决定外观和遮挡 | 重构四向贴图；透明度不改变通行规则 |
| 建筑位置、占地尺寸、`CollisionMap` | 决定建筑范围内哪些格挡路 | 按当前建筑实例的朝向转换尺寸和逐格阻挡分布 |
| `HumanDoor`、`AnimalDoor` | 定义人物/动物入口 | 同步入口位置、区域及对应外向方向 |
| `AdditionalPlacementTiles` | 放置时额外要求清空或可通行的区域 | 门前等区域也要按朝向校准，不能只检查主矩形 |

`Data/Buildings` 将这些字段分开定义。`CollisionMap` 中 `X` 表示阻挡、`O` 表示建筑自身允许通行；省略该图时，默认整个建筑尺寸范围阻挡。马厩是文档中的例子：其范围内有部分格可以走。因此，“属于建筑占地”不总等于“这一格不可走”。建筑允许通行也不保证世界中可走，地形或其他物体仍可能阻挡。[建筑字段文档](https://stardewvalleywiki.com/Modding:Buildings)

地图本身另有 tile 属性：`Buildings` 层通常挡路，`Passable` 可改变规则；`NPCPassable`、`NoPath` 等还区分 NPC 的通行和寻路。地图层名 `Buildings` 与农场中可搬动的建筑实例需要区分。旋转一栋牛棚时，不应把下面的河流、悬崖或其他地图属性整片旋转。[地图与属性文档](https://stardewvalleywiki.com/Modding:Maps)

在核对的实现中，`Building.GetBoundingBox()` 根据建筑位置与占地格生成世界像素矩形；`intersects()` 先检查矩形相交，再对相关格调用 `isTilePassable()`。后者把世界格转为建筑局部格，查询建筑数据。它没有读取贴图的透明像素来决定墙在哪里。[建筑实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley.Buildings/Building.cs)

玩家也有独立碰撞框：该快照的普通步行 `Farmer.GetBoundingBox()` 返回 48×32 世界像素的矩形，骑乘时采用坐骑碰撞框。它不是人物整张立绘的轮廓。这个数值只作实现观察，不作为我们跨版本硬编码的参数。[玩家实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley/Farmer.cs)

`GameLocation.isCollidingPosition()` 综合建筑、地形、物件和角色等检查；其中还对 NPC 的人物门、动物的动物门设置通行例外。仅调用名称类似“是否可通行”的某个辅助方法，不能假定检查了所有这些条件：该快照的 `GameLocation.isTilePassable(Vector2)` 主要读取地图层属性。[地点碰撞实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley/GameLocation.cs)

## 寻路与碰撞为何要分别验证

通用 `PathFindController.findPath()` 在四邻格中搜索，并调用地点碰撞检查筛掉障碍；NPC 日程使用的 `findPathForNPCSchedules()` 则有独立判断，查看地图、门、`NPCPassable`、`NoPath`、传送点及地形等条件。这不是根据画面找路，也不是所有角色都共享同一张完整导航图。该类还保存已计算出的路线；建筑改变后旧路线是否会及时作废，不能只凭新碰撞正确就假定成立。[寻路实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley.Pathfinding/PathFindController.cs)

特别需要处理朝下入口的假设：该快照的地点碰撞逻辑将 NPC/动物门的允许区域向下增加一格；动物离开建筑时检查动物门及其下方区域，并设为朝下移动。动物回家则可把建筑当前动物门作为寻路终点。只更新门坐标无法自动旋转这些区域检查和移动方向。[地点实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley/GameLocation.cs)、[动物实现](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley/FarmAnimal.cs)

## 对本项目的具体要求

每栋建筑以自身朝向提供占地、阻挡格、入口、门外通道与额外放置区；布局可以从基准朝向变换，也可以为重构后的门按朝向明确配置。建筑的可通行洞口要跟着旋转，人物自身的碰撞框和农场底图不随建筑旋转。Reveal 只改视觉，不能把墙变成可走格。

按实例保存布局，不能直接修改全局同类建筑的数据对象：参考实现的 `GetData()` 从按建筑类型索引的数据表取对象，通行图还会缓存解析结果。应核验实例适配与缓存失效方式，避免转一栋牛棚却影响其他牛棚，或图已改而通行缓存仍旧。[建筑数据访问](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/Stardew%20Valley/StardewValley.Buildings/Building.cs)、[通行图解析与缓存](https://github.com/Dannode36/StardewValleyDecompiled/blob/5225ef409e42a6159a82cf81200bf6eb315c9961/StardewValley.GameData/StardewValley.GameData.Buildings/BuildingData.cs)

后续已实现框内／框外格与区域变换、不可变逐格阻挡数据、实例布局、多格门与门前区域，以及保留角色碰撞框尺寸的候选落点计算；详见[核心接口说明](layout-core.md)。当前 63 组独立自检使用明确的样例和模拟世界检查，**仍没有接入真实 `CollisionMap`、游戏额外放置检查或 NPC 寻路**。本项目未添加猜测的运行时补丁。

下一步接入时，先读取实际建筑数据并加开发用叠加显示：分别画占地边界、真正阻挡格、人物碰撞框和门前区域，用不对称的通行缺口验证四个朝向。检查放置后新旧占地是否正确释放/阻挡，门前整条路线是否可达，以及同类型多栋建筑是否互不影响。再分别验证步行、NPC 和动物进出、已有路线遇到旋转后的障碍、保存重载；这些都属于待完成的验收。
