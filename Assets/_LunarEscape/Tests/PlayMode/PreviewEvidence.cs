using System;
using System.IO;
using UnityEngine;

namespace LunarEscape.Tests
{
    internal static class PreviewEvidence
    {
        private static readonly string RunFolder = Path.Combine("Logs", "PreviewEvidence", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        // A desktop image preview can memory-map the previous PNG on Windows.
        // Replace its directory entry instead of truncating that mapped file.
        public static void Write(string path, byte[] png)
        {
            Directory.CreateDirectory(RunFolder);
            string archive = Path.GetFullPath(Path.Combine(RunFolder,Path.GetFileName(path)));
            File.WriteAllBytes(archive,png);
            string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(staged, png);
                if (File.Exists(path)) File.Replace(staged, path, null);
                else File.Move(staged, path);
            }
            catch (IOException)
            {
                // The evidence is already safely saved. An open viewer may deny
                // even atomic replacement of the optional latest-preview alias.
                Debug.LogWarning("Latest preview is in use; fresh evidence saved at " + archive);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }
    }
}
