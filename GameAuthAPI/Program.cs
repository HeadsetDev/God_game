using Microsoft.EntityFrameworkCore;
using GameAuthAPI.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GameAuthAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using System.Reflection;
using GameAuthAPI.Hubs;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Добавляем SignalR
builder.Services.AddSignalR();

// Добавляем кэш в память (IMemoryCache)
builder.Services.AddMemoryCache();

// Регистрация RabbitMQService
builder.Services.AddSingleton<RabbitMQService>();

// Настройка Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "GameServerAPI";
});

// Добавляем AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Проверка подключения к базе данных
try
{
    var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
    optionsBuilder.UseSqlServer(builder.Configuration.GetConnectionString("Game"));

    using var context = new GameDbContext(optionsBuilder.Options);
    context.Database.OpenConnection();
    Console.WriteLine("Подключение к базе данных успешно!");
    context.Database.CloseConnection();
}
catch (Exception ex)
{
    Console.WriteLine("Ошибка подключения к базе данных: " + ex.Message);
}

// Регистрация сервисов
builder.Services.AddScoped<PasswordService>();
builder.Services.AddLogging();

// Настройка JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Секретный ключ не найден в конфигурации.");

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

// Настройка авторизации
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    policy.RequireClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "Admin"));
    //options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("PlayerOnly", policy => policy.RequireRole("Player"));
});

// Настройка базы данных
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Game"))
);

// Настройка CORS (исправлено)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins("https://example.com", "http://localhost:3000") // Укажите ваши домены
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Разрешаем передачу учетных данных
    });
});

// Регистрация контроллеров
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Регистрация HttpContextAccessor (если используется)
builder.Services.AddHttpContextAccessor();

// Настройка Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "GameAuthAPI", Version = "v1" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
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

var app = builder.Build();

// Middleware
app.UseRouting();
app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins"); // Используем исправленную политику CORS
app.UseAuthentication();
app.UseAuthorization();

// Подключение SignalR
app.MapHub<ChatHub>("/chatHub");
app.MapHub<BattleHub>("/battleHub");
// Включаем Swagger в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameAuthAPI v1"));
}

// Маршрутизация контроллеров
app.MapControllers();

app.Run();