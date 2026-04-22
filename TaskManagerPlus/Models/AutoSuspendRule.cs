using System;
using System.Collections.Generic;

namespace TaskManagerPlus.Models
{
    public class AutoSuspendRule
    {
        public string ProcessName { get; set; }
        public int CpuThreshold { get; set; }
        public int RamThresholdMB { get; set; }
        public SuggestedAction Action { get; set; }
    }
}
