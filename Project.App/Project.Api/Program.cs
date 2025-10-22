using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
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

        app.UseMiddleware<GlobalExceptionHandler>();

        // map default endpoint (shows connection string)
        app.MapGet(
            "/string",
            () =>
            {
                var CS = builder.Configuration.GetConnectionString("DefaultConnection");
                return Results.Ok(CS);
            }
        );

        // Configure the HTTP request pipeline.
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
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                // cross-site cookie SPA on :3000 to API on :7069
                cookie.Cookie.SameSite = SameSiteMode.None;
                // browsers require Secure when SameSite=None this is why we need https instead of http
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
                options.ClientId = configuration["Google:ClientId"]!; //  from secrets / config
                options.ClientSecret = configuration["Google:ClientSecret"]!; //  from secrets / config
                options.CallbackPath = "/auth/google/callback"; //  google redirects here after login if we change this we need to change it on google cloud as well!
                //options.Events = new OAuthEvents
                { //TEMPORARILY COMMENTED OUR BELOW BECAUSE IT WAS MESSING WITH GOOGLE AUTH LOGIN FOR SOME REASON GOTTA CHECK THIS LATER
                    // OnCreatingTicket = async ctx => //currently we are accessing the user json that we get back from google oauth and using it as a quick validation check since email is our unique primary identified on users rn
                    { /*
                        var email = ctx.User.GetProperty("email").GetString(); //all of these checks are quick validation can be moved elsewhere when we decide where to put it
                        var verified =
                            ctx.User.TryGetProperty("email_verified", out var ev)
                            && ev.GetBoolean();
                        var name = ctx.User.TryGetProperty("name", out var n)
                            ? n.GetString()
                            : null;
                        var picture = ctx.User.TryGetProperty("picture", out var p)
                            ? p.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(email) || !verified)
                        {
                            ctx.Fail("Google email must be present and verified.");
                            return;
                        }

                        var svc =
                            ctx.HttpContext.RequestServices.GetRequiredService<IUserService>();
                        await svc.UpsertGoogleUserByEmailAsync(email, name, picture);
                    */
                    }
                }
                ;
            });

        services.AddAuthorization();

        return services;
    }
}
