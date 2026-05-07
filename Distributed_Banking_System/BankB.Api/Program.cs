using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.

builder.Services.AddControllers();

// Add Bank services
builder.Services.AddDbContext<BankB.Api.Data.BankDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BankBDb")));

builder.Services.AddScoped<BankB.Api.Repositories.IAccountRepository, BankB.Api.Repositories.AccountRepository>();
builder.Services.AddScoped<BankB.Api.Repositories.ITransactionRepository, BankB.Api.Repositories.TransactionRepository>();
builder.Services.AddScoped<BankB.Api.Services.BankService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
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
