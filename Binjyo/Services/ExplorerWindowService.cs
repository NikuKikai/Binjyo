using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Binjyo
{
    internal static class ExplorerWindowService
    {
        public static bool TryGetCurrentDirectory(IntPtr hwnd, out string directoryPath)
        {
            directoryPath = null;
            if (hwnd == IntPtr.Zero)
                return false;

            Type shellApplicationType = Type.GetTypeFromProgID("Shell.Application");
            if (shellApplicationType == null)
                return false;

            object shellApplication = null;
            object windows = null;
            try
            {
                shellApplication = Activator.CreateInstance(shellApplicationType);
                windows = shellApplicationType.InvokeMember(
                    "Windows",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shellApplication,
                    null);
                if (windows == null)
                    return false;

                int count = Convert.ToInt32(GetComProperty(windows, "Count") ?? 0);
                for (int i = 0; i < count; i++)
                {
                    object shellWindow = null;
                    object document = null;
                    object folder = null;
                    object folderSelf = null;
                    try
                    {
                        shellWindow = windows.GetType().InvokeMember(
                            "Item",
                            System.Reflection.BindingFlags.InvokeMethod,
                            null,
                            windows,
                            new object[] { i });
                        if (shellWindow == null)
                            continue;

                        long windowHandleValue = Convert.ToInt64(GetComProperty(shellWindow, "HWND") ?? 0);
                        if (windowHandleValue != hwnd.ToInt64())
                            continue;

                        document = GetComProperty(shellWindow, "Document");
                        folder = GetComProperty(document, "Folder");
                        folderSelf = GetComProperty(folder, "Self");
                        string path = GetComProperty(folderSelf, "Path") as string;
                        if (string.IsNullOrWhiteSpace(path))
                            continue;
                        if (!Directory.Exists(path))
                            continue;

                        directoryPath = path;
                        return true;
                    }
                    finally
                    {
                        ReleaseComObject(folderSelf);
                        ReleaseComObject(folder);
                        ReleaseComObject(document);
                        ReleaseComObject(shellWindow);
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                ReleaseComObject(windows);
                ReleaseComObject(shellApplication);
            }

            return false;
        }

        private static object GetComProperty(object target, string propertyName)
        {
            if (target == null)
                return null;

            return target.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                target,
                null);
        }

        private static void ReleaseComObject(object comObject)
        {
            if (comObject == null || !Marshal.IsComObject(comObject))
                return;

            Marshal.FinalReleaseComObject(comObject);
        }
    }
}
