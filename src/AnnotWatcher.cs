using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PDFXEdit;

namespace ClickCircle
{
    /// <summary>
    /// 监听各文档的“新标注”事件（e.annots.inserted），按当前样式自动套用。
    /// 用定时器同步文档列表：新开的文档自动订阅，关掉的文档自动退订。
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class AnnotWatcher : IEventHandler, IDisposable
    {
        private readonly Plugin owner;
        private readonly Dictionary<uint, IEventServer> servers = new Dictionary<uint, IEventServer>();
        private int insertedEventId;
        private bool disposed;

        public AnnotWatcher(Plugin owner)
        {
            this.owner = owner;
        }

        internal int AppliedCount { get; private set; }

        internal void Sync()
        {
            if (disposed || owner == null || owner.Instance == null)
                return;
            if (owner.CurrentStyle == null)
                return;
            try
            {
                if (insertedEventId == 0)
                {
                    insertedEventId = owner.Instance.Str2ID("e.annots.inserted", false);
                    if (insertedEventId == 0)
                        return;
                }
                HashSet<uint> alive = new HashSet<uint>();
                for (uint index = 0; index < owner.Instance.DocCount; index++)
                {
                    IPXV_Document document = owner.Instance.get_Doc(index);
                    if (document == null)
                        continue;
                    alive.Add(document.ID);
                    if (servers.ContainsKey(document.ID))
                        continue;
                    IEventServer server = document.EventServer;
                    if (server == null)
                        continue;
                    server.RegisterNativeEventHandler(insertedEventId, this, 0);
                    servers[document.ID] = server;
                    owner.Log("Style watcher subscribed doc=" + document.ID);
                }
                List<uint> closed = new List<uint>();
                foreach (KeyValuePair<uint, IEventServer> pair in servers)
                    if (!alive.Contains(pair.Key))
                        closed.Add(pair.Key);
                foreach (uint id in closed)
                {
                    try { servers[id].UnregisterNativeEventHandler2(this); }
                    catch (Exception) { }
                    servers.Remove(id);
                }
            }
            catch (Exception exception)
            {
                owner.Log("Style watcher sync FAILED " + exception.Message);
            }
        }

        public void OnEvent(int nEventID, IEvent e, object pFrom)
        {
            if (disposed || owner == null || nEventID != insertedEventId)
                return;
            try
            {
                AnnotStyle style = owner.CurrentStyle;
                if (style == null || !style.Enabled)
                    return;
                IPXV_AnnotsEvent annotsEvent = e as IPXV_AnnotsEvent;
                if (annotsEvent == null)
                    return;
                IPXV_AnnotsList items = annotsEvent.Items;
                if (items == null)
                    return;
                IAUX_Inst auxiliary = null;
                try { auxiliary = owner.Instance.GetExtension("AUX") as IAUX_Inst; }
                catch (Exception) { }
                int applied = 0;
                for (uint index = 0; index < items.Count; index++)
                {
                    IPXC_Annotation annotation = items[index];
                    if (annotation == null)
                        continue;
                    string error;
                    bool changed;
                    try { changed = AnnotStyler.Apply(annotation, style, auxiliary, out error); }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                        changed = false;
                    }
                    if (error != null)
                        owner.Log("Style apply FAILED " + error);
                    if (changed)
                        applied++;
                }
                if (applied > 0)
                {
                    AppliedCount += applied;
                    owner.Log("Style applied count=" + applied);
                }
            }
            catch (Exception exception)
            {
                owner.Log("Style watcher FAILED " + exception.Message);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (KeyValuePair<uint, IEventServer> pair in servers)
            {
                try { pair.Value.UnregisterNativeEventHandler2(this); }
                catch (Exception) { }
            }
            servers.Clear();
        }
    }
}
