using System.Text.Json;
using AppInsightsMcp.Configuration;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.Options;

namespace AppInsightsMcp.Services;

/// <summary>
/// Wraps Azure Monitor Query SDK for Application Insights log and metric queries.
/// </summary>
public sealed class AppInsightsService
{
    private readonly LogsQueryClient _logsClient;
    private readonly MetricsQueryClient _metricsClient;
    private readonly string _workspaceId;
    private readonly string _resourceId;
    private readonly string? _tenantId;

    public AppInsightsService(IOptions<AppInsightsConfig> config)
    {
        var cfg = config.Value;
        _workspaceId = cfg.WorkspaceId;
        _resourceId = cfg.ResourceId;
        _tenantId = cfg.TenantId;

        var credOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(cfg.TenantId))
            credOptions.TenantId = cfg.TenantId;

        var credential = new DefaultAzureCredential(credOptions);
        _logsClient = new LogsQueryClient(credential);
        _metricsClient = new MetricsQueryClient(credential);
    }

    /// <summary>
    /// Executes an arbitrary KQL query against the Log Analytics workspace.
    /// </summary>
    public async Task<string> QueryLogsAsync(string kql, TimeSpan? timespan = null, CancellationToken ct = default)
    {
        var ts = timespan ?? TimeSpan.FromHours(1);
        var options = new LogsQueryOptions { AllowPartialErrors = true };

        Response<LogsQueryResult> response;
        try
        {
            response = await _logsClient.QueryWorkspaceAsync(
                _workspaceId, kql, new QueryTimeRange(ts), options, ct);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(AccessHint($"workspace {_workspaceId}", ex), ex);
        }

        var rows = new List<Dictionary<string, object?>>();
        foreach (var row in response.Value.Table.Rows)
        {
            var dict = new Dictionary<string, object?>();
            for (int i = 0; i < response.Value.Table.Columns.Count; i++)
            {
                dict[response.Value.Table.Columns[i].Name] = row[i];
            }
            rows.Add(dict);
        }

        return JsonSerializer.Serialize(new
        {
            rowCount = rows.Count,
            columns = response.Value.Table.Columns.Select(c => c.Name),
            rows
        }, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Queries multiple metrics at once. Returns all data points.
    /// </summary>
    public async Task<string> QueryMultiMetricsAsync(string[] metricNames, TimeSpan? timespan = null, CancellationToken ct = default)
    {
        var ts = timespan ?? TimeSpan.FromHours(1);
        var resourceId = new ResourceIdentifier(_resourceId);

        Response<MetricsQueryResult> response;
        try
        {
            response = await _metricsClient.QueryResourceAsync(
                resourceId,
                metricNames,
                new MetricsQueryOptions { TimeRange = new QueryTimeRange(ts) },
                ct);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(AccessHint($"resource {_resourceId}", ex), ex);
        }

        var results = response.Value.Metrics.Select(m => new
        {
            metric = m.Name,
            unit = m.Unit,
            points = m.TimeSeries
                .SelectMany(ts => ts.Values)
                .Select(v => new { timestamp = v.TimeStamp, value = v.Average })
                .ToList()
        });

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Turns Azure's generic auth/not-found errors into a message naming the account this server expects,
    /// so a wrong `az login` tenant/subscription is obvious instead of a half-hour hunt.
    /// </summary>
    private string AccessHint(string target, RequestFailedException ex)
    {
        var subscription = _resourceId.Split('/') is [_, "subscriptions", var sub, ..] ? sub : "(unknown)";
        return ex.Status is 401 or 403 or 404
            ? $"Azure denied access to {target} (HTTP {ex.Status}). This server expects tenant " +
              $"'{_tenantId ?? "(default)"}' and subscription '{subscription}'. Check `az account show` — " +
              $"if the active login is a different tenant/subscription, run `az login --tenant {_tenantId ?? "<tenant>"}` " +
              $"and `az account set --subscription {subscription}`. Original: {ex.Message}"
            : $"Azure Monitor query against {target} failed (HTTP {ex.Status}): {ex.Message}";
    }
}
