# 空牛棚游戏接入记录

**当前为可安装实验版 0.1.2：右键长按旋转、左键按下放置，保留取草槽修正及选择诊断；真实宿主、输入、碰撞、门和朝向保存代码已接入，完整玩法尚待实机验收。** 安装与操作见 [prototype-test.md](prototype-test.md)。首版只支持单人、同一农场、普通空 Barn；Big Barn／Deluxe Barn、动物、升级、联机和其他搬动提供方没有自动获得支持。

## 真实环境与已收到的证据

2026-09-15 最新 0.1.2 实机证据：日志确认普通 Barn 朝东／北／西提交及侧向 4×7、北向 7×4 的重选读取；用户确认放下后绕行没有穿墙或外部卡住，各方向均可进门并从对应一侧返回。后续红框／拒绝提示与用户反馈确认非法放置被拒绝，普通 Esc 取消后恢复原位；(32,53)、4×7 的取消后重选记录一致。下一步是副本存读档，其他中断路径未逐项验收。用户继续在旧档不过夜测试；预览重叠待修。见[实机记录](rotation-input-investigation.md)。

此前 0.1.0 实机日志显示 `prototype attached`，探针看到了普通 Barn，但尚无成功旋转预览。原始 Content 和 1.6.15 初始化代码确认默认取草槽被加入 objects，旧零物件限制错误拒绝新建牛棚。0.1.1 只允许实际数据中声明位置的固定设施，其余物品／动物限制不变；选择失败会打印原因，br_status 保留最后一次选择结果。见 [具体排查](rotation-input-investigation.md)。

此前三张截图确认 Windows 11、Stardew Valley 1.6.15 build 24356、SMAPI 4.5.2。0.0.2 诊断包已加载，显示 `read-only selection probe attached`；进入存档后配置为 `enabled=True, copy=False, multi=False`，观察到多次 `nothing held → building=Deluxe Barn → nothing held`。建筑移动后，真实位置从 (20,51) 变成 (38,53)。这证明旧包的加载和只读状态观察；不证明新旋转补丁或豪华牛棚支持。截图中个人标识不发布。

本批需要的引用已齐。游戏主 DLL 为 1.6.15.24356，SMAPI／CoreInterfaces／Toolkit 均为 4.5.2，Harmony 为 2.2.2，Let's Move It 为 0.6.20。最初游戏 ZIP 中的 SMAPI 4.3.2 与截图不一致，后来上传的 SMAPI 和 smapi-internal 已解除该问题。此前逐文件哈希可查 [0.0.2 核对记录](https://github.com/quellah1010-code/stardew-building-rotation/blob/0eb7a3a21ee456474f257b33fc18ab845d6915d6/docs/runtime-adapter.md)。

最新 MonoGame ZIP 同时包含以下三项，不再请求用户补取：

| 文件 | 版本 | SHA-256 |
| --- | --- | --- |
| MonoGame.Framework.dll | 3.8.0.1641 | `92e5423a5d002b399de4369e483577007274c5634745f5414fd508981b7494de` |
| xTile.dll | 1.0.0.0 | `a7c0a758ac446bb4f7715651478e3097b7b3bb6fbd4daca52bfa8e80ee1e7df1` |
| StardewValley.GameData.dll | 1.6.15.24356 | `9c03497c2d2ac24c94e2f25b3c2fc39ecde1bc97341e514c5f9fdcc1e759cb81` |

配置由已有探针读取，不另索要 config.json。所有原始引用与反编译内容只用于本地核对，不入公开仓库。

## 当前接入

详细行为和限制集中在 [prototype-test.md](prototype-test.md)。核心与 Runtime 继续使用现有实现，本轮接入以下游戏边界：

| 组件／接口 | 当前实现 |
| --- | --- |
| `LetsMoveItProbe` | 核对 4.5.2／0.6.20 后绑定实际 mod 实例及私有成员，读取单选、配置、地点、鼠标抓取偏移；已有旧诊断包实机证据 |
| `RotationController` | 拾取后创建编辑会话，在原放置方法前协调输入；右键长按 350ms／侧拖 24px 转一档，右键松手保留预览，左键按下提交；取消、失焦或保存时清理未提交操作 |
| `Read` | 从真实 GetData 映射不可变布局；按 Building 对象登记身份，快照修订包含位置、尺寸、门、元数据、房间、出口和编辑资格变化；换存档清除 |
| `CanPlace` | 候选占地和两类附加区域逐格调用原版 isBuildable，加边界、角色与地面检查；只在本次查询中排除旧建筑，不切换有副作用的 isMoving |
| `CanOccupy` | 用实际完整矩形查地图／物件／角色碰撞；由 Runtime 再合成候选建筑自身阻挡 |
| `TryApply` | 重读快照后重新验证，保存旧字段，再写位置／宽高／门／朝向键和出口；异常时尝试恢复每个旧字段，恢复异常明确报告 |
| `RotationPatches` | 已标记普通 Barn 的逐格通行、矩形碰撞、真实人门动作、返回传送、加载重建和占位绘制 |

编辑要求普通 Barn、原始 Building 类型、单人主玩家、当前农场、未施工／升级且房内除默认固定取草槽外无物品／角色／动物。房间后来放物品会阻止再次编辑，但不会仅因此阻止出门。旋转源格由 Let's Move It 的 TileOffset 逆变换取得，每次转向后仍跟随鼠标。物理按键只采样右键；按下／松开事件及时采样并抑制原游戏交互。左键在搬动的放置入口单独提交，不再等待松手分类。

预览不写游戏对象。提交采用直接更新本建筑必要字段的方式，避免原建造／放置回调删除草、物件或挖掘点；因此本版要求先清理地面。第三方依赖放置回调的行为、游戏内故障恢复与事件副作用仍待验证。没有改变同类型建筑的共享定义。

朝向写入真实 `Building.modData` 的 `quellah.BuildingRotation/facing`，值为 `1:south/east/north/west`，只修改自己的键。位置沿用游戏原字段。建筑数据重新载入、房间重建、出口更新和 SaveLoaded 后重建尺寸、门和 warp；保存前终止未提交编辑。未知朝向格式明确拒绝，不覆盖为默认值。

正常进门继续执行原版 doAction，以保留骑乘限制、锁、声音和 OnUseHumanDoor。入口须从当前门外格接近并面向门；房间保持未旋转布局。返回前核对归属、净空及取整后的完整人物碰撞框；传送完成后再核对并校准位置。若淡入期间出口被占用，尝试返回原房间标准入口。落点检查不等于已验证从门到农场其他地方的路线畅通。

## 验证与交付

`BuildingRotation.GameAudit` 使用 System.Reflection.Metadata 读取实际 DLL，不执行游戏。以下 **13/13 目标参数类型及方法体存在检查通过**：

- Building：occupiesTile(int,int,bool)、isTilePassable(Vector2)、intersects(Rectangle)、draw(SpriteBatch)、doAction(Vector2,Farmer)、LoadFromBuildingData(BuildingData,bool,bool)、load()、updateInteriorWarps(GameLocation)。
- Game1：performWarpFarmer(LocationRequest,int,int,int)。
- Let's Move It ModEntry：SelectTargetAction(ButtonPressedEventArgs)、SingleTargetAction(ButtonPressedEventArgs)、ClearSelection()；Target：Render(SpriteBatch,GameLocation,Vector2)。

Harmony 安装阶段异常会撤回本 mod ID 的补丁并报告 prototype unavailable。签名存在与编译成功不能代替它在游戏中的安装成功，更不能代替四向行为验证。

.NET SDK 8.0.424、官方 .NET 6.0.36 targeting packs 下 Release 构建通过；游戏 runtimeconfig 为 net6.0／6.0.32，没有凭空升级框架。外部 DLL 引用设 Private=false，不自动部署、不伪造同名桩引用。构建及打包命令见实验版说明。

0.1.2 ZIP 仅包含四个自有 DLL、manifest 和中文说明，六文件内容与 CRC 校验通过。本轮 Release 构建通过，新增输入分离回归检查，104/104 独立检查通过。补丁目标未改，以上 13 项签名检查为先前结果，没有重复执行。

打包后工作区曾短暂断线，已恢复并继续完整同步；中间的断线接续提交不代表代码丢失或仍缺文件。

**下一步：** 替换旧 Prototype，移除残留 Diagnostics，避免重复 UniqueID；使用现有普通牛棚先反馈预览及 Rotation selection 诊断，再确认旋转和放置，再完成四向碰撞／进出门、取消、多栋隔离和过夜重进。只将有实机证据的项目记为验收完成。渲染批次、输入顺序、真实落点和第三方影响是这次反馈要解决的具体不确定性。
