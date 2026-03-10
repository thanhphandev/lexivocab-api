using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using System.Diagnostics;
using LexiVocab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexiVocab.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DiagnosticsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DiagnosticsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Deep system diagnostics — intended for Admin usage only.
    /// Provides info about the environment, database, and process.
    /// </summary>
    [HttpGet("system-info")]
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
                dbVersion = conn.ServerVersion;
            }
        }
        catch (Exception ex)
        {
            dbVersion = $"Error: {ex.Message}";
        }

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
                    processorCount = Environment.ProcessorCount
                },
                process = new
                {
                    workingSet = process.WorkingSet64 / 1024 / 1024 + " MB",
                    privateMemory = process.PrivateMemorySize64 / 1024 / 1024 + " MB",
                    startTime = process.StartTime,
                    upTime = DateTime.Now - process.StartTime,
                    threads = process.Threads.Count
                },
                database = new
                {
                    canConnect = dbCanConnect,
                    version = dbVersion,
                    provider = _dbContext.Database.ProviderName
                },
                timestamp = DateTime.UtcNow
            }
        });
    }
}
