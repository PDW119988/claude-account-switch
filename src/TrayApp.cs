using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudeAccountSwitch
{
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "ClaudeAccountSwitch";

        static string Command { get { return "\"" + Application.ExecutablePath + "\" --tray"; } }

        public static bool Enabled
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(ValueName) != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(ValueName, Command);
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName);
                }
            }
        }

        // Turn it on the first time the app runs, and follow the exe if it moved.
        public static void Init()
        {
            string marker = Path.Combine(Store.DataDir, "autostart-initialized");
            if (!File.Exists(marker))
            {
                Directory.CreateDirectory(Store.DataDir);
                File.WriteAllText(marker, DateTime.Now.ToString("s"));
                Enabled = true;
                return;
            }
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                var v = k == null ? null : k.GetValue(ValueName) as string;
                if (v != null && v != Command) Enabled = true;
            }
        }
    }

    class TrayApp : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly MainForm form;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 30000 };
        List<Profile> profiles = new List<Profile>();
        Profile active;

        public TrayApp(bool showWindow, EventWaitHandle showSignal)
        {
            form = new MainForm(this);
            var h = form.Handle; // create the handle so BeginInvoke works before the window is shown

            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.ContextMenuStrip.Opening += delegate { Refresh(); BuildMenu(); };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.Visible = true;

            try { Autostart.Init(); } catch (Exception) { }

            timer.Tick += delegate { Refresh(); };
            timer.Start();
            Refresh();
            BuildMenu();

            // a second launch of the exe asks this instance to show its window
            ThreadPool.RegisterWaitForSingleObject(showSignal,
                delegate { try { form.BeginInvoke((Action)ShowWindow); } catch (Exception) { } },
                null, -1, false);

            if (showWindow) ShowWindow();
        }

        public void Refresh()
        {
            string unsavedEmail = null;
            try
            {
                Store.SyncActive();
                profiles = Store.Load();
                active = Store.Active(profiles);
                if (active == null)
                {
                    string acc, oauth;
                    Store.ReadCurrent(out acc, out oauth);
                    if (acc != null && oauth != null) unsavedEmail = Json.GetString(acc, "emailAddress") ?? "?";
                }
            }
            catch (Exception) { /* Claude Code may be mid-write; try again next tick */ }

            int idx = active == null ? -1 : profiles.FindIndex(p => p.Name == active.Name);
            tray.Icon = Ui.TrayIcon(idx, active == null ? "?" : active.Name);
            string tip = active == null
                ? L.T("Claude: 저장 안 된 계정", "Claude: account not saved")
                : "Claude: " + active.Name + " (" + active.Email + ")";
            tray.Text = tip.Length > 63 ? tip.Substring(0, 60) + "..." : tip;

            form.Reload(profiles, active, unsavedEmail);
        }

        void BuildMenu()
        {
            var m = tray.ContextMenuStrip;
            m.Items.Clear();
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                var item = new ToolStripMenuItem(p.Name + "   " + p.Email)
                {
                    Image = Ui.Badge(Ui.ColorFor(i), p.Name, 16),
                    Checked = active != null && active.Name == p.Name,
                };
                item.Click += delegate { SwitchTo(p, null); };
                m.Items.Add(item);
            }
            if (profiles.Count > 0) m.Items.Add(new ToolStripSeparator());
            m.Items.Add(L.T("창 열기", "Open"), null, delegate { ShowWindow(); });
            m.Items.Add(L.T("종료", "Exit"), null, delegate { Quit(); });
        }

        public void SwitchTo(Profile p, IWin32Window owner)
        {
            if (active != null && active.Name == p.Name)
            {
                Balloon(L.T("이미 '" + p.Name + "' 계정을 쓰고 있습니다.", "Already using '" + p.Name + "'."));
                return;
            }
            if (active == null)
            {
                string acc, oauth;
                Store.ReadCurrent(out acc, out oauth);
                if (oauth != null && MessageBox.Show(owner,
                        L.T("지금 로그인된 계정이 목록에 저장돼 있지 않습니다.\n전환하면 그 계정은 나중에 다시 로그인해야 합니다. 계속할까요?\n\n(저장하려면 [아니요] → 창 위쪽의 [이 계정 등록])",
                            "The signed-in account is not saved.\nIf you switch, you will need to sign in to it again later. Continue?\n\n(To keep it: No → [Save this account] at the top of the window)"),
                        L.T("Claude 계정 전환", "Claude Account Switch"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    ShowWindow();
                    return;
                }
            }
            try
            {
                Store.SwitchTo(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, L.T("전환하지 못했습니다: ", "Could not switch: ") + ex.Message,
                    L.T("Claude 계정 전환", "Claude Account Switch"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Refresh();
            Balloon(L.T("'" + p.Name + "' 계정으로 전환했습니다. /usage 로 확인할 수 있습니다.",
                        "Switched to '" + p.Name + "'. Check with /usage."));
        }

        public void Balloon(string text)
        {
            tray.BalloonTipTitle = L.T("Claude 계정 전환", "Claude Account Switch");
            tray.BalloonTipText = text;
            tray.ShowBalloonTip(4000);
        }

        void ShowWindow()
        {
            Refresh();
            if (!form.Visible) form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        void Quit()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            form.Quit();
            ExitThread();
        }
    }
}
