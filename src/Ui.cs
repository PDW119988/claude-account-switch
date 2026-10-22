using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeAccountSwitch
{
    // UI language follows Windows: Korean on Korean Windows, English otherwise.
    static class L
    {
        public static readonly bool Ko = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";
        public static string T(string ko, string en) { return Ko ? ko : en; }
    }

    static class Ui
    {
        public static readonly Font Font = new Font(L.Ko ? "Malgun Gothic" : "Segoe UI", 9f);
        public static readonly Font Bold = new Font(Font, FontStyle.Bold);

        static readonly Color[] Palette =
        {
            Color.FromArgb(0xD9, 0x6C, 0x3F), // orange
            Color.FromArgb(0x2F, 0x6F, 0xDE), // blue
            Color.FromArgb(0x1F, 0x9D, 0x6A), // green
            Color.FromArgb(0x8E, 0x4F, 0xD6), // purple
            Color.FromArgb(0xC9, 0x3C, 0x5B), // red
            Color.FromArgb(0x2A, 0x9D, 0xB0), // teal
        };

        public static Color ColorFor(int index)
        {
            return index < 0 ? Color.Gray : Palette[index % Palette.Length];
        }

        public static Bitmap Badge(Color color, string text, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(color)) g.FillEllipse(b, 0, 0, size - 1, size - 1);
                string letter = string.IsNullOrEmpty(text) ? "?" : text.Substring(0, 1).ToUpper();
                using (var f = new Font(Font.FontFamily, size * 0.45f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(letter, f, Brushes.White, new RectangleF(0, 1, size, size), sf);
            }
            return bmp;
        }

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        static readonly Dictionary<string, Icon> cache = new Dictionary<string, Icon>();

        public static Icon TrayIcon(int index, string name)
        {
            string key = index + "|" + name;
            Icon icon;
            if (cache.TryGetValue(key, out icon)) return icon;
            using (var bmp = Badge(ColorFor(index), name, 32))
            {
                IntPtr h = bmp.GetHicon();
                using (var tmp = Icon.FromHandle(h)) icon = (Icon)tmp.Clone();
                DestroyIcon(h);
            }
            cache[key] = icon;
            return icon;
        }

        public static string Prompt(IWin32Window owner, string title, string label, string initial)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.Font = Font;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.AutoSize = true;
                f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                f.Padding = new Padding(12);

                var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
                var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
                var box = new TextBox { Text = initial ?? "", Width = 280 };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
                var cancel = new Button { Text = L.T("취소", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
                var ok = new Button { Text = L.T("확인", "OK"), DialogResult = DialogResult.OK, AutoSize = true };
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                table.Controls.Add(lbl);
                table.Controls.Add(box);
                table.Controls.Add(buttons);
                f.Controls.Add(table);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
            }
        }
    }
}
