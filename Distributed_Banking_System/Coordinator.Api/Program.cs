using Coordinator.Api.Models;
using Coordinator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ─── HttpClient: Dùng để gọi API BankA / BankB ─────────────
builder.Services.AddHttpClient("BankClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ─── DI: Đăng ký các Service ────────────────────────────────
builder.Services.AddSingleton<RecoveryLogStore>();
builder.Services.AddScoped<TransferService>();
builder.Services.AddHostedService<RecoveryService>();

// ─── CORS: Cho phép gọi từ bất kỳ đâu ──────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
