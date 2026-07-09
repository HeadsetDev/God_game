using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Data;
using GameAuthAPI.Services;
using GameAuthAPI.Hubs;
using GameAuthAPI.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Serilog;
using HealthChecks.SqlServer;
using HealthChecks.Redis;
using HealthChecks.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// ========== LOGGING ==========
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// ========== SERVICES ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<RedisCacheService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<SecurityLogger>();
builder.Services.AddScoped<StaticDataService>();
builder.Services.AddScoped<StanceService>();

// ========== DATABASE ==========
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Game"))
);

// ========== REDIS ==========
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "GameServerAPI";
});

// ========== RABBITMQ ==========
builder.Services.AddSingleton<RabbitMQService>();

// ========== JWT ==========
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Секретный ключ не найден.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// ========== AUTHORIZATION ==========
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Admin"));
});

// ========== CORS ==========
builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCors", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// ========== SWAGGER ==========
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GameAuthAPI", Version = "v1" });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ========== HEALTH CHECKS ==========
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("Game")!,
        name: "SQL Server",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
    )
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "Redis"
    )
    .AddRabbitMQ(
        rabbitConnectionString: "amqp://guest:guest@localhost:5672",
        name: "RabbitMQ"
    );

// ========== AUTOMAPPER ==========
builder.Services.AddAutoMapper(typeof(Program));

var app = builder.Build();

// ========== MIDDLEWARE ==========
app.UseRouting();
app.UseHttpsRedirection();
app.UseCors("StrictCors");
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<DDoSProtectionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();

// ========== SIGNALR ==========
app.MapHub<ChatHub>("/chatHub");
app.MapHub<BattleHub>("/battleHub");

// ========== HEALTH CHECKS ==========
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.ToString()
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// ========== SEED DATA ==========
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await SeedData.InitializeAsync(dbContext);
}

// ========== SWAGGER ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();