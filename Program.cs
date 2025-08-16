using SolidFireGateway;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
// Create the web application builder
var builder = WebApplication.CreateBuilder(args);
// Custom middleware will handle simple request logging


// Enable CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://win25:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});




// 1) Bind config
builder.Services.Configure<VolumeAccessOptions>(
    builder.Configuration.GetSection("VolumeAccess"));
builder.Services.Configure<TenantOptions>(
    builder.Configuration.GetSection("TenantOptions"));

builder.Services.AddEndpointsApiExplorer();
// Restore default Swagger generator
// Configure Swagger to include XML comments from generated documentation
builder.Services.AddSwaggerGen(c =>
{
    // Use assembly version for Swagger doc
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var apiVersion = assembly.GetName().Version?.ToString() ?? "v1";
    c.SwaggerDoc(apiVersion, new Microsoft.OpenApi.Models.OpenApiInfo {
        Title = "SolidFire Gateway API",
        Version = apiVersion
    });
    var xmlFile = $"{assembly.GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
        // Customize attributes dictionary to accept various value types (int, float, bool, object)
        c.MapType<Dictionary<string, object>>(() => new Microsoft.OpenApi.Models.OpenApiSchema
        {
            Type = "object",
            AdditionalProperties = new Microsoft.OpenApi.Models.OpenApiSchema()
        });
});

// 2) Windows auth (requires Microsoft.AspNetCore.Authentication.Negotiate)
// Removed because we use IIS Windows Auth directly.

// 3) JSON-RPC client wrapper for multiple clusters
var clustersSection = builder.Configuration.GetSection("SolidFireClusters");
var clusters = clustersSection.GetChildren();
foreach (var cluster in clusters)
{
    var clusterName = cluster.Key;
    var endpoint = cluster.GetValue<string>("Endpoint");
    if (string.IsNullOrEmpty(endpoint))
        throw new InvalidOperationException($"Endpoint is required for cluster '{clusterName}'");
    var snakeOilTls = cluster.GetValue<bool>("SnakeOilTls");
    builder.Services.AddHttpClient<SolidFireClient>(clusterName, client =>
    {
        client.BaseAddress = new Uri(endpoint);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
        snakeOilTls
            ? new HttpClientHandler { ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true }
            : new HttpClientHandler()
    );
}

// 4) Dynamic authorization policies
builder.Services.AddAuthorization(options =>
{
    // Load global admin roles from configuration
    var globalAdmins = builder.Configuration.GetSection("GlobalAdminRoles").Get<string[]>()
                         ?? System.Array.Empty<string>();
    var access = builder.Configuration
                        .GetSection("VolumeAccess:ActionRoles")
                        .Get<Dictionary<string, string[]>>();

    foreach (var kv in access)
    {
        var actionName = kv.Key;
        var allowedRoles = kv.Value;

        options.AddPolicy($"Volume{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
    // Register QosAccess policies
    var qosAccess = builder.Configuration.GetSection("QosAccess:ActionRoles").Get<Dictionary<string, string[]>>();
    foreach (var kv2 in qosAccess)
    {
        var actionName = kv2.Key;
        var allowedRoles = kv2.Value;
        options.AddPolicy($"Qos{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
    // Register SnapshotAccess policies
    var snapshotAccess = builder.Configuration.GetSection("SnapshotAccess:ActionRoles").Get<Dictionary<string, string[]>>();
    foreach (var kvSnap in snapshotAccess)
    {
        var actionName = kvSnap.Key;
        var allowedRoles = kvSnap.Value;
        options.AddPolicy($"Snapshot{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
    var accountAccess = builder.Configuration.GetSection("AccountAccess:ActionRoles").Get<Dictionary<string, string[]>>();
    foreach (var kv3 in accountAccess)
    {
        var actionName = kv3.Key;
        var allowedRoles = kv3.Value;
        options.AddPolicy($"Account{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
    // Register AccountStatsAccess policies
    var statsAccess = builder.Configuration.GetSection("AccountStatsAccess:ActionRoles").Get<Dictionary<string, string[]>>();
    foreach (var kv4 in statsAccess)
    {
        var actionName = kv4.Key;
        var allowedRoles = kv4.Value;
        options.AddPolicy($"AccountStats{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
    // Register ClusterStatsAccess policies
    var clusterStatsAccess = builder.Configuration.GetSection("ClusterStatsAccess:ActionRoles").Get<Dictionary<string, string[]>>();
    foreach (var kv5 in clusterStatsAccess)
    {
        var actionName = kv5.Key;
        var allowedRoles = kv5.Value;
        options.AddPolicy($"ClusterStats{actionName}", policy =>
            policy.RequireAssertion(ctx =>
                // Global admin bypass
                globalAdmins.Any(r => ctx.User.IsInRole(r))
             || allowedRoles.Any(r => ctx.User.IsInRole(r))
            ));
    }
});

// Use Newtonsoft for richer object serialization (e.g. Dictionary<string, object>)
// Register controllers to always produce JSON responses
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.ProducesAttribute("application/json"));
})
    .AddNewtonsoftJson();

// Build the app after registering all services
var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger UI and JSON spec for API exploration
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    // Dynamically use the assembly version as the Swagger doc name
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var apiVersion = assembly.GetName().Version?.ToString() ?? "v1";
    options.SwaggerEndpoint($"/swagger/{apiVersion}/swagger.json", $"SolidFire Gateway API {apiVersion}");
});


// Simple request logging
app.Use(async (ctx, next) => {
    Console.WriteLine($"[{DateTime.UtcNow:O}] {ctx.Request.Method} {ctx.Request.Path}");
    await next();
    Console.WriteLine($"→ {ctx.Response.StatusCode}");
});
app.UseCors();
app.UseRouting();
// No app.UseAuthentication() for pure IIS Windows Auth
app.UseAuthorization();
app.MapControllers();
app.Run();