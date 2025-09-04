using FractalGpu.RenderServer.Models;
using FractalGpu.RenderServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure QueueSettings from appsettings.json
builder.Services.Configure<QueueSettings>(
    builder.Configuration.GetSection("QueueSettings"));

// Register services
builder.Services.AddSingleton<IRenderQueue, RenderQueue>();
builder.Services.AddHostedService<RenderBackgroundService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();

app.Run();
