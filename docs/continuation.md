# 继续开发时从这里接

本记录对应 2026-09-09 真实数据映射、素材坐标底稿及三栋建筑四视图待审的状态。开始续做先读取仓库最新提交和工作区修改；不要假定临时目录、SDK 或进程会跨执行保留。

## 最新用户指示优先

用户指出侧视图仍是老问题，要求暂缓透明度，先给几个建筑画正／左／右／后四视图，由助手完成一批并同步 GitHub，再一起看。此指示取代此前“先修北门 Reveal 再拆透明层”的美术顺序，不取消最终 Reveal 设计。首个实机原型仍是单人普通空牛棚，不因美术同时画三栋而扩大兼容承诺。

本批已完成 Barn、Shed、Stable 共三套四视图待审板（12 个方向），见 [turnarounds/README.md](art/turnarounds/README.md)。用户在本轮开始逐张审阅，先来 Barn：在右视图附属木构顶端补交叉木条、互换错误的左右标签、去掉左视图屋顶上方露出的远侧条。已直接按用户选中的 v3 原图生成 v4，三处修改均已目视核对。当前牛棚 v4、棚屋 v1、马厩 v2；牛棚 v3 留档。包含反馈共 7 次生成／修订，提示词、图片检查及结构参考一并保存。

左右名称按建筑本身的左／右侧使用；此前将入口朝向当成视图名称，标签反了。Barn v4 上右为 RIGHT、下左为 LEFT，位置没换。Shed／Stable 图片和旧标签暂不改，等用户逐张说；不是已审通过。游戏 Facing 枚举和门的计算不受文字纠正影响。剩余屋面体积感、跨视图比例和马厩开口表现仍待审，不要把“成批待审”写成正式素材验收通过。

新增 `scripts/build-turnaround-reference.py`，仅用标准库输出三个推定体块的四向 SVG。体块高度和隐藏结构是参考估计，不是原版模型；该脚本不修改游戏数据。三个 SVG 的重生成一致性、XML／画布边界与关键方向检查通过；三张 PNG 完整解码并检查四个面。没有本轮游戏内测试，也未重跑此前 89 项 C# 自检。

## 已完成的代码与数据

原有碰撞布局／多格门核心和 63 项自检保留。提交 `093bbd3f43b061e7864035dcaa9eda0f5d07fb65` 新增 `src/BuildingRotation.Data`、`tools/BuildingRotation.ContentAudit`：从上传 ZIP 或事实摘要读取真实字段，映射到不可变核心布局，并生成素材地面锚点。先读 [content-mapping.md](content-mapping.md) 和 [art/README.md](art/README.md)，不要重复实现。

验证为 **89/89 通过**：原有 63 项、16 项数据检查、9 项素材坐标检查、1 项可选原始 ZIP 全包核对。没有 ZIP 时运行 88 项。直接 Roslyn 编译和标准 MSBuild 构建均已通过；标准构建零警告、零错误，`dotnet run --no-build --no-restore` 执行成功。没有游戏内测试。

本包 25 个定义可读，24 个能映射当前几何布局，不代表实机支持 24 种建筑。Farmhouse 的 (9,4) 框外阻挡完整保留，但核心转换明确拒绝；不能裁掉它。`OnlyNeedsToBePassable` 已进入带类型的附加放置区，四向变换不丢标志；`AdditionalTilePropertyRadius` 保留但还没驱动游戏查询。Barn／Shed／Stable 真实字段及门位测试通过，基础／升级牛棚的动物门 X 差异也已验证。

已生成三种建筑共 12 方向的 SVG 地面／门区指导和锚点 JSON，保存在 `docs/art/templates`，三张预览均已检查。Barn 的 112×112 主体与 112×128 图集分开处理，Shed 的全零源区解析为整张 112×128。画布上部余量属于草稿约定，不是已确认的侧视投影；没有新产出原生分层像素画。包内仍没有游戏／SMAPI DLL，精确版本未核验。

## 前一批 Barn 概念探索（历史）

收到“继续”后用内置图像生成工具完成 5 次 Barn 绘制／修订，保留朝东 v2、朝北普通 v1、朝北 Reveal v2 共 3 张概念稿。见 [concepts/README.md](art/concepts/README.md) 及同目录完整提示词、PNG 检查 JSON。原始游戏参考图未提交。

本轮 5 张 PNG 全部完整解码，但均为 RGB，无 alpha 或透明色；朝东的两次真实透明请求失败，棋盘格是实色。输出不是所要求的原生画布，精确门槛未验收，所有图都是扁平合成。北向 Reveal v2 已淡化上墙并保留墙脚，但门口木板仍在内侧、地面表现仍有歧义。**这是可继续修改的概念稿，不是完成四向素材、透明分层或游戏 Reveal。** 本轮源码和正式模板未改，也未重跑此前 89 项自检。

## 接着做什么

1. 按用户最新节奏逐张审图，当前先看 `docs/art/turnarounds/barn-four-views-v4.png`；保留其他两栋，等各自反馈。修形体、屋面和比例，不自行跳回透明度调参。四向形体确定后再做原生画稿、透明分层和入口视觉重构。正式地面坐标仍以 `docs/art/templates` 为依据，改门位必须同步布局与底稿，不能只改画面。用户不需要代画。
2. 有真实游戏环境后，建立 SMAPI 入口并编译，再适配原版及首个确认兼容的移动流程。移动状态与鼠标输入协调尚未实现，不要因独立手势通过就标记兼容。
3. 需要扩展到 Farmhouse 时，先实现并验证框外阻挡表示与查询；当前拒绝是有意边界。素材快照仅为首帧子集，完整动画、条件、自定义图层纹理及皮肤要从实际数据补齐后再用于渲染。农场规划器仅作交互参考，不另造网站、照搬快捷键或上传用户存档。
4. 同步 [roadmap.md](roadmap.md) 与实际提交，说明完成内容、验证方式和剩余依赖。若别的执行已更新仓库，保留并协调其改动，不能强推或覆盖。

首个可玩目标仍是 PC 键鼠、单人、普通空牛棚，可先用占位图；动物出入、NPC 路径、升级、联机和全建筑素材都仍在后续范围。用户不需要替助手写代码或画图。

## 验证环境恢复

正常环境优先执行 README 的 `dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj`。可加 `-- --content-zip /path/to/Content-unpacked.zip` 做第 89 项全包检查。本次上传缓存路径为 `/workspace/scratch/0dfc15d37142/upload/01-Content-unpacked-.zip`；丢失时用会话可访问附件，或运行已嵌入事实摘要的 88 项测试。

本轮仍用 SDK 8.0.424、运行时 8.0.30；缓存 SDK 在 `/workspace/scratch/0dfc15d37142/tooling/dotnet8`。旧并行构建／进程问题不应再视为固定阻碍：已用 `dotnet msbuild 项目 -t:Restore -m:1 -nr:false -p:RestoreSources=一个现有本地目录 -p:NuGetAudit=false` 完成仅本地引用包的恢复，然后 `dotnet build 项目 --no-restore -m:1 -nr:false -p:UseSharedCompilation=false` 成功。当前四个项目没有第三方 NuGet 包，不要把关闭审计作为未来外部依赖的默认策略。

如标准构建再次受环境限制，执行 `node scripts/verify-direct.mjs /path/to/dotnet-root`，可附加 `--content-zip 路径`。脚本自动生成当前四个工程的响应文件，嵌入事实夹具；不要再复用旧的两个响应文件遗漏新项目。这种后备方式仅代表直接源码编译，本轮另有独立的标准构建成功记录。

注意：复跑时 `dotnet run` 仍偶发 SDK 的 `Process.GetStat`／`StartTime` 启动异常，并未彻底修复。标准构建成功后，可直接执行 `dotnet tests/BuildingRotation.Core.SelfTest/bin/Debug/net8.0/BuildingRotation.Core.SelfTest.dll`（可附 `--content-zip 路径`）；本轮最新标准产物的 88／89 项均以此方式再确认通过。

## 继续保持的设计边界

不新增全局启动快捷键，旋转编辑跟随已适配的建筑移动模式；日常外观、碰撞、门和 Reveal 在退出编辑后仍应工作。室内不转。背面显示的门必须对应真实门；透明分层不改变通行。

通用几何不代替游戏世界检查。`TryGetExit` 成功不是已经传送，也不证明从门到外部路线畅通；游戏实际取整后的碰撞框还需验证。当前类型级数据保持不可变，游戏对象身份、更新、缓存、存档和实际补丁仍由后续接入完成。

公开仓库保存自有源代码和文档；游戏 DLL、解包原版素材、个人存档与含私人信息的日志不提交。自动续做的调度保存在本会话的任务中；本文件是接续记录，本身不会启动任务。
