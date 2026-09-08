# 原型接入与验证

## 当前边界

仓库目前只有设计记录和通用几何核心，没有 SMAPI 入口、输入监听、运行时碰撞补丁、贴图切换、传送或存档写入。C# 编译及自检未执行，原因是初始化环境缺少 .NET SDK；游戏内测试也未执行。

核心库不引用游戏，让格子变换可以单独验证。自检是一个普通控制台程序，请用 README 的 `dotnet run` 命令执行，不要用 `dotnet test` 将“未发现测试”误认成成功。

## 核心坐标约定

地图格子 X 向右、Y 向下。朝向表示入口朝向：South 是门朝下的正向，East 朝右，North 是门朝上的背向，West 朝左。代码枚举顺序为 South、East、North、West，仅用于循环；不称它为屏幕坐标里的顺时针，也不预先决定鼠标手势映射。

占地原点为当前朝向包围矩形的左上格，不等于贴图左上像素，也不等于最终鼠标抓取锚点。以未旋转宽 W、高 H，局部格子坐标 (x, y) 为基准：

| 朝向 | 占地宽高 | 变换后局部格 |
| --- | --- | --- |
| South | W × H | (x, y) |
| East | H × W | (y, W − 1 − x) |
| North | W × H | (W − 1 − x, H − 1 − y) |
| West | H × W | (H − 1 − y, x) |

`Doorway` 接收已经处于当前朝向的占地和门格，检查门是否位于对应外边缘，计算门外相邻的一格。该结果不是已验证的游戏传送坐标：接入层还要检查地图范围、可通行性、玩家碰撞盒和实际传送偏移。它也不负责多格动物门、Reveal 区或美术像素坐标。

## 参考 mod 接入调查

在查阅的 Let's Move It `develop` 源码中，`OnButtonPressed` 在 `MoveKey.JustPressed()` 后调用 `SingleTargetAction`，后者执行 `MoveTo` 或 `CopyTo`。因此长按旋转的输入必须先区分点击与拖动，再决定是否放置；不能原样保留按下即放置，再追加长按逻辑。[源码：ModEntry.cs](https://github.com/Exblosis/StardewValleyMods/blob/develop/LetsMoveIt/ModEntry.cs)

建筑移动走 `buildStructure` 路径，施工或升级中的建筑被拒绝；其中还有室内出口更新逻辑。接入真正的四向占地和出口时，需要逐项复核这些行为，不应把原有搬动功能当成已提供了旋转接口。[源码：Move.cs](https://github.com/Exblosis/StardewValleyMods/blob/develop/LetsMoveIt/TargetData/Move.cs)

这些链接指向可变化的分支，真正引入代码前要固定并记录所用提交，同时检查许可证。本次没有导入上游实现，也没有决定 fork 或独立 mod 路线。

SMAPI 后续工程可通过 `Pathoschild.Stardew.ModBuildConfig` 引用游戏与 SMAPI，并配置游戏路径。相关测试若引用游戏程序集，仍需要游戏安装；不要把它们与当前纯几何自检混淆。[SMAPI 构建包文档](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md)

## 下一阶段验收顺序

1. 在具备 .NET SDK 的环境执行核心自检；确认本机游戏、SMAPI 版本和引用路径，再建立最小游戏入口。
2. 用单栋无动物测试牛棚和占位图打通拿起、移动、转一档、合法放置、取消，检查原建筑在预览期间不被写坏。
3. 逐方向验证占地框、实际碰撞、门格及门外通路；重点测试宽高交换后的窄通道、地图边缘和已占用地块。
4. 验证四向进出门，室内保持不变；门外被挡时不能将人物送入建筑碰撞区或地图外。
5. 接入背面专用 Reveal，保证可见门与真实门重合；覆盖进出触发边界、站在门口、远处回看等场景。
6. 保存并重载，检查朝向、贴图、碰撞、传送一致；测试取消、中断和异常恢复后，再扩展动物与升级场景。

没有完成上述游戏内检查之前，不生成“可安装版”或声称已兼容现有存档。测试游戏功能使用独立存档，且不把存档、日志中的私人信息、游戏 DLL 或解包贴图提交到仓库。
