// Purpose: Main Windows Forms application, Codex IPC client, and compact quota UI.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexQuotaTray
{
    internal sealed class Quota
    {
        public int FiveRemaining;
        public int WeekRemaining;
        public long FiveReset;
        public long WeekReset;
    }

    internal static class AppSettings
    {
        private const int DefaultRefreshMinutes = 5;
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaTray");
        private static readonly string FilePath = Path.Combine(Folder, "settings.txt");

        public static int LoadRefreshMinutes()
        {
            try
            {
                int value;
                if (Int32.TryParse(File.ReadAllText(FilePath).Trim(), out value) &&
                    new[] { 1, 3, 5, 10 }.Contains(value)) return value;
            }
            catch { }
            return DefaultRefreshMinutes;
        }

        public static void SaveRefreshMinutes(int value)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, value.ToString());
            }
            catch { }
        }
    }

    internal static class CodexClient
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static string FindCodex()
        {
            var paths = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in paths.Split(Path.PathSeparator))
            {
                try
                {
                    var exe = Path.Combine(dir.Trim(), "codex.exe");
                    if (File.Exists(exe)) return exe;
                }
                catch { }
            }
            var vscode = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode", "extensions");
            var npm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "node_modules", "@openai", "codex");
            foreach (var root in new[] { vscode, npm })
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var match = Directory.GetFiles(root, "codex.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (match != null) return match;
                }
                catch { }
            }
            return null;
        }

        private static ProcessStartInfo StartInfo(string exe, string args)
        {
            return new ProcessStartInfo(exe, args) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true,
                RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8
            };
        }

        public static Tuple<bool, string> LoginState()
        {
            var exe = FindCodex();
            if (exe == null) return Tuple.Create(false, "Codex is not installed");
            try
            {
                using (var p = Process.Start(StartInfo(exe, "login status")))
                {
                    var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(8000)) return Tuple.Create(false, "Login check timed out");
                    return p.ExitCode == 0 && output.Contains("Logged in")
                        ? Tuple.Create(true, "")
                        : Tuple.Create(false, "Not signed in - double-click to open Codex");
                }
            }
            catch { return Tuple.Create(false, "Unable to check Codex sign-in status"); }
        }

        public static Quota ReadQuota()
        {
            var state = LoginState();
            if (!state.Item1) throw new InvalidOperationException(state.Item2);
            var exe = FindCodex();
            using (var p = Process.Start(StartInfo(exe, "app-server --stdio")))
            {
                string init = Json.Serialize(new Dictionary<string, object> {
                    {"id", 1}, {"method", "initialize"},
                    {"params", new Dictionary<string, object> {
                        {"clientInfo", new Dictionary<string, object> {{"name", "codex-quota-tray"}, {"version", "1.3.2"}}},
                        {"capabilities", new Dictionary<string, object> {{"experimentalApi", true}}}
                    }}
                });
                string read = Json.Serialize(new Dictionary<string, object> {
                    {"id", 2}, {"method", "account/rateLimits/read"}, {"params", null}
                });
                p.StandardInput.WriteLine(init);
                p.StandardInput.WriteLine(read);
                p.StandardInput.Flush();
                var task = Task.Factory.StartNew(() => {
                    string line;
                    while ((line = p.StandardOutput.ReadLine()) != null)
                    {
                        Dictionary<string, object> msg;
                        try { msg = Json.Deserialize<Dictionary<string, object>>(line); }
                        catch { continue; }
                        object id;
                        if (!msg.TryGetValue("id", out id) || Convert.ToInt32(id) != 2) continue;
                        object error;
                        if (msg.TryGetValue("error", out error)) throw new InvalidOperationException("Codex returned an error");
                        var result = (Dictionary<string, object>)msg["result"];
                        var limits = (Dictionary<string, object>)result["rateLimits"];
                        var primary = (Dictionary<string, object>)limits["primary"];
                        var secondary = (Dictionary<string, object>)limits["secondary"];
                        return new Quota {
                            FiveRemaining = Math.Max(0, 100 - Convert.ToInt32(primary["usedPercent"])),
                            WeekRemaining = Math.Max(0, 100 - Convert.ToInt32(secondary["usedPercent"])),
                            FiveReset = Convert.ToInt64(primary["resetsAt"]),
                            WeekReset = Convert.ToInt64(secondary["resetsAt"])
                        };
                    }
                    throw new InvalidOperationException("No quota data was returned by Codex");
                });
                try
                {
                    if (!task.Wait(15000)) throw new TimeoutException("Timed out while reading Codex quota");
                    return task.Result;
                }
                finally
                {
                    // Closing stdin sends EOF so Codex app-server can shut down normally.
                    // Avoid force-terminating child processes, which can trigger AV heuristics.
                    try { p.StandardInput.Close(); } catch { }
                    try { p.WaitForExit(3000); } catch { }
                }
            }
        }

        public static void OpenCodex()
        {
            var exe = FindCodex();
            if (exe == null) return;
            // The official CLI handles both focusing an existing app and launching it.
            Process.Start(new ProcessStartInfo(exe, "app") { UseShellExecute = false, CreateNoWindow = true });
        }
    }

    internal static class NativeMethods
    {
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010;
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }

    internal sealed class QuotaForm : Form
    {
        private readonly Label main = new Label();
        private readonly Label detail = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly Dictionary<int, ToolStripMenuItem> intervalItems = new Dictionary<int, ToolStripMenuItem>();
        private readonly Screen startupScreen;
        private int refreshMinutes;
        private bool refreshing;
        private Point dragStart;
        private bool dragging;

        public QuotaForm()
        {
            Text = "Codex Quota Tray";
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(17, 20, 24);
            Opacity = 0.96;
            AutoScaleMode = AutoScaleMode.Dpi;
            startupScreen = Screen.FromPoint(Cursor.Position);

            // Follow the user's Windows desktop icon-text setting, but keep the
            // overlay deliberately smaller than normal application text.
            var desktopFont = SystemFonts.IconTitleFont;
            float mainSize = Math.Max(7.0f, Math.Min(9.0f, desktopFont.SizeInPoints * 0.88f));
            float detailSize = Math.Max(6.0f, Math.Min(7.5f, desktopFont.SizeInPoints * 0.75f));

            main.AutoSize = true;
            main.ForeColor = Color.FromArgb(233, 238, 244);
            main.Font = new Font(desktopFont.FontFamily, mainSize, FontStyle.Bold);
            main.Text = "Codex  Reading...";
            detail.AutoSize = true;
            detail.ForeColor = Color.FromArgb(141, 154, 170);
            detail.Font = new Font(desktopFont.FontFamily, detailSize, FontStyle.Regular);
            Controls.Add(main); Controls.Add(detail);
            FitToText();
            SnapToStartupScreen();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Refresh now", null, delegate { RefreshQuota(); });
            var intervalMenu = new ToolStripMenuItem("Refresh interval");
            foreach (int minutes in new[] { 1, 3, 5, 10 })
            {
                int selectedMinutes = minutes;
                var item = new ToolStripMenuItem(minutes + (minutes == 1 ? " minute" : " minutes"));
                item.Click += delegate { SetRefreshInterval(selectedMinutes, true); };
                intervalItems.Add(minutes, item);
                intervalMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(intervalMenu);
            menu.Items.Add("Open Codex", null, delegate { CodexClient.OpenCodex(); });
            menu.Items.Add(new ToolStripSeparator());
            var exitItem = menu.Items.Add("Exit quota tray");
            exitItem.ForeColor = Color.FromArgb(220, 70, 70);
            exitItem.Click += delegate { timer.Stop(); Close(); };
            ContextMenuStrip = menu; main.ContextMenuStrip = menu; detail.ContextMenuStrip = menu;
            foreach (Control c in new Control[] { this, main, detail }) {
                c.MouseDown += DragDown; c.MouseMove += DragMove; c.MouseUp += delegate { dragging = false; KeepOnTop(); };
                c.DoubleClick += delegate { CodexClient.OpenCodex(); };
            }
            timer.Tick += delegate { RefreshQuota(); };
            SetRefreshInterval(AppSettings.LoadRefreshMinutes(), false);
            timer.Start();
            Shown += delegate { SnapToStartupScreen(); KeepOnTop(); RefreshQuota(); };
            LocationChanged += delegate { KeepOnTop(); };
            Activated += delegate { KeepOnTop(); };
            Deactivate += delegate { KeepOnTop(); };
        }

        private void DragDown(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; } }
        private void DragMove(object sender, MouseEventArgs e) { if (dragging) { var p = Cursor.Position; Location = new Point(Left + p.X - dragStart.X, Top + p.Y - dragStart.Y); dragStart = p; KeepOnTop(); } }

        private void KeepOnTop()
        {
            if (!IsHandleCreated) return;
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        private void FitToText()
        {
            int right = Right;
            int bottom = Bottom;
            int width = Math.Max(TextRenderer.MeasureText(main.Text, main.Font).Width,
                                 TextRenderer.MeasureText(detail.Text, detail.Font).Width) + 12;
            width = Math.Max(112, width);
            main.Location = new Point(6, 2);
            detail.Location = new Point(6, main.Bottom - 1);
            ClientSize = new Size(width, detail.Bottom + 3);
            Left = right - Width;
            Top = bottom - Height;
            KeepOnTop();
        }

        private void SnapToStartupScreen()
        {
            var area = startupScreen.WorkingArea;
            Location = new Point(area.Right - Width - 6, area.Bottom - Height - 6);
        }

        private static DateTime FromUnix(long seconds) { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime(); }
        private static string FiveReset(long seconds) { var d = FromUnix(seconds); return d.Date == DateTime.Today ? d.ToString("HH:mm") : d.ToString("MM-dd HH:mm"); }
        private static string WeekReset(long seconds) { return FromUnix(seconds).ToString("ddd HH:mm"); }

        private void SetRefreshInterval(int minutes, bool save)
        {
            refreshMinutes = minutes;
            timer.Interval = minutes * 60 * 1000;
            foreach (var pair in intervalItems) pair.Value.Checked = pair.Key == minutes;
            if (save) AppSettings.SaveRefreshMinutes(minutes);
            if (timer.Enabled) { timer.Stop(); timer.Start(); }
        }

        private async void RefreshQuota()
        {
            if (refreshing) return;
            refreshing = true;
            timer.Stop();
            Exception lastError = null;
            try
            {
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    main.Text = attempt == 0 ? "Codex  Refreshing..." : String.Format("Codex  Retry {0}/3...", attempt);
                    main.ForeColor = Color.FromArgb(233, 238, 244);
                    FitToText();
                    try
                    {
                        var q = await Task.Factory.StartNew<Quota>(() => CodexClient.ReadQuota());
                        int low = Math.Min(q.FiveRemaining, q.WeekRemaining);
                        main.ForeColor = low > 30 ? Color.FromArgb(86, 211, 100) : low > 10 ? Color.FromArgb(227, 179, 65) : Color.FromArgb(248, 81, 73);
                        main.Text = String.Format("5h {0}%   |   Week {1}%", q.FiveRemaining, q.WeekRemaining);
                        detail.Text = "Resets " + FiveReset(q.FiveReset) + "   |   " + WeekReset(q.WeekReset);
                        FitToText();
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                    if (attempt < 3) await Task.Delay(1500);
                }

                var msg = lastError == null ? "Unknown refresh error" : lastError.GetBaseException().Message;
                main.Text = msg.Contains("not installed") ? "Codex  Not installed" : msg.Contains("Not signed in") ? "Codex  Not signed in" : "Codex  Unavailable";
                main.ForeColor = Color.FromArgb(248, 81, 73);
                detail.Text = msg.Length > 35 ? msg.Substring(0, 35) : msg;
                FitToText();
            }
            finally
            {
                refreshing = false;
                timer.Interval = refreshMinutes * 60 * 1000;
                if (!IsDisposed && !Disposing) timer.Start();
            }
        }
    }

    internal static class Program
    {
        private static Mutex instanceMutex;

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--self-test")
            {
                try
                {
                    var q = CodexClient.ReadQuota();
                    File.WriteAllText(args[1], String.Format("5h={0};week={1};fiveReset={2};weekReset={3}", q.FiveRemaining, q.WeekRemaining, q.FiveReset, q.WeekReset));
                }
                catch (Exception ex) { File.WriteAllText(args[1], "ERROR: " + ex.GetBaseException().Message); }
                return;
            }
            bool created;
            instanceMutex = new Mutex(true, "Local\\CodexQuotaTray.SingleInstance", out created);
            if (!created)
            {
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QuotaForm());
        }
    }
}
