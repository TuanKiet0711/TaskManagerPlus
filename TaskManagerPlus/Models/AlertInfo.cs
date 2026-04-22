using System;

namespace TaskManagerPlus.Models
{
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    public enum SuggestedAction
    {
        None,
        KillProcess,
        SuspendProcess,
        SetEcoMode,
        Ignore
    }

    public class AlertInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public int RelatedProcessId { get; set; }
        public string RelatedProcessName { get; set; }
        public SuggestedAction Action { get; set; }
    }
}
