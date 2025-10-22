using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http; // for Results
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Project.Api.Data;
using Project.Api.Middleware;
using Project.Api.Repositories;
using Project.Api.Services;
using Project.Api.Services.Interface;
using Serilog;

namespace Project.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        const string CorsPolicy = "FrontendCors";

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                CorsPolicy,
                policy =>
                {
                    policy
                        .WithOrigins("http://localhost:3000", "https://localhost:3000") //  Next.js dev origin add other frontend origins below this when we move to server
                        .AllowAnyHeader()
                        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                        .AllowCredentials(); // required cookie for auth
                }
            );
        });

        builder.Configuration.AddJsonFile(
            "adminsetting.json",
            optional: true,
            reloadOnChange: true
        );

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        );
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<IDeckApiService, DeckApiService>();

        builder.Services.AddScoped<IHandService, HandService>();

        builder.Services.AddScoped<IHandRepository, HandRepository>();

        //Auto Mapper
        builder.Services.AddAutoMapper(typeof(Program));

        // CORS configuration
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .WithOrigins("http://localhost:3000") // Your Next.js frontend
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Required for cookies
            });
        });

        builder.Services.AddAuthorization();

        //sanity check for auth errors logging
        var gid = builder.Configuration["Google:ClientId"];
        var gsec = builder.Configuration["Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(gid) || string.IsNullOrWhiteSpace(gsec))
        {
            throw new InvalidOperationException(
                "Google OAuth config missing. Set Google:ClientId and Google:ClientSecret."
            );
        }
        Log.Information("Google ClientId (first 8): {ClientId}", gid?.Length >= 8 ? gid[..8] : gid);

        // Google OAuth
        builder
            .Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; // where the app reads identity from on each request
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme; // how the app prompts an unauthenticated user to log in
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
                options.ClientId = builder.Configuration["Google:ClientId"]!; //  from secrets / config
                options.ClientSecret = builder.Configuration["Google:ClientSecret"]!; //  from secrets / config
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

        var app = builder.Build();

        app.UseMiddleware<GlobalExceptionHandler>();

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

        app.UseCors(CorsPolicy); // Enable CORS with our policy
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
