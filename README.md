# stardew-building-rotation

星露谷建筑四向旋转 mod，先用单栋普通牛棚验证玩法，再考虑扩展建筑种类。

**状态：已有独立几何与编辑逻辑，尚不可安装游玩。** 运行流程接入层已有独立验证；SMAPI 诊断入口已用上传 DLL 编译，但包内 SMAPI 4.3.2 与启动截图的 4.5.2 不一致，仍待对齐文件及游戏加载检查。没有成品贴图或游戏内测试结果；不要把本仓库直接放进 `Mods` 当作已完成的 mod。

目标是在已有搬建筑流程中增加四向旋转；配合已适配的便携搬动 mod 时，玩家不必去 Robin 那里。旋转编辑跟随建筑移动模式启用，不新增全局启动或拿起快捷键。外观、实际占地、碰撞和门的位置一起变化；首版保持室内布局不变，后续室内转向仍在讨论。背向镜头时，通过专门重构的 Reveal 贴图显示真实入口，辅以不占格的地面引导和建筑上的标识。

退出移动模式只停止编辑；已放置建筑的朝向、门、碰撞和日常 Reveal 仍需正常工作。目前已有独立的模式与手势判定，原版及第三方搬动流程的实际适配尚未实现。

2026-09-10 讨论新增：马厩侧面靠近显露、室内转向开关与自动打包候选流程，见[室内与马厩方案](docs/interior-and-stable-plan.md)。马厩左右侧／无马与棚屋布局示意已完成待审；本轮补了[棚屋操作流程](docs/art/use-studies/README.md)和[空牛棚接入准备](docs/runtime-adapter.md)，玩法仍未接进游戏。

## 从哪里看

- [接下来具体做什么](docs/roadmap.md)：已完成基础、现在可做的工作、实机验收顺序，以及首份可玩测试包的范围。
- [玩法规则与待定事项](docs/design.md)：把已确认决定、工程建议和未决定参数分开。
- [原型接入与验证计划](docs/development.md)：当前能做什么、下一步验证什么，以及参考 mod 的接入风险。
- [建筑碰撞与人物寻路调查](docs/collision-and-pathfinding.md)：已查到的通行数据、角色碰撞与寻路差异，以及四向入口仍需接入的行为。
- [碰撞布局与门区核心接口](docs/layout-core.md)：逐格阻挡、框外区域、多格门、实例隔离与经外部检查的出门落点。
- [上传 Content 的核对记录](docs/content-audit.md)：已取得的真实建筑字段与贴图尺寸、数据映射差异，以及农场规划器参考。
- [真实数据读取与映射](docs/content-mapping.md)：ZIP／摘要读取、带类型的放置区域、明确拒绝的布局，以及复现命令。
- [四向素材对齐底稿](docs/art/README.md)：Barn／Shed／Stable 的 12 方向地面、门口与画布锚点；尚非成品像素画。
- [继续开发时从这里接](docs/continuation.md)：本轮完成情况、验证方式和下一步真实依赖。
- `src/BuildingRotation.Core`：四向格子／区域坐标、逐格碰撞布局、多格门与门外落点、编辑草稿，以及由外部移动状态驱动的鼠标手势入口。
- `src/BuildingRotation.Runtime`：手势与编辑会话、朝向元数据、已提交布局查询及待实现的游戏宿主接口。
- `src/BuildingRotation.Smapi`：已用上传 DLL 编译的加载／诊断入口，待对齐实际运行版本，不含游戏旋转补丁。
- `src/BuildingRotation.Data`：不可变的真实字段快照、原始碰撞文本规范化、布局映射与素材坐标模型。
- `tools/BuildingRotation.ContentAudit`：读取上传 ZIP 或事实摘要的离线检查工具，不是游戏入口。
- `tests/BuildingRotation.Core.SelfTest`：无第三方测试包的 C# 自检程序，覆盖几何、取消、非法放置、移动模式切换与防误触场景；不是游戏内测试。

## 运行基础自检

需要 .NET 8 SDK。从仓库根目录执行：

```sh
dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj
```

核心库和数据层使用 `netstandard2.1`，自检及离线检查工具用 `net8.0`。它们不引用游戏程序集，也不读写存档。SMAPI 诊断工程使用已与上传游戏确认一致的 `net6.0`，但 SMAPI 文件版本仍待对齐。

2026-09-10：加入 Runtime 后五个无游戏依赖工程的标准 MSBuild 构建通过，**100/100 独立自检通过**（新增 12 组）。最新诊断入口 Debug／Release 构建通过，所用上传文件为游戏 1.6.15／SMAPI 4.3.2；不是截图中 SMAPI 4.5.2 的加载验证。新一批未修改核心或重跑独立自检，缺失文件与 Let's Move It 0.6.20 的接入点见[接入记录](docs/runtime-adapter.md)。

历史结果：2026-09-09 已完成 .NET SDK 8.0.424 的标准 MSBuild 构建：零警告、零错误。无需游戏资产的自检 **88/88 通过**；加 `-- --content-zip /path/to/Content-unpacked.zip` 后验证原始上传包，**89/89 通过**。直接 Roslyn 编译也通过。CLI 启动仍有间歇性环境限制，可直接运行标准构建产物，详见[验证记录](docs/development.md#验证记录)。这仍不代表实机兼容或可安装游玩。

## 参考与代码边界

交互参考是 Exblosis 的 [Let's Move It](https://www.nexusmods.com/stardewvalley/mods/20943)，[源仓库在这里](https://github.com/Exblosis/StardewValleyMods)。当前核心库为新写的通用几何与编辑逻辑，未复制参考 mod 源码、游戏程序集或游戏贴图。识别移动状态不会自动解决双方的输入处理冲突；未知搬动 mod 暂不接管，具体适配情况需要逐一验证。

究竟修改参考 mod，还是做独立的 SMAPI mod，暂未定案。项目尚未选择分发许可证；若后续引入上游代码，先核对该版本的许可证和署名要求，再决定分发方式。
