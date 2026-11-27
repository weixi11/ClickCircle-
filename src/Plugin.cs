using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using PDFXEdit;

namespace ClickCircle
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class Plugin : IPXV_Plugin, IUIX_CmdHandler
    {
        private enum StampMode { Paused, Picking, OcrPicking, Reviewing, Stamping }
        private StampMode mode;
        private CircleTool tool;
        private IUIX_Cmd command;
        private HotkeyService hotkey;
        private AnnotWatcher watcher;
        private System.Windows.Forms.Timer timer;
        private uint boundDocument;
        private uint observedDocument;
        private bool changingTool;
        private bool unloading;
        private int ocrGeneration;
        private bool updateCheckRunning;
        private DateTime toolRestoreAtUtc = DateTime.MinValue;
        private OcrReviewDialog ocrReview;
        private readonly Dictionary<uint, int> previousTools = new Dictionary<uint, int>();
        private readonly HashSet<ulong> installedFrames = new HashSet<ulong>();
        private readonly string dataDirectory;
        private readonly string settingsPath;
        public IPXV_Inst Instance { get; private set; }
        public StampPanel Panel { get; private set; }
        public bool IsPicking { get { return !unloading &&
            (mode == StampMode.Picking || mode == StampMode.OcrPicking); } }
        public bool IsOcrPicking { get { return mode == StampMode.OcrPicking && !unloading; } }

        /// <summary>正在连续圈注：每次左键点击放一个椭圆。</summary>
        public bool IsStamping { get { return mode == StampMode.Stamping && !unloading; } }

        /// <summary>当前模式对应的页面光标：取字是文本光标，框选与放椭圆是十字光标。</summary>
        public int CurrentCursorId
        {
            get
            {
                if (unloading) return ModeCursor.Arrow;
                switch (mode)
                {
                    case StampMode.Picking: return ModeCursor.Picking;
                    case StampMode.OcrPicking: return ModeCursor.Region;
                    case StampMode.Stamping: return ModeCursor.Stamping;
                    default: return ModeCursor.Arrow;
                }
            }
        }

        public Plugin()
        {
            dataDirectory = FindDataDirectory();
            settingsPath = Path.Combine(dataDirectory, "settings.xml");
        }

        public static int Create(uint apiVersion, IntPtr reserved, IntPtr outputInterface)
        {
            if (outputInterface == IntPtr.Zero)
                return unchecked((int)0x80004003);
            Marshal.WriteIntPtr(outputInterface, IntPtr.Zero);
            try
            {
                Plugin plugin = new Plugin();
                plugin.Log("Create api=" + apiVersion);
                IntPtr pointer = Marshal.GetComInterfaceForObject(plugin, typeof(IPXV_Plugin));
                Marshal.WriteIntPtr(outputInterface, pointer);
                return 0;
            }
            catch (Exception exception)
            {
                return Marshal.GetHRForException(exception);
            }
        }

        public void Setup(PXV_Inst instance)
        {
            Instance = instance;
            Log("Setup");
        }

        public void RegisterExts()
        {
            try
            {
                tool = new CircleTool(this);
                Instance.RegisterTool(tool);
                IUIX_Inst ui = (IUIX_Inst)Instance.GetExtension("UIX");
                IUIX_Icon icon = null;
                IUIX_CmdManager manager = ui.get_CmdManager();
                try
                {
                    IUIX_Cmd standard = manager.get_Cmd(Instance.Str2ID("cmd.tool.annot.circle", false));
                    if (standard != null) icon = standard.Icon;
                }
                catch (COMException) { }
                command = manager.CreateNewCmd("cmd.clickCircle.panel", "取字连续圈注",
                    this, icon, "取字连续圈注", 0);
                Log("RegisterExts tool=" + tool.ID);
            }
            catch (Exception exception)
            {
                Log("RegisterExts FAILED " + exception);
                throw;
            }
        }

        public void FinalizeRegistering() { Log("FinalizeRegistering"); }

        public void Init()
        {
            try
            {
                EnsurePanel();
                watcher = new AnnotWatcher(this);
                hotkey = new HotkeyService(
                    delegate
                    {
                        if (unloading || mode == StampMode.Reviewing)
                            return false;
                        string reason;
                        bool allowed = HotkeyScope.CanTrigger(Instance, Panel, IsToolContextActive,
                            out reason);
                        if (!allowed && reason != null)
                            Log("Hotkey ignored: " + reason);
                        return allowed;
                    },
                    delegate { return !unloading && HotkeyScope.CanCapture(Panel); });
                hotkey.Triggered += Pick;
                hotkey.Captured += OnHotkeyCaptured;
                if (!hotkey.IsActive)
                    Log("Hotkey unavailable: " + hotkey.Error);
                ApplyHotkey(Panel.Hotkey);
                timer = new System.Windows.Forms.Timer { Interval = 150 };
                timer.Tick += OnTick;
                timer.Start();
                OnTick(null, EventArgs.Empty);
                Log("Version " + VersionText);
                Log("Init OK");
            }
            catch (Exception exception)
            {
                Log("Init FAILED " + exception);
                throw;
            }
        }

        /// <summary>插件的页面工具是否正是当前文档的活动工具（取字、区域识别、圈注都算）。</summary>
        private bool IsToolContextActive
        {
            get
            {
                try
                {
                    if (tool == null || Instance == null)
                        return false;
                    IPXV_Document document = ActiveDocument();
                    return document != null && document.ActiveTool != null &&
                        document.ActiveTool.ID == tool.ID;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void Unload()
        {
            unloading = true;
            toolRestoreAtUtc = DateTime.MinValue;
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (watcher != null) { watcher.Dispose(); watcher = null; }
            if (hotkey != null)
            {
                hotkey.Triggered -= Pick;
                hotkey.Captured -= OnHotkeyCaptured;
                hotkey.Dispose();
                hotkey = null;
            }
            Pause();
            if (Instance != null && tool != null)
                for (uint index = 0; index < Instance.DocCount; index++)
                    RestoreTool(Instance.get_Doc(index));
            if (Panel != null)
            {
                SaveSettings();
                Panel.Dispose();
                Panel = null;
            }
            try
            {
                for (uint index = 0; index < Instance.MainFrmCount; index++)
                {
                    IUIX_CmdBar bar = Instance.get_MainFrm(index).View.CmdPaneTop.QABar;
                    if (bar != null && command != null) bar.DeleteItemsByCmd(command, false, false);
                }
            }
            catch (Exception exception) { Log("Unload toolbar " + exception.Message); }
            Log("Unload OK");
            command = null;
            tool = null;
            Instance = null;
        }

        public void ShowPrefsDlg(ulong parent) { ShowPanel(); }
        public void ImportAdditionalData(IAFS_Name appData, IAFS_Name userData) { }
        public void ExportAdditionalData(IAFS_Name appData, IAFS_Name userData) { SaveSettings(); }
        public void ResetSettings(uint flags)
        {
            if (Panel == null) return;
            Panel.ApplySettings(new PluginSettings());
            ApplyHotkey(Panel.Hotkey);
            SaveSettings();
        }

        public Guid _GUID { get { return new Guid("445F9883-A182-4A3F-A4E3-3FCA78F314EC"); } }
        public string Name { get { return "取字连续圈注"; } }
        public string CopyrightInfo { get { return "ClickCircle 2026"; } }
        /// <summary>插件版本号，面板标题显示同一数值。</summary>
        private const uint PluginVersionNumber = (1U << 16) | (8U << 8) | 4U;

        public uint version { get { return PluginVersionNumber; } }

        /// <summary>点分十进制版本文本，例如 1.6.5。</summary>
        public static string VersionText
        {
            get
            {
                return ((PluginVersionNumber >> 16) & 0xFFFFU).ToString() + "." +
                    ((PluginVersionNumber >> 8) & 0xFFU).ToString() + "." +
                    (PluginVersionNumber & 0xFFU).ToString();
            }
        }
        public uint VendorID { get { return 0; } }
        public string Description { get { return "取文字后，以固定尺寸连续添加红色椭圆标注。"; } }
        public string LegalInfo { get { return "Local annotation tool."; } }
        public string Publisher { get { return "ClickCircle"; } }
        public uint Features { get { return 0; } }
        public string PresetsCollectionsNames { get { return String.Empty; } }

        private void EnsurePanel()
        {
            if (Panel != null && !Panel.IsDisposed) return;
            Panel = new StampPanel(PluginSettings.Load(settingsPath));
            Panel.SetVersion(VersionText);
            Panel.PickRequested += Pick;
            Panel.OcrRequested += PickOcr;
            Panel.ResumeRequested += Resume;
            Panel.PauseRequested += Pause;
            Panel.DimensionsChanged += SaveSettings;
            Panel.SeparatorChanged += SaveSettings;
            Panel.HistoryChanged += SaveSettings;
            Panel.OcrLanguageChanged += SaveSettings;
            Panel.PanelSizeChanged += SaveSettings;
            Panel.HotkeyChanged += OnHotkeyChanged;
            Panel.HotkeyCaptureStarted += OnHotkeyCaptureStarted;
            Panel.HotkeyCaptureStopped += OnHotkeyCaptureStopped;
            Panel.CheckUpdateRequested += CheckForUpdates;
            Panel.StyleEditRequested += OnStyleEditRequested;
        }

        /// <summary>当前“新标注自动套用”的样式；未启用时返回 null 以便监听器直接跳过。</summary>
        internal AnnotStyle CurrentStyle
        {
            get
            {
                if (Panel == null || Panel.IsDisposed)
                    return null;
                AnnotStyle style = Panel.Style;
                return style != null && style.Enabled ? style : null;
            }
        }

        private void OnStyleEditRequested()
        {
            try
            {
                AnnotStyle current = Panel == null ? null : Panel.Style;
                using (AnnotStyleDialog dialog = new AnnotStyleDialog(current))
                {
                    if (dialog.ShowDialog(Panel) != DialogResult.OK || dialog.Style == null)
                    {
                        Log("Annot style dialog cancelled");
                        return;
                    }
                    Panel.Style = dialog.Style;
                    Panel.UpdateStyleStatus();
                    SaveSettings();
                    Log("Annot style updated enabled=" + dialog.Style.Enabled +
                        " measureOnly=" + dialog.Style.MeasureOnly);
                }
            }
            catch (Exception exception)
            {
                Log("Annot style dialog FAILED " + exception);
            }
        }

        private void OnHotkeyCaptureStarted()
        {
            if (hotkey == null)
            {
                Panel.SetStatus("快捷键不可用：" + (Instance == null ? "插件尚未初始化" : "请重启编辑器"));
                return;
            }
            hotkey.BeginCapture();
            Log("Hotkey capture started");
        }

        private void OnHotkeyCaptureStopped()
        {
            if (hotkey != null)
                hotkey.CancelCapture();
        }

        private void OnHotkeyCaptured(HotkeyBinding value)
        {
            Log("Hotkey captured " + (value == null ? "(cancelled)" : value.ToStorage()));
            if (Panel != null && !Panel.IsDisposed)
                Panel.CompleteHotkeyCapture(value);
        }

        private void OnHotkeyChanged()
        {
            ApplyHotkey(Panel == null ? HotkeyBinding.Default : Panel.Hotkey);
            SaveSettings();
        }

        private void ApplyHotkey(HotkeyBinding value)
        {
            if (hotkey == null)
                return;
            hotkey.Binding = value;
            if (hotkey.Binding.IsEmpty)
            {
                Log("Hotkey None");
                return;
            }
            if (!hotkey.IsActive)
            {
                if (Panel != null && !Panel.IsDisposed)
                    Panel.SetStatus("快捷键不可用：" + hotkey.Error);
                Log("Hotkey cannot be applied: " + hotkey.Error);
                return;
            }
            Log("Hotkey " + hotkey.Binding.ToStorage() + " via hook");
        }

        private void ShowPanel()
        {
            try
            {
                EnsurePanel();
                if (!Panel.Visible)
                    Panel.Show(new NativeOwner(new IntPtr(unchecked((long)Instance.ActiveMainFrmWndHandle))));
                else
                    Panel.Activate();
            }
            catch (Exception exception) { Log("ShowPanel FAILED " + exception); }
        }

        private IPXV_Document ActiveDocument()
        {
            if (Instance == null) return null;
            IPXV_MainView view = Instance.ActiveMainView;
            if (view == null || view.ActiveDocView == null) return null;
            return view.ActiveDocView.Doc;
        }

        private void OnTick(object sender, EventArgs args)
        {
            if (unloading || Instance == null) return;
            try
            {
                // 右键完成标注后会推迟 300ms 交回工具，这里到点执行；期间页面工具负责吞掉右键菜单消息。
                if (ContextMenuGuard.ShouldRestore(DateTime.UtcNow, toolRestoreAtUtc))
                {
                    toolRestoreAtUtc = DateTime.MinValue;
                    RestoreTools();
                }
                for (uint index = 0; index < Instance.MainFrmCount; index++)
                {
                    IPXV_MainFrame frame = Instance.get_MainFrm(index);
                    ulong handle = frame.Obj.WndHandle;
                    if (installedFrames.Contains(handle)) continue;
                    IUIX_CmdBar bar = frame.View.CmdPaneTop.QABar;
                    if (bar == null || command == null) continue;
                    if (bar.FindFirstItemByCmdID(command.ID) == null)
                        bar.InsertItem(command, -1, 0, 0, 0);
                    installedFrames.Add(handle);
                }
                IPXV_Document document = ActiveDocument();
                if (watcher != null) watcher.Sync();
                if (tool != null) tool.CheckGestureView();
                uint current = document == null ? 0 : document.ID;
                if (current != observedDocument)
                {
                    observedDocument = current;
                    if (mode != StampMode.Paused && current != boundDocument)
                        PauseWithStatus("已暂停：文档已切换");
                }
            }
            catch (Exception exception) { Log("Timer " + exception.Message); }
        }

        public void Pick()
        {
            Log("Hotkey fired (mode=" + mode + ")");
            Activate(StampMode.Picking);
        }
        public void PickOcr() { Activate(StampMode.OcrPicking); }
        public void Resume()
        {
            if (Panel == null || String.IsNullOrWhiteSpace(Panel.Subject)) return;
            Activate(StampMode.Stamping);
            if (mode == StampMode.Stamping) Panel.RememberCurrentSubject();
        }

        /// <summary>取字状态行：已取几段，并提示右键完成。</summary>
        private string PickingHint
        {
            get
            {
                int count = Panel == null || Panel.IsDisposed ? 0 : Panel.PickedFragmentCount;
                return count == 0
                    ? "点击文字取字，右键完成"
                    : "已取 " + count + " 段，右键完成取字";
            }
        }

        /// <summary>右键完成取字：用累积的片段作为主题，进入连续圈注。</summary>
        public void FinishPicking()
        {
            if (mode != StampMode.Picking) return;
            if (Panel == null || Panel.IsDisposed) return;
            if (String.IsNullOrWhiteSpace(Panel.Subject))
            {
                PauseWithStatus("已暂停：主题为空");
                return;
            }
            SetMode(StampMode.Stamping, null);
            Panel.RememberCurrentSubject();
            Log("Picking finished count=" + Panel.PickedFragmentCount + " subject=" + Panel.Subject);
        }

        /// <summary>
        /// 右键完成标注：结束本次连续圈注，主题保留以便“继续标注”。
        /// 已画好的椭圆不受影响。工具稍后再交回编辑器：这次右键的抬起与菜单请求
        /// 还要由页面工具吃掉，交回太早编辑器会弹出页面右键菜单（见 ContextMenuGuard）。
        /// </summary>
        public void FinishStamping()
        {
            if (mode != StampMode.Stamping) return;
            string subject = Panel == null || Panel.IsDisposed ? String.Empty : Panel.Subject;
            PauseWithStatus("已完成标注，可继续标注", true);
            Log("Stamping finished subject=" + subject);
        }

        // ── 在线更新（GitHub 公共仓库） ──────────────────────────────────

        private void CheckForUpdates()
        {
            if (updateCheckRunning || unloading) return;
            updateCheckRunning = true;
            if (Panel != null && !Panel.IsDisposed)
            {
                Panel.SetUpdateState("检查中…", false);
                Panel.SetUpdateStatus("正在检查更新…");
            }
            Log("Update check started");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                string tag = UpdateService.GetLatestTag(out error);
                if (tag == null)
                {
                    FinishUpdateCheck("检查更新失败：" + error, true);
                    return;
                }
                if (UpdateService.CompareVersions(tag, VersionText) <= 0)
                {
                    FinishUpdateCheck("已是最新版本 " + VersionText, false);
                    return;
                }
                Log("Update available " + tag);
                RunOnPanel(delegate { ConfirmUpdate(tag); });
            });
        }

        private void FinishUpdateCheck(string status, bool error)
        {
            updateCheckRunning = false;
            RunOnPanel(delegate
            {
                if (Panel == null || Panel.IsDisposed) return;
                Panel.SetUpdateState("检查更新", true);
                Panel.SetUpdateStatus(status, error);
            });
        }

        private void ConfirmUpdate(string tag)
        {
            if (Panel == null || Panel.IsDisposed)
            {
                updateCheckRunning = false;
                return;
            }
            string question = "发现新版本 " + tag + "（当前 " + VersionText + "）。\r\n\r\n" +
                "更新会先关闭编辑器，替换插件文件后自动重启。\r\n" +
                "有未保存的文档时会取消本次更新。\r\n\r\n现在更新吗？";
            if (MessageBox.Show(Panel, question, "检查更新", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                updateCheckRunning = false;
                Panel.SetUpdateState("检查更新", true);
                Panel.SetUpdateStatus("已取消更新");
                return;
            }
            if (HasUnsavedDocuments())
            {
                updateCheckRunning = false;
                Panel.SetUpdateState("检查更新", true);
                Panel.SetUpdateStatus("有未保存的文档，已取消更新", true);
                MessageBox.Show(Panel, "还有未保存的文档，请先保存后再更新。", "检查更新",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Panel.SetUpdateState("下载中…", false);
            Panel.SetUpdateStatus("正在下载 " + tag + "…");
            Log("Update download started " + tag);
            ThreadPool.QueueUserWorkItem(delegate { DownloadAndApply(tag); });
        }

        private bool HasUnsavedDocuments()
        {
            try
            {
                for (uint index = 0; index < Instance.DocCount; index++)
                {
                    IPXV_Document document = Instance.get_Doc(index);
                    if (document != null && document.Modified)
                        return true;
                }
                return false;
            }
            catch (Exception exception)
            {
                Log("Unsaved check " + exception.Message);
                return true;
            }
        }

        private void DownloadAndApply(string tag)
        {
            string stage = Path.Combine(Path.GetTempPath(),
                "ClickCircle-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                string error;
                if (!UpdateService.Download(tag, stage, out error))
                    throw new IOException(error);
                string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string editorPath = Process.GetCurrentProcess().MainModule.FileName;
                string helper = UpdateService.CreateHelper(stage, pluginDirectory, editorPath,
                    Process.GetCurrentProcess().Id);
                if (!UpdateService.StartHelper(helper, out error))
                    throw new IOException(error);
                Log("Update scheduled " + tag);
                RunOnPanel(delegate
                {
                    Process current = Process.GetCurrentProcess();
                    Log("Update closing pid=" + current.Id);
                    bool closed = false;
                    try { closed = current.CloseMainWindow(); }
                    catch (Exception closeError) { Log("Update close " + closeError.Message); }
                    if (!closed && Instance != null)
                    {
                        try { Instance.Shutdown(0); }
                        catch (Exception shutdownError) { Log("Update shutdown " + shutdownError.Message); }
                    }
                });
            }
            catch (Exception exception)
            {
                Log("Update FAILED " + exception);
                try
                {
                    if (Directory.Exists(stage))
                        Directory.Delete(stage, true);
                }
                catch (Exception cleanup) { Log("Update cleanup " + cleanup.Message); }
                updateCheckRunning = false;
                RunOnPanel(delegate
                {
                    if (Panel == null || Panel.IsDisposed) return;
                    Panel.SetUpdateState("检查更新", true);
                    Panel.SetUpdateStatus("更新失败：" + exception.Message, true);
                });
            }
        }

        private void RunOnPanel(Action action)
        {
            StampPanel panel = Panel;
            if (panel == null || panel.IsDisposed)
                return;
            try { panel.BeginInvoke(action); }
            catch (Exception exception) { Log("Update UI " + exception.Message); }
        }

        private void Activate(StampMode next)
        {
            try
            {
                EnsurePanel();
                CancelOcr();
                toolRestoreAtUtc = DateTime.MinValue;
                if (tool != null) tool.CancelGesture();
                IPXV_Document document = ActiveDocument();
                if (document == null)
                {
                    SetMode(StampMode.Paused, "请先打开 PDF");
                    return;
                }
                boundDocument = document.ID;
                observedDocument = document.ID;
                if (document.ActiveTool == null || document.ActiveTool.ID != tool.ID)
                    previousTools[document.ID] = document.ActiveTool == null
                        ? Instance.Str2ID("tool.hand", false) : document.ActiveTool.ID;
                // 取字状态下再按 Q 或再点“取文字”＝重新取字：清空已取片段和主题，从第一段重新累积。
                bool restartingPick = next == StampMode.Picking && mode == StampMode.Picking;
                if (next == StampMode.Picking)
                    Panel.ResetPickedFragments();
                if (restartingPick)
                    Panel.Subject = String.Empty;
                SetMode(next, next == StampMode.Picking ? PickingHint : null);
                changingTool = true;
                try { document.ActivateTool(tool.ID); }
                finally { changingTool = false; }
                if (document.ActiveView != null && document.ActiveView.PagesView != null)
                    document.ActiveView.PagesView.Obj.SetInputFocus(false);
                // 重新取字给个即时反馈：否则只清了主题，用户会以为快捷键没响应。
                if (restartingPick)
                    Panel.ShowPickHint("重新取字，点击文字继续", 0);
                Log(next + " doc=" + boundDocument);
                if (restartingPick)
                    Log("Picking restarted doc=" + boundDocument);
            }
            catch (Exception exception)
            {
                SetMode(StampMode.Paused, "启用失败：" + exception.Message);
                Log("Activate FAILED " + exception);
            }
        }

        public void Pause() { PauseWithStatus(null); }

        private void PauseWithStatus(string status)
        {
            PauseWithStatus(status, false);
        }

        /// <summary>暂停并交出工具；deferToolRestore 为真时稍后再交回，先让页面工具吃掉右键菜单消息。</summary>
        private void PauseWithStatus(string status, bool deferToolRestore)
        {
            CancelOcr();
            if (tool != null) tool.CancelGesture();
            SetMode(StampMode.Paused, status);
            if (changingTool || Instance == null || tool == null) return;
            if (deferToolRestore)
            {
                toolRestoreAtUtc = ContextMenuGuard.RestoreFrom(DateTime.UtcNow);
                return;
            }
            toolRestoreAtUtc = DateTime.MinValue;
            RestoreTools();
        }

        /// <summary>把当前文档（以及原绑定文档）的工具交回编辑器。</summary>
        private void RestoreTools()
        {
            if (Instance == null || tool == null || changingTool) return;
            changingTool = true;
            try
            {
                IPXV_Document document = ActiveDocument();
                RestoreTool(Instance.FindDocByID(boundDocument));
                if (document != null && document.ID != boundDocument) RestoreTool(document);
            }
            catch (Exception exception) { Log("Pause " + exception.Message); }
            finally { changingTool = false; }
        }

        private void RestoreTool(IPXV_Document document)
        {
            try
            {
                if (document == null || document.ActiveTool == null || document.ActiveTool.ID != tool.ID) return;
                int previous;
                if (!previousTools.TryGetValue(document.ID, out previous) || previous == tool.ID)
                    previous = Instance.Str2ID("tool.hand", false);
                document.ActivateTool(previous);
            }
            catch (Exception exception) { Log("Restore tool " + exception.Message); }
        }

        private void SetMode(StampMode next, string status)
        {
            mode = next;
            if (Panel == null || Panel.IsDisposed) return;
            Panel.Enabled = next != StampMode.Reviewing;
            Panel.SetMode(next == StampMode.Picking || next == StampMode.OcrPicking ? "picking" :
                next == StampMode.Stamping ? "stamping" : "paused",
                status ?? (next == StampMode.OcrPicking ? "请框选要识别的区域" : null));
            ApplyModeCursor();
        }

        /// <summary>把当前模式的光标写到活动文档的页面视图；切换模式后立即生效。</summary>
        private void ApplyModeCursor()
        {
            if (unloading || Instance == null || tool == null) return;
            try
            {
                IPXV_Document document = ActiveDocument();
                if (document == null || document.ActiveView == null) return;
                tool.ApplyModeCursor(document.ActiveView.PagesView);
            }
            catch (Exception exception) { Log("Cursor " + exception.Message); }
        }

        public void OnToolActivated(IPXV_Document document)
        {
            if (!changingTool && mode != StampMode.Paused && document.ID != boundDocument)
                PauseWithStatus("已暂停：文档已切换");
        }

        public void OnToolDeactivated(IPXV_Document document)
        {
            if (!changingTool && mode != StampMode.Paused)
            {
                CancelOcr();
                SetMode(StampMode.Paused, null);
            }
        }

        public void HandlePageClick(IPXV_PagesView view, uint pageIndex, PXC_Point point)
        {
            if (mode != StampMode.Picking && mode != StampMode.OcrPicking &&
                mode != StampMode.Stamping) return;
            try
            {
                IPXV_Document document = view.Doc;
                IPXV_Document active = ActiveDocument();
                if (active == null || active.ID != document.ID || document.ID != boundDocument)
                {
                    PauseWithStatus("已暂停：文档已切换");
                    return;
                }
                IPXC_Page page = document.CoreDoc.Pages[pageIndex];
                if (mode == StampMode.OcrPicking)
                {
                    Panel.SetStatus("请按住鼠标拖框选择识别区域");
                    return;
                }
                if (mode == StampMode.Picking)
                {
                    string text;
                    if (!TextPicker.TryGetWord(page, point, out text))
                    {
                        Panel.SetMode("picking", Panel.PickedFragmentCount == 0
                            ? "未点中文字，可拖框识别" : PickingHint);
                        return;
                    }
                    Panel.AppendPickedFragments(new[] { text });
                    SetMode(StampMode.Picking, PickingHint);
                    Panel.ShowPickHint(text, Panel.PickedFragmentCount);
                    Log("Picked " + text + " count=" + Panel.PickedFragmentCount);
                    return;
                }
                string subject = Panel.Subject.Trim();
                if (subject.Length == 0) { PauseWithStatus("已暂停：主题为空"); return; }
                IPXC_Annotation annotation = CircleWriter.Add(Instance, document, pageIndex, point,
                    Panel.WidthMm, Panel.HeightMm, subject, page.Rotation + document.PagesViewRotation);
                Panel.RememberCurrentSubject();
                bool selected = AnnotSelector.Keep(Instance, document, annotation);
                Log(String.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Circle doc={0} page={1} x={2:F4} y={3:F4} subject={4} selected={5} index={6} count={7}",
                    document.ID, pageIndex, point.x, point.y, subject, selected,
                    annotation.IndexOnPage, page.GetAnnotsCount()));
            }
            catch (Exception exception)
            {
                PauseWithStatus("已暂停：" + exception.Message);
                Log("Click FAILED " + exception);
            }
        }

        public void HandlePageBoxSelection(IPXV_PagesView view, uint pageIndex, RegionPickResult result,
            PXC_Rect region, int displayRotation)
        {
            if (!IsPicking) return;
            try
            {
                IPXV_Document document = view.Doc;
                IPXV_Document active = ActiveDocument();
                if (active == null || active.ID != document.ID || document.ID != boundDocument ||
                    pageIndex >= document.CoreDoc.Pages.Count)
                {
                    PauseWithStatus("已暂停：文档已切换");
                    return;
                }
                if (IsOcrPicking || result == null || result.Fragments.Length == 0)
                {
                    QueueOcr(document, pageIndex, region, displayRotation);
                    return;
                }
                Panel.AppendPickedFragments(result.Fragments);
                SetMode(StampMode.Picking, PickingHint);
                Panel.ShowPickHint(String.Join(" / ", result.Fragments), Panel.PickedFragmentCount);
                Log("BoxPicked count=" + result.Fragments.Length +
                    " total=" + Panel.PickedFragmentCount + " subject=" + Panel.Subject);
            }
            catch (Exception exception)
            {
                PauseWithStatus("已暂停：" + exception.Message);
                Log("BoxPick FAILED " + exception);
            }
        }

        private void CancelOcr()
        {
            ocrGeneration++;
            OcrReviewDialog review = ocrReview;
            ocrReview = null;
            if (review != null && !review.IsDisposed) review.CancelRecognition();
        }

        private bool IsOcrContextCurrent(uint documentId, uint pageIndex, int request)
        {
            if (unloading || Instance == null || mode != StampMode.Reviewing ||
                request != ocrGeneration) return false;
            IPXV_Document document = ActiveDocument();
            return document != null && document.ID == documentId &&
                boundDocument == documentId && pageIndex < document.CoreDoc.Pages.Count &&
                document.ActiveView != null && document.ActiveView.PagesView != null &&
                document.ActiveTool != null && document.ActiveTool.ID == tool.ID;
        }

        private void QueueOcr(IPXV_Document document, uint pageIndex, PXC_Rect region, int rotation)
        {
            StampMode previous = mode;
            int request = ++ocrGeneration;
            uint documentId = document.ID;
            SetMode(StampMode.Reviewing, "正在准备识别区域");
            // Defer the modal until the page tool has returned from the mouse-up callback.
            IntPtr dispatcher = Panel.Handle;
            Panel.BeginInvoke((Action)delegate {
                try
                {
                    if (!IsOcrContextCurrent(documentId, pageIndex, request)) return;
                    using (Bitmap bitmap = OcrRegionRenderer.Render(Instance,
                        document.CoreDoc.Pages[pageIndex], region, rotation))
                    using (OcrReviewDialog review = new OcrReviewDialog(bitmap, Panel.Separator, Panel.OcrLanguage))
                    {
                        review.LanguagePreferenceChanged += delegate {
                            Panel.OcrLanguage = review.SelectedLanguageTag;
                        };
                        ocrReview = review;
                        Log(String.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "OcrReview doc={0} page={1} region={2:F3},{3:F3},{4:F3},{5:F3} rotation={6}",
                            documentId, pageIndex, region.left, region.bottom, region.right, region.top, rotation));
                        DialogResult result = review.ShowDialog(
                            new NativeOwner(new IntPtr(unchecked((long)Instance.ActiveMainFrmWndHandle))));
                        if (!IsOcrContextCurrent(documentId, pageIndex, request)) return;
                        if (result == DialogResult.OK && review.ConfirmedFragments != null)
                        {
                            if (previous == StampMode.Picking)
                            {
                                // 取字过程中框选落到 OCR：识别结果并入本次取字，右键再完成。
                                Panel.AppendPickedFragments(review.ConfirmedFragments);
                                SetMode(StampMode.Picking, PickingHint);
                                Panel.ShowPickHint(String.Join(" / ", review.ConfirmedFragments),
                                    Panel.PickedFragmentCount);
                            }
                            else
                            {
                                Panel.SetPickedFragments(review.ConfirmedFragments);
                                SetMode(StampMode.Stamping, null);
                            }
                            Log("OcrPicked count=" + review.ConfirmedFragments.Length +
                                " total=" + Panel.PickedFragmentCount + " subject=" + Panel.Subject);
                        }
                        else SetMode(previous, "已取消识别，原主题保留");
                        if (document.ActiveView != null && document.ActiveView.PagesView != null)
                            document.ActiveView.PagesView.Obj.SetInputFocus(false);
                    }
                }
                catch (Exception exception)
                {
                    if (request == ocrGeneration && !unloading)
                        SetMode(previous, "区域识别失败：" + exception.Message);
                    Log("OCR FAILED " + exception);
                }
                finally
                {
                    if (request == ocrGeneration)
                    {
                        ocrReview = null;
                        if (mode == StampMode.Reviewing)
                            PauseWithStatus("已暂停：识别区域已失效");
                    }
                }
            });
        }

        private void SaveSettings()
        {
            if (Panel != null && !Panel.IsDisposed && !Panel.GetSettings().Save(settingsPath))
                Log("Settings could not be saved");
        }

        public void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(dataDirectory);
                File.AppendAllText(Path.Combine(dataDirectory, "plugin.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string FindDataDirectory()
        {
            string testDirectory = Environment.GetEnvironmentVariable("CLICKCIRCLE_DATA_DIR");
            if (!String.IsNullOrWhiteSpace(testDirectory)) return Path.GetFullPath(testDirectory);
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            for (DirectoryInfo current = directory; current != null; current = current.Parent)
                if (File.Exists(Path.Combine(current.FullName, "PDFXChangeEditorPlusPortable.exe")))
                    return Path.Combine(current.FullName, "Data", "ClickCircle");
            return Path.Combine(directory.FullName, "ClickCircleData");
        }

        public void OnNotify(int code, IUIX_Cmd cmd, IUIX_CmdItem item, IUIX_Obj owner, ulong parameter)
        {
            if (code == (int)UIX_CmdNotifyCodes.UIX_CmdNotify_Exec) ShowPanel();
        }
        public void OnCreateNewCtl(IUIX_Cmd cmd, IUIX_CmdBar bar, out IUIX_Obj control) { control = null; }
        public void OnGetCtlSizes(IUIX_CmdItem item, ref tagSIZE size, ref tagSIZE minimum, ref tagSIZE maximum) { }
        public void OnGetItemState(IUIX_Cmd cmd, IUIX_CmdItem item, IUIX_Obj owner, out int state) { state = 0; }
        public void OnGetItemSubMenu(IUIX_CmdItem item, out IUIX_CmdMenu menu) { menu = null; }
        public void OnDrawItemIcon(IUIX_RenderContext context, IUIX_CmdItem item, ref tagRECT iconRect, ref tagRECT clipRect) { }

        private sealed class NativeOwner : IWin32Window
        {
            private readonly IntPtr handle;
            public NativeOwner(IntPtr value) { handle = value; }
            public IntPtr Handle { get { return handle; } }
        }
    }
}












