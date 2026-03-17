using Coordinator.Api.Services;
using Coordinator.Api.Workers;
using Coordinator.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình BaseAddress cho Bank A và Bank B (tuỳ port launchSettings.json của bạn)
// Đổi lại dùng Http theo port mặc định
builder.Services.AddHttpClient("BankA", client =>
{
    client.BaseAddress = new Uri("https://localhost:7102/"); // Dùng http, không dùng https
});

builder.Services.AddHttpClient("BankB", client =>
{
    client.BaseAddress = new Uri("https://localhost:7016/"); // Dùng http, không dùng https
});
// Dang ky Service
builder.Services.AddSingleton<CoordinatorData>();
builder.Services.AddSingleton<CoordinatorService>();
builder.Services.AddHostedService<RecoveryWorker>();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
