# 用户提供的 Content 数据核对

来源为本会话上传的 `Content (unpacked).zip`，本轮直接读取压缩包中的 JSON、PNG 和 TMX。以下是这份 Content 快照中的数据，不保证等于其他 mod 修改后的运行时结果；包内没有游戏／SMAPI DLL，也没有可确认精确游戏版本的版本清单。

已读取 `Data/Buildings.json` 的 25 个建筑定义，并核对其主贴图的 PNG 尺寸。供接续使用的数字、区域与来源哈希在 [content-building-facts.json](reference/content-building-facts.json)。该文件仅保留项目需要的事实摘要；不提交原始贴图、地图文件或整个 Content。

## 首批建筑的实际字段

坐标从源占地左上角 `(0,0)` 起，入口方向为 South；动物门按 `(x,y,宽,高)` 记录。

| 建筑 | 占地 | 人物门 | 动物门 | 本份数据的通行图 |
| --- | --- | --- | --- | --- |
| Barn | 7×4 | (1,3) | (3,3,2,1) | null，采用建筑默认实心规则 |
| Big Barn | 7×4 | (1,3) | (4,3,2,1) | null |
| Deluxe Barn | 7×4 | (1,3) | (4,3,2,1) | null |
| Shed | 7×3 | (3,2) | 禁用 | null |
| Big Shed | 7×3 | (3,2) | 禁用 | null |
| Stable | 4×2 | 禁用 | 禁用 | `XXXX` / `XOOX` |
| Coop | 6×3 | (1,2) | (2,2,1,1) | null |

原有核心里的 7×4 此前只是测试样例；现在能确认上传包内 Barn 恰好也是该尺寸，但不能反过来说此前已经读到了真实数据。前面红屋顶侧视图的美术参考可对应包内 `Buildings/Shed.png`；Shed 的 7×3 不能混用作 Barn 的占地。首个可玩工程切片仍按已定的空牛棚推进。

三种牛棚的外部占地相同，但普通牛棚升级后动物门的 X 会从 3 变为 4；升级适配不能只换一张图片。

## 贴图与室内地图

Barn、Big Barn、Deluxe Barn 的 PNG 都是 **112×128**，主体 `SourceRect` 是 `(0,0,112,112)`；底部从 Y=112 起另有动物门图层取图区域。普通牛棚共有四个动物门绘制层，包含各自的 `DrawPosition` 和 `AnimalDoorOffset`。因此不能把 PNG 总高度直接作为建筑主体高度，更不能把整张图旋转当作侧视素材。

Shed 的 PNG 是 **112×128**，`SourceRect` 在解包数据中为全零，`DrawLayers` 为 null；Stable 的 PNG 是 **64×96**。零源区域需要结合游戏的默认全图语义解析，不能构造一个零宽高的 `PixelRectangle`。源像素裁切、地面占地、世界像素和摄影机缩放是不同层次；默认源区域与额外图层的字段意义见[建筑文档](https://stardewvalleywiki.com/Modding:Buildings)。

另已读取 Barn／Barn2／Barn3、Shed／Shed2 和 Farm 的 TMX 元数据：这些地图的源 tile 尺寸都是 16×16。Barn 室内为 18×15，Shed 室内为 13×14；其原始 Warp 属性分别包含 `11 15 Farm 14 11` 和 `6 14 Farm 21 22`。这是内容文件中的出口配置，不能把其中 Farm 的固定目标当成每栋搬动后建筑的最终室外落点。旋转后室内地图保持不变，返回点由当前外部布局和真实游戏传送逻辑接入。

## 真实数据暴露出的接入差异

1. **碰撞文本包含缩进和首尾空白。** 本包 Stable、Cabin、Pet Bowl、Farmhouse 都是带换行／缩进的字符串。数据读取层要按原格式规范化，核心的严格 `CollisionMask.FromRows` 不直接承担原版字符串解析。原版字段文档说明会裁去整个文本及各行首尾空白。[碰撞字段](https://stardewvalleywiki.com/Modding:Buildings)
2. **Farmhouse 确有框外阻挡。** 标称占地是 9×5，但规范化的末行 `XXXXOOOXXX` 有 10 列，其中 `(9,4)` 是框外阻挡格；`AdditionalTilePropertyRadius` 为 1。当前核心掩码只支持框内格子，对它不能静默裁剪或直接套等宽 `FromRows`。需扩展数据表示与范围查询，或明确将此类型暂列为未适配。
3. **附加放置区域带有检查类型。** Farmhouse 在 `(9,4,1,1)` 要求普通建造检查，在 `(5,5,1,1)` 只要求可通行；Greenhouse 的 `(2,6,3,2)` 也只要求可通行，半径为 2。读取时必须保留 `OnlyNeedsToBePassable`；当前 `SourceClearance` 仅储存几何区域，不包含该布尔语义，不能在映射时丢掉。[附加区域字段](https://stardewvalleywiki.com/Modding:Buildings)
4. **禁用门不是一个实际矩形。** Shed 等建筑使用 `HumanDoor=(-1,-1)` 或动物门的负坐标／零尺寸表示未启用的门。映射层要保留无门状态，不能强行创建 `DoorRegion`。
5. **数据快照不等于运行时签名。** Content 能补足资产数据，不能提供 Harmony 要补的方法签名、SMAPI 版本、正在使用的搬动 mod 状态，或其他 mod 最终修改后的字段。后续应读取实际运行时定义，保持实例隔离。

初次核对提交 `4166984` 仅整理数据，当时的 63 项属于之前的核心测试。2026-09-09 已续做真实读取／映射及素材锚点，新增数据与画稿坐标测试，含原始 ZIP 时 89/89 通过；标准工程构建也通过。详见 [content-mapping.md](content-mapping.md)。仍没有运行时适配或游戏内验证。

## 用户提到的农场规划器

找到可能对应的 [Stardew Planner V3](https://v3.stardew.info/planner/)，其作者仓库为 [hpeinar/planner_v3](https://github.com/hpeinar/planner_v3)。用户未提供精确链接，因此暂记为候选参考。网页说明包含完整建筑绘制、放置限制提示、撤销／重做，以及可建／可耕区域显示。

对本项目有用的是研究格子上的占地提示、整张建筑贴图与地面基准的对齐，以及遮挡下的预览可读性。这里是参考方向，不是已采用的新玩法，也不是要求另做一个网页版农场规划器。该站的快捷键、多选确认和删除规则不自动进入本 mod；不向该站上传用户存档，也不复制网站或上游素材代码。

## 下一步交付

真实字段映射、Barn／Shed／Stable 独立验证、附加检查类型保留及四向锚点已完成。农舍框外数据保留但拒绝生成当前布局。下一步由助手按[底稿](art/README.md)继续原生分层像素画；有经过核验的游戏环境后再接 SMAPI，不猜测补丁签名。
