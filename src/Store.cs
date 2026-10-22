using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ClaudeAccountSwitch
{
    class Profile
    {
        public string Name;
        public string FilePath;
        public string AccountRaw;   // ~/.claude.json  -> oauthAccount
        public string OauthRaw;     // ~/.claude/.credentials.json -> claudeAiOauth
        public string SavedAt;

        public string Email { get { return Json.GetString(AccountRaw, "emailAddress") ?? ""; } }
        public string Org { get { return Json.GetString(AccountRaw, "organizationName") ?? ""; } }
        public string Plan { get { return Json.GetString(OauthRaw, "subscriptionType") ?? ""; } }
        public string AccountUuid { get { return Json.GetString(AccountRaw, "accountUuid"); } }
        public string OrgUuid { get { return Json.GetString(AccountRaw, "organizationUuid"); } }

        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (Email.Length > 0) parts.Add(Email);
                if (Plan.Length > 0) parts.Add(char.ToUpper(Plan[0]) + Plan.Substring(1));
                if (Org.Length > 0) parts.Add(Org);
                return string.Join(" · ", parts.ToArray());
            }
        }
    }

    // Reads and swaps the two pieces of Claude Code state that identify the
    // signed-in account. Everything else (MCP tokens, settings) is untouched.
    static class Store
    {
        static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static string ConfigDir
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
                if (!string.IsNullOrEmpty(env)) return env;
                return Path.Combine(Home, ".claude");
            }
        }

        static string Home { get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } }

        public static string CredentialsPath { get { return Path.Combine(ConfigDir, ".credentials.json"); } }

        public static string GlobalConfigPath
        {
            get
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")))
                    return Path.Combine(ConfigDir, ".claude.json");
                return Path.Combine(Home, ".claude.json");
            }
        }

        public static string DataDir { get { return Path.Combine(ConfigDir, "account-switch"); } }
        static string ProfilesDir { get { return Path.Combine(DataDir, "profiles"); } }
        static string BackupDir { get { return Path.Combine(DataDir, "backup"); } }

        static string Read(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        // Write to a temp file and swap it in, so a crash never leaves half a file.
        static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".switch-tmp";
            File.WriteAllText(tmp, content, Utf8);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(tmp, path, null);
                    else File.Move(tmp, path);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 4) throw;
                    Thread.Sleep(150);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt >= 4) throw;
                    Thread.Sleep(150);
                }
            }
        }

        public static string SafeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
                sb.Append(Path.GetInvalidFileNameChars().Contains(c) || char.IsWhiteSpace(c) ? '_' : c);
            return sb.ToString();
        }

        // ---- current sign-in -------------------------------------------------

        public static void ReadCurrent(out string accountRaw, out string oauthRaw)
        {
            ReadFrom(CredentialsPath, GlobalConfigPath, out accountRaw, out oauthRaw);
        }

        public static void ReadFrom(string credPath, string confPath, out string accountRaw, out string oauthRaw)
        {
            oauthRaw = Json.Get(Read(credPath), "claudeAiOauth");
            accountRaw = Json.Get(Read(confPath), "oauthAccount");
        }

        static bool SameAccount(string a, string b)
        {
            if (a == null || b == null) return false;
            string ua = Json.GetString(a, "accountUuid"), ub = Json.GetString(b, "accountUuid");
            if (ua != null && ub != null)
                return ua == ub && Json.GetString(a, "organizationUuid") == Json.GetString(b, "organizationUuid");
            string ea = Json.GetString(a, "emailAddress"), eb = Json.GetString(b, "emailAddress");
            return ea != null && ea == eb;
        }

        // ---- profiles --------------------------------------------------------

        public static List<Profile> Load()
        {
            var list = new List<Profile>();
            if (!Directory.Exists(ProfilesDir)) return list;
            foreach (string f in Directory.GetFiles(ProfilesDir, "*.json").OrderBy(x => x, StringComparer.CurrentCulture))
            {
                try
                {
                    string text = Read(f);
                    var p = new Profile
                    {
                        Name = Path.GetFileNameWithoutExtension(f),
                        FilePath = f,
                        AccountRaw = Json.Get(text, "account"),
                        OauthRaw = Json.Get(text, "oauth"),
                        SavedAt = Json.GetString(text, "savedAt"),
                    };
                    if (p.OauthRaw != null) list.Add(p);
                }
                catch (FormatException) { /* skip a broken file rather than failing the whole list */ }
            }
            return list;
        }

        public static Profile Active(List<Profile> list)
        {
            string acc, oauth;
            ReadCurrent(out acc, out oauth);
            if (acc == null) return null;
            return list.FirstOrDefault(p => SameAccount(p.AccountRaw, acc));
        }

        static void WriteProfile(string path, string accountRaw, string oauthRaw, string savedAt)
        {
            string text = "{\n  \"account\": " + (accountRaw ?? "null") +
                          ",\n  \"oauth\": " + oauthRaw +
                          ",\n  \"savedAt\": " + Json.Quote(savedAt ?? Now()) +
                          ",\n  \"syncedAt\": " + Json.Quote(Now()) + "\n}\n";
            Write(path, text);
        }

        static string Now() { return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); }

        // Save an account under a name. If the same account was saved under
        // another name, that entry is replaced.
        public static void Save(string name, string accountRaw, string oauthRaw)
        {
            string safe = SafeName(name);
            if (safe.Length == 0) throw new ArgumentException("name");
            Directory.CreateDirectory(ProfilesDir);
            foreach (var p in Load())
                if (p.Name != safe && SameAccount(p.AccountRaw, accountRaw)) File.Delete(p.FilePath);
            WriteProfile(Path.Combine(ProfilesDir, safe + ".json"), accountRaw, oauthRaw, null);
        }

        public static bool NameTaken(string name, string exceptAccountRaw)
        {
            string path = Path.Combine(ProfilesDir, SafeName(name) + ".json");
            if (!File.Exists(path)) return false;
            if (exceptAccountRaw == null) return true;
            var p = Load().FirstOrDefault(x => x.FilePath == path);
            return p == null || !SameAccount(p.AccountRaw, exceptAccountRaw);
        }

        // Tokens rotate while Claude Code runs. Copy the live tokens back into
        // the active profile so switching back later never uses a dead token.
        public static void SyncActive()
        {
            string acc, oauth;
            ReadCurrent(out acc, out oauth);
            if (acc == null || oauth == null) return;
            var p = Active(Load());
            if (p == null || p.OauthRaw == oauth) return;
            WriteProfile(p.FilePath, acc, oauth, p.SavedAt);
        }

        public static void SwitchTo(Profile target)
        {
            SyncActive();
            Backup();

            string cred = Read(CredentialsPath) ?? "{}";
            Write(CredentialsPath, Json.Set(cred, "claudeAiOauth", target.OauthRaw));

            string conf = Read(GlobalConfigPath) ?? "{}";
            if (target.AccountRaw != null)
                Write(GlobalConfigPath, Json.Set(conf, "oauthAccount", target.AccountRaw));
        }

        public static void Remove(Profile p)
        {
            File.Delete(p.FilePath);
        }

        public static void Rename(Profile p, string newName)
        {
            string safe = SafeName(newName);
            if (safe.Length == 0 || safe == p.Name) return;
            File.Move(p.FilePath, Path.Combine(ProfilesDir, safe + ".json"));
        }

        static void Backup()
        {
            Directory.CreateDirectory(BackupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            if (File.Exists(CredentialsPath)) File.Copy(CredentialsPath, Path.Combine(BackupDir, "credentials-" + stamp + ".json"));
            if (File.Exists(GlobalConfigPath)) File.Copy(GlobalConfigPath, Path.Combine(BackupDir, "claude-" + stamp + ".json"));
            // keep the 10 most recent switches
            foreach (string prefix in new[] { "credentials-", "claude-" })
            {
                var old = Directory.GetFiles(BackupDir, prefix + "*.json").OrderByDescending(x => x).Skip(10);
                foreach (string f in old) File.Delete(f);
            }
        }
    }
}
