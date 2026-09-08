# stardew-building-rotation

星露谷建筑四向旋转 mod，先用单栋普通牛棚验证玩法，再考虑扩展建筑种类。

**状态：规则与基础代码阶段，尚不可安装游玩。** 当前没有 SMAPI 入口、成品贴图或游戏内测试结果；不要把本仓库直接放进 `Mods` 当作已完成的 mod。

目标是让玩家不必去 Robin 那里，就能搬动、转向建筑。外观、实际占地、碰撞和门的位置一起变化；室内布局不变。背向镜头时，通过专门重构的 Reveal 贴图显示真实入口，辅以不占格的地面引导和建筑上的标识。

## 从哪里看

- [玩法规则与待定事项](docs/design.md)：把已确认决定、工程建议和未决定参数分开。
- [原型接入与验证计划](docs/development.md)：当前能做什么、下一步验证什么，以及参考 mod 的接入风险。
- `src/BuildingRotation.Core`：不依赖游戏的四向格子坐标、占地和单格门外落点计算。
- `tests/BuildingRotation.Core.SelfTest`：无第三方测试包的 C# 自检程序，覆盖几何边界；不是游戏内测试。

## 运行基础自检

需要 .NET 8 SDK。从仓库根目录执行：

```sh
dotnet run --project tests/BuildingRotation.Core.SelfTest/BuildingRotation.Core.SelfTest.csproj
```

核心库暂用 `netstandard2.1`，自检程序用 `net8.0`。它们不引用游戏程序集，也不读写游戏存档。实际 SMAPI 工程和版本依赖将在确认本机游戏环境后添加。

初始化环境没有 .NET SDK，因此这里的 C# 自检**尚未执行，编译也尚未验证**。文件与项目结构检查不等于编译通过，更不等于游戏兼容。

## 参考与代码边界

交互参考是 Exblosis 的 [Let's Move It](https://www.nexusmods.com/stardewvalley/mods/20943)，[源仓库在这里](https://github.com/Exblosis/StardewValleyMods)。当前核心库为新写的通用几何代码，未复制参考 mod 源码、游戏程序集或游戏贴图。

究竟修改参考 mod，还是做独立的 SMAPI mod，暂未定案。项目尚未选择分发许可证；若后续引入上游代码，先核对该版本的许可证和署名要求，再决定分发方式。
