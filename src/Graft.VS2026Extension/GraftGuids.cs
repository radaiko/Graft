using System;

namespace Graft.VS2026Extension
{
    internal static class GraftGuids
    {
        // Package
        public const string PackageGuidString = "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d";
        public static readonly Guid PackageGuid = new Guid(PackageGuidString);

        // Command set
        public const string CommandSetGuidString = "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e";
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);

        // Tool window
        public const string StackExplorerToolWindowGuidString = "c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f";
        public static readonly Guid StackExplorerToolWindowGuid = new Guid(StackExplorerToolWindowGuidString);

        // Output pane
        public const string OutputPaneGuidString = "d4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f80";
        public static readonly Guid OutputPaneGuid = new Guid(OutputPaneGuidString);
    }
}
