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
        [Description("Time range: '30m', '12h', '7d', or a bare number of minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var metricNames = metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return await service.QueryMultiMetricsAsync(metricNames, Kql.Range(timeRange), ct);
    }
}
