using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeAccountSwitch
{
    // 계정 등록: 이름 입력 → [연결하기] → 브라우저 인증 → 코드 붙여넣기 → 완료
    class RegisterForm : Form
    {
        readonly TextBox nameBox = new TextBox { Width = 300 };
        readonly TextBox emailBox = new TextBox { Width = 300 };
        readonly Button connect = new Button { AutoSize = true };
        readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 4, 0, 6) };
        readonly LinkLabel openLink = new LinkLabel { AutoSize = true, Visible = false };
        readonly TextBox codeBox = new TextBox { Width = 300, Enabled = false };
        readonly Button finish = new Button { AutoSize = true, Enabled = false };
        readonly Button cancel = new Button { AutoSize = true, DialogResult = DialogResult.Cancel };
        LoginSession session;

        public string SavedName { get; private set; }

        public RegisterForm()
        {
            Text = L.T("계정 등록", "Add account");
            Font = Ui.Font;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };

            t.Controls.Add(Heading(L.T("1. 계정 이름", "1. Account name")));
            t.Controls.Add(Hint(L.T("목록에 보일 이름입니다. 예: 회사, 개인", "Shown in the list. e.g. Work, Personal")));
            t.Controls.Add(nameBox);
            t.Controls.Add(Hint(L.T("이메일 (선택 · 로그인 화면에 미리 채워짐)", "Email (optional, pre-fills the sign-in page)")));
            t.Controls.Add(emailBox);

            connect.Text = L.T("연결하기", "Connect");
            connect.Margin = new Padding(0, 8, 0, 4);
            connect.Click += OnConnect;
            t.Controls.Add(connect);
            t.Controls.Add(Hint(L.T(
                "※ 브라우저가 claude.ai 에 다른 계정으로 로그인돼 있으면 그 계정으로 승인됩니다.\n   인증 화면에서 계정을 확인하세요.",
                "Note: if your browser is signed in to claude.ai with another account,\n   check the account on the approval page.")));

            t.Controls.Add(Heading(L.T("2. 인증 코드", "2. Authentication code")));
            status.Text = L.T("[연결하기]를 누르면 브라우저에서 Claude 인증 화면이 열립니다.",
                              "Press [Connect] to open the Claude sign-in page in your browser.");
            t.Controls.Add(status);
            openLink.Text = L.T("브라우저가 열리지 않았다면 여기를 누르세요", "Browser didn't open? Click here");
            openLink.LinkClicked += delegate { if (session != null && session.AuthUrl != null) Process.Start(session.AuthUrl); };
            t.Controls.Add(openLink);
            t.Controls.Add(codeBox);

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            cancel.Text = L.T("취소", "Cancel");
            finish.Text = L.T("완료", "Finish");
            finish.Click += OnFinish;
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(finish);
            t.Controls.Add(buttons);

            Controls.Add(t);
            AcceptButton = connect;
            CancelButton = cancel;

            codeBox.TextChanged += delegate { finish.Enabled = codeBox.Enabled && codeBox.Text.Trim().Length > 0; };
            Activated += delegate { TryPasteCode(); };
            FormClosed += delegate { EndSession(); };
        }

        static Label Heading(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = Ui.Bold, Margin = new Padding(0, 10, 0, 2) };
        }

        static Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 4, 0, 2) };
        }

        void OnConnect(object sender, EventArgs e)
        {
            string name = nameBox.Text.Trim();
            if (Store.SafeName(name).Length == 0)
            {
                MessageBox.Show(this, L.T("계정 이름을 입력하세요.", "Enter an account name."), Text);
                nameBox.Focus();
                return;
            }
            if (Store.NameTaken(name, null) &&
                MessageBox.Show(this, L.T("같은 이름이 이미 있습니다. 덮어쓸까요?", "That name already exists. Replace it?"),
                    Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            EndSession();
            session = new LoginSession();
            session.UrlFound += delegate { OnUi(delegate { openLink.Visible = true; }); };
            session.Exited += delegate { OnUi(OnLoginExited); };
            try
            {
                Store.SyncActive();
                session.Start(emailBox.Text);
            }
            catch (Exception ex)
            {
                EndSession();
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            nameBox.Enabled = emailBox.Enabled = connect.Enabled = false;
            codeBox.Enabled = true;
            codeBox.Text = "";
            AcceptButton = finish;
            status.Text = L.T(
                "브라우저에서 승인하면 화면에 코드가 나옵니다.\n그 코드를 복사해 아래 칸에 붙여넣고 [완료]를 누르세요.",
                "Approve in the browser. A code will be shown.\nCopy it, paste it below and press [Finish].");
            codeBox.Focus();
        }

        // Auth codes look like "xxxx#yyyy" with no spaces. Fill it in when the
        // user comes back from the browser with it on the clipboard.
        void TryPasteCode()
        {
            if (!codeBox.Enabled || codeBox.Text.Length > 0) return;
            try
            {
                string clip = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                if (clip.Length >= 20 && clip.IndexOf('#') > 0 && clip.IndexOf(' ') < 0 && !clip.StartsWith("http"))
                    codeBox.Text = clip;
            }
            catch (Exception) { }
        }

        void OnFinish(object sender, EventArgs e)
        {
            if (session == null) return;
            finish.Enabled = codeBox.Enabled = false;
            status.Text = L.T("확인 중…", "Verifying…");
            try { session.SubmitCode(codeBox.Text); }
            catch (Exception ex) { Fail(ex.Message); }
        }

        void OnUi(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch (InvalidOperationException) { }
        }

        void OnLoginExited()
        {
            if (session == null || IsDisposed) return;
            bool ok = session.Succeeded;
            string acc, oauth;
            session.ReadResult(out acc, out oauth);
            if (!ok || oauth == null)
            {
                Fail(session.LastMessage);
                return;
            }
            try
            {
                Store.Save(nameBox.Text.Trim(), acc, oauth);
                SavedName = Store.SafeName(nameBox.Text);
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                return;
            }
            EndSession();
            DialogResult = DialogResult.OK;
            Close();
        }

        void Fail(string message)
        {
            EndSession();
            status.Text = L.T("인증에 실패했습니다: ", "Sign-in failed: ") + message + "\n" +
                          L.T("[연결하기]를 다시 눌러 처음부터 하세요.", "Press [Connect] to try again.");
            nameBox.Enabled = emailBox.Enabled = connect.Enabled = true;
            codeBox.Enabled = finish.Enabled = false;
            openLink.Visible = false;
            AcceptButton = connect;
        }

        void EndSession()
        {
            if (session == null) return;
            var s = session;
            session = null;
            s.Dispose();
        }
    }
}
