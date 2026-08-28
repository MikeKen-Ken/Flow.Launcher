using System;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class WebDavSyncSettings
    {
        public string Url { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool SyncSettings { get; set; } = true;

        public bool SyncPlugins { get; set; } = true;

        public DateTime? LastSuccessfulSyncUtc { get; set; }

        public string LastResult { get; set; } = string.Empty;
    }
}
