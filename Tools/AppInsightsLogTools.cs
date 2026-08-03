using System.ComponentModel;
using System.Text.Json;
using AppInsightsMcp.Services;
using ModelContextProtocol.Server;

namespace AppInsightsMcp.Tools;

/// <summary>
/// Tools for querying Application Insights logs (requests, exceptions, dependencies, AppTraces).
/// </summary>
internal class AppInsightsLogTools(AppInsightsService service)
{
    [McpServerTool(Name = "query_logs")]
    [Description("Executes an arbitrary KQL query against the Application Insights Log Analytics workspace. " +
                 "Use for custom queries on tables: AppRequests, AppExceptions, AppDependencies, AppTraces, AppCustomEvents, AppPageViews, AppAvailabilityResults.")]
    public async Task<string> QueryLogs(
        [Description("The KQL query to execute")] string kql,
        [Description("Time range in minutes (default 60 = 1 hour)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_exceptions")]
    [Description("Returns the most recent exceptions from Application Insights, ordered by TimeGenerated descending.")]
    public async Task<string> GetExceptions(
        [Description("Maximum number of exceptions to return (default 50)")] int top = 50,
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppExceptions
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, type=OuterMessage, Method, ProblemId, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_slow_requests")]
    [Description("Returns the slowest requests from Application Insights, ordered by duration descending.")]
    public async Task<string> GetSlowRequests(
        [Description("Maximum number of requests to return (default 20)")] int top = 20,
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppRequests
            | order by DurationMs desc
            | take {{top}}
            | project TimeGenerated, Name, DurationMs, ResultCode, Success, Url, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_dependencies")]
    [Description("Summarizes dependency calls by target, showing call count and average duration.")]
    public async Task<string> GetDependencies(
        [Description("Maximum number of dependency targets to return (default 30)")] int top = 30,
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppDependencies
            | summarize count=count(), avgDuration=avg(DurationMs) by Target, DependencyType
            | order by count desc
            | take {{top}}
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_availability")]
    [Description("Returns availability test results from Application Insights.")]
    public async Task<string> GetAvailability(
        [Description("Maximum number of results (default 50)")] int top = 50,
        [Description("Time range in minutes (default 1440 = 24 hours)")] int timeRangeMinutes = 1440,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppAvailabilityResults
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, Name, Success, DurationMs, Location, Message
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_failed_requests")]
    [Description("Returns failed requests from Application Insights for the specified time range.")]
    public async Task<string> GetFailedRequests(
        [Description("Maximum number of results (default 50)")] int top = 50,
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        [Description("Filter by specific HTTP status code prefix (e.g., '5' for 5xx)")] string? statusPrefix = null,
        CancellationToken ct = default)
    {
        var statusFilter = statusPrefix is not null
            ? $"| where ResultCode startswith \"{statusPrefix}\""
            : "| where Success == false";

        var kql = $$"""
            AppRequests
            {{statusFilter}}
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, Name, DurationMs, ResultCode, Url, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }

    [McpServerTool(Name = "get_request_summary")]
    [Description("Returns a summary of request activity: total count, success/failure rates, and duration percentiles.")]
    public async Task<string> GetRequestSummary(
        [Description("Time range in minutes (default 60)")] int timeRangeMinutes = 60,
        CancellationToken ct = default)
    {
        var kql = """
            AppRequests
            | summarize total=count(),
                        succeeded=countif(Success == true),
                        failed=countif(Success == false),
                        avgDuration=avg(DurationMs),
                        p50=percentile(DurationMs, 50),
                        p95=percentile(DurationMs, 95),
                        p99=percentile(DurationMs, 99)
            | extend successRate = round(succeeded * 100.0 / total, 2),
                     failureRate = round(failed * 100.0 / total, 2)
            """;
        return await service.QueryLogsAsync(kql, TimeSpan.FromMinutes(timeRangeMinutes), ct);
    }
}
