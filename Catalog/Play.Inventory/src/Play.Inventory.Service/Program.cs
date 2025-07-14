using MassTransit;
using Microsoft.OpenApi.Models;
using Play.Common.MassTransit;
using Play.Common.MongoDB;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// MongoDB servisini ve repository’yi ekle
builder.Services
    .AddMongo()
    .AddMongoRepository<InventoryItem>("inventoryitems")
    .AddMongoRepository<CatalogItem>("catalogitems")
    .AddMassTransitWithRabbitMQ();

AddCatalogClient(builder);

builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Inventory.Service", Version = "v1" });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

static void AddCatalogClient(WebApplicationBuilder builder)
{
    var jitterer = new Random();

    var serviceProvider = builder.Services.BuildServiceProvider();
    var logger = serviceProvider.GetRequiredService<ILogger<CatalogClient>>();

    builder.Services.AddHttpClient<CatalogClient>(client =>
    {
        client.BaseAddress = new Uri("https://localhost:7187");
    })
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.Or<TimeoutRejectedException>().WaitAndRetryAsync(
            5,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                            TimeSpan.FromMilliseconds(jitterer.Next(0, 1000)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                logger.LogWarning($"Delaying {timespan.TotalSeconds} seconds before retry {retryAttempt}");
            }))
    .AddTransientHttpErrorPolicy(policyBuilder =>
        policyBuilder.Or<TimeoutRejectedException>().CircuitBreakerAsync(
            3,
            TimeSpan.FromSeconds(15),
            onBreak: (outcome, timespan) =>
            {
                logger.LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds...");
            },
            onReset: () =>
            {
                logger.LogWarning("Closing the circuit...");
            }))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(1)));
}