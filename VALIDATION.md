# 取字连续圈注 验收记录（1.8.12）

日期：2026-11-28。宿主：便携版 PDF-XChange Editor 10.8.4.409 x64（`F:\桌面1\PDFEditor`）。

## 本轮（1.8.12）：历史关着时“设置”入口消失的回归修复

- 现象（用户截图，v1.8.10）：面板只剩主题、四个按钮和“锁定段数”，底部状态行整行不见了，“设置”入口找不到。
- 根因：1.8.10 在主页插入“锁定段数”行，主页布局从 4 行变成 5 行，主题历史行索引从 3 变成 4；但 `ApplyHistoryVisibility` 里仍把 `mainLayout.RowStyles[3]` 当成历史行。主题历史默认关（1.8.8 起），启动/关历史时这个函数把索引 3（实际是状态行）高度置 0，于是状态行连同“设置”按钮、快捷键、尺寸摘要一起被隐藏。1.8.9 及以前历史行确实是第 4 行（索引 3），所以正常；1.8.10、1.8.11 受影响。
- 修改：`ApplyHistoryVisibility` 改为操作 `RowStyles[4]`（真正的历史行），并把行数判断改为 `> 4`；另加回归测试——历史关闭时状态行与“设置”按钮必须可见。历史开关逻辑、历史打开时的高度与表格都不变。
- 测试：15 个测试程序 **996 项**通过（`HistoryPreferenceTests` 93 → 94：新增“历史关闭时状态行与设置入口仍然可见”）。
- 真机验证（2026-11-28 11:57，独立编辑器副本，空数据目录 = 历史默认关）：
  - 1.8.11：面板客户区 480 × 230，状态行高度 0，**找不到“设置”按钮**（截图 `output\gui-test\host-settings-missing-1812\before\default-panel.png`）；
  - 1.8.12：同一条件下状态行恢复，显示“待取字 · 快捷键 Ctrl+… · 1.5×1.2mm · / · **设置**”（截图 `...\after\default-panel.png`）；
  - 报告：`...\before\default-report.txt`、`...\after\default-report.txt`。
- 发布（1.8.12）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.12`）并打标签 `v1.8.12`；远端校验与提交号见下方“发布记录（1.8.12）”。
- 未覆盖：设置页本身未改动；历史打开/关闭的尺寸行为与 1.8.10 一致。
## 早前（1.8.11）：取字后自动复制，带开关

- 需求：加一个取字自动复制的功能，带开关。
- 修改：
  - `PluginSettings.AutoCopyPicked`（默认 **false**）；设置页在“主题历史”下面新增一行“剪贴板 → 取字后自动复制主题到剪贴板”，勾选后立即生效并保存。
  - 取字成功的四条路径（单击取字、框选取字、区域识别直采采用结果、校对窗确认结果）都会在片段并入主题后调用 `AutoCopySubject()`，把**当前完整主题**写入剪贴板；日志记 `AutoCopy ok=True/False len=... subject=...`。剪贴板被别的程序占用时只失败不打断取字（`ok=False`）；主题为空不写剪贴板。
  - 复制内容跟随锁定段数：锁定两段时复制的是“锁定段 + 新取段”，与面板主题一致。
- 测试：15 个测试程序 **995 项**通过。`HistoryPreferenceTests` 91 → 93（自动复制默认关、开关随设置保存）；`PanelSizeTests` 46 → 50（设置页控件存在、勾选后 `AutoCopyPicked` 为真、复制主题到剪贴板内容正确、空主题不写剪贴板）。
- 真机验证（2026-11-28 11:49–11:50，独立编辑器副本，后台消息取字，不动真实鼠标；`settings.xml` 预写 `AutoCopyPicked=true`，测试前后保存/恢复了本机剪贴板文本）：
  - 1.8.10（改前）：取到 `FCU`（`Picked FCU count=1`），无 `AutoCopy` 日志、剪贴板保持原内容，`AUTOCOPY_PICKED=False`。
  - 1.8.11（改后）：日志 `AutoCopy ok=True len=3 subject=FCU | Picked FCU count=1`，剪贴板变为 `FCU`，`AUTOCOPY_PICKED=True`。
  - 设置页截图（新增“剪贴板”行）：`output\gui-test\host-autocopy-1811\settings\settings-settings.png`（480 × 566，未出现截断/滚动条）；报告：`...\before\before-report.txt`、`...\after\after-report.txt`。
- 发布（1.8.11）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.11`）并打标签 `v1.8.11`；远端校验与提交号见下方“发布记录（1.8.11）”。
- 未覆盖：剪贴板被独占时只返回失败、不自动重试（下一次取字会再复制）；校对窗点取消时不复制；复制的是完整主题，不是最后一次点击的单个片段。
### 发布记录（1.8.11）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `ba9a6b5f8b0cc59679162d8db6a1a99a0887bd29`（“1.8.11: 取字后自动复制，带开关”），标签 `v1.8.11`。
- 远端校验（2026-11-28 11:52）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.11`；`v1.8.11/dist/` 三个文件下载后 SHA-256 与本地 `dist` 一致（Core 150528 字节 `8295C5486D9AE178C4BCA3772523FF2F8F7C0BC124F351972F1A6121008D8467`、pvp `671C0C768611F65E640C3864D646409713D8D4BF3BCB4551CD5AF898856EE78F`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`），`allMatch=True`。
- 本机升级方式：用户当前 1.8.7：保存文档 → 设置 → 检查更新 → `v1.8.11`（会一并带上 1.8.8 默认关闭、1.8.9 放置提示、1.8.10 锁定段数与 1.8.11 自动复制）。
- 回滚：`output\gui-test\host-autocopy-1811\core-1.8.10\`（1.8.10）。
## 早前（1.8.10）：锁定前 N 段，重新取字时保持不变

- 需求（用户两张截图）：一段图纸里同一配电回路要配很多不同设备，每次重新取字都从第一段开始太麻烦；希望“锁定两段，重新取字时锁定的两段不动，新取的字接在后面”。
- 修改：
  - 主页新增“**锁定段数**”一行：数字框 0–20（0 = 不锁定）+ 说明文字。点“取文字”时，`BeginPickingRound` 会用锁定片段作为新一轮的起点并把主题先填回锁定内容；之后取的字照常追加。右键完成/继续标注/普通取字都不改变锁定内容。
  - 锁定时会立刻把当前已取片段裁剪到前 N 段（主题同步重组）；N 超过已取段数时按实际段数锁定；解锁（0）后恢复原来的“重新取字清空”行为。
  - 锁定段数与内容一起存进 `settings.xml`（`LockedSegments` / `LockedFragments`），重开面板、重开编辑器和在线更新后都还在。
  - 锁定状态下重新取字会给提示“已锁定 N 段，点击文字继续”。
  - 面板主页多一行，默认紧凑高度 190 → **230**（客户区 480 × 230）。
- 测试：15 个测试程序 **989 项**通过。`SeparatorPanelTests` 111 → 133（+22：锁定两段、重新取字保留锁定、新段追加、再次重新取字只留锁定、锁定中改分隔符、改锁定数为 1、超量锁定按实际段数、解锁后清空、空取字不锁定、设置保存/恢复与重开面板后仍从锁定段起步等）；`HistoryPreferenceTests` 89 → 91（锁定字段默认空与磁盘往返）；`PanelSizeTests` 43 → 46（锁定输入框存在/宽度，尺寸框按名称定位）。
- 真机验证（2026-11-28 10:24–10:26，独立编辑器副本，后台消息，不动真实鼠标；`settings.xml` 预写 `LockedSegments=2`、`LockedFragments=[V2K-12, 0-10V]`）：
  - 1.8.9（改前，不认识锁定设置）：点“取文字”→ 主题为空；取一段 → `Picked FCU count=1`、主题 `[FCU]`；再点“取文字”→ 主题清空 `[]`，没有锁定。
  - 1.8.10（改后）：点“取文字”→ 主题自动变成 `[V2K-12/0-10V]`（锁定两段）；再取一段 → `Picked FCU count=3`、主题 `[V2K-12/0-10V/FCU]`；右键完成后 `Picking finished count=3 subject=V2K-12/0-10V/FCU`；从圈注状态再点“取文字”→ 主题回到 `[V2K-12/0-10V]`，设备号被丢掉、锁定两段保留。
  - 面板截图（含“锁定段数”行）：`output\gui-test\host-lock-1810\panel\default-panel.png`；报告：`...\before3\before3-report.txt`、`...\after3\after3-report.txt`。
- 发布（1.8.10）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.10`）并打标签 `v1.8.10`；远端校验与提交号见下方“发布记录（1.8.10）”。
- 未覆盖：锁定只作用于“取字片段”，手动改主题文字不会自动跟随锁定；锁定内容变化不会回写已有椭圆的 Subject。
### 发布记录（1.8.10）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），发布提交 `8ac36ea`（“1.8.10: 发布最终构建（dist 对齐 148992 字节版本）”），标签 `v1.8.10`（重建后指向该提交；首次推送的标签指向未对齐 dist 的构建，已 `push --force` 修正）。
- 远端校验（2026-11-28 10:33，GitHub API / git 对象）：`v1.8.10` 标签树里的 `dist/ClickCircle.Core.dll` blob = `57a1d95b3ad9e2de26dd4e48f7c5d7f1c4ee5998`、**148992 字节**，与本地 `dist` 一致；pvp `2cf96d3b…`、PDFXEdit.dll `19c1c0bb…` 同样与本地一致。`raw.githubusercontent.com` 的 CDN 缓存可能滞后几分钟，以 API/git 对象校验为准。
- `main/version.txt` 返回 `v1.8.10`。
- 本地 `dist` SHA-256：Core `C7A7FBBF89E153F9794A726851E59A44140C6FB38130D873A62E108B3B2D2D64`（148992 字节）、pvp `E208C718F25714A4CACF76388A0E15FE808E0EB198BEC34DC2D9C7A91B85C238`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：用户当前 1.8.7/1.8.9：保存文档 → 设置 → 检查更新 → `v1.8.10`；升级后主页多出“锁定段数”一行。
- 回滚：`output\gui-test\host-lock-1810\core-1.8.9\`（1.8.9）。
## 早前（1.8.9）：放置圈注时也在鼠标旁显示提示

- 需求（用户截图：连续圈注里刚放下一个椭圆）：放置也跟取文字一样有提示。
- 修改：
  - 新增纯逻辑 `PickHintText`（`src\PickHint.cs`）：`ForPick(内容, 段数)` → “已取 N 段 · 内容”，`ForStamp(主题, 个数)` → “已圈注 N 个 · 主题”，正文超过 44 字截断。
  - `StampPanel` 增加 `ShowStampHint(subject, count)`，复用取字提示的同一个 `PickHintForm`（鼠标右下 18/22px、半透明 88%、0.8 秒消失、不抢焦点不挡点击）。
  - `Plugin` 维护本次连续圈注计数 `stampCount`（每次进入圈注模式清零），放圈成功后调用提示，并在 `Circle` 日志末尾加 `session=N`。
- 测试：15 个测试程序 **962 项**通过。`PanelSizeTests` 36 → 43（新增 8 项提示文字格式：取字段数/内容/去空白/空值不弹、超长按 44 字截断、圈注个数/空主题/无主题个数）。
- 真机验证（2026-11-28 10:06–10:07，独立编辑器副本，后台消息放圈，不动真实鼠标）：
  - 1.8.8（改前）：放圈成功（日志 `Circle doc=65541 ... subject=GUI-TEST`），**没有**任何提示窗口（`HINT: none found after placing circle`）。
  - 1.8.9（改后）：放圈后提示窗口 `rect=1109,875,1255,902`、`size=146x27` = 鼠标 `(1091,853)` + `(18,22)`，文字 `已圈注 1 个 · GUI-TEST`，`layeredAlpha=224`（0.88），可见 **801 ms**；日志 `Circle ... session=1`。
  - 证据：`output\gui-test\host-stamp-hint-189\before\before-report.txt`、`after\after-report.txt`、`after\after-hint.png`。
- 发布（1.8.9）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.9`）并打标签 `v1.8.9`；远端校验与提交号见下方“发布记录（1.8.9）”。
- 未覆盖：提示正文仍按 44 字截断；`session` 计数从进入圈注模式开始（暂停/重新进入会重数），不是整个文档的圈注总数。
### 发布记录（1.8.9）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `dc603ae`（“1.8.9: 放置圈注时也在鼠标旁显示提示”），标签 `v1.8.9`。
- 远端校验（2026-11-28 10:10）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.9`；`v1.8.9/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `684D3CA2AFDF5BA2A28BA3F69139FAD61CB8160C63935869FC6ABA352169C4B6`（146432 字节）、pvp `0221CC326805B0B06AE8C33C0E57AA922A86712DC5D43E35203DC2E52A93DFFD`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：用户当前 1.8.7：保存文档 → 设置 → 检查更新 → `v1.8.9`（会一并带上 1.8.8 两个默认关闭的开关与 1.8.9 放置提示）。
- 回滚：可用 `output\gui-test\host-stamp-hint-189\core-1.8.8\`（1.8.8）。
## 早前（1.8.8）：区域识别校对窗与主题历史改成默认关闭

- 需求（用户截图：设置页里“识别后显示校对窗”“显示主题历史”两个复选框都没勾）：这俩改成默认关闭。
- 修改：`PluginSettings` 构造默认改为 `ShowOcrReview = false`、`ShowHistory = false`；旧 `settings.xml` 缺这两个节点时也按关闭处理（用户显式保存过 `true` 的仍以文件为准）。默认效果：区域识别在后台识别完直接采用结果、不弹校对窗；主题历史不显示也不记录、面板默认紧凑 480 × 190。设置页里手动打开后行为与之前完全一致。
- 测试：15 个测试程序 **955 项**通过。`HistoryPreferenceTests` 85 → 89（新增“全新面板两个开关都关闭、默认紧凑、GetSettings 保持关闭”4 项，并把旧“默认开”断言改成“默认关”）；`PanelSizeTests` 33 → 36（默认紧凑 480 × 190、打开历史时 480 × 566 与最小高度 380、历史关时按设置宽度打开但高度收紧凑）。
- 真机验证（2026-11-28 09:25，独立编辑器副本，**空数据目录**=全新设置）：
  - 启动打开面板：窗口 496 × 229、客户区 **480 × 190**（默认历史关，直接紧凑）；
  - 设置页：两个复选框都是未勾选状态（截图 `output\gui-test\host-defaults-188\settings\settings-settings.png`）；
  - 区域识别：拖框后日志只有 `OcrDirect ...`，`DIALOG=False`（默认不弹校对窗，直接采用结果），空区域 `OcrDirect empty`；
  - 报告：`output\gui-test\host-defaults-188\ocr\default-report.txt`、`...\settings\settings-report.txt`。
- 发布（1.8.8）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.8`）并打标签 `v1.8.8`；远端校验与提交号见下方“发布记录（1.8.8）”。
- 未覆盖：用户已有 `settings.xml` 里显式写过的开关值仍然优先（这次只改“没设置过”时的默认值）；默认关闭校对窗后想逐行改识别文字需要先去设置里打开。
### 发布记录（1.8.8）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `a82391f`（“1.8.8: 区域识别校对窗与主题历史默认关闭”），标签 `v1.8.8`。
- 远端校验（2026-11-28 09:27）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.8`；`v1.8.8/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `2BC35031942FFCB6A71738043A566132E2CAB76FB58310884ECA914281F4CE3D`（145920 字节）、pvp `5E2229C648482CA50B693A4296222B8CD0D0D5458A295C9A32E46F51023EAD39`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：用户当前 1.8.7：保存文档 → 设置 → 检查更新 → `v1.8.8`；升级后设置页两个开关默认不勾，行为与用户当前已关闭的状态一致。
- 回滚：历史版本可用 `output\gui-test\host-history-settings-187\core-1.8.6\`（1.8.6）与 `output\gui-test\host-ocr-review\core-1.8.4\`（1.8.4）。
## 早前（1.8.7）：历史关掉后打开设置页不再被截断

- 现象（用户截图，红箭头指向面板底部）：1.8.6 关掉“显示主题历史”后窗口收成 480 × 190，再点“设置”时设置页只露出“标注参数”就没了，下面的快捷键/标注样式/区域识别/主题历史/检查更新全被截断；用户反馈“有点问题，就直接把窗口缩回去了”。
- 根因：1.8.6 的 `ApplyHistoryVisibility` 不管当前显示的是主页面还是设置页，关掉历史就把 `ClientSize.Height` 收到 190；而设置页固定行高合计约 480，190 高的窗口装不下，设置页又没有滚动条。
- 修改：
  - `ShowSettings(true)`：历史关着且当前是紧凑高度时，先把面板临时撑回关闭前的高度（默认 566），并把最小高度恢复成 380；
  - 在设置页里把“显示主题历史”取消勾选时不再立刻缩窗口（只隐藏历史区并把当前高度记成恢复高度），等点“返回主页面”后再收成 190；
  - `ShowSettings(false)`：历史关着时收紧凑并记录当前高度；
  - 设置页加 `AutoScroll = true` 兜底，万一窗口又被拖小也能滚动看全。
- 测试：15 个测试程序 **948 项**通过。`HistoryPreferenceTests` 82 → 85 项（+3）：设置页打开时保持高度不被压扁、在设置页里关历史不立即缩、返回主页面后收成紧凑高度。
- 真机验证（2026-11-28 09:16–09:19，独立编辑器副本，`settings.xml` 预写 `ShowHistory=false`，UIAutomation 打开面板/设置页）：
  - 1.8.6（改前）：面板窗口 496 × 229（客户区 480 × 190），点开设置后窗口仍是 480 × 190，设置页只显示到“标注参数”即被截断 → `output\gui-test\host-history-settings-187\settings-before\settings-before-settings.png`。
  - 1.8.7（改后）：点开设置 → 面板自动变成 496 × 605（客户区 **480 × 566**），设置页完整显示（分隔符/圈注尺寸/快捷键/标注样式/区域识别/主题历史/检查更新） → `settings-after-back5\settings-after-back-settings.png`。
  - “返回主页面”后再回缩：UIA 下该 LinkLabel 不暴露 Invoke/Legacy DefaultAction，自动点击没能触发；这一步由单元测试覆盖（`ShowSettings(false)` 后客户区收为 190）。
  - 报告：`output\gui-test\host-history-settings-187\*\*-report.txt`。
- 发布（1.8.7）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.7`）并打标签 `v1.8.7`；远端校验与提交号见下方“发布记录（1.8.7）”。
- 未覆盖：设置页打开期间用户手动拖动改变高度后返回主页面，会按拖动后的高度作为恢复高度；UIA 无法自动点“返回主页面”链接。
### 发布记录（1.8.7）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `cb0616a`（“1.8.7: 历史关掉后打开设置页不再被截断”），标签 `v1.8.7`。
- 远端校验（2026-11-28 09:21）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.7`；`v1.8.7/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `CEE4ABE3A4999215B9A31AB722C8D1FF36EB61C8D248DE7DECE54FF86AE70BD0`（145920 字节）、pvp `79C724F6C782C57789DC1139BA891AED98381DC2F3A5550EF23A435F81643D7A`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：用户当前 1.8.6：保存文档 → 设置 → 检查更新 → `v1.8.7`；确认后 helper 自动关闭编辑器、备份、替换并重启，失败回滚。
- 回滚：`output\gui-test\host-history-settings-187\core-1.8.6\`（1.8.6）、`output\gui-test\host-history-186\core-1.8.5` 不存在时可用 `output\gui-test\host-ocr-review\core-1.8.4\`（1.8.4）。
## 早前（1.8.6）：设置页新增“显示主题历史”开关

- 需求（用户截图，蓝框圈住面板里的“主题历史”区域）：把历史主题关掉，在设置里弄个开关。
- 修改：
  - `PluginSettings` 新增 `ShowHistory`（默认 `true`；旧 `settings.xml` 没有该节点也按 `true`）。设置页“区域识别”下面新增“主题历史”一行 + 复选框“显示主题历史（关闭后不记录、不显示）”，改动立即写回 `settings.xml`。
  - 关掉后：主页面隐藏主题历史区（标题/搜索框/表格），主布局第 4 行高度置 0，面板客户区收成 **480 × 190**（原 480 × 566），并且不再记录新主题（`RememberCurrentSubject` 直接返回）。重新打开时恢复关闭前的高度并继续记录。
  - `OnLoad` 的最小尺寸改为按当前开关状态计算（显示 380 / 隐藏 190）：真机验证时发现启动时会被旧最小高度顶回 380，已修掉（改后实测客户区 190）。
- 测试：15 个测试程序 **945 项**通过。`HistoryPreferenceTests` 71 → 82 项（+11）：`ShowHistory` 往返、默认 `true`、旧设置默认 `true`、设置页复选框默认勾选、取消勾选立即生效并保存、历史表格隐藏、面板收成紧凑高度、隐藏期间不记录、重新打开恢复表格与高度、继续记录。
- 真机验证（2026-11-27 17:42–17:45，独立编辑器副本，UIAutomation 打开面板/设置页）：
  - 1.8.5（改前）：面板窗口 496 × 605，客户区 480 × 566，主题历史可见。
  - 1.8.6 默认（开关开）：同样 480 × 566、主题历史可见（回归不变）。
  - 1.8.6 关掉（`settings.xml` 里 `ShowHistory=false`）：面板窗口 **496 × 229**、客户区 **480 × 190**，历史区消失；`settings.xml` 读回 `ShowHistory=false`。
  - 设置页截图可见两个开关：“区域识别 ☑ 识别后显示校对窗”“主题历史 ☑ 显示主题历史”。
  - 证据：`output\gui-test\host-history-186\{before,default,off,settings}\*-panel.png`、`settings\settings-settings.png`、对应 `*-report.txt`。
- 发布（1.8.6）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.6`）并打标签 `v1.8.6`；远端校验与提交号见下方“发布记录（1.8.6）”。
- 未覆盖：关掉历史后原有主题（含固定项）仍保留在 `settings.xml`，重新打开即可恢复；隐藏状态下用户手动把面板拖大不会自动缩回；没有做“只隐藏界面、仍继续记录”的独立选项。
### 发布记录（1.8.6）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `ae5d6c8`（“1.8.6: 设置页新增“显示主题历史”开关”），标签 `v1.8.6`。
- 远端校验（2026-11-27 17:52）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.6`；`v1.8.6/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `7FE8724031108BC109BA0A4FCAA821365D1DE2E8EF54FAA757D1BC869418ABD6`（145408 字节）、pvp `796227E4963A6041B2366D04AD86258BD2B9785B9BCE9963A1F81B64BEB4D1B3`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：用户当前运行 1.8.4（编辑器已打开、有未保存文档）：保存文档 → 设置 → 检查更新 → `v1.8.6`（含 1.8.5 校对窗开关 + 1.8.6 主题历史开关）；确认后 helper 自动关闭编辑器、备份、替换并重启，失败回滚。
- 回滚：`output\gui-test\host-history-186\core-1.8.5\`（1.8.5）、`output\gui-test\host-ocr-review\core-1.8.4\`（1.8.4）、`output\gui-test\host-toast-184\core-1.8.2\`（1.8.2），需要时退出编辑器后复制回 `Plugins.x64`。
## 早前（1.8.5）：设置页新增“区域识别后显示校对窗”开关

- 需求（用户截图，红箭头指向“区域识别校对”窗口）：区域识别弹的校对窗要在设置里加个开关。
- 修改：
  - `PluginSettings` 新增 `ShowOcrReview`（默认 `true`；旧 `settings.xml` 没有该节点也按 `true`）。设置页在“标注样式”下面新增“区域识别”一行 + 复选框“识别后显示校对窗（关闭则直接采用结果）”，改动立即写回 `settings.xml`。
  - 关掉开关后走新的 `Plugin.StartDirectOcr` / `CompleteDirectOcr`：后台调用本地 OCR（上次语言不可用时自动回退“自动”再试一次），完成后直接采用结果——取字过程中就并入当前取字并提示；否则填入主题并进入连续圈注；识别不到文字时状态提示“未识别到文字”、保留原主题。全程不弹校对窗。
- 测试：15 个测试程序 **934 项**通过。`HistoryPreferenceTests` 64 → 71 项（+7）：`ShowOcrReview=false` 往返、旧设置默认 `true`、新设置默认 `true`、设置页复选框默认勾选、取消勾选立即生效并写入 `GetSettings()`、应用设置静默恢复、默认设置重新勾选。
- 真机验证（2026-11-27 17:16–17:19，独立编辑器副本，后台消息拖框触发 OCR，不动真实鼠标）：
  - 1.8.4（改前，没有开关）：拖框 → 日志 `OcrReview ...`、`DIALOG=True`（弹“区域识别校对”窗）。
  - 1.8.5 开关默认开：拖框 → 同样 `OcrReview ...`、`DIALOG=True`，原有行为不变。
  - 1.8.5 关掉开关（`settings.xml` 里 `ShowOcrReview=false`，面板状态“请框选要识别的区域”）：拖框 → 日志 `OcrDirect ...`、`DIALOG=False`；空白区域 → `OcrDirect empty`（状态“未识别到文字”）；再框到 FCU 大字 → `OcrDirectPicked count=2 total=2 subject=ZIJ/FCU`，结果直接进入圈注，全程没有弹窗。
  - 在真实面板设置页点复选框后，`settings.xml` 立即出现 `<ShowOcrReview>false</ShowOcrReview>`（17:17:22 那次量测）。
  - 报告：`output\gui-test\host-ocr-review\{before,default,after}\*-report.txt`；校对窗截图 `before\before-ocr-dialog.png`、`default\default-ocr-dialog.png`。
- 发布（1.8.5）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.5`）并打标签 `v1.8.5`；远端校验与提交号见下方“发布记录（1.8.5）”。
- 未覆盖：关掉校对窗后无法手动修改识别文字（需要改时可再打开开关）；直接识别的语言回退只重试一次；没有做“只在识别失败时才弹校对窗”的第三种模式。
### 发布记录（1.8.5）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `a78d35f`（“1.8.5: 设置页新增区域识别校对窗开关”），标签 `v1.8.5`。
- 远端校验（2026-11-27 17:25）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.5`；`v1.8.5/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `03885C35B96C27387D8E251D4D9630D143B49FAB0BBEAD073C9DE5529FE1FBC5`（143360 字节）、pvp `385A4A03466AACFEA28E483B9671BA85A9F785295F7F54676CCD8A637925891B`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 最终 dist 冒烟（2026-11-27 17:22，独立副本）：日志 `Version 1.8.5`，区域识别默认仍弹校对窗（`OcrReview` + `DIALOG=True`）。
- 本机升级方式：编辑器里保存文档 → 设置 → 检查更新：从 1.8.2 直接升到 `v1.8.5`（包含 1.8.3 标注标签取字、1.8.4 取字提示、1.8.5 校对窗开关）；确认后 helper 自动关闭编辑器、备份、替换并重启，失败回滚。
- 回滚：`output\gui-test\host-ocr-review\core-1.8.4\` 留着 1.8.4 的 Core/pvp，`output\gui-test\host-toast-184\core-1.8.2\` 留着 1.8.2，需要时退出编辑器后复制回 `Plugins.x64`。
## 早前（1.8.4）：取字提示回到鼠标旁、半透明、0.8 秒消失

- 反馈（用户截图）：1.8.2 的取字提示被固定在屏幕工作区右下角，用户在图纸上取字时看不到（截图里提示贴在编辑器状态栏右下角），要求“还原回来、加点透明度、消失的速度快一点”。
- 修改：
  - `PickHintPlacement` 由 `BottomRight(工作区, 尺寸)` 改回 `NearCursor(鼠标位置, 尺寸, 工作区)`：默认在鼠标右下偏移 18 / 22 像素；靠近屏幕右/下边缘时自动翻到鼠标左上；鼠标贴左上边缘时夹进工作区，不会跑到屏幕外。
  - `PickHintForm`：整体不透明度 `Opacity = 0.88`（半透明），自动隐藏时间 1400 ms → 800 ms；`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT` 不变，仍然不抢焦点、不挡鼠标点击。
- 测试：15 个测试程序 927 项通过。`PanelSizeTests` 仍 33 项，其中取字提示落点 4 项改为覆盖：鼠标右下偏移、右/下边缘翻到左上、鼠标贴左上边缘夹紧、副屏（非零工作区原点）落点。
- 真机验证（2026-11-27 16:40–16:41，独立编辑器副本，后台消息取字、不动真实鼠标）：
  - 1.8.2：提示 rect=`1806,1001,1908,1028`（固定在屏幕右下角，离鼠标 240 / 885 px）、`layeredAlpha=none`（不透明）、可见 1373 ms。
  - 1.8.4：提示 rect=`518,649,620,676` = 鼠标 `(500,627)` + `(18,22)`、`layeredAlpha=224`（0.88 × 255）、可见 804 ms，之后窗口隐藏（`AFTER_HIDDEN=True`）。
  - 截图：`output\gui-test\host-toast-184\before\before-hint.png` 与 `after\after-hint.png`（后者能看到图纸线条透过提示背景）；量测报告在同目录 `before-report.txt`、`after-report.txt`。
- 发布（1.8.4）：已推送到 GitHub `weixi11/ClickCircle-` 的 `main`（`version.txt` = `v1.8.4`）并打标签 `v1.8.4`；远端校验与提交号见下方“发布记录（1.8.4）”。
- 未覆盖：提示文字过长时仍按 44 字截断；“半透明”用的是窗口整体 Opacity（不改变文字颜色）；未做淡出动画。

### 发布记录（1.8.4）

- 仓库：`https://github.com/weixi11/ClickCircle-`（公开），提交 `6319734`（“1.8.4: 取字提示回到鼠标旁、半透明、0.8 秒消失”），标签 `v1.8.4`。
- 远端校验（2026-11-27 16:44）：`raw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt` 返回 `v1.8.4`；`v1.8.4/dist/` 三个文件下载后 SHA-256 与本地 `dist` 完全一致：Core `74194B5EBF9AA2C7894DA143B86034A65EAF2EF03C0EC75CB7AA4F9B2C81B4BB`（140288 字节）、pvp `6577E51DF9BE85FFE7D0580E0FEF77A6410A72F959B1A1EFE38C0FF41CD4D5F5`（3072）、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`（868864）。
- 本机升级方式：编辑器里保存文档 → 设置 → 检查更新：从 1.8.2 直接升到 `v1.8.4`（包含 1.8.3 标注标签取字修复与 1.8.4 取字提示改动）；确认后 helper 自动关闭编辑器、备份、替换并重启，失败回滚。升级后 `plugin.log` 应出现 `Version 1.8.4`。
- 回滚：`output\gui-test\host-toast-184\core-1.8.2\` 留着 1.8.2 三文件中的 Core/pvp，`output\install-backup-1.8.1-20261125-155826` 留着 1.8.1，需要时退出编辑器后复制回 `Plugins.x64` 即可。
## 早前（1.8.3）：点击位置在测量标注/批注标签上也能取字

- 现象（用户反馈，附截图）：图纸第 5 页上点击绿色尺寸标签（`300X200 EAD`、`400X200 EAD` 等）取不了字；面板处于取字状态，点了没有结果，也没提示取到什么。
- 根因：这些标签不是页面文字层，而是 **PDF-XChange 测量标注（PolyLineDimension）的标签**：标签行和测量值存在标注 `/Contents` 里（例如 `EAD\300X200\` + 换行 + `50.89 m `），由编辑器按标注样式渲染成屏幕上的 “300X200 EAD”。插件原来只读 `IPXC_Page.GetText`（页面文字层）和 SHX 批注，点在标注标签上自然取不到；实测把 `GetText` 换成 `GPTOF_IncludeComments | GPTOF_IncludeFormFields` 也不会把标注内容放进页面文字层（标注矩形内文字字符数仍是 0）。
- 修复：
  - 新增 `src\AnnotationLabel.cs`：把 `/Contents` 还原成可见标签——去掉末尾测量值行（`50.89 m` 这类），按反斜杠分行、去掉空段后**反序**用空格连接（`EAD\300X200\` → `300X200 EAD`；`EAL/3000x550/1/` 原样保留）；没有反斜杠的普通批注按原顺序取文字。
  - `TextPicker.TryGetWord` 变成三级回退：**页面文字层 → SHX 原文 → 标注标签**。标注标签只认点击位置附近（到标注折线的距离 < 8 pt，统一取最近的一条，同样近取上层）且可见（跳过 Invisible/Hidden/NoView）的标注；插件自己画的椭圆 Contents 为空，不受影响。这样点到标注外框大片空白不会乱取字。
- 构建与测试：15 个测试程序共 927 项通过。`TextPickerTests` 15 → 34 项，新增 19 项覆盖标签还原（`EAD\300X200\`→`300X200 EAD`、三行 `EAG\500X500\1\`→`1 500X500 EAG`、斜杠标签原样、普通批注按原序、只有测量值/空内容返回 null）与测量值行识别（`50.89 m`、`0.6 m²`、`1,234.5` 是；`300X200`、`300 x 200`、`EAD` 不是）。其余：HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 111、ShxText 91、OCR 142、HistoryPreference 64、Cursor 8、RightClick 21、LeftRelease 10、PanelSize 33、Update 31、AnnotStyle 39。
- 构建方式：同前，用系统 `csc.exe` / `ilasm.exe`、以 `dist\PDFXEdit.dll` 为互操作引用编译；`ClickCircle.Core.dll` 139776 字节、`ClickCircle.pvp` 3072 字节。`build.ps1` 的编译清单与 `package.ps1` 白名单加入 `src\AnnotationLabel.cs`，`dist` 三者 SHA-256：Core `3D3A428A8CA57D0D6B3DF8A567AF721ADE8DC1DDFE8D9F818245D20D3A365FBB`、pvp `D5074B8E1D19C2028580C8E678BB3AD6F23E197C8F595F9ED816E0F112E535AB`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。
- 真机验证（2026-11-25 17:47–17:52，独立编辑器副本 `F:\桌面1\PDFEditor - 副本`）：写了一个一次性验收插件，直接在真实编辑器里打开用户那张 `0535A-AC-风管.pdf` 第 5 页，沿标注折线每 2 pt 取样调用**正式** `TextPicker.TryGetWord`（不弹 UI、不动鼠标）：
  - 改前（1.8.2 Core 138240 字节）：annot0/12/14/26/42 全部取不到（`<none>`）；页面原生文字点仍取到 `1`（原生路径基线）；空白处、标注外框角落均 `<none>`。
  - 改后（1.8.3 Core 139776 字节）：annot0 → `300X200 EAD`、annot12 → `400X200 EAD`、annot14 → `600X400 EAD`、annot26 → `200X150 EAD`（旋转标签）、annot42 → `SILENCER/400x200/1/`、annot30 → `500X300 PAD`；原生文字、空白、外框角落结果与改前一致（原生优先，8 pt 容差挡住外框空白）。
  - 报告：`output\gui-test\probe\pick-before.txt`、`pick-after.txt`；渲染真值图（PDF-XChange 自己渲染的标签外观）：`output\gui-test\probe\render-annot0.png`、`render-annot12.png`、`render-annot26.png`。
- 发布（1.8.3）：本轮不手工覆盖本机 `Plugins.x64`，而是把 1.8.3 推到 GitHub 仓库 `weixi11/ClickCircle-` 的 `main`（`version.txt` 写成 `v1.8.3`）并打标签 `v1.8.3`，三个发行文件随标签 `dist/` 分发；编辑器里「设置 → 检查更新」走原有在线更新流程（有未保存文档会拒绝，确认后 helper 自动关闭编辑器、替换文件并重启，失败回滚）。远端校验与提交号见下方“发布记录（1.8.3）”。
- 未覆盖：框选取字目前仍只读页面文字层，框选尺寸标注标签未支持（会落到 OCR 或取不到）；“反序拼接”规则来自本图纸的 PDF-XChange 测量标注实测，其它软件生成的怪 Contents 需要再核对；多行 FreeText 批注未真机验证。

### 发布记录（1.8.3）

- 仓库：https://github.com/weixi11/ClickCircle-（公开），提交 6f32d3（“1.8.1-1.8.3: 右键菜单/取字提示/拖动落圈/标注标签取字”），标签 1.8.3。
- 远端校验（2026-11-25 18:52）：aw.githubusercontent.com/weixi11/ClickCircle-/main/version.txt 返回 1.8.3；1.8.3/dist/ 三个文件下载后 SHA-256 与本地 dist 完全一致：Core 3D3A428A8CA57D0D6B3DF8A567AF721ADE8DC1DDFE8D9F818245D20D3A365FBB（139776 字节）、pvp D5074B8E1D19C2028580C8E678BB3AD6F23E197C8F595F9ED816E0F112E535AB（3072）、PDFXEdit.dll 2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1（868864）。
- 本机仍在运行 1.8.2，升级方式：编辑器里**保存文档**后 → 设置 → 检查更新：发现 1.8.3 → 确认 → 检查所有文档未保存修改 → 下载三个文件到临时目录 → 生成独立批处理，等编辑器退出后备份旧文件、替换、重启（失败回滚）。升级后 plugin.log 应出现 Version 1.8.3，面板标题显示 1.8.3。
- 回滚：本机 CircleStampPlugin\output\install-backup-1.8.1-20261125-155826 保留着 1.8.1 三文件；如需退回任意旧版，把对应 dist 三文件复制回 Plugins.x64（先退出编辑器）。
## 早前（1.8.2）：圈注时按住左键拖动也能落圈

- 现象（用户反馈）：连续圈注时必须“原地不动点一下”才放圈；按住左键时鼠标稍微拖动（或手抖），松开后什么也不画，只能重新点。截图里一直用“继续标注”逐个点圈，手一抖就漏一个。
- 根因：`CircleTool.CompleteGesture` 只在 `!dragged && insidePage` 时调用 `HandlePageClick`；圈注状态没有框选含义，于是“按下-拖动-松开”的整个手势被丢弃。取字状态的拖动是框选，必须保持。
- 修复：新增 `src\LeftReleaseRouting.cs`（`LeftReleaseAction`：`None` / `Click` / `BoxSelect`，入口 `For(picking, dragged, releaseInsidePage)`）；`CompleteGesture` 改为：取字拖动 → 框选；其余落在页内的释放（无论是否拖动过）→ 一次点击。圈注时按下-拖动-松开在松开点落圈（`HandlePageClick` 用松开点换算页面坐标），取字拖动仍是框选、行为不变。
- 构建与测试：15 个测试程序共 908 项通过。新增 `tests\LeftReleaseTests.cs` 10 项（圈注单击/拖动都落圈、页外不落圈、取字单击取字、取字拖动框选含页外、拖动与单击结果一致、取字与圈注动作不同）；其余：TextPicker 15、HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 111、ShxText 91、OCR 142、HistoryPreference 64、Cursor 8、RightClick 21、PanelSize 33、Update 31、AnnotStyle 39。
- 构建方式：同 1.8.1，用系统 `csc.exe` / `ilasm.exe`、以 `dist\PDFXEdit.dll` 为互操作引用编译；`ClickCircle.Core.dll` 138240 字节、`ClickCircle.pvp` 3072 字节。`build.ps1` 的编译/测试清单与 `package.ps1` 白名单同步加入 `LeftReleaseRouting.cs` / `LeftReleaseTests.cs`。
- 装机：2026-11-25 15:58:26，先把 1.8.1 三文件、`settings.xml`、`plugin.log` 备份到 `output\install-backup-1.8.1-20261125-155826`，再写入 1.8.2；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `053AA031CAF6D8DF456BEC7B0832D99DBF81F2BC27B1D6E6768BFF65DD5CE1BC`、pvp `78327FC2676835F3270F19B314C74D99C706553EC5A56A9A7EC13377BAEA59AA`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。安装前编辑器已正常卸载（日志 `15:56:25 Unload OK`），重开后加载 1.8.2。
- 真机验证（2026-11-25 15:37–15:39，独立编辑器副本 `F:\桌面1\PDFEditor - 副本`；插件数据目录用 `CLICKCIRCLE_DATA_DIR` 隔离，窗口 `SW_SHOWNOACTIVATE` 不抢前台，鼠标消息投递、真实光标不动）：
  - 改前（旧 1.8.0 Core 137216 字节）：面板“继续标注”→单击日志 `Circle ...`；**按住左键拖动 60×40 px 后松开没有任何日志、没有新圈**（复现“拖动就没有标注”）。
  - 改后（新 1.8.2 Core 138240 字节）：同样的拖动日志 `Circle doc=65541 page=0 x=374.5763 y=350.4629 subject=GUI-TEST`（在松开点落圈）；取字状态拖动仍是 `BoxPicked count=2`（框选未受影响）；右键完成标注复测 `Stamping finished` 且 `RESULT: NO-NEW-WINDOW`（1.8.1 的右键菜单修复没有回退）。见 `output\gui-test\host-drag-before\drag-before-report.txt`、`host-drag-after\drag-after-report.txt`、`host-menu-after\menu-after-report.txt`。
- 未覆盖：真实鼠标拖动（尤其高 DPI 或慢速拖动）的手感需人工确认；圈注状态长距离拖动现在也会在松开处落一个圈，若需要“按住拖动连续放多个圈”是另一个功能，本轮未实现。

## 早前（1.8.1）：右键完成不再弹菜单、取字提示不挡图纸

- 现象（用户反馈，附截图）：连续圈注中右键完成标注，面板状态已变成“已完成标注”，但编辑器的页面右键菜单同时也弹出来；取字成功后鼠标旁的小提示正好盖住图纸上下一次要点的编号。
- 根因（右键菜单）：`FinishStamping` 走 `PauseWithStatus`，在 `WM_RBUTTONDOWN` 回调里同步 `ActivateTool(previous)` 把页面工具交回编辑器。工具一停用，紧随其后的 `WM_RBUTTONUP` / `WM_CONTEXTMENU` 就不再经过页面工具，原先 750 ms 的“吞掉窗口”对这条路径完全失效。取字完成（`FinishPicking`）仍处于圈注状态、工具不换，所以只有“右键完成标注”会漏菜单；1.6.2 的验收记录本来就把“右键完成不弹菜单”列为未覆盖、需人工确认。
- 修复（右键菜单）：
  - `src\RightClickRouting.cs` 新增 `ContextMenuGuard`：集中右键消息号（`0x0204` / `0x0205` / `0x007B`）、吞掉窗口 750 ms、交回工具延迟 300 ms，以及 `ShouldSwallow` / `ShouldRestore` 判断；`CircleTool` 只通过它处理右键消息。
  - `src\Plugin.cs`：`FinishStamping` 改为延迟交回工具（300 ms，由现有 150 ms 定时器执行 `RestoreTools`）；延迟期间页面工具仍处于激活状态，吃掉这次右键的抬起与菜单请求，之后再交回编辑器。`Activate` / `Unload` 清掉待执行的交回，避免状态串线。
- 修复（取字提示）：`src\PickHint.cs` 新增 `PickHintPlacement.BottomRight(workingArea, size)`，提示固定落在鼠标所在屏幕工作区的右下角（12 px 边距），不再跟随鼠标；仍不抢焦点、`WS_EX_TRANSPARENT` 不挡点击、1.4 秒后自动隐藏，文案不变。
- 修复（状态行排版）：完成标注的状态文案由“已完成标注，主题保留可继续标注”收短为“已完成标注，可继续标注”，取字提示文案也收短（“点击文字取字，右键完成”“未点中文字，可拖框识别”），默认面板宽度下状态行不再折成两行；`statusLabel` 显式 `AutoSize = false`，继续保留 `AutoEllipsis`。
- 构建与测试：14 个测试程序共 898 项通过。新增 `ContextMenuGuard` 13 项（窗口内的右键抬起/菜单请求被吞、右键按下不吞、左键不受影响、窗口过期不吞、交回延迟必须短于吞掉窗口等），`RightClickTests` 8 → 21 项；新增 `PickHintPlacement` 3 项（右下角落点、副屏偏移、工作区过小时夹紧），`PanelSizeTests` 共 33 项。其余：TextPicker 15、HotkeyBinding 92、HotkeyPanel 35、HotkeyService 145、RegionPicker 71、SeparatorPanel 111、ShxText 91、OCR 142、HistoryPreference 64、Cursor 8、Update 31、AnnotStyle 39。
- 构建方式：本机仍没有 `build.ps1` 需要的 `PXCEditorCore.x64.dll`，按既有记录用系统 `csc.exe` / `ilasm.exe`、以 `dist\PDFXEdit.dll` 为互操作引用（`/codepage:65001`，资源 `tools\ocr-helper.ps1`）编译；`ClickCircle.Core.dll` 137728 字节、`ClickCircle.pvp` 3072 字节。
- 装机：2026-11-25 14:28:53，旧 1.8.0 三文件、`settings.xml`、`plugin.log` 备份到 `output\install-backup-1.8.0-20261125-142853`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `1C86630949FDAA6A6F7D8DABEF681A08FB56A9814E6BF96DD3A1D474EA779917`、pvp `9FF171482F7EC09B87EEAA3B392ACD2982E3CAF96BFC8FCBBF37B49BE2797533`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。重开编辑器日志 `14:29:57 Version 1.8.1`、`Init OK`，快捷键 `Q`、10×10 mm、主题历史保留。
- 真机验证（2026-11-25 14:46–14:48，独立编辑器副本 `F:\桌面1\PDFEditor - 副本`）：为不干扰正在使用的编辑器，插件数据目录用 `CLICKCIRCLE_DATA_DIR` 隔离；副本窗口以 `SW_SHOWNOACTIVATE` 显示、不抢前台，鼠标输入通过向页面窗口投递消息完成（真实光标/键盘不动），面板用 UI Automation 打开、`BM_CLICK` 触发。
  - 改前（旧 `ClickCircle.Core.dll` 137216 字节）：面板“继续标注”→左键放圈日志 `Circle ... subject=GUI-TEST`→右键日志 `Stamping finished subject=GUI-TEST`，同时新增 5 个可见 `UIX:Window` 弹出窗（右侧右键菜单及其滚动按钮），即菜单确实弹出；见 `output\gui-test\host-before2\before2-report.txt`。
  - 改后（新 Core 137728 字节）：同样步骤日志 `Stamping finished subject=GUI-TEST`，**没有新增可见窗口**（`RESULT: NO-NEW-WINDOW`）；见 `output\gui-test\host-after2\after2-report.txt`。
  - 取字提示：取字成功日志 `Picked FCU count=1`，提示窗矩形 `(1806,1001)-(1908,1028)`，即 1920×1040 工作区右下角留 12 px，与鼠标位置无关；截图 `output\gui-test\host-toast\toast-bottom-right.png`。
  - 面板状态行离屏渲染：改前 `output\gui-test\panel-1.8.0-finish.png`（“已完成标注，主题保留可继续标注”折成两行），改后 `output\gui-test\panel-1.8.1-finish.png`（“已完成标注，可继续标注”单行）。
  - 验证脚本与测试 PDF 留在 `output\gui-test\`：`bg-host-test2.ps1`（右键菜单前后对比）、`bg-host-toast.ps1`（提示位置）、`ClickCircle_gui_fixture.pdf`。
- 未覆盖：150%/200% DPI 下提示位置与状态行观感需目视；用户真实鼠标右键的手感与菜单消失瞬间的视觉表现仍需人工确认（自动化用消息投递、未动真实光标）；多屏时提示落在鼠标所在屏幕的右下角。

## 早前（1.8.0）：标注字段、类型与样式扩展

- 需求：不只套主题，还要作者、标签/正文；不只椭圆，还要周长、面积等测量标注；颜色与其它参数可配。
- 方案（已与用户确认）：仍用编辑器原生工具绘制（周长/面积多点绘制保持原生交互），插件监听 `e.annots.inserted`，对新增标注自动写入样式。
- 新增文件：`src\AnnotStyle.cs`（样式模型，同时作为 settings.xml 序列化载体）、`src\AnnotStyler.cs`（把样式写进 `IPXC_AnnotData`：主题/作者/标签、SColor/FColor、边框宽度与虚线、不透明度、测量比例尺与数字格式）、`src\AnnotWatcher.cs`（按文档订阅 `e.annots.inserted`，定时同步新开/关闭的文档）、`src\AnnotStyleDialog.cs`（样式编辑对话框）。
- 面板：设置页“快捷键”下方新增“标注样式”一行，显示当前状态（如“测量标注 · 周长”）并打开对话框；样式随 settings.xml 保存，升级不丢。
- 测量参数：`IPXC_MeasureRL` 设置 `Ratio`（“1 mm = 0.5 m”），并用 `IPXC_NumberFormatArray`/`IPXC_NumberFormatItem` 设置距离与面积系数、单位、小数位、千位分隔。
- 兼容：样式默认关闭（`Enabled=false`），不启用时监听器直接返回，行为与 1.7.7 完全一致；旧版 settings.xml 缺少 `<Style>` 时自动补默认值。
- 构建与测试：14 个测试程序共 881 项通过，其中 `AnnotStyleTests` 39 项覆盖默认值、数值夹取、颜色换算（含 A=255 修正）、比例尺换算、克隆隔离、settings.xml 往返与旧配置补默认。
- 待真机确认：`e.annots.inserted` 事件订阅是否被宿主接受、自动套用后的外观刷新、测量比例尺在周长/面积上的显示效果（日志会写 `Style watcher subscribed doc=` 与 `Style applied count=`）。

## 早前（1.7.7）：修复取字途中按快捷键“没反应”

- 现象：取字过程中按 `Q` 没有反应（日志里有时出现 `Picking restarted`、有时完全没有记录），用户必须先点一下页面再按才生效。
- 根因（已用日志证实）：`HotkeyScope.CanTrigger` 要求页面视图当前持有输入焦点（`pageObject.HasInputFocus` 且焦点窗口正是页面画布）。取字过程中出现过的焦点变化（点面板、弹取字提示窗、重新取字时再次 `SetInputFocus`）会让该条件不成立，热键被静默丢弃，没有任何提示。复现日志：连续三次按 Q 只记录 `Hotkey ignored: 页面视图没有输入焦点`。
- 修复：编辑器是前台、键盘焦点在编辑器进程内、且不在原生输入框/面板输入框/标注文字编辑区时，Q 一律响应；不再要求页面视图持有输入焦点。`CanTrigger` 增加 `toolActive` 参数与 `out reason`，被忽略时日志写 `Hotkey ignored: <原因>`，触发时写 `Hotkey fired (mode=…)`，便于下次定位。
- 真机验证（2026-11-23 12:41，`GF Cable Containment 202060918-0300.pdf`）：点击页面后连按三次 Q，日志依次 `Hotkey fired (mode=Paused)` → `Picking doc=65541`、`Hotkey fired (mode=Picking)` → `Picking restarted`、同样再重启一次；Esc 正常暂停。同一操作在修复前三次全部被 `Hotkey ignored: 页面视图没有输入焦点` 拦下。
- 反馈：重新取字时在鼠标旁提示“重新取字，点击文字继续”，不再只清空主题而没有可见反馈。
- 构建与测试：13 个测试程序共 842 项通过（快捷键门控依赖真实编辑器，无法单元测试）。

## 早前（1.7.6）：设置页提示区

- 现象：检查更新的结果（“已是最新版本”）显示在主页状态行，把“待取字/已暂停/标注中”顶掉；设置页中部大片空白，没有说明或结果。
- 修复：设置页新增提示区（“快捷键”与“软件版本”之间）：一行静态说明“取字与主题历史在主页操作；这里的分隔符、圈注尺寸和快捷键修改后立即生效”，下面一行动态显示更新状态（正在检查 / 已是最新 / 正在下载 / 已取消 / 失败原因，失败为红色）。
- 主页状态行恢复只显示当前模式与取字段数，不再被更新消息覆盖。
- 构建与测试：13 个测试程序共 842 项通过（未改动用例）；提示区排版需真机目视。
- 未覆盖：长错误文本在 440 像素最小宽度下的换行观感需确认。

## 早前（1.7.5）：取字中再按 Q 重新取字

- 需求：取字状态下再按 `Q`（或再点“取文字”）应当重新开始一轮取字，而不是继续往上一轮累积。
- 实现：`Activate(Picking)` 在 `mode == Picking` 时清空已取片段并把主题框清空，状态行回到“点击文字可多次取，右键完成取字”；日志新增 `Picking restarted doc=<ID>`。
- 第一次从暂停/圈注进入取字仍保持原行为：主题文本先保留，第一次取字替换。
- 构建与测试：13 个测试程序共 842 项通过（未改动用例）；重启行为需真机确认。
- 未覆盖：重新取字会丢弃上一轮尚未完成的主题，已完成的主题仍在主题历史里。

## 早前（1.7.4）：取字时在鼠标旁显示短暂提示

- 新增 `src\PickHint.cs`：无边框、置顶、`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT` 的小窗口，只显示一行文字，1.4 秒后自动隐藏；不抢焦点、不挡鼠标点击，靠近屏幕边缘时自动翻到鼠标另一侧。
- 触发点：单击取字、框选取字、取字过程中自动 OCR 确认后都会显示“已取 N 段 · 内容”；内容超过 44 字截断加省略号；连续取字时复用同一个提示窗口并重新计时。
- 面板新增 `ShowPickHint(fragment, count)`；`Dispose` 时释放提示窗口。提示窗口不进入任务栏，也不参与 Alt+Tab。
- 构建与测试：`build.ps1`/`package.ps1` 白名单加入 `PickHint.cs`；13 个测试程序共 842 项通过（未改动用例）。提示为纯 UI 反馈，无法在无宿主的单元测试中截图验证。
- 真机验证（2026-11-19 17:18）：1.7.3 点“检查更新”自动升级到 1.7.4 成功，`%TEMP%\ClickCircle-update.log` 记录 `helper start pid=28080` → `replaced`；重开日志 `Version 1.7.4`。同日放圈日志 `Circle ... index=515 count=516`，确认新椭圆已置顶；截图中取字提示显示“已取 2 段 · CGMN-S2-03-23-2-2”。
- 未覆盖：提示的出现位置与淡出观感需在真机上目视确认。

## 早前（1.7.3）：修复中文路径导致的更新失败

- 现象：1.7.1 自动更新到 1.7.2 时，编辑器关闭后弹出 Windows 错误“找不到 `F:\??1\PDFEditor\PDFXEdit.exe`”，文件没有替换（日志 `rollback`），临时目录残留。
- 根因：更新批处理用 `Encoding.ASCII` 写入，`F:\桌面1\...` 里的中文被写成 `?`（`F:\??1\...`）；`TARGET` 和 `EXE` 同时失效，复制失败进入回滚，重启又因为错误路径弹出系统错误框，批处理卡在对话框上，连清理都没执行。
- 修复：批处理改用 `Encoding.Default`（系统代码页，中文 Windows 为 GBK）；重启前加 `if exist "%EXE%"` 守卫，路径无效时不再弹框；仍写入 `%TEMP%\ClickCircle-update.log`。
- 验证：临时目录改用**中文路径**重跑两条路径（正常退出、卡住强杀），均完成替换、重启、清理、自删，`FAILURES=0`；`UpdateTests` 增补 3 项（中文路径写入、`start` 守卫、日志），13 个测试程序共 842 项通过。
- 装机：2026-11-19 17:03，`install.cmd` 安装，旧 1.7.1 三文件与设置备份在 `output\install-backup-20261119-170338`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `142BE0229C6073545A6C12A9F2500E4C173B7AFAE749DB77F4D898599BEE0C25`、pvp `B055D492FF88F462FA931937F185F3067FD203726671412236B926629B24A8CF`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。重开日志 `17:03:39.987 Version 1.7.3`、`17:03:39.995 Init OK`。
- 未覆盖：仍需在真机上从 1.7.3 升级到下一个版本做一次完整验证。

## 早前（1.7.2）：新椭圆置顶

- 现象：新画的椭圆总是压在已有标注下面（置底）。
- 根因：`IPXC_Page::InsertNewAnnot` 的第三个参数 `nPos` 是注释在页面列表中的插入位置，`CircleWriter` 传了 `0`，等于每次都插到列表最前面，渲染时就在最底层。
- 修复：插入位置改为 `page.GetAnnotsCount()`，新注释追加到列表末尾（最上层）；放圈日志追加 `index=<当前序号> count=<页面注释总数>`，便于确认。
- 构建与测试：13 个测试程序共 839 项通过（未改动用例）；置顶效果需在真实图纸上目视确认。
- 未覆盖：已有标注的层级不变；插件只保证新放置的椭圆位于当前页注释列表末尾。

## 早前（1.7.1）：修复在线更新无法落地

- 现象：1.6.6 点“检查更新”后日志依次出现 `Update check started`、`Update available v1.7.0`、`Update download started`、`Update scheduled`，编辑器关闭，但重开后仍是 1.6.6，临时目录残留。
- 根因一：`IPXV_Inst.Shutdown(0)` 只卸载插件并关闭窗口，进程本身没有退出（实测留下两个无窗口的 `PDFXEdit.exe`，一个还占着原 PDF）。替换批处理一直在等这个 PID，等满 120 秒后放弃并清理，文件从未被替换。
- 根因二：批处理重启命令用了 `start /d "%EXE%\.."`，把文件路径当成目录；即使替换成功也无法重启编辑器。
- 修复：关闭编辑器改用 `Process.CloseMainWindow()`（返回 false 才回退 `Shutdown(0)`）；批处理等待 15 次（约 30 秒）后执行 `taskkill /pid %PID% /f` 再替换；重启目录用 `for %%D in ("%EXE%") do set "EXEDIR=%%~dpD"` 计算；批处理把开始/强制结束/替换/回滚写入 `%TEMP%\ClickCircle-update.log`。
- 验证：用复制出来的当前程序冒充编辑器，在临时目录跑两条路径——正常退出（外部结束 PID）与卡住不退（批处理强杀）——均完成三文件替换、重启、暂存目录清理、批处理自删，`FAILURES=0`；`UpdateTests` 增补 2 项（taskkill/日志/重启目录），13 个测试程序共 839 项通过。
- 装机：先结束两个无窗口的 1.6.6 残留进程（PID 20000、30640，否则 DLL 被占用），再用 `install.cmd` 安装；旧 1.6.6 三文件与设置备份在 `output\install-backup-20261119-165429`。`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `2B76EAC58B9E6FB80B57E3EF606E2AD64A4FD5264279958F5A6DB2D62FE95659`、pvp `B055D492FF88F462FA931937F185F3067FD203726671412236B926629B24A8CF`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。重开日志 `16:54:34.897 Version 1.7.1`、`16:54:34.914 Init OK`。
- 未覆盖：仍需在真机上从 1.7.1 升级到下一个版本做一次完整验证。

## 早前（1.7.0）：主页 + 设置页的界面重排

- 面板拆成两页：主页（主题、取文字/区域识别/继续标注/暂停、状态条、主题历史）与设置页（标注参数、快捷键、软件版本与检查更新）。设置页由状态条右侧“设置”进入，左上角“返回主页面”退出；设置页标题显示“取字连续圈注 v1.7.0 / 设置”。
- 视觉：浅灰页面、白色输入框、蓝色主按钮与描边次按钮、状态条圆点（暂停橙、取字蓝、标注绿）、历史表头浅灰、选中行浅蓝；主题与搜索框加占位提示。
- 主页状态条一行显示状态、快捷键、当前尺寸与分隔符，右侧“设置”；分隔符、尺寸、快捷键和检查更新不再占用主页空间。
- 布局：默认客户区 480×566（最小 440×380）；主页行高 56/56/44；分隔符下拉固定 200 像素、宽高输入框各 86 像素，历史表格默认高度仍 ≥160。
- 兼容：所有旧控件名与字段保留（`Subject`/`Separator`/`HistorySearch`/`SubjectHistory`、取字/区域识别/继续标注/暂停/快捷键/清除/检查更新按钮、状态标签），设置读写、快捷键录入、主题历史、在线更新逻辑不变。
- 构建与测试：13 个测试程序共 837 项通过（与 1.6.6 相同，未新增或删减用例）；离屏渲染核对主页与设置页（`output\panel-1.7.0-main.png`、`output\panel-1.7.0-settings.png`）。
- 未覆盖：150% 缩放下的人工观感与真实窗口拖拽仍需目视确认。

## 早前（1.6.6）：GitHub 在线更新

- 仓库已设为公开；新增 `src\UpdateService.cs`，更新检查优先读取 `raw.githubusercontent.com/.../main/version.txt`（不受 GitHub API 每小时 60 次限制），失败才回退到 tags API，不携带任何凭据。
- 版本按三段数字比较，只有远端更高才提示；下载走对应 tag 的 `dist` 三个文件（raw），逐个校验非空且不小于 1 KB。
- 面板状态行右侧新增“检查更新”按钮：检查中/下载中按钮禁用并写状态行；发现新版本弹窗确认；确认后遍历所有文档的 `Modified`，有未保存文档直接取消，不关闭编辑器。
- 更新落地：生成独立批处理（`%TEMP%\ClickCircle-update-<guid>.cmd`），等待编辑器退出、备份旧三文件到暂存目录、替换失败回滚、成功后重启编辑器并清理；等待超过 120 秒放弃并保留原文件。
- 构建与测试：新增 `tests\UpdateTests.cs` 26 项（版本解析/比较、`version.txt` 解析含 BOM/注释、tags JSON 取最大、批处理内容与回滚/超时/重启标记）；13 个测试程序共 837 项通过（原 811 + 26）。
- 网络实测：`UpdateService.GetLatestTag` 读取 `main/version.txt` 返回 `v1.6.6`；随后从 `v1.6.6` 标签下载三个 raw 文件成功（pvp 3072、Core 111104、PDFXEdit 868864 字节）。
- 装机：2026-11-19 14:11，旧 1.6.5 三文件、清单与设置备份到 `output\install-backup-1.6.5-20261119-141103`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `FED95831756BC73DA51414E67EA0DACC2FFD3D22270CCF636D3C230BAA1352AA`、pvp `B055D492FF88F462FA931937F185F3067FD203726671412236B926629B24A8CF`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。重开编辑器日志 `14:11:04.678 Version 1.6.6`、`14:11:04.684 Init OK`；旧 `ClickCircle.install.json` 按新方案移除。
- 未覆盖：真正的端到端升级需要发布一个更高版本并在真机点击“检查更新”；本轮验证了检查、下载、批处理生成、编译与装载。

## 早前（1.6.5）：面板标题显示版本号

- 面板标题改为“取字连续圈注 v1.6.5”：版本取自 `Plugin.version` 的同一常量 `PluginVersionNumber`，由新增的 `Plugin.VersionText` 生成点分十进制文本，`EnsurePanel` 创建面板后调用 `StampPanel.SetVersion`；传空值保持原标题。
- 启动日志新增 `Version 1.6.5`，排在 `Init OK` 之前，用于确认实际加载的版本；面板标题与日志共用同一常量，不会各写一份。
- 构建与测试：`PanelSizeTests` 增补 4 项（默认标题不带版本号、`SetVersion` 后标题为“取字连续圈注 v9.8.7”、空版本号保持原标题、`VersionText` 为三段点分十进制）；12 个测试程序共 811 项通过（原 807 + 4）。`version` 改为 1.6.5，`package.ps1` 默认版本改为 1.6.5。
- 本机没有 `build.ps1` 需要的 `PXCEditorCore.x64.dll`，按相同参数用 csc/ilasm 引用 `dist\PDFXEdit.dll` 编译（`/codepage:65001`，资源 `ocr-helper.ps1`）。
- 装机：2026-11-19 09:28，旧 1.6.4 三文件、清单、设置与日志备份到 `output\install-backup-1.6.4-20261119-092741`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `E0E2EC9743A90BF4675846826688B86A88158A1FBA67CEF6BC4E11F5123DE87A`、pvp `B055D492FF88F462FA931937F185F3067FD203726671412236B926629B24A8CF`、PDFXEdit.dll `2C8C6BA1F29654898AFBA5F7C566F9853E4411E6E05F4B963C93AC2751E208C1`。重开编辑器日志 `09:28:25.062 Hotkey Q via hook`、`09:28:25.073 Version 1.6.5`、`09:28:25.073 Init OK`；快捷键 `Q`、14 × 15 mm 与主题历史保留。
- 安装方式简化：用 `install.cmd` / `uninstall.cmd` 取代 `install.ps1` / `uninstall.ps1`。批处理只做四件事——检查编辑器未运行、备份旧的三文件、复制并逐字节校验、提示重开；卸载先备份再删除三个文件，保留设置与日志。纯 ASCII 文本，不受 PowerShell 执行策略限制，双击即可运行；`ClickCircle.install.json` 清单机制随之移除。
- 沙箱验证（临时目录模拟编辑器）：`install.cmd` 把旧 `ClickCircle.Core.dll` 备份到 `output\install-backup-<时间>` 后复制三个新文件；`uninstall.cmd` 删除三个文件并留下 `output\uninstall-backup-<时间>`；模拟目录里放 `PDFXEdit.exe` 且真有同名进程运行时，安装被正确拒绝并返回 1。
- 未覆盖：150% 缩放下标题栏是否截断需人工确认（最小客户区宽度 440，标题约 16 个字符）。

## 早前（1.6.4）：面板排版收敛

日期：2026-11-19。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。

- 行高改为按字体与控件首选高度计算（`Font.Height`、`PreferredSize`）：主题行 = 输入框首选高 + 16，分隔符/尺寸行同法，按钮行 = 按钮首选高 + 14，状态行 = 字体高 + 14。固定 44/48 的行高在 150% 缩放下会把字挤满、在 100% 下又留大片空白，这一条同时修两头。
- 标签列统一：主题、分隔符、宽高、快捷键、主题历史五行的第 0 列都改成同一宽度（`TextRenderer` 实测“分隔符 / 主题历史 / 宽 mm”里最宽者 + 12），四行输入框左边缘对齐；“分隔符”“主题历史”不再折行。
- 尺寸行重排：宽/高数字框从各占 50% 的拉伸列改为固定 96 像素，中间 16 像素间隔，末尾用空列收边，右半行留白；分隔符下拉框固定 150 像素 + 空列，不再通栏。
- 文本类控件改为 `Dock=Fill` + 上下等分边距（净高等于首选高度），在行内垂直居中；按钮边距统一 5 像素；状态行边距收紧到 2 像素。
- 表格跟随字号：`RowTemplate.Height = Font.Height + 14`，`ColumnHeadersHeight = Font.Height + 16`（表头改为可设高度）；历史区搜索行高 = 搜索框首选高 + 16，计数行高 = 字体高 + 10。
- 构建与测试：`PanelSizeTests` 增补 8 项排版检查（两个尺寸框都存在、宽高框等宽、尺寸框 ≤140 像素、主题框宽于尺寸框两倍、分隔符下拉框 100–220 像素、默认尺寸下历史表格高度 ≥160、行高不小于字体高 + 10）；`build.ps1 -RunTests` 12 个程序共 807 项通过（原 799 + 8）。`version` 改为 1.6.4，`package.ps1` 默认版本改为 1.6.4。
- 视觉检查：离屏渲染 `output\panel-1.6.4-600x600.png`（600 × 600）与 `output\panel-1.6.4-default.png`（480 × 566）——标签对齐、数字框不再拉满、按钮等宽、历史表格铺满剩余空间。
- 未覆盖：真实窗口下拖动与不同系统 DPI 组合的观感仍需人工确认；自动化只断言尺寸关系与下限。

## 早前（1.6.3）：面板可缩放并记住大小

日期：2026-11-19。宿主：便携版 PDF-XChange Editor 11.0.1 x64（`D:\PDF-XChangeEditorPlusPortable\App\PDF-XChange\PDF Editor`）。

## 本轮（1.6.3）：面板可缩放并记住大小

- 面板从 `FixedToolWindow` 改为 `SizableToolWindow`：可拖动边缘缩放，仍保持工具窗口外观（无最大/最小化按钮、不进任务栏）。默认客户区仍为 480 × 566，主题历史行沿用 `Percent 100` 行样式，放大时优先长高、变宽。
- 客户区下限 440 × 380（`MinimumPanelWidth` / `MinimumPanelHeight`）；`MinimumSize` 是窗口尺寸，`OnLoad` 里用真实工具窗口边框（`Width - ClientSize.Width`）补正，保证拖到最小时主题历史表格仍有可用高度而不是一条缝。
- 大小持久化：`PluginSettings` 新增 `PanelWidth` / `PanelHeight`（客户区像素），`ApplySettings` 恢复、`GetSettings` 回写；`Load` / `Save` 都做 `ClampPanelWidth/ClampPanelHeight`（非正数回落默认，夹在 440×380 … 4000 之间）。旧 `settings.xml` 没有这两个字段时回落默认，不影响其它设置。
- 只在 `ResizeEnd` 触发一次 `PanelSizeChanged`，由 `Plugin` 挂到 `SaveSettings`：拖动过程中不反复写文件，`ApplySettings` 里的程序化设置也不会触发保存。
- 顺带修 DPI 挤压：主题历史的“主题历史”标签列 84 → 110 像素，“固定”列 54 → 64 像素，150% 缩放下标题不再折行。
- 构建与测试：新增 `tests\PanelSizeTests.cs`（17 项：默认尺寸、可拖边框、无最大最小化、不进任务栏、窗口最小尺寸、按设置恢复 900×800、过小设置被撑到下限、`GetSettings` 反映实际大小、保存/读取往返、旧文件缺字段回落默认、超上限夹到 4000、非正数回落默认）；`build.ps1 -RunTests` 12 个程序共 799 项通过（原 782 + 17）。`version` 改为 1.6.3，`package.ps1` 默认版本改为 1.6.3。
- 视觉检查：离屏渲染 720 × 640 面板（`output\panel-1.6.3-720x640.png`），历史表格随窗口长高，搜索行与状态行不再互相挤压，状态行显示“已取 2 段，右键完成取字”。
- 装机：2026-11-19 02:15:03，旧 1.6.2 三文件、清单、设置与日志备份到 `output\install-backup-1.6.2-20261119-021503`；`Plugins.x64` 三者 SHA-256 与 `dist` 一致：Core `BE7DFEAB1C75AFAA28AD74C7AE147C5E93D332871E84D07F9415815C75DBE373`、pvp `70B2E620A14B05B860216D937AF0B37B79F8F195154DAB7FA0347F51C717A7A7`、PDFXEdit.dll `0D41E1BE4756B0F7727222D5895CDE69F0A015425B57AA8A3D32537171B692DE`。重开编辑器日志 `02:15:14.942 Hotkey Q via hook`、`02:15:14.942 Init OK`；升级前的 `settings.xml` 没有 `PanelWidth`/`PanelHeight` 字段，正好走旧配置回落默认 480 × 566 的路径，5 条主题历史与快捷键 `Q` 全部保留。
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
