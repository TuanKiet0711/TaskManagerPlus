namespace TaskManagerPlus.Models
{
    public class ProcessResourceUsage
    {
        public int ProcessId { get; set; }
        public string DisplayName { get; set; }
        public double CpuPercent { get; set; }
        public long WorkingSetBytes { get; set; }
    }
}

