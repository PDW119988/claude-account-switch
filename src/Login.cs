using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudeAccountSwitch
{
    // Runs `claude auth login` inside a throwaway CLAUDE_CONFIG_DIR, so signing
    // in a new account never touches the account currently in use. When it
    // succeeds we copy the new tokens out and delete the temp folder.
    class LoginSession : IDisposable
    {
        Process proc;
        readonly string tempDir;
        readonly StringBuilder output = new StringBuilder();

        public string AuthUrl { get; private set; }
        public event EventHandler UrlFound;
        public event EventHandler Exited;

        public LoginSession()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "ClaudeAccountSwitch-" + Guid.NewGuid().ToString("N"));
        }

        public static string FindClaude()
        {
            var candidates = new System.Collections.Generic.List<string>();
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in path.Split(';'))
            {
                if (dir.Trim().Length == 0) continue;
                candidates.Add(Path.Combine(dir.Trim(), "claude.exe"));
                candidates.Add(Path.Combine(dir.Trim(), "claude.cmd"));
            }
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            candidates.Add(Path.Combine(home, @".local\bin\claude.exe"));
            candidates.Add(Path.Combine(appData, @"npm\claude.cmd"));
            foreach (string c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch (ArgumentException) { }
            }
            return null;
        }

        public void Start(string email)
        {
            string claude = FindClaude();
            if (claude == null) throw new FileNotFoundException(L.T(
                "claude 명령을 찾을 수 없습니다. Claude Code 가 설치돼 있는지 확인하세요.",
                "Could not find the claude command. Make sure Claude Code is installed."));

            Directory.CreateDirectory(tempDir);
            string args = "auth login";
            if (!string.IsNullOrWhiteSpace(email)) args += " --email \"" + email.Trim().Replace("\"", "") + "\"";

            var psi = new ProcessStartInfo();
            if (claude.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                psi.Arguments = "/d /s /c \"\"" + claude + "\" " + args + "\"";
            }
            else
            {
                psi.FileName = claude;
                psi.Arguments = args;
            }
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.EnvironmentVariables["CLAUDE_CONFIG_DIR"] = tempDir;

            proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += OnData;
            proc.ErrorDataReceived += OnData;
            proc.Exited += delegate { if (Exited != null) Exited(this, EventArgs.Empty); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }

        void OnData(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (output) output.AppendLine(e.Data);
            if (AuthUrl == null)
            {
                var m = Regex.Match(e.Data, @"https://\S+oauth/authorize\S*");
                if (m.Success)
                {
                    AuthUrl = m.Value;
                    if (UrlFound != null) UrlFound(this, EventArgs.Empty);
                }
            }
        }

        public void SubmitCode(string code)
        {
            proc.StandardInput.WriteLine(code.Trim());
            proc.StandardInput.Flush();
        }

        public bool HasExited
        {
            get
            {
                try { return proc == null || proc.HasExited; }
                catch (InvalidOperationException) { return true; }
            }
        }

        public bool Succeeded
        {
            get
            {
                if (proc == null) return false;
                try { proc.WaitForExit(); } catch (Exception) { } // also flushes async output
                return proc.HasExited && proc.ExitCode == 0;
            }
        }

        // Last meaningful line of output, for error messages.
        public string LastMessage
        {
            get
            {
                string text;
                lock (output) text = output.ToString();
                text = Regex.Replace(text, @"Paste code here if prompted\s*>", "\n");
                if (Regex.IsMatch(text, @"status code 4\d\d|invalid_grant", RegexOptions.IgnoreCase))
                    return L.T("코드가 맞지 않거나 만료됐습니다.", "The code is wrong or has expired.");
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string l = lines[i].Trim();
                    if (l.Length > 0 && !l.StartsWith("http") && !l.StartsWith("If the browser") && !l.StartsWith("Opening browser"))
                        return l.Length > 200 ? l.Substring(0, 200) : l;
                }
                return L.T("로그인이 끝나지 않았습니다.", "Sign-in did not finish.");
            }
        }

        public void ReadResult(out string accountRaw, out string oauthRaw)
        {
            Store.ReadFrom(Path.Combine(tempDir, ".credentials.json"), Path.Combine(tempDir, ".claude.json"),
                out accountRaw, out oauthRaw);
        }

        public void Dispose()
        {
            if (proc != null)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        // kill the whole tree (claude.cmd -> node, etc.)
                        var kill = Process.Start(new ProcessStartInfo("taskkill", "/T /F /PID " + proc.Id)
                        { CreateNoWindow = true, UseShellExecute = false });
                        if (kill != null) kill.WaitForExit(3000);
                    }
                }
                catch (Exception) { }
                proc.Dispose();
                proc = null;
            }
            // the temp folder holds live tokens: always remove it
            for (int i = 0; i < 5 && Directory.Exists(tempDir); i++)
            {
                try { Directory.Delete(tempDir, true); }
                catch (Exception) { System.Threading.Thread.Sleep(200); }
            }
        }
    }
}
