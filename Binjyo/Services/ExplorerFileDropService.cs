using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Binjyo
{
    internal static class ExplorerFileDropService
    {
        private const int MkShift = 0x0004;
        private const int MkControl = 0x0008;

        public static DragDropEffects GetPreferredEffect(string targetDirectoryPath, string[] filePaths, int keyState)
        {
            if (string.IsNullOrWhiteSpace(targetDirectoryPath) || filePaths == null || filePaths.Length == 0)
                return DragDropEffects.None;
            if (!Directory.Exists(targetDirectoryPath))
                return DragDropEffects.None;
            if (filePaths.Any(path => string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path))))
                return DragDropEffects.None;

            bool isControlPressed = (keyState & MkControl) == MkControl;
            bool isShiftPressed = (keyState & MkShift) == MkShift;

            if (isShiftPressed && !isControlPressed)
                return DragDropEffects.Move;
            if (isControlPressed)
                return DragDropEffects.Copy;

            return AreAllSourcesOnSameVolume(targetDirectoryPath, filePaths)
                ? DragDropEffects.Move
                : DragDropEffects.Copy;
        }

        public static bool TryApplyDrop(string targetDirectoryPath, string[] filePaths, DragDropEffects effect, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (effect == DragDropEffects.None)
                {
                    errorMessage = "The selected drop operation is not supported.";
                    return false;
                }

                bool moveRequested = effect == DragDropEffects.Move;
                foreach (string sourcePath in filePaths)
                {
                    string name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    string destinationPath = Path.Combine(targetDirectoryPath, name);

                    if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                    {
                        errorMessage = $"A file or folder named '{name}' already exists in the target directory.";
                        return false;
                    }

                    MoveOrCopyPath(sourcePath, destinationPath, moveRequested);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static void MoveOrCopyPath(string sourcePath, string destinationPath, bool moveRequested)
        {
            bool isDirectory = Directory.Exists(sourcePath);
            bool canMoveDirectly = moveRequested && AreOnSameVolume(Path.GetDirectoryName(destinationPath), sourcePath);

            if (isDirectory)
            {
                if (canMoveDirectly)
                {
                    Directory.Move(sourcePath, destinationPath);
                    return;
                }

                CopyDirectory(sourcePath, destinationPath);
                if (moveRequested)
                    Directory.Delete(sourcePath, true);
                return;
            }

            if (canMoveDirectly)
            {
                File.Move(sourcePath, destinationPath);
                return;
            }

            File.Copy(sourcePath, destinationPath);
            if (moveRequested)
                File.Delete(sourcePath);
        }

        private static void CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath)
        {
            Directory.CreateDirectory(destinationDirectoryPath);

            foreach (string filePath in Directory.GetFiles(sourceDirectoryPath))
            {
                string destinationPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(filePath));
                File.Copy(filePath, destinationPath);
            }

            foreach (string directoryPath in Directory.GetDirectories(sourceDirectoryPath))
            {
                string destinationPath = Path.Combine(destinationDirectoryPath, Path.GetFileName(directoryPath));
                CopyDirectory(directoryPath, destinationPath);
            }
        }

        private static bool AreAllSourcesOnSameVolume(string targetDirectoryPath, string[] filePaths)
        {
            return filePaths.All(path => AreOnSameVolume(targetDirectoryPath, path));
        }

        private static bool AreOnSameVolume(string targetDirectoryPath, string sourcePath)
        {
            string targetRoot = Path.GetPathRoot(Path.GetFullPath(targetDirectoryPath)) ?? string.Empty;
            string sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath)) ?? string.Empty;
            return string.Equals(targetRoot, sourceRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
