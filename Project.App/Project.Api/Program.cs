using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Repositories;
using Project.Api.Repositories.Interface;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Middleware;
using Serilog;

namespace Project.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // use adminsetting.json as configuration
        builder.Configuration.AddJsonFile(
            "adminsetting.json",
            optional: true,
            reloadOnChange: true
        );

        // configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();

        // use extension methods to configure services
        builder.Services.AddDatabase(builder.Configuration);
        builder.Services.AddApplicationServices();
        builder.Services.AddCorsPolicy();
        builder.Services.AddAuth(builder.Configuration, builder.Environment);

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // optional/safe auto-migrate in other words our startup wont crash if the our migration model doenst match the DB
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                db.Database.Migrate();
                Log.Information("[Startup] Database migration check complete.");
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "[Startup] Database migration skipped or failed. Continuing startup."
                );
            }
        }

        app.UseMiddleware<GlobalExceptionHandler>();

        // connection string print
        app.MapGet(
            "/string",
            () =>
            {
                var CS = builder.Configuration.GetConnectionString("DefaultConnection");
                return Results.Ok(CS);
            }
        );

        // is development envrionment
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(ProgramExtensions.CorsPolicy); // Enable CORS with our policy
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}

public static class ProgramExtensions
{
    public const string CorsPolicy = "FrontendCors";

    /// <summary>
    /// Applies the configuration for the CORS policy.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                CorsPolicy,
                policy =>
                {
                    policy
                        .WithOrigins("http://localhost:3000", "https://localhost:3000") // Next.js frontend
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials(); // Required for cookies
                }
            );
        });
        return services;
    }

    /// <summary>
    /// Registers the database.
    /// </summary>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
        );
        return services;
    }

    /// <summary>
    /// Registers the services and repositories used by the application.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // scoped services
        services.AddScoped<IBlackjackService, BlackjackService>();
        services.AddScoped<IHandService, HandService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IUserService, UserService>();

        // singleton services
        services.AddHttpClient<IDeckApiService, DeckApiService>();
        services.AddSingleton<IRoomSSEService, RoomSSEService>();

        // scoped repositories
        services.AddScoped<IHandRepository, HandRepository>();
        services.AddScoped<IRoomPlayerRepository, RoomPlayerRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // automapper!!!
        services.AddAutoMapper(typeof(Program));

        return services;
    }

    /// <summary>
    /// Configures and registers the authentication services.
    /// </summary>
    public static IServiceCollection AddAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        // sanity check for auth errors logging
        if (!environment.IsEnvironment("Testing"))
        {
            var gid = configuration["Google:ClientId"];
            var gsec = configuration["Google:ClientSecret"];
            if (string.IsNullOrWhiteSpace(gid) || string.IsNullOrWhiteSpace(gsec))
            {
                throw new InvalidOperationException(
                    "Google OAuth config missing. Set Google:ClientId and Google:ClientSecret."
                );
            }
            Log.Information(
                "Google ClientId (first 8): {ClientId}",
                gid?.Length >= 8 ? gid[..8] : gid
            );
        }

        // Google OAuth
        services
            .AddAuthentication(options =>
            {
                // where the app reads identity from on each request
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // how the app prompts an unauthenticated user to log in
                // you can set DefaultChallengeScheme to Cookies instead but it didnt work before
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                // cross-site cookie SPA on :3000 to API on :7069
                cookie.Cookie.SameSite = SameSiteMode.None;
                // browsers require Secure when SameSite=None
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                // for APIs return status codes instead of 302 redirects
                cookie.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        ctx.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    },
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = configuration["Google:ClientId"]!;
                options.ClientSecret = configuration["Google:ClientSecret"]!;
                options.CallbackPath = "/auth/google/callback";
                options.Scope.Add("email");
                options.Scope.Add("profile");

                // ensure we actually fetch profile claims from Google UserInfo endpoint
                options.SaveTokens = true;
                options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v3/userinfo";
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey("picture", "picture");
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");

                // Upsert user into DB when Google signs in successfully
                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async ctx =>
                    {
                        var log = ctx.HttpContext.RequestServices.GetRequiredService<
                            ILogger<Program>
                        >();

                        // fetch the full user JSON from Google's userinfo endpoint
                        var request = new System.Net.Http.HttpRequestMessage(
                            System.Net.Http.HttpMethod.Get,
                            ctx.Options.UserInformationEndpoint
                        );
                        request.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue(
                                "Bearer",
                                ctx.AccessToken
                            );

                        var response = await ctx.Backchannel.SendAsync(request);
                        response.EnsureSuccessStatusCode();

                        using var userJson = JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync()
                        );
                        var j = userJson.RootElement;

                        var email = j.TryGetProperty("email", out var e) ? e.GetString() : null;
                        var verified =
                            j.TryGetProperty("email_verified", out var v) && v.GetBoolean();
                        var name = j.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var picture = j.TryGetProperty("picture", out var p) ? p.GetString() : null;

                        log.LogInformation(
                            "[OAuth] CreatingTicket: email={Email}, verified={Verified}, name={Name}",
                            email,
                            verified,
                            name
                        );

                        if (string.IsNullOrWhiteSpace(email) || !verified)
                        {
                            log.LogWarning("[OAuth] Missing or unverified email; failing ticket.");
                            ctx.Fail("Google email must be present and verified.");
                            return;
                        }

                        try
                        {
                            using var scope = ctx.HttpContext.RequestServices.CreateScope();
                            var users = scope.ServiceProvider.GetRequiredService<IUserService>();

                            var user = await users.UpsertGoogleUserByEmailAsync(
                                email!,
                                name,
                                picture
                            );

                            log.LogInformation(
                                "[OAuth] Upsert complete: UserId={UserId}, Email={UserEmail}",
                                user.Id,
                                user.Email
                            );
                        }
                        catch (Exception ex)
                        {
                            log.LogError(
                                ex,
                                "[OAuth] Error during UpsertGoogleUserByEmailAsync for {Email}",
                                email
                            );
                            ctx.Fail("Internal error during user creation.");
                        }
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }
}
