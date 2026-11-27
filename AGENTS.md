# AGENTS.md — ClickCircle（取字连续圈注）接手文档

给接手本项目的编码代理：**先读这份文件**，一般不需要通读整个仓库。
需要细节时按需读：功能说明 `CircleStampPlugin\README.md`、最近验收 `CircleStampPlugin\VALIDATION.md` 最上面一节、代码从 `CircleStampPlugin\src` 里挑相关文件（`CircleTool.cs` 33KB、`Plugin.cs` 43KB、`StampPanel.cs` 60KB，尽量用 grep 定位，不要整读）。

本文件在 `F:\桌面1\PDFEditor\AGENTS.md` 和 `F:\桌面1\PDFEditor\CircleStampPlugin\AGENTS.md` 各一份，内容相同；以后修改请两边一起同步。

最后更新：2026-11-27，插件版本 1.8.6。

## 1. 项目是什么

- 宿主：便携版 **PDF-XChange Editor 10.8.4.409 x64**，安装目录 `F:\桌面1\PDFEditor`（`PDFXEdit.exe` 所在，**不是** git 仓库）。
- 插件：**ClickCircle / 面板标题「取字连续圈注」**。在暖通/电气图纸上点文字取编号，按分隔符累积成主题，再在图上按固定尺寸连续放红色椭圆圈注；取不到原文时可框选走本地 OCR；不依赖网络（在线更新除外）。
- 技术栈：C# **.NET Framework 4.x x64 + WinForms**，编译只用系统 `csc.exe` / `ilasm.exe`（不需要 Visual Studio，不注册 COM）；入口是 `tools\ClickCircle.Bootstrap.il` 汇编出的原生 `.pvp`，运行时反射加载 `ClickCircle.Core.dll`，互操作程序集是 `dist\PDFXEdit.dll`。
- git 仓库根：`F:\桌面1\PDFEditor\CircleStampPlugin`，remote `https://github.com/weixi11/ClickCircle-.git`（公开，`main`）。用户升级走编辑器「设置 → 检查更新」，从 GitHub 标签分发。
- 当前版本：**1.8.6**，已发布标签 `v1.8.6`。用户机 `Plugins.x64` 里实际装的可能是旧版，**以 `plugin.log` 的 `Version` 行为准**（`F:\桌面1\PDFEditor\Plugins.x64\ClickCircleData\plugin.log`）。

## 2. 目录速览

| 路径 | 说明 |
| --- | --- |
| `F:\桌面1\PDFEditor\` | 便携编辑器根：`PDFXEdit.exe`、`PDFXEditCore.x64.dll`、`Plugins.x64`（插件安装目录，三个发行文件必须同目录） |
| `F:\桌面1\PDFEditor\Plugins.x64\ClickCircleData\` | 用户设置 `settings.xml` 与日志 `plugin.log`（没有 `Data\ClickCircle` 时用这里） |
| `F:\桌面1\PDFEditor - 副本\` | **独立编辑器副本**，真机验证专用（独立进程；不是 git 仓库） |
| `CircleStampPlugin\src\` | 插件源码（详见 README 的 src 表） |
| `CircleStampPlugin\tests\` | 15 个测试程序 + 测试素材/脚本 |
| `CircleStampPlugin\tools\` | `ClickCircle.Bootstrap.il`、`ocr-helper.ps1`、类型库提取器、一次性预览工具 |
| `CircleStampPlugin\dist\` | 发行三件：`ClickCircle.pvp`、`ClickCircle.Core.dll`、`PDFXEdit.dll`（**提交进 git**，在线更新要从标签里下载） |
| `CircleStampPlugin\release\` | `package.ps1` 产出的 zip（gitignore） |
| `CircleStampPlugin\output\` | 验收证据、安装备份、`gui-test` 脚本与探针（gitignore） |
| `CircleStampPlugin\output\gui-test\` | 后台真机验证脚本：`bg-host-test2.ps1`（右键/菜单）、`bg-host-drag.ps1`（拖动落圈）、`bg-host-toast184.ps1`（取字提示量测）、`bg-host-ocr-review.ps1`（校对窗开关 before/default/after）、`bg-host-history.ps1`（主题历史开关/紧凑高度量测）；`probe\` 里是一次性 SDK 探针；`ClickCircle_gui_fixture.pdf` 是测试 PDF |
| `CircleStampPlugin\output\install-backup-*` | 历次装机前备份（三件 + settings + log） |

## 3. 用户可见行为（速查）

- **取字**：快速访问工具栏「取字连续圈注」打开面板 →「取文字」或快捷键（默认 `Ctrl+Alt+P`）→ 单击文字/框选；每次只取字不画圈，按面板分隔符累积主题；右键完成取字，进入连续圈注。
- **取字来源优先级**：页面文字层 → AutoCAD SHX 专用批注 → **标注标签**（绿色尺寸标签这类，见 `AnnotationLabel.cs`：读标注 `/Contents`，反斜杠分行、**反序**、去掉末尾测量值行，点击命中按到折线的距离取最近且 < 8pt）→ 框选无原文自动进 OCR。
- **取字提示**（用户明确要求，别改回去）：显示在**鼠标右下 18/22px**（屏幕边缘自动翻到另一侧）、**半透明 88%**、**0.8 秒**消失；不抢焦点、不挡点击。
- **连续圈注**：每次左键在松开处（即使按下后拖动过）放一个椭圆；默认纸面 1.5×1.2 mm（面板可改）；新椭圆保持选中，属性面板显示主题/颜色/尺寸/不透明度。**右键完成本次标注**：吞掉编辑器页面右键菜单，约 0.3 秒后把工具交回编辑器。`Esc` 暂停/放弃。
- **主题历史**：最近 50 条、可固定 50 条；右键完成取字、成功放圈或确认 OCR 结果时记入。设置页“主题历史”开关关掉后：历史区隐藏、面板客户区收成 480×190、不再记录新主题；重新打开恢复原高度并继续记录。
- **其他**：分隔符可改并即时重组、面板可缩放、设置页有尺寸/快捷键/样式自动套用/检查更新/区域识别校对窗开关；区域 OCR 默认弹校对窗，设置里关掉后后台识别完直接把结果并入取字或圈注（不弹窗），日志 `OcrDirect`/`OcrDirectPicked`（本地 Windows.Media.Ocr，不外传）。
- 标注本体是编辑器标准 `/Circle` 椭圆，Subject=主题，填充 RGB 239/83/80、边框 211/47/47 0.2pt、不透明度 60%。

## 4. 硬性规则（必须遵守）

1. **不要碰用户正在用的编辑器**：用户经常开着编辑器画图、有未保存文档。禁止结束/关闭用户进程、禁止抢焦点、禁止移动真实鼠标。装插件前必须让用户先保存关闭，或走「设置 → 检查更新」（更新 helper 会自己关/备份/替换/重启）。真机验证一律用 `F:\桌面1\PDFEditor - 副本`。
2. **版本号三处同步**：`src\Plugin.cs` 的 `PluginVersionNumber`（`(1U<<16)|(大<<8)|小`）、`version.txt`（单独一行 `vX.Y.Z`）、`package.ps1` 默认 `$Version`；README 标题与示例、VALIDATION 顶部标题也要同步。
3. **每个版本固定流程**：改代码 → 15 个测试全过 → 独立副本真机验证（改前/改后证据）→ 更新 README/VALIDATION → 发布 GitHub（commit + tag + push + 远端校验）→ `package.ps1` 出 zip → 中文向用户汇报并附证据。
4. **新源文件**必须加入 `build.ps1` 编译清单（第 39–61 行）和 `package.ps1` 的 `$requiredFiles`，并在 README 的 src 表加一行。**新测试程序**必须加入 `build.ps1` 第 106 行的测试数组，并更新 README 测试计数。
5. **提交信息用中文**，格式 `1.8.x: 主要改动`；每轮发布后再提交一次 `记录 1.8.x 发布与远程校验`（只改 VALIDATION）。
6. **不要改用户的 PDF/编辑器数据**；测试一律用 `CLICKCIRCLE_DATA_DIR` 隔离数据目录或副本自己的 `ClickCircleData`。
7. **编码坑**：`.cs` 用 UTF-8（编译加 `/codepage:65001`）；`.ps1` 写 **UTF-8 带 BOM**（否则 5.1 会按 ANSI 读成乱码）；PowerShell 会把中文智能引号 “ ” ‘ ’ 当成字符串定界符，含这些引号的脚本用 `@'...'@` 或避免。
8. **中文沟通**：用户用中文，回复也用中文，结论给数据（测试数、量测值、截图路径），不要只给“应该可以”。

## 5. 构建与测试（本机唯一可行方式）

本机没有 `PXCEditorCore.x64.dll`，`build.ps1` 默认会报缺少依赖，请用手工编译（已验证）：

```powershell
$proj='F:\桌面1\PDFEditor\CircleStampPlugin'; $dist="$proj\dist"; $build="$env:TEMP\cc-build"
New-Item -ItemType Directory -Path $build -Force | Out-Null
$csc="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$ilasm="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe"
& $csc /nologo /target:library /platform:x64 /optimize+ "/out:$build\ClickCircle.Core.dll" `
  "/reference:$dist\PDFXEdit.dll" "/resource:$proj\tools\ocr-helper.ps1,ClickCircle.Ocr.ps1" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /codepage:65001 `
  (Get-ChildItem "$proj\src" -Filter *.cs | ForEach-Object FullName)
& $ilasm /nologo /dll /x64 "/output:$build\ClickCircle.pvp" "$proj\tools\ClickCircle.Bootstrap.il"
```

- **测试程序**（15 个）：引用 `$build\ClickCircle.Core.dll` + `$dist\PDFXEdit.dll` + WinForms/Drawing/Xml，`HotkeyServiceTests` 额外带 `src\HotkeyService.cs`。当前全过：**945 项** = TextPicker 34、HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 111、ShxText 91、OCR 142、HistoryPreference 82、Cursor 8、RightClick 21、LeftRelease 10、PanelSize 33、Update 31、AnnotStyle 39。
- 构建产物复制进 `dist` 后提交；`release\ClickCircle-<版本>-x64.zip` 用 `package.ps1` 生成（同名存在会拒绝，先删旧包）。
- **执行策略**禁止直接跑 `.ps1`，用绝对路径：
  `& powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& '<绝对路径>\package.ps1' -Version '1.8.6'"`
- `ShxHostTests.cs`、`OcrHostTests.cs` 是装进编辑器的只读 SDK 验收，不是上面 15 个之一，另有 `tests\run-*-host-tests.ps1`。

## 6. 真机验证（不要碰用户编辑器）

用副本 `F:\桌面1\PDFEditor - 副本\PDFXEdit.exe`（独立进程）：

1. 把 `dist` 三个文件复制进副本的 `Plugins.x64`（少一个都不会加载）；
2. 设 `$env:CLICKCIRCLE_DATA_DIR=<隔离目录>`，日志读 `<隔离目录>\plugin.log`；
3. 启动参数用 `CircleStampPlugin\output\gui-test\ClickCircle_gui_fixture.pdf`（不可用主编辑器目录和正在运行的副本）；
4. 窗口用 `SW_SHOWNOACTIVATE (4)` 显示，输入全部用 `PostMessage`/`SendMessage`（`WM_LBUTTONDOWN/UP`、`WM_MOUSEMOVE`、`WM_RBUTTONDOWN/UP`）；QAB 按钮用 UIAutomation `InvokePattern`；面板控件用子窗口 `WM_SETTEXT (0x000C)` + `BM_CLICK (0x00F5)`；
5. 结果看 `plugin.log` 关键行 + 新窗口 diff；验完结束副本进程并删掉副本 `Plugins.x64` 里的插件三件。

现成脚本（`output\gui-test\`）可直接改参数复用：`bg-host-test2.ps1`（右键完成不弹菜单）、`bg-host-drag.ps1`（圈注拖动落圈）、`bg-host-toast184.ps1`（提示位置/透明度/时长；before 装旧 Core、after 装新 Core 各跑一次）、`bg-host-ocr-review.ps1`（区域识别校对窗开关；settings.xml 预写 ShowOcrReview=false 验直采路径）、`bg-host-history.ps1`（主题历史开关；settings.xml 预写 ShowHistory=false 验紧凑高度）、`probe\PickProbe.cs`（标注标签取字探针，编译成插件装进副本）。需要直接查 SDK 文字层/标注时参考 `probe\TextProbe.cs`；渲染真值图可用 `OcrRegionRenderer.Render`。

## 7. 发布流程（GitHub 在线更新是唯一分发渠道）

机制：编辑器「设置 → 检查更新」读 `raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt`，发现新版本后从 `raw.githubusercontent.com/weixi11/ClickCircle-/<标签>/dist/` 下载三个文件。**所以发布 = main 的 `version.txt` 改成 `vX.Y.Z` + 打同名标签 + 标签里包含 `dist` 三文件**。

- 本机 PATH 没有 git，但 `%TEMP%\cc-git\cmd\git.exe` 是便携 MinGit（重启后可能没了；重新下载解压即可：`https://mirrors.tuna.tsinghua.edu.cn/github-release/git-for-windows/git/LatestRelease/MinGit-2.56.1-64-bit.zip`）。
- 仓库已配置 `credential.helper=wincred`，凭据在 Windows 凭据管理器（target `git:https://github.com`），可直接 push；push 前设 `$env:GIT_TERMINAL_PROMPT='0'` 防卡住。
- 发布命令：

```powershell
$git="$env:TEMP\cc-git\cmd\git.exe"; $repo='F:\桌面1\PDFEditor\CircleStampPlugin'
& $git -C $repo add -A
& $git -C $repo commit -m '1.8.x: 主要改动'
& $git -C $repo tag -a v1.8.x -m '取字连续圈注 1.8.x：主要改动'
& $git -C $repo push origin main
& $git -C $repo push origin v1.8.x
```

- 推送后**必须验证远端**：`main/version.txt` 等于新版本；`<tag>/dist/` 三个文件 SHA-256 与本地 `dist` 一致；再把提交号/标签/校验结果补进 VALIDATION 并提交第二次（`记录 1.8.x 发布与远程校验`）。
- 用户升级：保存文档 → 设置 → 检查更新 → 确认，helper 自动关编辑器/备份/替换/重启，失败回滚；有未保存文档会拒绝。**不要用 `install.cmd` 覆盖用户正在用的编辑器**（它检测到进程在跑也会拒绝）。

## 8. 文档约定

- `VALIDATION.md`：顶部是 `# 取字连续圈注 验收记录（<当前版本>）`；每轮在**最上面**加 `## 本轮（x.y.z）：一句话`，旧节改成 `## 早前（x.y.z）：...`；每节包含：现象/根因、修改、构建与测试、真机验证（带数值/截图路径）、发布（commit/tag/远端校验）、未覆盖。
- `README.md`：标题版本、功能 bullets、src 表、打包默认版本、测试统计一起同步。
- 证据放 `output\gui-test\...`（不进 git），发行包放 `release\`（不进 git）。

## 9. 已知坑 / 排障

- 测量标注（尺寸标签）的文字**不在页面文字层**：`GetText(null,false)`、`GPTOF_IncludeComments|IncludeFormFields` 都拿不到；必须读标注 `/Contents` 再还原（`AnnotationLabel.cs`）。插件自己的椭圆 Contents 为空，标注取字要跳过空内容。
- 右键完成标注后**不能立刻**把工具交回编辑器，否则编辑器会弹页面右键菜单；`RightClickRouting.cs` 的 `ContextMenuGuard` 负责吞菜单 + 延迟约 0.3s 交回。
- 圈注状态左键按下后拖动也要落圈，取字状态拖动才是框选：逻辑在 `LeftReleaseRouting.cs`，别混用。
- WinForms 控件在 UIA 里常只暴露成 Pane，面板控件要用子窗口消息驱动。
- 日志关键字见 README“诊断”一节：`Version`、`Picking doc=`、`Picked`、`BoxPicked`、`Picking finished`、`Stamping finished`、`Circle`、`Update ...`。
- 主题历史开关：`PluginSettings.ShowHistory`（默认 true）；关掉后 `RememberCurrentSubject` 直接返回、面板收成 `CompactPanelHeight=190`，`OnLoad` 的最小高度必须按 `showHistory` 计算，否则启动会被顶回 380。
- 区域识别校对窗开关：`PluginSettings.ShowOcrReview`（默认 true，旧 settings.xml 缺节点按 true）；关掉后 `QueueOcr` 走 `StartDirectOcr`/`CompleteDirectOcr`，日志 `OcrDirect ...` / `OcrDirect empty` / `OcrDirectPicked count=... subject=...`。
- 工作目录 `F:\桌面1\PDFEditor` ≠ git 根 `...\CircleStampPlugin`，git 命令加 `-C` 或先 `cd`。
- 中文路径/编码/执行策略/智能引号四个坑见 §4 第 7 条，踩过一次就照做。

## 10. 收到任务怎么干（决策流程）

1. **先看现状**：`plugin.log` 最后几行（当前版本、模式、错误）、`git -C <repo> status --short`、`git log --oneline -3`；别急着重编。
2. **用户报 bug**：用副本 + 日志复现（留下改前证据）→ 定位（优先纯逻辑 helper 便于写测试）→ 改 → 15 测试全过 → 副本验证改后行为（量测/截图/日志）→ 发布 → 汇报“根因 + 改法 + 数据”。
3. **用户要新功能/改 UI**：先看 README 功能表和 src 表找落点；GUI 行为改动尽量给 before/after 量测（位置、时长、透明度、窗口数等），用户很看重这个。
4. **发布检查单**：版本三处 → README/VALIDATION → `dist` 已更新（新 Core 字节数/hash 变了）→ 15 程序 945 项全过 → 副本真机验证 → commit+tag+push+远端校验 → 第二次提交记录 → `package.ps1` → 告诉用户怎么在线升级。
