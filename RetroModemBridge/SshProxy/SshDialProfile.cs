namespace RetroModemBridge.SshProxy
{
    public sealed class SshDialProfile
    {
        public string Alias { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Terminal { get; set; } = "ansi";

        // Do not save passwords in plain text.
        // First version should ask for the password at connect time.
        public bool PromptForPassword { get; set; } = true;

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Alias)
                ? $"{Username}@{Host}:{Port}"
                : $"{Alias}  {Username}@{Host}:{Port}";
        }
    }
}
