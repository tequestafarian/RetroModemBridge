using System;
using System.Drawing;
using System.Windows.Forms;

namespace RetroModemBridge.SshProxy
{
    public sealed class SshPasswordPrompt : Form
    {
        private readonly TextBox _txtPassword;
        private readonly Label _lblTitle;

        public string Password => _txtPassword.Text;

        public SshPasswordPrompt(string host, string username)
        {
            Text = "SSH Login";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 160);

            _lblTitle = new Label
            {
                AutoSize = false,
                Left = 16,
                Top = 16,
                Width = 388,
                Height = 42,
                Text = $"Enter SSH password for {username}@{host}",
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
            };

            _txtPassword = new TextBox
            {
                Left = 16,
                Top = 68,
                Width = 388,
                UseSystemPasswordChar = true
            };

            var btnOk = new Button
            {
                Text = "Connect",
                DialogResult = DialogResult.OK,
                Left = 228,
                Top = 112,
                Width = 84
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Left = 320,
                Top = 112,
                Width = 84
            };

            Controls.Add(_lblTitle);
            Controls.Add(_txtPassword);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
