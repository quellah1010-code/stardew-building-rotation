# 本机诊断包 0.0.2

这是首个用于确认加载和搬动状态读取的测试包。**可以安装做诊断，还不能旋转建筑。** 使用用户提供的 Stardew Valley 1.6.15／SMAPI 4.5.2 文件编译；搬动状态读取只针对 Let's Move It 0.6.20，尚待用户游戏内验证。

## 安装和反馈

1. 完全退出游戏，将 `BuildingRotation.Diagnostics-0.0.2.zip` 解压。
2. 把解压出的 `BuildingRotation.Diagnostics` 文件夹放入游戏的 `Mods`。文件夹内应直接看到 `manifest.json` 和三个 `BuildingRotation.*.dll`，不要多套一层同名目录。保留现有 Let's Move It。
3. 用平时的 SMAPI 方式启动。查看日志中是否出现 `Building Rotation — Diagnostics 0.0.2`，以及 `read-only selection probe attached`。如果出现报错，截报错段即可。
4. 进入存档后，用原有 Let's Move It 操作拿起一栋普通牛棚，稍停一下，再取消拿起。日志应从 `nothing held` 变为 `building=Barn`，取消后恢复 `nothing held`。没有普通牛棚时，现有其他建筑也可以用于检查是否被识别，但不代表已支持它的旋转。
5. 将这一段日志截图反馈。开发命令 `br_status` 可在 SMAPI 日志窗口中输入并回车，重新输出当前状态；不是游戏聊天框，也不是旋转快捷键。

观察器每 10 个游戏更新检查一次，仅在报告内容变化时输出。非常短的拾取／取消可能落在两次采样之间；这是诊断采样，不是正式手势时序测试。`origin` 和 `footprint` 是建筑当前真实位置和占地，不是鼠标下的候选预览；本包也没有检查房间是否为空或授予旋转输入所有权。

`enabled`、`copy`、`multi` 是运行中读取的 Let's Move It 设置，诊断包不修改它们。多选显示的是选中格组数，不是建筑总数。现有搬动 mod 仍按原来的规则执行；本包不移动建筑、不写朝向或存档、不安装 Harmony 补丁。

若显示 `selection probe unavailable`／`selection probe stopped`，观察器会停止读取并报告原因，原搬动 mod 不受本包控制。这个结果需要反馈，不能当作检测通过。卸载时退出游戏，移除 `Mods/BuildingRotation.Diagnostics` 即可。

## 下一批编译仍需要的文件

SMAPI 4.5.2 和 `smapi-internal` 已收到，上一批 4.3.2／4.5.2 不一致已解除。请从同一游戏安装目录补取：

- `MonoGame.Framework.dll`
- `xTile.dll`
- `StardewValley.GameData.dll`

它们用于后续图形矩形、地图、真实建筑数据及旋转补丁接入；诊断包本身已经能用现有引用编译，不需要把用户安装里的这些依赖替换掉。已收到的 Content 和其他 DLL 不用再传。Let's Move It 的运行时设置现在可以通过诊断读取，因此 `config.json` 不再是做这次诊断的前置条件。

## 构建与打包

```sh
dotnet build src/BuildingRotation.Smapi/BuildingRotation.Smapi.csproj -c Release -p:GamePath="/path/to/game"
python scripts/package-diagnostics.py
```

打包脚本仅包含自己的 Core、Runtime、Smapi DLL、manifest 和本说明；不包含游戏 DLL、SMAPI、Harmony、Let's Move It、配置或存档。打包不运行游戏，标准构建成功也不等于本机加载成功。
