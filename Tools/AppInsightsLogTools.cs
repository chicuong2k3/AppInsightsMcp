using System.ComponentModel;
using AppInsightsMcp.Services;
using ModelContextProtocol.Server;

namespace AppInsightsMcp.Tools;

/// <summary>
/// Tools for querying Application Insights logs (requests, exceptions, dependencies, AppTraces).
/// </summary>
internal class AppInsightsLogTools(AppInsightsService service)
{
    [McpServerTool(Name = "query_logs")]
    [Description("""
        Executes an arbitrary KQL query against the Application Insights Log Analytics workspace.
        Tables: AppRequests, AppExceptions, AppDependencies, AppTraces, AppCustomEvents, AppPageViews, AppAvailabilityResults.
        Common columns (all tables): TimeGenerated, OperationId, ParentId, OperationName, AppRoleName, AppRoleInstance, ClientIP, ClientType, Properties, Measurements, ItemCount.
        AppRequests: Name, Url, DurationMs, ResultCode, Success, Source.
        AppDependencies: Name, Target, DependencyType, Data, DurationMs, ResultCode, Success.
        AppExceptions: ProblemId, Type, OuterMessage, InnermostMessage, Method, Assembly, SeverityLevel, Details.
        AppTraces: Message, SeverityLevel.
        AppAvailabilityResults: Name, Success, DurationMs, Location, Message.
        Note: sampled data is scaled by ItemCount - use sum(ItemCount) rather than count() for accurate volumes.
        """)]
    public async Task<string> QueryLogs(
        [Description("The KQL query to execute")] string kql,
        [Description("Time range: '30m', '12h', '7d', or a bare number of minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_exceptions")]
    [Description("Returns the most recent exceptions from Application Insights, ordered by TimeGenerated descending.")]
    public async Task<string> GetExceptions(
        [Description("Optional KQL where-clause on AppExceptions, e.g. \"ProblemId has 'Sql' and AppRoleName == 'api'\"")] string? filter = null,
        [Description("Maximum number of exceptions to return (default 50)")] int top = 50,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppExceptions
            {{Kql.Where(filter)}}
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, type=OuterMessage, Method, ProblemId, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_slow_requests")]
    [Description("Returns the slowest requests from Application Insights, ordered by duration descending.")]
    public async Task<string> GetSlowRequests(
        [Description("Optional KQL where-clause on AppRequests, e.g. \"OperationName has 'api/Events'\"")] string? filter = null,
        [Description("Maximum number of requests to return (default 20)")] int top = 20,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppRequests
            {{Kql.Where(filter)}}
            | order by DurationMs desc
            | take {{top}}
            | project TimeGenerated, Name, DurationMs, ResultCode, Success, Url, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_dependencies")]
    [Description("Summarizes dependency calls by target, showing call count and average duration.")]
    public async Task<string> GetDependencies(
        [Description("Optional KQL where-clause on AppDependencies, e.g. \"DependencyType == 'SQL'\"")] string? filter = null,
        [Description("Maximum number of dependency targets to return (default 30)")] int top = 30,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppDependencies
            {{Kql.Where(filter)}}
            | summarize count=count(), avgDuration=avg(DurationMs) by Target, DependencyType
            | order by count desc
            | take {{top}}
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_dependency_breakdown")]
    [Description("Joins AppRequests with AppDependencies on OperationId to show, per operation, which dependencies it calls, " +
                 "how many calls per request, and how much time per request they cost. Use this to find N+1 call patterns.")]
    public async Task<string> GetDependencyBreakdown(
        [Description("Optional KQL where-clause on AppRequests, e.g. \"OperationName has 'api/Events'\"")] string? filter = null,
        [Description("Maximum number of operation/dependency rows to return (default 30)")] int top = 30,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            let reqs = AppRequests
            {{Kql.Where(filter)}}
            | summarize requests=dcount(OperationId), avgRequestMs=avg(DurationMs) by OperationName;
            AppRequests
            {{Kql.Where(filter)}}
            | project OperationId, OperationName
            | join kind=inner (AppDependencies | project OperationId, Target, DependencyType, depName=Name, DurationMs) on OperationId
            | summarize calls=count(), totalMs=sum(DurationMs) by OperationName, DependencyType, Target, depName
            | join kind=inner reqs on OperationName
            | extend callsPerRequest=round(calls * 1.0 / requests, 2), msPerRequest=round(totalMs / requests, 1)
            | project OperationName, requests, avgRequestMs=round(avgRequestMs, 1), DependencyType, Target, depName, calls, callsPerRequest, msPerRequest
            | order by msPerRequest desc
            | take {{top}}
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_failed_requests")]
    [Description("Returns failed requests from Application Insights for the specified time range.")]
    public async Task<string> GetFailedRequests(
        [Description("Optional KQL where-clause on AppRequests, e.g. \"OperationName has 'api/Events'\"")] string? filter = null,
        [Description("Maximum number of results (default 50)")] int top = 50,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        [Description("Filter by specific HTTP status code prefix (e.g., '5' for 5xx). Defaults to all unsuccessful requests.")] string? statusPrefix = null,
        CancellationToken ct = default)
    {
        var statusFilter = statusPrefix is not null
            ? $"| where ResultCode startswith \"{statusPrefix}\""
            : "| where Success == false";

        var kql = $$"""
            AppRequests
            {{statusFilter}}
            {{Kql.Where(filter)}}
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, Name, DurationMs, ResultCode, Url, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_request_summary")]
    [Description("Returns a summary of request activity: total count, success/failure rates, and duration percentiles.")]
    public async Task<string> GetRequestSummary(
        [Description("Optional KQL where-clause on AppRequests, e.g. \"OperationName has 'api/Events'\"")] string? filter = null,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppRequests
            {{Kql.Where(filter)}}
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
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }
}
