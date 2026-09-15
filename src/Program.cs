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
                    if (!p.WaitForExit(8000)) { p.Kill(); return Tuple.Create(false, "Login check timed out"); }
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
                        {"clientInfo", new Dictionary<string, object> {{"name", "codex-quota-tray"}, {"version", "1.2.0"}}},
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
                    try { p.StandardInput.Close(); p.Kill(); } catch { }
                }
            }
        }

        public static void OpenCodex()
        {
            foreach (var process in Process.GetProcessesByName("Codex"))
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(process.MainWindowHandle, 9);
                    NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                    return;
                }
            }
            var exe = FindCodex();
            if (exe == null) return;
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
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int command);
    }

    internal sealed class QuotaForm : Form
    {
        private readonly Label main = new Label();
        private readonly Label detail = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer topmostTimer = new System.Windows.Forms.Timer();
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
            ClientSize = new Size(174, 40);
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 8, area.Bottom - Height - 6);

            main.AutoSize = true;
            main.Location = new Point(8, 3);
            main.ForeColor = Color.FromArgb(233, 238, 244);
            main.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            main.Text = "Codex  Reading...";
            detail.AutoSize = true;
            detail.Location = new Point(8, 21);
            detail.ForeColor = Color.FromArgb(141, 154, 170);
            detail.Font = new Font("Segoe UI", 7.5f);
            Controls.Add(main); Controls.Add(detail);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Refresh now", null, delegate { RefreshQuota(); });
            menu.Items.Add("Open Codex", null, delegate { CodexClient.OpenCodex(); });
            menu.Items.Add(new ToolStripSeparator());
            var exitItem = menu.Items.Add("Exit quota tray");
            exitItem.ForeColor = Color.FromArgb(220, 70, 70);
            exitItem.Click += delegate { timer.Stop(); topmostTimer.Stop(); Close(); };
            ContextMenuStrip = menu; main.ContextMenuStrip = menu; detail.ContextMenuStrip = menu;
            foreach (Control c in new Control[] { this, main, detail }) {
                c.MouseDown += DragDown; c.MouseMove += DragMove; c.MouseUp += delegate { dragging = false; };
                c.DoubleClick += delegate { CodexClient.OpenCodex(); };
            }
            timer.Interval = 60000; timer.Tick += delegate { RefreshQuota(); }; timer.Start();
            topmostTimer.Interval = 750; topmostTimer.Tick += delegate { KeepOnTop(); }; topmostTimer.Start();
            Shown += delegate { KeepOnTop(); RefreshQuota(); };
            LocationChanged += delegate { KeepOnTop(); };
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
            int width = Math.Max(TextRenderer.MeasureText(main.Text, main.Font).Width,
                                 TextRenderer.MeasureText(detail.Text, detail.Font).Width) + 16;
            width = Math.Max(138, width);
            ClientSize = new Size(width, 40);
            main.Location = new Point(8, 3);
            detail.Location = new Point(8, 21);
            Left = right - Width;
            KeepOnTop();
        }

        private static DateTime FromUnix(long seconds) { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToLocalTime(); }
        private static string FiveReset(long seconds) { var d = FromUnix(seconds); return d.Date == DateTime.Today ? d.ToString("HH:mm") : d.ToString("MM-dd HH:mm"); }
        private static string WeekReset(long seconds) { return FromUnix(seconds).ToString("ddd HH:mm"); }

        private async void RefreshQuota()
        {
            main.Text = "Codex  Refreshing..."; main.ForeColor = Color.FromArgb(233, 238, 244);
            try
            {
                var q = await Task.Factory.StartNew<Quota>(() => CodexClient.ReadQuota());
                int low = Math.Min(q.FiveRemaining, q.WeekRemaining);
                main.ForeColor = low > 30 ? Color.FromArgb(86, 211, 100) : low > 10 ? Color.FromArgb(227, 179, 65) : Color.FromArgb(248, 81, 73);
                main.Text = String.Format("5h {0}%   |   Week {1}%", q.FiveRemaining, q.WeekRemaining);
                detail.Text = "Resets " + FiveReset(q.FiveReset) + "   |   " + WeekReset(q.WeekReset);
                FitToText();
            }
            catch (Exception ex)
            {
                var msg = ex.GetBaseException().Message;
                main.Text = msg.Contains("not installed") ? "Codex  Not installed" : msg.Contains("Not signed in") ? "Codex  Not signed in" : "Codex  Unavailable";
                main.ForeColor = Color.FromArgb(248, 81, 73); detail.Text = msg.Length > 35 ? msg.Substring(0, 35) : msg;
                FitToText();
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
