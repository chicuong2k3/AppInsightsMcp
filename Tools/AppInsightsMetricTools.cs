using System.ComponentModel;
using AppInsightsMcp.Services;
using ModelContextProtocol.Server;

namespace AppInsightsMcp.Tools;

/// <summary>
/// Tools for querying Application Insights metrics (CPU, memory, requests/sec, response time, failures).
/// </summary>
internal class AppInsightsMetricTools(AppInsightsService service)
{
    [McpServerTool(Name = "get_metrics")]
    [Description("Queries Azure Monitor metrics for the Application Insights resource. " +
                 "Available metrics: requests/count, requests/duration, requests/failed, " +
                 "performanceCounters/processCpuPercentage, performanceCounters/processMemoryPercentage, " +
                 "exceptions/count, dependencies/duration, dependencies/failed.")]
    public async Task<string> GetMetrics(
        [Description("Comma-separated metric names (e.g. 'requests/count,requests/duration,exceptions/count')")] string metrics,
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        [Description("Aggregation type: avg, min, max, sum, count (default avg)")] string aggregation = "avg",
        CancellationToken ct = default)
    {
        var metricNames = metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return await service.QueryMultiMetricsAsync(metricNames, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_cpu_metrics")]
    [Description("Returns CPU usage percentage for the Application Insights resource over the time range.")]
    public async Task<string> GetCpuMetrics(
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        return await service.QueryMultiMetricsAsync(
            ["performanceCounters/processCpuPercentage"],
            TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_memory_metrics")]
    [Description("Returns memory usage for the Application Insights resource over the time range.")]
    public async Task<string> GetMemoryMetrics(
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        return await service.QueryMultiMetricsAsync(
            ["performanceCounters/processMemoryPercentage"],
            TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_request_metrics")]
    [Description("Returns request rate, duration, and failure count metrics in a single query.")]
    public async Task<string> GetRequestMetrics(
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        return await service.QueryMultiMetricsAsync(
            ["requests/count", "requests/duration", "requests/failed"],
            TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }
}
