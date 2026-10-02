using EasyAlumni.Core.Entities;
using EasyAlumni.Core.Interfaces;
using EasyAlumni.Infrastructure.Data;
using EasyAlumni.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyAlumni.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class ErrorLogsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogsWebsiteService _logsService;

        public ErrorLogsController(ApplicationDbContext context, ILogsWebsiteService logsService)
        {
            _context = context;
            _logsService = logsService;
        }

        // GET: /ErrorLogs
        public async Task<IActionResult> Index(string? q, string? type, string? sync, int page = 1)
        {
            if (page < 1) page = 1;
            int pageSize = 25;

            var baseQuery = _context.AppErrorLogs.AsNoTracking();

            // Total counts for metric counters
            var totalCount = await baseQuery.CountAsync();
            var pendingSyncCount = await baseQuery.CountAsync(l => !l.IsSyncedToRemote);
            var criticalCount = await baseQuery.CountAsync(l => l.Type == "critical");
            var errorCount = await baseQuery.CountAsync(l => l.Type == "error");
            var warningCount = await baseQuery.CountAsync(l => l.Type == "warning" || l.Type == "warn");

            // Filters
            var filteredQuery = baseQuery;

            if (!string.IsNullOrWhiteSpace(q))
            {
                var cleanQ = q.Trim().ToLower();
                filteredQuery = filteredQuery.Where(l =>
                    l.Title.ToLower().Contains(cleanQ) ||
                    l.Message.ToLower().Contains(cleanQ) ||
                    l.Controller.ToLower().Contains(cleanQ) ||
                    (l.RequestPath != null && l.RequestPath.ToLower().Contains(cleanQ)) ||
                    (l.UserIdentifier != null && l.UserIdentifier.ToLower().Contains(cleanQ)) ||
                    (l.ClientIp != null && l.ClientIp.ToLower().Contains(cleanQ)));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "all")
            {
                var cleanType = type.Trim().ToLower();
                filteredQuery = filteredQuery.Where(l => l.Type == cleanType);
            }

            if (!string.IsNullOrWhiteSpace(sync) && sync != "all")
            {
                if (sync.Equals("synced", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(l => l.IsSyncedToRemote);
                }
                else if (sync.Equals("pending", StringComparison.OrdinalIgnoreCase))
                {
                    filteredQuery = filteredQuery.Where(l => !l.IsSyncedToRemote);
                }
            }

            var filteredCount = await filteredQuery.CountAsync();

            var logs = await filteredQuery
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new ErrorLogsIndexViewModel
            {
                Logs = logs,
                TotalCount = totalCount,
                FilteredCount = filteredCount,
                PendingSyncCount = pendingSyncCount,
                CriticalCount = criticalCount,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                Query = q,
                Type = type,
                SyncStatus = sync,
                CurrentPage = page,
                PageSize = pageSize
            };

            return View(vm);
        }

        // GET: /ErrorLogs/GetDetails/5
        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            var log = await _context.AppErrorLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            if (log == null)
            {
                return NotFound(new { error = "Log entry not found" });
            }

            return Json(new
            {
                id = log.Id,
                title = log.Title,
                type = log.Type,
                controller = log.Controller,
                message = log.Message,
                stackTrace = log.StackTrace,
                requestPath = log.RequestPath,
                requestMethod = log.RequestMethod,
                userIdentifier = log.UserIdentifier,
                clientIp = log.ClientIp,
                createdAt = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                isSynced = log.IsSyncedToRemote,
                remoteSyncError = log.RemoteSyncError,
                remoteSyncedAt = log.RemoteSyncedAt?.ToString("yyyy-MM-dd HH:mm:ss UTC")
            });
        }

        // POST: /ErrorLogs/RetrySync/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetrySync(int id)
        {
            var (success, message) = await _logsService.RetrySyncLogAsync(id);
            if (success)
            {
                TempData["Success"] = $"Log #{id} successfully synced to logs.website!";
            }
            else
            {
                TempData["Error"] = $"Sync failed for Log #{id}: {message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /ErrorLogs/SyncAllPending
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncAllPending()
        {
            var (successCount, failCount, message) = await _logsService.SyncAllPendingLogsAsync();
            if (successCount > 0 && failCount == 0)
            {
                TempData["Success"] = message;
            }
            else if (failCount > 0)
            {
                TempData["Warning"] = message;
            }
            else
            {
                TempData["Info"] = message;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /ErrorLogs/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var log = await _context.AppErrorLogs.FindAsync(id);
            if (log != null)
            {
                _context.AppErrorLogs.Remove(log);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Error Log #{id} deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /ErrorLogs/ClearLogs
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs(string filter)
        {
            int deletedCount = 0;
            var now = DateTime.UtcNow;

            if (filter == "older30")
            {
                var threshold = now.AddDays(-30);
                var logs = await _context.AppErrorLogs.Where(l => l.CreatedAt < threshold).ToListAsync();
                deletedCount = logs.Count;
                _context.AppErrorLogs.RemoveRange(logs);
            }
            else if (filter == "older7")
            {
                var threshold = now.AddDays(-7);
                var logs = await _context.AppErrorLogs.Where(l => l.CreatedAt < threshold).ToListAsync();
                deletedCount = logs.Count;
                _context.AppErrorLogs.RemoveRange(logs);
            }
            else if (filter == "synced")
            {
                var logs = await _context.AppErrorLogs.Where(l => l.IsSyncedToRemote).ToListAsync();
                deletedCount = logs.Count;
                _context.AppErrorLogs.RemoveRange(logs);
            }
            else if (filter == "all")
            {
                var logs = await _context.AppErrorLogs.ToListAsync();
                deletedCount = logs.Count;
                _context.AppErrorLogs.RemoveRange(logs);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{deletedCount} error log entries removed successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /ErrorLogs/TriggerTestError
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerTestError()
        {
            try
            {
                // Intentionally create a diagnostic test error
                throw new InvalidOperationException("Test diagnostic exception: Verifying local error log keeping and remote streaming pipeline.");
            }
            catch (Exception ex)
            {
                var log = await _logsService.RecordErrorAsync(
                    title: "Diagnostic Test Error",
                    type: "error",
                    controller: "ErrorLogsController",
                    message: ex.Message,
                    stackTrace: ex.ToString(),
                    requestPath: "/ErrorLogs/TriggerTestError",
                    requestMethod: "POST",
                    userIdentifier: User.Identity?.Name ?? "Admin",
                    clientIp: HttpContext.Connection.RemoteIpAddress?.ToString());

                TempData["Success"] = $"Test Error Log #{log.Id} created! Local keep: OK, Remote sync: {(log.IsSyncedToRemote ? "Synced" : log.RemoteSyncError ?? "Pending")}.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
