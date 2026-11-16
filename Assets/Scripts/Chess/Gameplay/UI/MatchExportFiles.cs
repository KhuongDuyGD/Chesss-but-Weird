using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Platform file boundary. Desktop shares use an explicit saved-file/clipboard fallback.</summary>
public static class MatchExportFiles
{
    public static string ExportDirectory
    {
        get
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents)) return Path.Combine(documents, "Chess but Weird", "Exports");
#endif
            return Path.Combine(Application.persistentDataPath, "Exports");
        }
    }

    public static Task<string> SaveAsync(byte[] bytes, string mode, string extension)
    {
        if (bytes == null || bytes.Length == 0) throw new ArgumentException("There is no content to save.");
        if (extension != ".png" && extension != ".pgn" && extension != ".fen") throw new ArgumentException("Unsupported export format.");
        string directory = ExportDirectory;
        var safeMode = new StringBuilder();
        foreach (char c in mode ?? "match")
            if (char.IsLetterOrDigit(c) && safeMode.Length < 24) safeMode.Append(char.ToLowerInvariant(c));
        string name = "cbw-" + safeMode + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension;
        return Task.Run(() =>
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            string temporary = path + ".tmp";
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                { created = true; stream.Write(bytes, 0, bytes.Length); }
                File.Move(temporary, path);
                return path;
            }
            catch
            {
                if (created) { try { File.Delete(temporary); } catch { /* Preserve the original write error. */ } }
                throw;
            }
        });
    }
}
