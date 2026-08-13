using System.ComponentModel;
using AppInsightsMcp.Services;
using ModelContextProtocol.Server;

namespace AppInsightsMcp.Tools;

/// <summary>
/// Tools for tracing operations and searching traces in Application Insights.
/// </summary>
internal class AppInsightsTraceTools(AppInsightsService service)
{
    [McpServerTool(Name = "trace_operation")]
    [Description("Traces an entire operation by its operation ID. Returns the request, all AppDependencies, " +
                 "traces, and exceptions correlated to the same operation, ordered by TimeGenerated. " +
                 "Use this to reconstruct a complete transaction flow.")]
    public async Task<string> TraceOperation(
        [Description("The OperationId to trace")] string operationId,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '24h')")] string timeRange = "24h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            union AppRequests, AppDependencies, AppTraces, AppExceptions
            | where OperationId == "{{operationId}}"
            | order by TimeGenerated asc
            | project TimeGenerated, itemType=Type, Name, DurationMs, Success, Message, ResultCode, Target, SeverityLevel
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "search_traces")]
    [Description("Searches traces and exception messages in Application Insights for a given text. " +
                 "Useful for finding all log entries related to a specific error, user, or event.")]
    public async Task<string> SearchTraces(
        [Description("Text to search for in trace messages and exception messages")] string searchText,
        [Description("Optional extra KQL where-clause, e.g. \"AppRoleName == 'api' and SeverityLevel >= 3\"")] string? filter = null,
        [Description("Maximum number of results (default 100)")] int top = 100,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '24h')")] string timeRange = "24h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            union AppTraces, AppExceptions
            | where * contains "{{searchText}}"
            {{Kql.Where(filter)}}
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, itemType=Type, message=coalesce(Message, OuterMessage), SeverityLevel, OperationId, AppRoleName
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_exception_details")]
    [Description("Returns detailed exception information including stack traces for a specific problem ID or exception type.")]
    public async Task<string> GetExceptionDetails(
        [Description("The ProblemId or OuterMessage to filter on (partial match)")] string filter,
        [Description("Maximum number of results (default 20)")] int top = 20,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '24h')")] string timeRange = "24h",
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppExceptions
            | where ProblemId contains "{{filter}}" or OuterMessage contains "{{filter}}"
            | order by TimeGenerated desc
            | take {{top}}
            | project TimeGenerated, OuterMessage, ProblemId, Type, Method, Assembly,
                      details=trim(@"\s+", trim_end(@"\r\n.*", tostring(parse_json(Details[0]).parsedStack)))
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "find_exception_spikes")]
    [Description("Detects time periods with unusually high exception rates by bucketing AppExceptions into 5-minute windows.")]
    public async Task<string> FindExceptionSpikes(
        [Description("Optional KQL where-clause on AppExceptions, e.g. \"AppRoleName == 'api'\"")] string? filter = null,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '24h')")] string timeRange = "24h",
        [Description("Minimum exception count per bucket to report (default 5)")] int threshold = 5,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppExceptions
            {{Kql.Where(filter)}}
            | summarize exceptionCount=count() by bin(TimeGenerated, 5m), ProblemId, Type
            | where exceptionCount >= {{threshold}}
            | order by exceptionCount desc
            | project TimeGenerated, exceptionCount, ProblemId, Type
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }

    [McpServerTool(Name = "get_operation_summary")]
    [Description("Returns a high-level summary of operations in the time range: count, duration, success rate by operation name.")]
    public async Task<string> GetOperationSummary(
        [Description("Optional KQL where-clause on AppRequests, e.g. \"OperationName has 'api/Events'\"")] string? filter = null,
        [Description("Time range: '30m', '12h', '7d', or minutes (default '1h')")] string timeRange = "1h",
        [Description("Maximum number of operations to return (default 20)")] int top = 20,
        CancellationToken ct = default)
    {
        var kql = $$"""
            AppRequests
            {{Kql.Where(filter)}}
            | summarize count=count(),
                        avgDuration=avg(DurationMs),
                        p95Duration=percentile(DurationMs, 95),
                        failedCount=countif(Success == false)
                      by Name, AppRoleName
            | extend failureRate = round(failedCount * 100.0 / count, 2)
            | order by count desc
            | take {{top}}
            """;
        return await service.QueryLogsAsync(kql, Kql.Range(timeRange), ct);
    }
}
