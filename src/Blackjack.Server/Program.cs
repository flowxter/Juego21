using System;
using System.Text;
using System.Threading.Tasks;
using Blackjack.Data;
using Blackjack.Data.Entities;
using Blackjack.Data.History;
using Blackjack.Data.Wallet;
using Blackjack.Server.Auth;
using Blackjack.Server.Hubs;
using Blackjack.Server.Tables;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Base de datos
// ---------------------------------------------------------------------------
// La cadena se resuelve desde el IServiceProvider, no leyendo
// builder.Configuration aquí mismo: quien hospeda el servidor para tests
// inyecta su configuración después de que estas líneas se ejecuten, y leerla
// directa haría que los tests escribieran en la base de desarrollo.
static string ResolveConnectionString(IServiceProvider services)
    => services.GetRequiredService<IConfiguration>().GetConnectionString("Blackjack")
        ?? throw new InvalidOperationException(
            "Falta ConnectionStrings:Blackjack. Levanta la base con 'docker compose up -d db'.");

// Factory en vez de DbContext inyectado: las mesas son actores de larga vida y
// un DbContext no es seguro entre hilos ni está pensado para durar horas.
builder.Services.AddDbContextFactory<BlackjackDbContext>((sp, options) =>
    options.UseNpgsql(ResolveConnectionString(sp)));

// Identity sí necesita un contexto por petición. Se saca del mismo factory en
// vez de registrar AddDbContext aparte: eso registraría las opciones dos veces
// con vidas distintas (singleton para el factory, scoped para el contexto) y
// el contenedor lo rechaza al validar.
builder.Services.AddScoped<BlackjackDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<BlackjackDbContext>>().CreateDbContext());

// ---------------------------------------------------------------------------
// Identidad y tokens
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;

        // Bloqueo tras intentos fallidos: es la defensa básica contra fuerza
        // bruta sobre contraseñas de jugadores.
        options.Lockout.MaxFailedAccessAttempts = 8;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<BlackjackDbContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// La validación se configura desde IOptions<JwtOptions>, NO leyendo
// builder.Configuration aquí mismo.
//
// Es deliberado: quien hospeda el servidor para tests inyecta su configuración
// después de que estas líneas se hayan ejecutado. Leyendo la clave directa, el
// emisor firmaba con una y el validador comprobaba con otra, y todo token
// resultaba inválido. Con IOptions ambos leen la misma fuente ya resuelta.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        JwtOptions options = jwt.Value;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Los WebSocket del navegador no admiten cabeceras propias, así
                // que SignalR manda el token por la query string. Solo se acepta
                // en las rutas del hub, nunca en la API REST.
                string? accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// Juego
// ---------------------------------------------------------------------------
builder.Services.Configure<TableOptions>(builder.Configuration.GetSection("Table"));

builder.Services.AddSingleton<IWalletService>(sp => new EfWalletService(
    sp.GetRequiredService<IDbContextFactory<BlackjackDbContext>>(),
    startingBalance: sp.GetRequiredService<IConfiguration>()
        .GetValue("Game:StartingBalance", 1000m)));

builder.Services.AddSingleton<IRoundArchive, EfRoundArchive>();
builder.Services.AddSingleton<TableManager>();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// El cliente Unity no comparte origen con el servidor durante el desarrollo.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

WebApplication app = builder.Build();

// Aplicar migraciones al arrancar. Cómodo en desarrollo; en producción esto
// debería ser un paso aparte del despliegue, no algo que haga el propio
// servidor al levantarse.
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlackjackDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapHub<GameHub>("/hub/game");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/tables", (TableManager tables) => Results.Ok(tables.TableIds));

app.Run();

/// <summary>
/// Necesario para que los tests de integración con WebApplicationFactory
/// puedan referenciar el punto de entrada.
/// </summary>
public partial class Program;
