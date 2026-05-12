using System;
using System.Drawing;
using System.Windows.Forms;

namespace RetroModemBridge;

public sealed class SshPasswordPrompt : Form
{
    private readonly TextBox _passwordBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public string Password => _passwordBox.Text;

    private SshPasswordPrompt(string target)
    {
        Text = "SSH Password";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 155);

        var label = new Label
        {
            Left = 16,
            Top = 16,
            Width = 388,
            Height = 40,
            Text = "Enter SSH password for:\r\n" + target
        };

        _passwordBox = new TextBox
        {
            Left = 16,
            Top = 65,
            Width = 388,
            UseSystemPasswordChar = true
        };

        _okButton = new Button
        {
            Text = "Connect",
            Left = 230,
            Top = 105,
            Width = 82,
            DialogResult = DialogResult.OK
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            Left = 322,
            Top = 105,
            Width = 82,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        Controls.Add(label);
        Controls.Add(_passwordBox);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
    }

    public static string? Ask(IWin32Window owner, string target)
    {
        using var prompt = new SshPasswordPrompt(target);
        var result = prompt.ShowDialog(owner);
        return result == DialogResult.OK ? prompt.Password : null;
    }
}
