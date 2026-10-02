using EasyAlumni.Core.Entities;

namespace EasyAlumni.Web.ViewModels
{
    public class ErrorLogsIndexViewModel
    {
        public List<AppErrorLog> Logs { get; set; } = new();
        public int TotalCount { get; set; }
        public int FilteredCount { get; set; }
        public int PendingSyncCount { get; set; }
        public int CriticalCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }

        public string? Query { get; set; }
        public string? Type { get; set; }
        public string? SyncStatus { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages => (int)Math.Ceiling((double)FilteredCount / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }
}
