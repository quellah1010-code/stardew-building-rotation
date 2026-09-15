# 旋转输入排查：当前 0.1.2

## 2026-09-15 实机结果：旋转及朝东放置已确认

用户在办公室电脑完成安装后反馈“正常了”，提供四张游戏画面和一张日志截图。窗口显示游戏 1.6.15、SMAPI 4.5.2；本批界面和日志符合 0.1.2 的右键旋转／左键放置流程。用户另已澄清：一直在既有旧档中临时测试，不过夜保存，并非新建专用测试档。只保存下面的行为记录，不将截图或个人存档提交公开仓库。

| 观察 | 本批证据及结论 |
| --- | --- |
| 普通 Barn 拿起 | debug build Barn 在 (35,55) 创建普通牛棚；探针记录 7×4，enabled=True、copy=False、multi=False；画面出现 SOUTH 绿色预览 |
| 旋转预览 | 日志依次出现 East、North、West 的 Rotation preview turned；画面可见东／西为 4×7、北为 7×4，青色门标记随向变化 |
| 朝东放置 | 日志明确出现 Rotation committed: Barn / East，随后选择清空；重选时真实位置为 (37,54)，footprint=4x7 |
| 未提交预览结束 | 北向预览结束后重选仍为 (37,54)、4×7；没有对应的 North 提交，不能据此声称北／西已放置。具体是 Esc、失焦或其他结束原因，日志未注明 |
| 当前显示问题 | 再次编辑时旧朝东占位图与新北／西预览同时显示，标签、占地和两套青色门标记发生重叠。记录为预览绘制问题；不由双重绘制推定真实碰撞也重复 |

**本批确认了进入预览、多个方向转向、一次朝东提交及提交后重选读取。** 碰撞、四向进出门、非法位置拒绝、完整取消／失焦操作、同类型实例隔离和保存重进仍未验收。用户不过夜，所以本批没有存读档证据。保持 0.1.2 安装包，下一步先在已放下的朝东牛棚外绕行，再从右侧门外格面向门交互，检查进入同一房间和从右侧返回。显示重叠已登记，后续清理绘制；这次只更新实测记录，没有改代码、重跑测试或重新打包。

## 0.1.2：旋转与放置分键

用户指出左键在原搬动流程中一按就放下，不能只修空房判断后继续要求左键长按。已核对上传的 Let's Move It 0.6.20：左键默认负责拿起／放置，右键没有默认搬动用途。0.1.2 右键长按横拖旋转，左键按下显式提交；右键点击或松手不会放下。Runtime 不再根据旋转按钮的短点击自动提交。原有配置若将右键分配给其他搬动动作，则输出冲突，不改用户配置。

SMAPI 右键按下／松开事件及时采样并抑制原交互，逐帧继续采样；无有效旋转选择时不接管右键。成功转向记录 Rotation preview turned，预览显示左右键用途。104/104 独立检查与匹配引用 Release 构建通过；旋转和朝东提交的实机结果见上节，完整玩法仍待验收。以下保留此前资格排查证据，0.1.1 的左键操作说明已被本节取代。

## 0.1.1 历史：资格修正

## 实机证据

用户在单人旧档中临时测试，通过 `debug build Barn` 生成普通牛棚；此前写成新建测试档有误。0.1.0 的 `br_status` 确认游戏 1.6.15.24356、SMAPI 4.5.2，显示 `ordinary empty Barn prototype attached; placeholder graphics`；设置 enabled=True、copy=False、multi=False。这确认加载流程报告补丁安装成功，不证明具体选择回调已执行。

随后用户拿起建筑，提供：

    Move observation: building=Barn; origin=(41,57); footprint=7x4; enabled=True, copy=False, multi=False
    Move observation: nothing held; enabled=True, copy=False, multi=False

所提供片段没有 `Rotation preview active` 或拒绝原因。探针看到普通牛棚，但旧包的提前返回没有诊断，不能由这两行单独确定用户当时的具体拒绝分支。

0.1.0／0.1.1 会忽略拿起时已按住的左键；这一旧输入安排已被 0.1.2 的右键旋转取代。Left Control 在 Let's Move It 0.6.20 中仍是强制放置。

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
