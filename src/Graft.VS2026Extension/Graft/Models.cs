using System;
using System.Collections.Generic;

namespace Graft.VS2026Extension.Graft
{
    internal sealed class StackInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Trunk { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<BranchInfo> Branches { get; set; } = new List<BranchInfo>();
        public bool IsActive { get; set; }
    }

    internal sealed class BranchInfo
    {
        public string Name { get; set; } = string.Empty;
        public ulong? PrNumber { get; set; }
        public string? PrUrl { get; set; }
        public string? PrState { get; set; }
    }
}
