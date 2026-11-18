using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

internal static class ExtractInterop
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int LoadTypeLibEx(string file, int registrationKind, out ITypeLib typeLibrary);

    private sealed class ImportNotifications : ITypeLibImporterNotifySink
    {
        public void ReportEvent(ImporterEventKind kind, int code, string message)
        {
            if (kind != ImporterEventKind.NOTIF_TYPECONVERTED)
                Console.WriteLine(kind + ": " + message);
        }

        public Assembly ResolveRef(object typeLibrary)
        {
            string name, description, help;
            int context;
            ((ITypeLib)typeLibrary).GetDocumentation(-1, out name, out description, out context, out help);
            if (name == "stdole")
                return Assembly.Load("stdole, Version=7.0.3300.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            throw new NotSupportedException("Unexpected referenced type library: " + name);
        }
    }

    private static int Main(string[] arguments)
    {
        if (arguments.Length != 2)
        {
            Console.Error.WriteLine("Usage: ExtractInterop.exe <PXCEditorCore.x64.dll> <output-directory>");
            return 2;
        }

        ITypeLib typeLibrary = null;
        try
        {
            string sourcePath = Path.GetFullPath(arguments[0]);
            string outputDirectory = Path.GetFullPath(arguments[1]);
            Directory.CreateDirectory(outputDirectory);
            Environment.CurrentDirectory = outputDirectory;
            // REGKIND_NONE reads the embedded type library without COM registration.
            Marshal.ThrowExceptionForHR(LoadTypeLibEx(sourcePath, 2, out typeLibrary));
            AssemblyBuilder assembly = new TypeLibConverter().ConvertTypeLibToAssembly(
                typeLibrary, Path.Combine(outputDirectory, "PDFXEdit.dll"),
                TypeLibImporterFlags.SafeArrayAsSystemArray, new ImportNotifications(),
                null, null, null, null);
            assembly.Save("PDFXEdit.dll");
            Console.WriteLine("Generated PDFXEdit.dll; types=" + typeLibrary.GetTypeInfoCount());
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (typeLibrary != null && Marshal.IsComObject(typeLibrary))
                Marshal.ReleaseComObject(typeLibrary);
        }
    }
}
