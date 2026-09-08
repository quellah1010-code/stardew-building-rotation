# stardew-building-rotation

星露谷建筑四向旋转 mod，先用单栋普通牛棚验证玩法，再考虑扩展建筑种类。

**状态：已有独立几何与编辑逻辑，尚不可安装游玩。** 当前没有 SMAPI 入口、成品贴图或游戏内测试结果；不要把本仓库直接放进 `Mods` 当作已完成的 mod。

目标是让玩家不必去 Robin 那里，就能搬动、转向建筑。外观、实际占地、碰撞和门的位置一起变化；室内布局不变。背向镜头时，通过专门重构的 Reveal 贴图显示真实入口，辅以不占格的地面引导和建筑上的标识。

## 从哪里看

- [玩法规则与待定事项](docs/design.md)：把已确认决定、工程建议和未决定参数分开。
- [原型接入与验证计划](docs/development.md)：当前能做什么、下一步验证什么，以及参考 mod 的接入风险。
- `src/BuildingRotation.Core`：四向格子坐标、占地、单格门外落点，以及独立的编辑草稿和鼠标手势判定。
- `tests/BuildingRotation.Core.SelfTest`：无第三方测试包的 C# 自检程序，覆盖几何、取消、非法放置与防误触场景；不是游戏内测试。

## 运行基础自检

需要 .NET 8 SDK。从仓库根目录执行：

```sh
dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj
```

核心库暂用 `netstandard2.1`，自检程序用 `net8.0`。它们不引用游戏程序集，也不读写游戏存档。实际 SMAPI 工程和版本依赖将在确认本机游戏环境后添加。

已使用 .NET SDK 8.0.424 随附的 Roslyn 直接编译当前 C# 源码，并在 .NET 8.0.30 执行自检：**29/29 通过**。当前环境的进程信息接口不兼容标准 `dotnet` CLI / MSBuild 构建，完整项目构建链仍待正常开发环境复核；详见[验证记录](docs/development.md#验证记录)。游戏内兼容性尚未验证。

## 参考与代码边界

交互参考是 Exblosis 的 [Let's Move It](https://www.nexusmods.com/stardewvalley/mods/20943)，[源仓库在这里](https://github.com/Exblosis/StardewValleyMods)。当前核心库为新写的通用几何代码，未复制参考 mod 源码、游戏程序集或游戏贴图。

究竟修改参考 mod，还是做独立的 SMAPI mod，暂未定案。项目尚未选择分发许可证；若后续引入上游代码，先核对该版本的许可证和署名要求，再决定分发方式。
