// Shura launcher: the one file a player clicks.
//
// Two shapes, same code (C# 5, compiled by the csc that ships with Windows' .NET Framework):
//   * Folder: "Shura.exe" sits next to the folder "jogo\" that holds the Unity build; it
//     starts jogo\Shura.exe. The player sees one icon, the guide and a folder.
//   * Single file: the Unity build travels inside the executable as the resource "jogo.zip".
//     The first run unpacks it once into %LOCALAPPDATA%\Shura\<version> (with a progress
//     window), every later run starts the game at once. Nothing else to download or extract.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Shura")]
[assembly: AssemblyProduct("Shura")]
[assembly: AssemblyDescription("Shura — duelos de aparar e postura")]
[assembly: AssemblyCompany("Shura")]
[assembly: AssemblyCopyright("Shura")]
[assembly: AssemblyVersion("0.9.0.0")]
[assembly: AssemblyFileVersion("0.9.0.0")]

static class Launcher
{
    const string Version = "0.9.0";
    const string Payload = "jogo.zip";

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        string here = AppDomain.CurrentDomain.BaseDirectory;

        // Folder: the game lives right here.
        string local = Path.Combine(Path.Combine(here, "jogo"), "Shura.exe");
        if (File.Exists(local)) return Start(local, args);

        // Single file: the game travels inside this executable.
        Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(Payload);
        if (payload == null)
        {
            MessageBox.Show("Os arquivos do jogo não foram encontrados.\n\nExtraia o zip inteiro (não abra de dentro do zip) e clique em Shura.exe de novo.",
                            "Shura", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 1;
        }
        string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shura");
        string root = Path.Combine(baseDir, Version);
        string exe = Path.Combine(root, "Shura.exe");
        string ready = Path.Combine(root, ".pronto");
        if (!File.Exists(ready) || !File.Exists(exe))
        {
            if (!Unpack(payload, root, ready)) return 1;
            RemoveOldVersions(baseDir, Version);
        }
        return Start(exe, args);
    }

    static int Start(string exe, string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = false,
                Arguments = JoinArgs(args),
            };
            Process.Start(info);
            return 0;
        }
        catch (Exception e)
        {
            MessageBox.Show("Não foi possível abrir o jogo:\n" + e.Message, "Shura", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static string JoinArgs(string[] args)
    {
        var parts = new string[args.Length];
        for (int i = 0; i < args.Length; i++) parts[i] = args[i].Contains(" ") ? "\"" + args[i] + "\"" : args[i];
        return string.Join(" ", parts);
    }

    /// <summary>First run of the single file: unpack the game once, showing progress.</summary>
    static bool Unpack(Stream payload, string root, string ready)
    {
        var form = new Form
        {
            Text = "Shura",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(460, 150),
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(20, 17, 24),
            ForeColor = Color.FromArgb(241, 232, 214),
        };
        try { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var title = new Label
        {
            Text = "修羅  Shura",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(242, 193, 78),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 12, 460, 36),
        };
        var text = new Label
        {
            Text = "Preparando o jogo pela primeira vez…",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 54, 460, 26),
        };
        var bar = new ProgressBar { Bounds = new Rectangle(30, 92, 400, 22), Minimum = 0, Maximum = 1000 };
        form.Controls.Add(title);
        form.Controls.Add(text);
        form.Controls.Add(bar);

        bool ok = false;
        string error = null;
        var worker = new Thread(() =>
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                using (var zip = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    int total = zip.Entries.Count, done = 0;
                    string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                        // Never write outside the game folder.
                        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) continue;
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            Directory.CreateDirectory(target);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            entry.ExtractToFile(target, true);
                        }
                        done++;
                        int value = (int)(1000L * done / Math.Max(1, total));
                        form.BeginInvoke(new Action(() => bar.Value = Math.Min(1000, value)));
                    }
                }
                File.WriteAllText(ready, Version);
                ok = true;
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            form.BeginInvoke(new Action(() => form.Close()));
        });
        worker.IsBackground = true;
        form.Shown += (s, e) => worker.Start();
        Application.Run(form);
        worker.Join();
        if (!ok)
        {
            MessageBox.Show("Não foi possível preparar o jogo:\n" + error + "\n\nConfira se há espaço livre (cerca de 200 MB).",
                            "Shura", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return ok;
    }

    /// <summary>Older unpacked versions are removed once the new one is ready.</summary>
    static void RemoveOldVersions(string baseDir, string keep)
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(baseDir))
            {
                if (!string.Equals(Path.GetFileName(dir), keep, StringComparison.OrdinalIgnoreCase)) Directory.Delete(dir, true);
            }
        }
        catch
        {
            // A folder in use stays; it costs only disk space.
        }
    }
}
