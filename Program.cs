// AppInsightsMcp - MCP server exposing Application Insights data (logs, metrics, traces) as tools over stdio.
using AppInsightsMcp.Configuration;
using AppInsightsMcp.Services;
using AppInsightsMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args is ["--selftest"])
{
    Kql.SelfTest();
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Configure Application Insights settings from appsettings.json + env vars
builder.Services.Configure<AppInsightsConfig>(
    builder.Configuration.GetSection("ApplicationInsights"));

// Register the Azure Monitor service
builder.Services.AddSingleton<AppInsightsService>();

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<AppInsightsLogTools>()
    .WithTools<AppInsightsMetricTools>()
    .WithTools<AppInsightsTraceTools>();

await builder.Build().RunAsync();
