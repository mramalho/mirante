using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

static string? TryReadTrimmed(string path)
{
    try
    {
        if (File.Exists(path))
            return File.ReadAllText(path).Trim();
    }
    catch
    {
    }

    return null;
}

static (ulong? limitBytes, bool unlimited, string raw) InterpretCgroupMemory(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return (null, false, "");

    raw = raw.Trim();
    if (raw == "max")
        return (null, true, raw);

    if (ulong.TryParse(raw, out var n))
    {
        if (n > 9_000_000_000_000_000_000UL)
            return (null, true, raw);

        return (n, false, raw);
    }

    return (null, false, raw);
}

static object? InterpretCpuCgroup2(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return null;

    var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2)
        return new { raw };

    if (parts[0] == "max")
        return new { raw, unlimited = true };

    if (long.TryParse(parts[0], out var quota) && long.TryParse(parts[1], out var period) && period > 0)
        return new { raw, quotaMicros = quota, periodMicros = period, cpus = Math.Round((double)quota / period, 4) };

    return new { raw };
}

static object? InterpretCpuCgroup1(string? quotaRaw, string? periodRaw)
{
    if (!long.TryParse(quotaRaw, out var quota))
        return null;

    if (!long.TryParse(periodRaw, out var period) || period <= 0)
        period = 100_000;

    if (quota < 0)
        return new { rawQuota = quotaRaw, rawPeriod = periodRaw, unlimited = true };

    return new { rawQuota = quotaRaw, rawPeriod = periodRaw, cpus = Math.Round((double)quota / period, 4) };
}

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () =>
    Results.Json(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/info", (IWebHostEnvironment env) =>
    {
        var entry = Assembly.GetEntryAssembly();
        var name = entry?.GetName();

        var gcInfo = GC.GetGCMemoryInfo();
        var proc = Process.GetCurrentProcess();
        proc.Refresh();

        var memCgroupV2 = TryReadTrimmed("/sys/fs/cgroup/memory.max");
        var memCgroupV1 = memCgroupV2 is null
            ? TryReadTrimmed("/sys/fs/cgroup/memory/memory.limit_in_bytes")
            : null;
        var memRaw = memCgroupV2 ?? memCgroupV1 ?? "";
        var memInterpreted = InterpretCgroupMemory(string.IsNullOrEmpty(memRaw) ? null : memRaw);

        var cpuCgroupV2 = TryReadTrimmed("/sys/fs/cgroup/cpu.max");
        var cpuQuotaV1 = cpuCgroupV2 is null ? TryReadTrimmed("/sys/fs/cgroup/cpu/cpu.cfs_quota_us") : null;
        var cpuPeriodV1 = cpuQuotaV1 is null ? null : TryReadTrimmed("/sys/fs/cgroup/cpu/cpu.cfs_period_us");

        object? disk = null;
        try
        {
            var d = new DriveInfo("/");
            if (d.IsReady)
            {
                disk = new
                {
                    mount = d.RootDirectory.FullName,
                    driveFormat = d.DriveFormat,
                    totalBytes = d.TotalSize,
                    availableBytes = d.AvailableFreeSpace,
                    usedBytes = d.TotalSize - d.AvailableFreeSpace,
                };
            }
        }
        catch
        {
        }

        var loadAvg = OperatingSystem.IsLinux() ? TryReadTrimmed("/proc/loadavg") : null;

        return Results.Json(new
        {
            timestamp = DateTime.UtcNow,
            environment = env.EnvironmentName,
            application = new
            {
                name = env.ApplicationName,
                assemblyName = name?.Name,
                assemblyVersion = name?.Version?.ToString(),
                contentRootPath = env.ContentRootPath,
            },
            runtime = new
            {
                framework = RuntimeInformation.FrameworkDescription,
                clrVersion = Environment.Version.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                osDescription = RuntimeInformation.OSDescription,
            },
            host = new
            {
                machineName = Environment.MachineName,
                processorCount = Environment.ProcessorCount,
                userName = Environment.UserName,
                currentDirectory = Environment.CurrentDirectory,
                workingSetBytes = Environment.WorkingSet,
            },
            containerHints = new
            {
                hostnameEnv = Environment.GetEnvironmentVariable("HOSTNAME"),
                dotnetRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                aspnetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS"),
                aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            },
            infra = new
            {
                memory = new
                {
                    gc = new
                    {
                        gcInfo.TotalAvailableMemoryBytes,
                        gcInfo.HeapSizeBytes,
                        gcInfo.MemoryLoadBytes,
                        gcInfo.TotalCommittedBytes,
                        gcInfo.HighMemoryLoadThresholdBytes,
                    },
                    process = new
                    {
                        proc.WorkingSet64,
                        proc.PrivateMemorySize64,
                        proc.VirtualMemorySize64,
                        proc.PeakWorkingSet64,
                    },
                    cgroup = new
                    {
                        source = memCgroupV2 is not null ? "cgroup2" : memCgroupV1 is not null ? "cgroup1" : null,
                        raw = string.IsNullOrEmpty(memRaw) ? null : memRaw,
                        limitBytes = memInterpreted.limitBytes,
                        unlimited = memInterpreted.unlimited,
                        limitMb = memInterpreted.limitBytes is { } b
                            ? Math.Round(b / 1024.0 / 1024.0, 2)
                            : (double?)null,
                    },
                },
                cpu = new
                {
                    logicalProcessors = Environment.ProcessorCount,
                    processThreads = proc.Threads.Count,
                    cgroup = cpuCgroupV2 is not null
                        ? new
                        {
                            source = "cgroup2",
                            parsed = InterpretCpuCgroup2(cpuCgroupV2),
                            raw = (string?)cpuCgroupV2,
                            rawQuota = (string?)null,
                            rawPeriod = (string?)null,
                        }
                        : cpuQuotaV1 is not null
                            ? new
                            {
                                source = "cgroup1",
                                parsed = InterpretCpuCgroup1(cpuQuotaV1, cpuPeriodV1),
                                raw = (string?)null,
                                rawQuota = cpuQuotaV1,
                                rawPeriod = cpuPeriodV1,
                            }
                            : null,
                },
                disk,
                loadAverage = loadAvg is null
                    ? null
                    : new
                    {
                        raw = loadAvg,
                    },
            },
        });
    });

app.MapGet("/{**catchAll}", () => Results.Json(new { value = "ok" }));

app.Run();
