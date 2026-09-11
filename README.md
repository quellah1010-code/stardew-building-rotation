# stardew-building-rotation

星露谷建筑四向旋转 mod，先用单栋普通牛棚验证玩法，再考虑扩展建筑种类。

**状态：普通空牛棚实验包 0.1.0 已构建，等待游戏内旋转验收。** 已接入真实建筑状态、Let's Move It 0.6.20 输入、放置、碰撞、进出门与朝向保存，使用格子占位外观。旧诊断包 0.0.2 的加载和拿起／取消状态读取已收到用户实机截图；这不代表新旋转功能已通过实测。

下载 [BuildingRotation.Prototype-0.1.0.zip](artifacts/BuildingRotation.Prototype-0.1.0.zip)，按[实验版说明](docs/prototype-test.md)移除旧 Diagnostics 文件夹、安装新包，在单人测试存档中使用普通空牛棚。两个包的 UniqueID 相同，不能同时安装；整个仓库不是安装包。

目标是在已有搬建筑流程中增加四向旋转；配合已适配的便携搬动 mod 时，玩家不必去 Robin 那里。旋转编辑跟随建筑移动模式启用，不新增全局启动或拿起快捷键。外观、实际占地、碰撞和门的位置一起变化；首版保持室内布局不变，后续室内转向仍在讨论。背向镜头时，通过专门重构的 Reveal 贴图显示真实入口，辅以不占格的地面引导和建筑上的标识。

退出移动模式只停止编辑；已放置建筑的朝向、门和碰撞继续由补丁处理。当前只接入上述版本的 Let's Move It 单选搬动；Robin 菜单、其他搬动提供方及 Reveal 尚未适配。

2026-09-10 讨论新增：马厩侧面靠近显露、室内转向开关与自动打包候选流程，见[室内与马厩方案](docs/interior-and-stable-plan.md)。马厩左右侧／无马与棚屋布局示意已完成待审；[棚屋操作流程](docs/art/use-studies/README.md)保持候选状态，[空牛棚接入](docs/runtime-adapter.md)已推进到可安装实验包。

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
- `src/BuildingRotation.Runtime`：手势与编辑会话、朝向元数据、已提交布局查询及游戏宿主接口。
- `src/BuildingRotation.Smapi`：真实游戏宿主、Let's Move It 输入协调、Harmony 补丁、占位绘制和诊断报告。
- `src/BuildingRotation.Data`：不可变的真实字段快照、原始碰撞文本规范化、布局映射与素材坐标模型。
- `tools/BuildingRotation.ContentAudit`：读取上传 ZIP 或事实摘要的离线检查工具，不是游戏入口。
- `tools/BuildingRotation.GameAudit`：直接检查实际 DLL 的 13 个补丁目标签名，不执行游戏。
- `tests/BuildingRotation.Core.SelfTest`：无第三方测试包的 C# 自检程序，覆盖几何、取消、非法放置、移动模式切换与防误触场景；不是游戏内测试。

## 运行基础自检

需要 .NET 8 SDK。从仓库根目录执行：

```sh
dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj
```

核心库和数据层使用 `netstandard2.1`，自检及离线检查工具用 `net8.0`。它们不引用游戏程序集，也不读写存档。SMAPI 工程使用已与上传游戏确认一致的 `net6.0`，引用游戏 1.6.15、SMAPI 4.5.2 及匹配依赖。

2026-09-10：此前 Runtime 的 **100/100 独立自检通过**；本轮未更改其行为，不重复跑这组检查。0.1.0 用完整匹配引用完成 Release 构建，13/13 补丁目标签名核对通过，ZIP 六文件内容与 CRC 校验通过。Harmony 运行安装、四向操作、碰撞、传送和存读档仍待用户实测，见[接入记录](docs/runtime-adapter.md)。

历史结果：2026-09-09 已完成 .NET SDK 8.0.424 的标准 MSBuild 构建：零警告、零错误。无需游戏资产的自检 **88/88 通过**；加 `-- --content-zip /path/to/Content-unpacked.zip` 后验证原始上传包，**89/89 通过**。直接 Roslyn 编译也通过。CLI 启动仍有间歇性环境限制，可直接运行标准构建产物，详见[验证记录](docs/development.md#验证记录)。这仍不代表实机兼容或可安装游玩。

## 参考与代码边界

交互参考是 Exblosis 的 [Let's Move It](https://www.nexusmods.com/stardewvalley/mods/20943)，[源仓库在这里](https://github.com/Exblosis/StardewValleyMods)。当前核心库为新写的通用几何与编辑逻辑，未复制参考 mod 源码、游戏程序集或游戏贴图。识别移动状态不会自动解决双方的输入处理冲突；未知搬动 mod 暂不接管，具体适配情况需要逐一验证。

当前实现是独立 SMAPI mod，通过 Harmony 适配参考 mod。项目尚未选择分发许可证；若后续引入上游代码，先核对该版本的许可证和署名要求，再决定分发方式。
