# AppInsights MCP Server

MCP server for Azure Application Insights — lets AI agents query logs, metrics, traces, and exceptions via KQL and Azure Monitor APIs.

## Tools (12)

Every wrapper takes an optional `filter` (a raw KQL where-clause, e.g. `OperationName has 'api/Events'`)
and a `timeRange` string (`30m`, `12h`, `7d`, or a bare number of minutes).

### Log Queries
| Tool | Description |
|---|---|
| `query_logs` | Execute arbitrary KQL against Log Analytics workspace (description lists the common columns) |
| `get_exceptions` | Recent exceptions, ordered by timestamp |
| `get_slow_requests` | Slowest requests by duration |
| `get_dependencies` | Dependency call summary by target |
| `get_dependency_breakdown` | Per-operation dependency cost: calls/request and ms/request — N+1 detection |
| `get_failed_requests` | Failed requests, optionally filtered by status prefix |
| `get_request_summary` | Request count, success/failure rate, duration percentiles |
| `get_operation_summary` | Operation-level aggregation: count, duration, failure rate |

### Metrics
| Tool | Description |
|---|---|
| `get_metrics` | Query any Azure Monitor metric |

### Trace & Diagnostics
| Tool | Description |
|---|---|
| `trace_operation` | Reconstruct a full transaction by operation ID |
| `search_traces` | Full-text search in traces and exception messages |
| `get_exception_details` | Exception details with stack traces |
| `find_exception_spikes` | Detect time windows with elevated exception rates |

## Configuration

Create `appsettings.json` (or set env vars):

```json
{
  "ApplicationInsights": {
    "WorkspaceId": "your-workspace-guid",
    "ResourceId": "/subscriptions/{sub}/resourceGroups/{rg}/providers/microsoft.insights/components/{name}"
  }
}
```

Environment variable overrides:
- `ApplicationInsights__WorkspaceId`
- `ApplicationInsights__ResourceId`

**Auth**: Uses `DefaultAzureCredential` — works with Azure CLI (`az login`), Managed Identity, Visual Studio, etc.

## Developing locally

```json
{
  "servers": {
    "AppInsightsMcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "<PATH TO PROJECT>"]
    }
  }
}
```

## Build

```bash
dotnet build
dotnet run -- --selftest   # checks timeRange/filter parsing
```

## Architecture

```
AI Client (Claude / Copilot)
        |  MCP JSON-RPC (stdio)
  AppInsightsMcp (.NET 10)
        |
  +-----+------+
  |            |
LogsQuery   MetricsQuery
Client      Client
  |            |
Azure Monitor REST API
  |
Log Analytics + App Insights
```

## Publishing to NuGet

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/*.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
```
