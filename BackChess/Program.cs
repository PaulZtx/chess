using System.Runtime.CompilerServices;
using BackChess;
using BackChess.Configurations;
using BackChess.Services;
using ChessInfrastructure;
using ChessInfrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
Config.Initialize(builder.Configuration);

// Настройки подтверждения почты из секции Email
var email = Config.GetSection<Email>();
if (!Uri.TryCreate(email.ConfirmationUrl, UriKind.Absolute, out var confirmationUri)
    || confirmationUri.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException("Email:ConfirmationUrl должен содержать абсолютный HTTP/HTTPS URL");
}

// Строки подключения из одноименной секции appsettings.json
var connectionStrings = Config.GetSection<ConnectionStrings>();
if (string.IsNullOrWhiteSpace(connectionStrings.PostgresConnection))
    throw new InvalidOperationException("ConnectionStrings:PostgresConnection не задана");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddAntiforgery();
builder.Services.AddTransient<IEmailSender<User>, EmailSender>();
builder.Services.AddSingleton<ChessBridgeService>();
builder.Services.AddHostedService<EngineJobWorker>();
builder.Services.AddDbContext<Storage>(options =>
    options.UseNpgsql(connectionStrings.PostgresConnection));


builder.Services
    .AddIdentity<User, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
       
        // Lockout
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User
        options.User.RequireUniqueEmail = true;

        // Sign‑in
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<Storage>()
    .AddClaimsPrincipalFactory<PlayerClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        // Перенаправляем Swagger UI на правильный JSON-файл от .NET 10
        options.SwaggerEndpoint("/openapi/v1.json", "API v1");
    });
}

app.UseHttpsRedirection();

app.MapGet("/events", (CancellationToken cancellationToken) =>
    
    TypedResults.ServerSentEvents(
        GetEventsAsync(cancellationToken),
        eventType: "message"
    )
);

async IAsyncEnumerable<string> GetEventsAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        // yield return Encoding.GetEncoding("cp1251").GetString(Encoding.UTF8.GetBytes($"Время на сервере: {DateTime.Now}"));
        yield return $"Время на сервере: {DateTime.Now}";
        await Task.Delay(1000, cancellationToken);
    }
}


app.MapControllers();
app.Run();
