# 0.1.0 拿起后未进入旋转：实机排查

## 已有证据

用户在单人测试档中通过之前核对的 debug build Barn 生成普通牛棚。br_status 输出确认游戏 1.6.15.24356、SMAPI 4.5.2，显示 ordinary empty Barn prototype attached; placeholder graphics；设置 enabled=True、copy=False、multi=False。这确认加载流程报告补丁安装成功，不证明具体选择回调已执行。

随后用户拿起建筑，提供：

    Move observation: building=Barn; origin=(41,57); footprint=7x4; enabled=True, copy=False, multi=False
    Move observation: nothing held; enabled=True, copy=False, multi=False

所提供的片段中没有 Rotation preview active，也没有具体拒绝原因。可以确认搬动探针看到了普通牛棚；还不能区分选择回调未运行、资格检查提前返回或其他未贴出的错误。没有证据认定室内实际有物品、建筑类型有问题或是用户手速问题。

拿起那次左键一直不松、只松 Alt 再拖动，当前 MoveModeGesture 会忽略该初始按住；这条路径不旋转符合现有防误触规则，不能用来单独证明手势故障。Left Control 在该版本 Let's Move It 中是强制放置。

## 本分支的修改

- 不改 IsEmpty 的原有判断，不扩大建筑／动物／多人支持。
- 普通建筑选择被拒绝时，输出实际输入配置、世界状态或建筑资格快照。快照包括类型、运行时类、施工／升级天数、室内类，以及动物、物件、家具、角色、玩家和地形数量；不猜测物品归属。
- 把此前位于 try 外的选择读取纳入异常记录；已有配置或目标反射失败也留下明确消息。
- br_status 增加 active 状态和最后一次选择尝试。取消或切到控制台失焦后仍保留这一历史结果，在换存档时清除。
- 首次 Sample 已终止会话时，不再错误输出 Rotation preview active。

## 验证边界与下一步

**仅已完成源码审查，尚未编译或实机运行。** 当前执行环境 unavailable，终端、文件读取和 .NET 构建能力均未提供；GitHub 连接仍可用。因此修改保存在草稿分支，不改 main 的已发布二进制，也没有生成新安装包。0.1.0 ZIP 仍是用户正在测试的版本，不能把本分支叫作已修复旋转。

源码比对确认 IsEmpty 谓词保持原样，Core／Runtime／旋转手势、放置和保存逻辑没有修改。不运行与此无关的独立测试。

开发工作区恢复后：取本分支并使用此前用户已提供的匹配引用编译；确认能编译后再更新版本号并打包。用户仅需再拿起一次并贴 Rotation selection 日志；据具体拒绝条件修正宿主或输入接入，而不是直接删除空房限制。若最后尝试仍是 no selection attempt，继续查选择补丁是否实际触发。若 accepted 但手势不转，再查按键采样及事件时序。

目前不需要用户重传 DLL、重建测试档，或继续重复旧包的同一操作。后续完整交付时同步当前接续记录，并清楚区分加载成功、选择成功与旋转成功。
