using System;
using System.Runtime.InteropServices;
using PDFXEdit;

namespace ClickCircle
{
    /// <summary>
    /// Keeps the newest ellipse in the document's annotation selection so the
    /// editor shows its handles and the properties pane stays on it.
    /// </summary>
    internal static class AnnotSelector
    {
        private const string SelectionName = "selection.annots";
        private static int selectionId;

        public static bool Keep(IPXV_Inst instance, IPXV_Document document, IPXC_Annotation annotation)
        {
            if (instance == null || document == null || annotation == null)
                return false;
            try
            {
                IPXV_AnnotSelection selection = Find(instance, document);
                if (selection == null)
                    return false;
                selection.Items.Clear();
                selection.Items.Insert(annotation, 0);
                try { document.ActiveSel = selection; }
                catch (COMException) { document.ActivateSel(selection.ClusterID); }
                selection.Redraw();
                return true;
            }
            catch (COMException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static IPXV_AnnotSelection Find(IPXV_Inst instance, IPXV_Document document)
        {
            int id = Id(instance);
            if (id == 0)
                return null;
            IPXV_AnnotSelection active = document.ActiveSel as IPXV_AnnotSelection;
            if (active != null && active.ID == id)
                return active;
            IPXV_DocSelection selection = document.GetSel(id);
            if (selection == null)
                selection = document.CreateStdSel((uint)id);
            return selection as IPXV_AnnotSelection;
        }

        private static int Id(IPXV_Inst instance)
        {
            if (selectionId == 0)
            {
                try { selectionId = instance.Str2ID(SelectionName, false); }
                catch (COMException) { }
            }
            return selectionId;
        }
    }
}
