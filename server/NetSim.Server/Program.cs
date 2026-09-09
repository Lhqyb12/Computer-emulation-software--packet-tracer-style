using Microsoft.EntityFrameworkCore;
using NetSim.Server.Data;
using NetSim.Server.Services;


var builder = WebApplication.CreateBuilder(args);

// Controllers host the API surface (see Controllers/). OpenAPI/Swagger is dev-only.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<AuthService>();



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Simple liveness probe – lets the client (and us) confirm the server is up.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "NetSim.Server" }))
   .WithName("Health");

app.MapControllers();

app.Run();
