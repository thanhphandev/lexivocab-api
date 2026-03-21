using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Diagnostics;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Asp.Versioning;

namespace LexiVocab.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DiagnosticsController : BaseApiController
{
    private readonly AppDbContext _dbContext;

    public DiagnosticsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// System health and environment diagnostics.
    /// </summary>
    /// <remarks>
    /// Provides deep system information including OS, process memory, and database connectivity.
    /// **Admin only.**
    /// </remarks>
    /// <response code="200">Returns system health data.</response>
    [HttpGet("system-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        
        // Database connectivity check
        bool dbCanConnect = false;
        string? dbVersion = "Unknown";
        try
        {
            dbCanConnect = await _dbContext.Database.CanConnectAsync();
            if (dbCanConnect)
            {
                using var conn = _dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }
                dbVersion = conn.ServerVersion;
            }
        }
        catch (Exception ex)
        {
            dbVersion = $"Error: {ex.Message}";
        }

        // Memory info (approximated)
        var memStatus = new GCMemoryInfo();
        try {
            memStatus = GC.GetGCMemoryInfo();
        } catch {}

        return Ok(new
        {
            success = true,
            data = new
            {
                environment = new
                {
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.OSArchitecture.ToString(),
                    runtime = RuntimeInformation.FrameworkDescription,
                    machineName = Environment.MachineName,
                    processorCount = Environment.ProcessorCount,
                    workingDirectory = Environment.CurrentDirectory
                },
                process = new
                {
                    workingSet = process.WorkingSet64 / 1024 / 1024 + " MB",
                    privateMemory = process.PrivateMemorySize64 / 1024 / 1024 + " MB",
                    peakWorkingSet = process.PeakWorkingSet64 / 1024 / 1024 + " MB",
                    startTime = process.StartTime,
                    upTime = (DateTime.Now - process.StartTime).ToString(@"dd\.hh\:mm\:ss"),
                    threads = process.Threads.Count,
                    handleCount = process.HandleCount,
                    is64Bit = Environment.Is64BitProcess
                },
                database = new
                {
                    canConnect = dbCanConnect,
                    version = dbVersion,
                    provider = _dbContext.Database.ProviderName,
                    server = _dbContext.Database.GetDbConnection().DataSource
                },
                runtime = new {
                    heapSize = memStatus.HeapSizeBytes / 1024 / 1024 + " MB",
                    totalAvailableMemory = memStatus.TotalAvailableMemoryBytes / 1024 / 1024 + " MB",
                    highMemoryLoadThreshold = memStatus.HighMemoryLoadThresholdBytes / 1024 / 1024 + " MB",
                    memoryLoad = memStatus.MemoryLoadBytes / 1024 / 1024 + " MB"
                },
                timestamp = DateTime.UtcNow
            }
        });
    }
}
