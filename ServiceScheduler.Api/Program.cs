using ServiceScheduler.Api.Middleware;
using ServiceScheduler.Application;
using ServiceScheduler.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = Problems.InvalidModelState);
builder.Services.AddScoped<IdempotencyKeyAccessor>();
builder.Services.AddScoped<IIdempotencyKeyAccessor>(sp => sp.GetRequiredService<IdempotencyKeyAccessor>());

// Swashbuckle is the OpenAPI/Swagger provider; the template's AddOpenApi is intentionally not registered as well.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<IdempotencyKeyHeaderOperationFilter>());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Scheduler")
    ?? throw new InvalidOperationException("ConnectionStrings:Scheduler is not configured."));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<IdempotencyKeyMiddleware>();
app.MapControllers();

app.Run();

public partial class Program;
