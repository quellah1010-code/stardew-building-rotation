# 拿起后未进入旋转：0.1.1 修正与实机排查

## 实机证据

用户在单人测试档通过 `debug build Barn` 生成普通牛棚。0.1.0 的 `br_status` 确认游戏 1.6.15.24356、SMAPI 4.5.2，显示 `ordinary empty Barn prototype attached; placeholder graphics`；设置 enabled=True、copy=False、multi=False。这确认加载流程报告补丁安装成功，不证明具体选择回调已执行。

随后用户拿起建筑，提供：

    Move observation: building=Barn; origin=(41,57); footprint=7x4; enabled=True, copy=False, multi=False
    Move observation: nothing held; enabled=True, copy=False, multi=False

所提供片段没有 `Rotation preview active` 或拒绝原因。探针看到普通牛棚，但旧包的提前返回没有诊断，不能由这两行单独确定用户当时的具体拒绝分支。

拿起那次左键一直不松、只松 Alt 再拖动，当前手势会忽略该初始按住。应松开拿起按键后再长按侧拖；Left Control 在 Let's Move It 0.6.20 中是强制放置。本轮不修改手势或要求用户反复试旧包。

## 已确认的代码缺陷

工作区恢复后，读取用户提供的原始 `Data/Buildings.json`：普通 Barn 的 `BuildingType=null`、`IndoorMap=Barn`、`IndoorMapType=StardewValley.AnimalHouse`。其 `IndoorItems` 包含默认固定取草槽：`Id=Default_FeedHopper`、`ItemId=(BC)99`、`Tile=(6,3)`、`Indestructible=true`。

用户提供的 1.6.15 `Building.InitializeIndoor` 实现会在建造初始化时把该设施加入房间 `objects`，并设 `fragility=2`。旧 `IsEmpty` 要求 `objects` 为零，因此会拒绝正常新建牛棚。这个缺陷已有数据与代码证据；用户游戏里的完整选择路径仍需新包日志验证。

## 0.1.1 修改

- 仅放行实际建筑数据所声明位置的固定 `(BC)99` 取草槽；额外箱子、干草、放错位置或非固定的物件仍会拒绝。默认设施保留原样，不移动、不删除，也不新增打包功能。
- 普通 Barn 类型、施工／升级、动物、家具、角色、玩家、地形与单人限制保持。室内和外部的旋转规则不变。
- 选择被拒绝时打印配置、世界状态或建筑快照；包括运行时类、施工状态、室内类与各类内容数量，以及 `onlyBuiltInObjects`。
- `br_status` 显示 active 和最后一次选择尝试。取消或切到控制台失焦后保留最后结果；换存档时清除。此前 try 外的读取也纳入异常记录；首次采样已终止时不再误报预览成功。

## 验证与下一步

使用已提供的匹配引用完成 0.1.1 Release 构建。新增两组固定设施／额外物件回归检查，**102/102 独立检查通过**。这不执行游戏，不证明实机旋转已修好。补丁目标和输入手势未改，不重复无关签名审计。

诊断代码起初在执行环境 unavailable 时保存为草稿；该阻碍已解除，新包见 [prototype-test.md](prototype-test.md)。不需要重传 DLL、重新建档或移除取草槽。

用户换包后先拿起现有普通牛棚：若出现彩色格子与 `Rotation preview active`，继续测长按侧拖；否则贴 `Rotation selection rejected`，或运行 `br_status` 并贴 `Rotation selection`。若最后尝试仍为 `no selection attempt observed since load`，查选择补丁；若 `accepted` 但不转，查采样与事件时序。四向放置、碰撞、进出门和存读档仍待实机验收。
