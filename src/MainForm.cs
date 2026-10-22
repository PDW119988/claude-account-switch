using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeAccountSwitch
{
    class MainForm : Form
    {
        readonly TrayApp app;
        readonly FlowLayoutPanel list = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Window,
            Dock = DockStyle.Fill, Padding = new Padding(6), MinimumSize = new Size(0, 120),
        };
        readonly TableLayoutPanel unsaved = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1,
            BackColor = Color.FromArgb(0xFF, 0xF4, 0xD6), Padding = new Padding(6), Visible = false,
        };
        readonly Label unsavedText = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        readonly Button switchBtn = new Button { AutoSize = true };
        readonly Button renameBtn = new Button { AutoSize = true };
        readonly Button removeBtn = new Button { AutoSize = true };
        readonly CheckBox autostart = new CheckBox { AutoSize = true };
        readonly List<RadioButton> radios = new List<RadioButton>();
        List<Profile> profiles = new List<Profile>();
        bool quitting;
        bool hintShown;

        public MainForm(TrayApp app)
        {
            this.app = app;
            Text = L.T("Claude 계정 전환", "Claude Account Switch");
            Font = Ui.Font;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            Size = new Size(560, 420);
            MinimumSize = new Size(480, 360);
            Padding = new Padding(12);

            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            t.Controls.Add(new Label
            {
                AutoSize = true, Margin = new Padding(0, 0, 0, 8),
                Text = L.T("사용할 계정을 고르고 [전환]을 누르세요.", "Pick an account and press [Switch]."),
            }, 0, 0);

            var register = new Button { Text = L.T("이 계정 등록", "Save this account"), AutoSize = true, Anchor = AnchorStyles.Right };
            register.Click += OnSaveCurrent;
            unsaved.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            unsaved.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            unsaved.Controls.Add(unsavedText, 0, 0);
            unsaved.Controls.Add(register, 1, 0);
            unsaved.Margin = new Padding(0, 0, 0, 8);
            t.Controls.Add(unsaved, 0, 1);

            t.Controls.Add(list, 0, 2);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            switchBtn.Text = L.T("전환", "Switch");
            switchBtn.Font = Ui.Bold;
            switchBtn.Click += OnSwitch;
            var add = new Button { Text = L.T("계정 등록", "Add account"), AutoSize = true };
            add.Click += OnAdd;
            renameBtn.Text = L.T("이름 변경", "Rename");
            renameBtn.Click += OnRename;
            removeBtn.Text = L.T("삭제", "Remove");
            removeBtn.Click += OnRemove;
            buttons.Controls.AddRange(new Control[] { switchBtn, add, renameBtn, removeBtn });
            t.Controls.Add(buttons, 0, 3);

            autostart.Text = L.T("Windows 시작 시 자동 실행 (작업표시줄 알림 영역)", "Start with Windows (notification area)");
            autostart.Margin = new Padding(0, 10, 0, 0);
            autostart.CheckedChanged += delegate { Autostart.Enabled = autostart.Checked; };
            t.Controls.Add(autostart, 0, 4);

            t.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 8, 0, 0), MaximumSize = new Size(520, 0),
                Text = L.T(
                    "전환한 뒤에는 Claude Code 에서 새 대화를 열어야 새 계정이 적용됩니다.\n" +
                    "VSCode 는 Ctrl+Shift+P → Developer: Reload Window 를 실행하면 확실합니다.",
                    "After switching, start a new Claude Code conversation to use the new account.\n" +
                    "In VS Code, Ctrl+Shift+P → Developer: Reload Window is the sure way."),
            }, 0, 5);

            Controls.Add(t);
            AcceptButton = switchBtn;

            FormClosing += OnClosing;
            Shown += delegate { autostart.Checked = Autostart.Enabled; };
        }

        public void Reload(List<Profile> all, Profile active, string unsavedEmail)
        {
            profiles = all;
            string keep = null;
            foreach (var r in radios) if (r.Checked) keep = ((Profile)r.Tag).Name;

            list.SuspendLayout();
            list.Controls.Clear();
            radios.Clear();
            if (all.Count == 0)
            {
                list.Controls.Add(new Label
                {
                    AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(6),
                    Text = L.T("등록된 계정이 없습니다. [계정 등록]으로 추가하세요.", "No accounts yet. Use [Add account]."),
                });
            }
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                bool isActive = active != null && active.Name == p.Name;
                var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
                var badge = new PictureBox { Image = Ui.Badge(Ui.ColorFor(i), p.Name, 20), Size = new Size(20, 20), Margin = new Padding(0, 3, 6, 0) };
                var radio = new RadioButton
                {
                    AutoSize = true, Tag = p, Margin = new Padding(0, 3, 0, 0),
                    Text = p.Name + "    " + p.Summary,
                    Font = isActive ? Ui.Bold : Ui.Font,
                };
                radio.CheckedChanged += OnRadio;
                radio.DoubleClick += OnSwitch;
                row.Controls.Add(badge);
                row.Controls.Add(radio);
                if (isActive)
                {
                    row.Controls.Add(new Label
                    {
                        AutoSize = true, Text = L.T("사용 중", "in use"), ForeColor = Color.White,
                        BackColor = Ui.ColorFor(i), Padding = new Padding(4, 1, 4, 1), Margin = new Padding(8, 4, 0, 0),
                    });
                }
                list.Controls.Add(row);
                radios.Add(radio);
            }
            foreach (var r in radios)
            {
                string n = ((Profile)r.Tag).Name;
                if (keep != null ? n == keep : active != null && n == active.Name) r.Checked = true;
            }
            list.ResumeLayout();

            unsaved.Visible = unsavedEmail != null;
            unsavedText.Text = unsavedEmail == null ? "" : L.T(
                "지금 로그인된 " + unsavedEmail + " 이(가) 목록에 없습니다.",
                "The signed-in account " + unsavedEmail + " is not saved yet.");
            UpdateButtons();
        }

        // radio buttons sit in separate rows, so group them by hand
        void OnRadio(object sender, EventArgs e)
        {
            var me = (RadioButton)sender;
            if (!me.Checked) return;
            foreach (var r in radios) if (r != me) r.Checked = false;
            UpdateButtons();
        }

        Profile Selected
        {
            get
            {
                foreach (var r in radios) if (r.Checked) return (Profile)r.Tag;
                return null;
            }
        }

        void UpdateButtons()
        {
            bool any = Selected != null;
            switchBtn.Enabled = renameBtn.Enabled = removeBtn.Enabled = any;
        }

        void OnSwitch(object sender, EventArgs e)
        {
            var p = Selected;
            if (p != null) app.SwitchTo(p, this);
        }

        void OnAdd(object sender, EventArgs e)
        {
            using (var f = new RegisterForm())
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                app.Refresh();
                var p = profiles.Find(x => x.Name == f.SavedName);
                if (p == null) return;
                if (MessageBox.Show(this,
                        L.T("'" + p.Name + "' 계정을 등록했습니다.\n지금 이 계정으로 전환할까요?",
                            "Saved '" + p.Name + "'.\nSwitch to it now?"),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    app.SwitchTo(p, this);
            }
        }

        void OnSaveCurrent(object sender, EventArgs e)
        {
            string acc, oauth;
            Store.ReadCurrent(out acc, out oauth);
            if (oauth == null) return;
            string name = Ui.Prompt(this, Text, L.T("이 계정을 목록에 보일 이름 (예: 회사)", "Name for this account (e.g. Work)"), "");
            if (string.IsNullOrEmpty(name) || Store.SafeName(name).Length == 0) return;
            if (Store.NameTaken(name, acc))
            {
                MessageBox.Show(this, L.T("같은 이름이 이미 있습니다.", "That name already exists."), Text);
                return;
            }
            Store.Save(name, acc, oauth);
            app.Refresh();
        }

        void OnRename(object sender, EventArgs e)
        {
            var p = Selected;
            if (p == null) return;
            string name = Ui.Prompt(this, Text, L.T("새 이름", "New name"), p.Name);
            if (string.IsNullOrEmpty(name) || Store.SafeName(name) == p.Name) return;
            if (Store.NameTaken(name, null))
            {
                MessageBox.Show(this, L.T("같은 이름이 이미 있습니다.", "That name already exists."), Text);
                return;
            }
            Store.Rename(p, name);
            app.Refresh();
        }

        void OnRemove(object sender, EventArgs e)
        {
            var p = Selected;
            if (p == null) return;
            var active = Store.Active(profiles);
            if (active != null && active.Name == p.Name)
            {
                MessageBox.Show(this, L.T("사용 중인 계정은 다른 계정으로 전환한 뒤 삭제하세요.",
                                          "Switch to another account before removing the one in use."), Text);
                return;
            }
            if (MessageBox.Show(this,
                    L.T("'" + p.Name + "' 을(를) 목록에서 삭제할까요?\n다시 쓰려면 계정 등록을 새로 해야 합니다.",
                        "Remove '" + p.Name + "' from the list?\nYou will need to add it again to use it."),
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Store.Remove(p);
            app.Refresh();
        }

        public void Quit()
        {
            quitting = true;
            Close();
        }

        // closing the window keeps the app in the notification area
        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (quitting || e.CloseReason == CloseReason.WindowsShutDown) return;
            e.Cancel = true;
            Hide();
            if (!hintShown)
            {
                hintShown = true;
                app.Balloon(L.T("알림 영역에서 계속 실행 중입니다. 아이콘을 누르면 다시 열립니다.",
                                "Still running in the notification area. Click the icon to reopen."));
            }
        }
    }
}
