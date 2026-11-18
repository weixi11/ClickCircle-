# 取字连续圈注 验收记录（1.6.3）

日期：2026-11-19。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。

## 本轮（1.6.3）：面板可缩放并记住大小

- 面板从 `FixedToolWindow` 改为 `SizableToolWindow`：可拖动边缘缩放，仍保持工具窗口外观（无最大/最小化按钮、不进任务栏）。默认客户区仍为 480 × 566，主题历史行沿用 `Percent 100` 行样式，放大时优先长高、变宽。
- 客户区下限 440 × 380（`MinimumPanelWidth` / `MinimumPanelHeight`）；`MinimumSize` 是窗口尺寸，`OnLoad` 里用真实工具窗口边框（`Width - ClientSize.Width`）补正，保证拖到最小时主题历史表格仍有可用高度而不是一条缝。
- 大小持久化：`PluginSettings` 新增 `PanelWidth` / `PanelHeight`（客户区像素），`ApplySettings` 恢复、`GetSettings` 回写；`Load` / `Save` 都做 `ClampPanelWidth/ClampPanelHeight`（非正数回落默认，夹在 440×380 … 4000 之间）。旧 `settings.xml` 没有这两个字段时回落默认，不影响其它设置。
- 只在 `ResizeEnd` 触发一次 `PanelSizeChanged`，由 `Plugin` 挂到 `SaveSettings`：拖动过程中不反复写文件，`ApplySettings` 里的程序化设置也不会触发保存。
- 顺带修 DPI 挤压：主题历史的“主题历史”标签列 84 → 110 像素，“固定”列 54 → 64 像素，150% 缩放下标题不再折行。
- 构建与测试：新增 `tests\PanelSizeTests.cs`（17 项：默认尺寸、可拖边框、无最大最小化、不进任务栏、窗口最小尺寸、按设置恢复 900×800、过小设置被撑到下限、`GetSettings` 反映实际大小、保存/读取往返、旧文件缺字段回落默认、超上限夹到 4000、非正数回落默认）；`build.ps1 -RunTests` 12 个程序共 799 项通过（原 782 + 17）。`version` 改为 1.6.3，`package.ps1` 默认版本改为 1.6.3。
- 视觉检查：离屏渲染 720 × 640 面板（`output\panel-1.6.3-720x640.png`），历史表格随窗口长高，搜索行与状态行不再互相挤压，状态行显示“已取 2 段，右键完成取字”。
- 未覆盖：真实鼠标拖动窗口边缘的效果与“关掉面板重开是否按上次大小出现”需人工确认；自动化只覆盖尺寸映射、下限与设置往返。

## 早前（1.6.2）：连续圈注右键完成标注

日期：2026-11-19。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。

## 本轮（1.6.2）：连续圈注右键完成标注

- 新增 `src\RightClickRouting.cs`：把“页面右键在当前状态做什么”抽成纯函数 `RightClickAction For(bool picking, bool ocrPicking, bool stamping)`——取字（非区域识别）→ 完成取字；连续圈注 → 完成标注；区域识别、暂停等其余状态 → 不处理，右键菜单交回编辑器。`CircleTool` 的 `WM_RBUTTONDOWN`（`0x0204`）只调用这一个入口，并统一设置 750 ms 的右键菜单吞掉窗口。
- 新增 `Plugin.FinishStamping()`：连续圈注状态下右键结束本次标注，走 `PauseWithStatus`，工具与光标交回编辑器，主题保留，点“继续标注”可接着放圈；已画好的椭圆不受影响，历史记录不变。日志新增 `Stamping finished subject=<主题>`。
- 优先级：取字标志与圈注标志同时为真时按取字处理，避免丢掉已取片段（正常状态下两者互斥）。
- 光标：完成标注后 `SetMode(Paused)` 触发 `ApplyModeCursor()`，页面立刻回到编辑器箭头，不必移动鼠标。
- 构建与测试：新增 `tests\RightClickTests.cs`（8 项：两种完成、区域识别不拦截、区域识别同时取字不完成、暂停交回菜单、双标志优先级、结果稳定、两次右键动作不同）；`build.ps1 -RunTests` 11 个程序共 782 项通过（原 774 + 8）。`version` 改为 1.6.2，`package.ps1` 默认版本改为 1.6.2，打包清单加入 `src\RightClickRouting.cs` 与 `tests\RightClickTests.cs`。
- 装机：2026-11-19 00:37:14，旧 1.6.1 三文件、清单与设置备份到 `output\install-backup-1.6.1-20261119-003714`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `7190117088145D6361C1F7D16151F287A8E2864E35192192A9B204ADE114C24E`、pvp `1CA5F8A0B2DA687B3B64510A3E463067A0DDD03562C68CB40214151B6947C61C`、PDFXEdit.dll `738B697DC83D99A9CD02EEDBEAC3568D3B1B473E344B5C6474F8EDDBE70D91B8`。重开编辑器日志 `00:37:22.413 Hotkey Q via hook`、`00:37:22.413 Init OK`，自定义快捷键 `Q` 保留。
- 未覆盖：右键的真实鼠标交互仍未自动化，需要人工确认以下三点——圈注中右键后不再出现编辑器右键菜单、面板状态行显示“已完成标注，主题保留可继续标注”、随后“继续标注”能接着放圈。

## 早前（1.6.1）：取字与标注使用不同鼠标光标

日期：2026-11-18。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。

## 本轮（1.6.1）：取字与标注使用不同鼠标光标

- 新增 `src\ModeCursor.cs`，把光标按状态集中成常量：取字 `UIX_StdCursor_IBeam`（文本光标，去点一行字），框选拖框 `Cross`、区域识别等待框选 `Cross`、连续圈注 `Cross`（定位置/拉范围），非取字非标注交回 `Arrow`。
- `CircleTool` 的 `WM_MOUSEMOVE`（`0x0200`）不再写死十字，改为按状态取 `owner.CurrentCursorId`；按住左键拖框期间固定用 `ModeCursor.Dragging`，松开后回到当前状态光标。
- 切换状态立刻生效：`Plugin.SetMode` 末尾调用新增的 `ApplyModeCursor()`，把光标直接写到活动文档的 `ActiveView.PagesView`，不必先移动鼠标才看到变化；取不到活动文档或写入失败只记 `Cursor <原因>` 日志，不影响模式切换。
- `Plugin.CurrentCursorId` 为唯一映射入口；`unloading` 期间返回箭头，避免卸载时再去碰视图。
- 构建与测试：新增 `tests\CursorTests.cs`（8 项：取字≠标注、取字≠区域框选、各状态的具体 enum、非活动状态不得停在取字/标注光标），`build.ps1 -RunTests` 10 个程序共 774 项通过（原 766 + 8）。`version` 改为 1.6.1，`package.ps1` 默认版本改为 1.6.1，打包清单加入 `src\ModeCursor.cs` 与 `tests\CursorTests.cs`（57 条目）。
- 装机：首次尝试时编辑器正在编辑 `PODIUM Lighting Control Zone Schedule-0001`（标题带未保存星号），`install.ps1` 按既有规则拒绝在进程运行期间改动文件，未结束任何进程；用户保存并关闭后于 2026-11-19 00:27:45 完成安装，旧 1.6.0 三文件、清单、设置与日志备份在 `output\install-backup-1.6.0-20261118-214641`。`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `EEFF0DBF5D66287BA5FC89979F0E76F4E2193200EC18692F973D6D4787BF725A`、pvp `CF6A1022E833D4F6CF365F15CD3EF11210F2AE4928689A58D926C1F6C75C716D`、PDFXEdit.dll `C5E27F9CADEE3B8B2F9B91414386CACE72B5EBE7FB420AA1F585AD27FD4A0F0E`。重开编辑器日志 `00:27:54.305 Hotkey Q via hook`、`00:27:54.305 Init OK`，自定义快捷键 `Q` 与设置随升级保留。
- 未覆盖：光标形状本身属于视觉表现，自动化只断言映射关系；取字时看到 I 形、右键完成后变十字的实际效果需人工确认。

## 早前（1.6.0）：主题历史与连续取字合并

日期：2026-11-18（1.6.0 合并，1.5.0 发布）。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。1.6.0 是把两条并行开发线合成一个版本：本地线（1.4.0 + 主题历史 + OCR 语言偏好）与 1.5.0 发布线（连续取字 + 右键完成）。下文保留此前版本的验收历史。

## 本轮（1.6.0）：主题历史与连续取字合并

- 合并方式：以本地线为底，移植 1.5.0 的取字累积模型（`PickedFragmentCount`、`ResetPickedFragments`、`AppendPickedFragments`）、右键完成（`FinishPicking` + `CircleTool` 的 `WM_RBUTTONDOWN` 处理与 750 ms 右键菜单吞掉窗口），并保留本地线的主题历史面板、OCR 语言偏好与原子写设置。
- 行为：进入取字状态清空累积片段、保留主题文本；单击/框选/取字中的区域识别结果一律追加到本次取字并用分隔符重组；右键完成时把累积主题记入历史并进入连续圈注；`Esc`/“暂停”放弃本次取字；未进入取字时的区域识别仍是一次性替换主题。
- 1.5.0 包里被移除的两项按本地线保留，并在合并版里继续生效：主题历史（`SubjectHistory`、搜索、固定 50/最近 50）与 OCR 语言偏好（校对窗口“上次选择”、`LANGUAGE_UNAVAILABLE` 回退）。
- `Plugin.version` 由 `1.5.0` 改为 `1.6.0`；`package.ps1` 默认版本同步改为 `1.6.0`。
- 构建：`build.ps1 -RunTests` 通过，9 个测试程序共 766 项：TextPicker 15、HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 111（含追加/重置/计数 13 项）、ShxText 91、OCR 142、HistoryPreference 64。编译器与汇编器仍为系统自带 `csc.exe`/`ilasm.exe`，互操作由宿主 `PXCEditorCore.x64.dll` 现场生成。
- 装载：2026-11-18 21:16:51 把旧 1.5.0 三文件、清单、设置与日志备份到 `output\install-backup-1.5.0-20261118-211651`，随后 `install.ps1` 写入三个新文件并重写清单。`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `F979CD1869BCF9E6CF32152424C3FDEE0016E9D053CB782D17AB7818B933212B`、pvp `F35E1D0C9FFA995089CCE485AAE49973F92988D5CDACCD7CC744FADCF7CD3CE2`、PDFXEdit.dll `3E77D7FDBF006378980FFD46110ECBE3D34C585D867D7D0AFBAC2C7DBD9BC6AA`。重开编辑器日志 `21:17:00.878 Hotkey Ctrl+Alt+P via hook`、`21:17:00.878 Init OK`。
- 未覆盖：右键完成、连续取字与历史双击取回的真实鼠标交互仍需人工试用；本轮自动化只覆盖编译、单元测试与装载。

## 早前（1.5.0 / 1.4.0 及以前）

日期：2026-11-13（1.4.0）、2026-11-18（1.5.0）。宿主：1.4.0 为便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`），其本轮实机测试在 `box-test-host` 副本运行，设置与日志通过 `CLICKCIRCLE_DATA_DIR` 隔离；1.5.0 为 `F:\桌面1\PDFEditor` 的便携版 PDF-XChange Editor 10.8.4.409 x64。下文保留此前版本的验收历史。

## 本轮（1.4.0）：区域 OCR 与校对

- 新增“区域识别”入口；普通框选无原生文字或 SHX 原文时自动进入 OCR。按当前页面与临时视图方向渲染区域，最长边不超过 2400 像素；后台调用本地 Windows OCR。辅助脚本嵌入 Core，发行安装仍为原来的三个文件。
- 校对窗口包含原图、已安装语言、旋转与重新识别、逐行编辑片段、合并主题和明确确认。确认前不改主题、不生成圈注；取消保留旧主题。保留重复编号和原始识别字符，不做 `0/O`、`1/I` 等字符替换。辅助输入绕过 `TextChanged` 时由窗口内 200 ms 定时复核更新预览；关窗停止并释放定时器。
- `build.ps1 -RunTests`：8 个程序共 689 项通过（此前 547 + OCR 142）。新增覆盖协议完整性与错误处理、无效范围、文字排序与重复项、内部空格、7 种分隔符、空白确认保护、最小窗口尺寸与按钮可见性。
- 真实 Windows OCR 与识别中取消：`OcrTests.exe --live output\ocr-validation` 共 145 项通过，其中 3 项调用真实引擎；证据 `output\ocr-validation\ocr-live-input.png`、`ocr-live-result.txt`。本机安装 `ja`、`ko`、`zh-Hans-CN`，无英语 OCR；“自动（编号优先）”比较候选后选择 `ko`，测试结果为 `LI-058/LI-058/LI-282`。不同图纸仍需人工校对，不以本样例推定普遍准确率。
- 最终构建的真实 SDK 验收共 47 项通过：纯图片四页不含原生文字或 SHX；0/90/180/270 度页面旋转与四种临时视图旋转共 16 组渲染像素检查通过；重复编号识别、空白区域、标注数量不变均通过。证据 `output\ocr-validation\ocr-host-tests.txt`、`ocr-host-region.png`。
- 实机鼠标验收：显式区域识别、普通框选自动识别、反向拖框均打开正确区域；取字和取消没有新增椭圆，保存按钮仍禁用。校对后连续点击三次生成三个选中的圈注，撤销一次移除一个，重做恢复。
- 已保存副本 `output\ClickCircle_ocr_selection.pdf`：只读脚本确认 4 页、分页圈注数 3/0/0/0，三个主题均为手工校对后的 `LI-058/LI-058/LI-282-CHECK`；尺寸 1.5 × 1.2 mm、边框 0.2 pt、填充/边框颜色与不透明度 0.6 均符合。渲染结果 `output\ocr-validation\ocr-saved-result.png` 已目视检查，原始测试样例仍为零标注。
- 最终预览修复用同一个 `OcrReviewDialog` 的独立入口 `tools\OcrReviewPreview.cs` 验证：辅助输入把第三段改为 `LI-282-CHECK`，合并主题同步更新；辅助输入清空后主题为空且确认按钮禁用。宿主内的辅助输入接口未能稳定定位模态编辑框，因此该次预览修复通过独立窗口验证；宿主内确认后的主题写入由上述已保存 PDF 验证。
- 单次识别可取消，最长等待约 30 秒；取消/超时仅结束本次辅助进程，清理临时图片与脚本。协议错误不应用部分结果，文档或工具失效时不应用旧任务。缺少 OCR 组件/语言及组织策略拦截属于错误路径，未在本机移除系统组件复现。
- 安装：2026-11-13 02:29，确认正式编辑器已退出后，将旧版三个文件、安装清单和设置备份到 `output\install-backup-1.3.0-20261113-022902`，运行 `install.ps1`。三个安装文件 SHA-256 全部与最终 `dist` 一致；Core 为 `FDBABC9DB445B1A62373B908267F9BDD15510813B6016B158FBE10DB3B688546`。重开正式编辑器，日志 `02:29:15.937 Init OK`，工具栏已有插件入口。最后一次正式面板自动点击受桌面工具 `no monitor found for window` 错误影响，未作为面板实机验收依据；交互验收来自隔离宿主，正式加载由日志和哈希确认。未强制结束任何编辑器进程。

### 1.4.0 范围与限制

仅做所框区域识别和确认后取字，不写入 PDF 文字层。细小、倾斜、与线条重叠的编号仍可能识别错误；方向不正时可旋转后重识别，也可直接校对片段。“主题历史＋数量统计”留待后续版本。保留此前 SHX、快捷键保护、分隔符重组与手动主题保护。

## 本轮（1.3.0）：SHX 原文取字

- 新增 `ShxTextReader.cs`：仅接受作者标记完全为 `AutoCAD SHX Text`、内容非空、范围有效的批注；用 `IPXC_AnnotData_Markup.Title/Contents` 和 `IPXC_Annotation.get_Rect()` 读取，先归一化倒置坐标。普通批注不进入取字列表。原生取字失败后才尝试 SHX，原生文字保持原行为。
- 框选快照同时包含原生字符和 SHX 整段，统一按显示坐标排序、交给现有分隔符组合；SHX 按整段矩形中心命中，预览为整段范围。没有伪造字符位置，因此不能选半个 SHX 编号。相同位置、相同原生文字段的第二种 SHX 表示被抑制，不同位置的重复编号保留。
- `build.ps1 -RunTests`：7 个程序共 547 项通过（原有 456 + SHX 91）；覆盖作者与空内容过滤、倒置/无效矩形、重叠范围、边界和反向框选、重复文本、混合文本、局部原生文字选择、4 种显示旋转、七种分隔符。
- 四页矢量样例的真实 SDK 验收：各页识别 5 个有效 SHX 标签、忽略普通作者和空 SHX；0°/90°/180°/270° 原生旋转与四种临时旋转组合共 16 组均通过读取、排序、混合内容去重与高亮范围检查；各页 7 个原有批注在取字后保持不变。这里的旋转组合使用宿主 SDK 的真实页面数据及四向坐标变换，实机鼠标拖框另在 0° 页面操作验证。
- 实际 `00-0233.pdf`：真实 SDK 读取 509 条 SHX 原文，509 条均找到了可取字位置，框选快照包含全部 509 条；6 处 `3x2500/5A MCT` 在整页框选中全部保留。509 个标签中心有 508 个直接返回自身；一处 `3xO/L` 的中心同时落在较小的 `E/F` 内，按重叠规则返回后者，偏开后可取得前者。取字前后均为 513 个原有批注。
- 原生照明图回归：宿主 SDK 读得 39,882 个字符，快照保留 27,976 个非空白字符，单击仍返回 `LI-282`。只读宿主检查总计 640 项通过，证据 `output\shx-host-tests.txt`。
- 实机鼠标验收：单击纯矢量 `LI-058` 得到主题；随后拖框得到 `LI-058/LI-058/LI-282`，取字动作没有新增椭圆；接着三次点击分别创建三个 `/Circle`，日志均为 `selected=True`，最新标注显示手柄。一次撤销移除一个圈、重做恢复。
- 另存副本 `output\ClickCircle_shx_result.pdf`：`verify_output.py --expect-count 3 --expect-page-counts 3 0 0 0` 通过，三个圈主题均为 `LI-058/LI-058/LI-282`，1.5 × 1.2 mm、不透明度 0.6、边框与填充颜色保持；渲染检查无异常，样例原文件未覆盖。
- 安装：2026-11-13 01:25，先备份旧插件、归属清单和磁盘图纸到 `output\install-backup-1.2.1-20261113-012438`，再用编辑器保存当前用户修改，确认标题无未保存星号、保存按钮禁用后正常退出。`install.ps1` 完成升级，三个安装文件 SHA-256 全部与 `dist` 一致（Core：`80B8F618E9D08268C12C82CB47A9649D8D5EBF88F899DA2517EF6E0187FB8DC2`）。主编辑器已重开原图纸，插件初始化成功，保存状态正常；未强制结束任何进程。

### 1.3.0 范围与限制

保留此前的快捷键输入保护、分隔符与手动主题保护，以及最新椭圆保持选中的实现。纯扫描图和已经丢失 SHX 原文的矢量图不在本次支持范围；没有新增 OCR、全局热键或对原文批注的写操作。单击重叠标签仍按确定的命中优先级处理，框选可取出两段供用户调整。

## 本轮（1.2.0）通过项

| 项目 | 证据 |
| --- | --- |
| 构建与宿主兼容 | 从宿主内嵌类型库生成 1237 个类型；C# x64 与 `.pvp` IL 入口构建成功；`dist` 为 `ClickCircle.pvp` 3072 B、`ClickCircle.Core.dll` 57856 B、`PDFXEdit.dll` 868864 B |
| 自动检查 | 6 个测试程序共 456 项全部通过：TextPicker 15、HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 98 |
| 取字与连续圈注 | 点击 `LI-282` 只取样；之后每次点击生成一个标准 `/Circle`，日志逐条 `Circle doc=… page=… subject=…` |
| 框选取字 | 隔离宿主日志 `BoxPicked count=3 subject=LI-058/LI-282/KEEP-OUT`、`BoxPicked count=6 subject=LI-058/LI-282/KEEP-OUT/1/2/3`：矩形覆盖的多段文字按视觉行序（上→下、行内左→右）全部取到 |
| 分隔符重组 | 面板分隔符改为“空格”后再框选，日志 `BoxPicked count=2 subject=LI-058 LI-282`，已取分段按新分隔符立即重组 |
| 框选后连续标注 | 同一会话随后连续 8 条 `Circle … subject=LI-058 LI-282`，椭圆以点击处为中心 |
| 新圈注保持选中 | 在隔离宿主取 `LI-058` 后连点三处，日志三条 `Circle … selected=True`；屏幕上最新椭圆带八个缩放手柄和旋转柄，右侧“椭圆形 属性”面板显示其主题、名称、创建时间、填充 RGB 239/83/80、线条 RGB 211/47/47、边框 0.2 pt 实线、不透明度 60%、旋转 0°；此前的椭圆恢复未选中外观 |
| 选中不打断连续标注 | 选中期间继续点击仍逐个新增（三连击三条 `Circle`），无需先取消选择 |
| 默认快捷键 | 日志 `Hotkey Ctrl+Alt+P via hook`；页面获得焦点时按该组合得到 `Picking doc=65541` |
| 单键快捷键与触发范围 | 绑定单键 `A` 后：焦点在页面画布时按 `A` 触发取字；在编辑器查找框、另存为文件名框、Typewriter 标注文字编辑区按 `A` 只输入字符、不触发；焦点在资源管理器地址栏按 `A` 同样只输入字符（其他程序不受影响） |
| 失败状态不被成功提示覆盖 | HotkeyPanel 的 35 项含“注册失败后状态行保留真实错误、清除仍成功、Esc 取消保持原值”的回归用例 |
| 按键去重与延迟复核 | HotkeyService 的 145 项覆盖：按住只触发一次、`WM_KEYUP` 复位、消息入队后改绑/切焦点则动作作废、捕获时 Esc/Backspace 语义、无钩子时不吞键 |
| 安装 | 编辑器未运行时 `install.ps1` 更新三个文件；`Plugins.x64` 三者 SHA-256 与 `dist` 完全一致，并写入 `ClickCircle.install.json` 归属清单（`ClickCircle.Core.dll` = `2178EF39…20B996`） |
| 打包 | `package.ps1` 生成 `release\ClickCircle-1.2.0-x64.zip`（31 个条目）：三个发行文件、7 个源码、脚本、说明、6 个测试程序与两个样例 PDF |

## 早前构建（1.1.x）已完成、本轮未复测

以下结论来自 1.1.1 构建的验收，1.2.0 只改了 `CircleTool.cs` 的手势分支、`HotkeyService`/`HotkeyScope` 与面板，标注写入与坐标换算未变；若需 1.2.0 的旋转回归，重跑 `tests\ClickCircle_acceptance.pdf` 即可。

- 0°/90°/180°/270° 原生页面旋转下点击，椭圆均以点击处为中心，保存后纸面尺寸一致。
- 204.25%、144.05%、200% 缩放，以及 200% 下滚动 350 px 后放置自定义 2 × 3 mm 圈注，位置与尺寸正确。
- 临时视图顺时针旋转 90° 后，2 × 3 mm 圈注中心与目标点在两个坐标方向偏差均不超过 0.5 pt。
- 保存后样例共 9 个圈注（3 个 `LI-282`、5 个 `LI-156x8rows`、1 个 `CUSTOM-2x3`，分页 5/1/1/2），主题、颜色 RGB 239/83/80 与 211/47/47、边框 0.2 pt 实线、不透明度 0.6、纸面尺寸全部符合；标注可打印、可重开编辑。
- 撤销逐次移除一个椭圆、重做逐个恢复；切换文档自动暂停；尺寸与快捷键记忆，重启后恢复。
- `tests\verify_output.py` 只读复查过上述保存结果；照明图演示副本三圈半透明红色椭圆渲染正常。
- 安装/卸载脚本在隔离目录通过：首次安装、重复安装、同名冲突保护、被改动文件保护、卸载保留无关文件。

## 快捷键设计（1.2.0 变更点）

- 只用低级键盘钩子 `WH_KEYBOARD_LL`，去掉了 `RegisterHotKey` 回退：不会在其他程序里抢键，钩子装不上就明确报告不可用。
- 只在首次 `WM_KEYDOWN` 触发、按住不连发，`WM_KEYUP` 复位；`consumedKeys` 保证一次按下只吞一个键消息。
- 触发前与消息派发时各校验一次（绑定时版本号 + 前台进程 + 焦点范围），切到输入框、改绑或取消后旧动作作废。
- 取字生效条件：本进程前台 + 无模态/弹出菜单 + 焦点不在原生输入框（Edit/RichEdit/Scintilla）+ 页面视图持有输入焦点；快捷键录入只在面板持有焦点时生效。

## 早前版本未覆盖（1.4.0 已增加区域 OCR）

- 早前版本不识别图片型扫描件；1.4.0 的区域识别与验证见本页顶部。
- 框选只在按下时所在页面内取字，不支持跨页拖选。
- 行距极小、上下行重叠的文字按行中心聚类分行，必要时可改分隔符或手动修主题。
- 面板本身可自由拖动、关闭，重新打开走快速访问工具栏按钮；未做多余的窗口位置记忆。

## 新增：新建标注保持选中（1.2.1）

- 选择集通过 `IPXV_Inst::Str2ID("selection.annots")` 取得（本宿主为 5891，cluster 29），不写死；取不到就静默跳过，不影响标注创建。
- 每次新增后：清空选择集 → 插入新标注 → `IPXV_Document::ActiveSel = 选择集` → `Redraw()`。
- 只调用 `ActivateSel(cluster)` 不够：实测 `ActiveSel` 仍为 null、无手柄（探针日志 `P2 after activate activeSel=null`）。改用 `ActiveSel` 属性赋值后正常，日志 `P2 set ActiveSel ok -> … items=1`。
- 切换到 `tool.editAnnots` 会把 `ActiveSel` 清空，因此插件不改当前工具，选中状态与自定义工具共存。
- 选择集始终只含最新一个标注，属性面板跟随它；暂停、切文档、保存都不受影响。
