using System.Windows.Forms;
using JKMon.Core.Settings;

namespace JKMon.App;

/// <summary>Asks for a preset name. WinForms has no input dialog, and a themed one keeps the window consistent.</summary>
internal sealed class NamePrompt : Form
{
    private readonly TextBox _entry = new() { BorderStyle = BorderStyle.FixedSingle };

    private NamePrompt(ThemeChrome chrome, string title, string prompt, string initial)
    {
        var surface = Colour(chrome.Surface);
        var ink = Colour(chrome.Ink);

        Text = title;
        BackColor = surface;
        ForeColor = ink;
        Font = new Font(chrome.BodyFont, chrome.BodyFontSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;

        var caption = new Label
        {
            Text = prompt,
            AutoSize = true,
            ForeColor = ink,
            Location = new Point(16, 16)
        };

        _entry.MaxLength = ThemePreset.MaxNameLength;
        _entry.Text = initial;
        _entry.BackColor = Colour(chrome.Field);
        _entry.ForeColor = ink;
        _entry.Location = new Point(16, caption.Bottom + 28);
        _entry.Width = Font.Height * 18;

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        foreach (var button in (Button[])[ok, cancel])
        {
            button.FlatStyle = FlatStyle.Flat;
            button.ForeColor = ink;
            button.BackColor = Colour(chrome.Field);
            button.FlatAppearance.BorderColor = Colour(chrome.Hairline);
            button.Padding = new Padding(12, 4, 12, 4);
        }

        ok.Location = new Point(_entry.Right - ok.PreferredSize.Width, _entry.Bottom + 20);
        cancel.Location = new Point(ok.Left - cancel.PreferredSize.Width - 8, ok.Top);

        Controls.AddRange([caption, _entry, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        ClientSize = new Size(_entry.Right + 16, ok.Bottom + 16);

        // Saving an empty name would create a slot nothing can select again.
        _entry.TextChanged += (_, _) => ok.Enabled = ThemePreset.CleanName(_entry.Text).Length > 0;
        ok.Enabled = ThemePreset.CleanName(_entry.Text).Length > 0;
    }

    private static Color Colour(string value)
    {
        var parsed = HexColor.ParseOrDefault(value, new HexColor(255, 0, 0, 0));
        return Color.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B);
    }

    /// <summary>Returns the cleaned name, or null when the user backed out.</summary>
    internal static string? Ask(IWin32Window owner, ThemeChrome chrome, string title, string prompt, string initial)
    {
        using var dialog = new NamePrompt(chrome, title, prompt, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? ThemePreset.CleanName(dialog._entry.Text)
            : null;
    }
}
