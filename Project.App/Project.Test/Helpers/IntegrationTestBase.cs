using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Project.Api;
using Project.Api.Data;
using Project.Api.Services.Interface;
using Project.Api.Utilities.Enums;

namespace Project.Test.Helpers;

/// <summary>
/// Base class for integration tests, providing common setup like an in-memory database and mocked services.
/// </summary>
public abstract class IntegrationTestBase(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    protected readonly WebApplicationFactory<Program> _factory = factory;

    protected static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleEnumConverterFactory() },
    };

    /// <summary>
    /// Creates a WebApplicationFactory configured for a specific test run, ensuring all
    /// created clients and service scopes share the same in-memory database.
    /// </summary>
    /// <param name="dbName">The unique name for the in-memory database.</param>
    /// <param name="testServicesConfiguration">An action to configure test-specific services, like mocks.</param>
    /// <returns>A configured WebApplicationFactory.</returns>
    protected WebApplicationFactory<Program> CreateConfiguredWebAppFactory(
        string dbName,
        Action<IServiceCollection>? testServicesConfiguration = null
    )
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration(
                (context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            { "Google:ClientId", "dummy-client-id" },
                            { "Google:ClientSecret", "dummy-client-secret" },
                        }
                    );
                }
            );

            builder.ConfigureServices(services =>
            {
                // silence logging during tests
                services.RemoveAll<ILoggerFactory>();
                services.AddSingleton<ILoggerFactory>(new NullLoggerFactory());

                // Allow for additional, test-specific service configurations.
                testServicesConfiguration?.Invoke(services);

                // Use a single, consistent in-memory database for the entire test.
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options
                        .UseInMemoryDatabase(dbName)
                        .ConfigureWarnings(warnings =>
                        {
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                        });
                });
            });
        });
    }

    /// <summary>
    /// Create a test HttpClient with a mocked service and in-memory database.
    /// </summary>
    protected HttpClient CreateTestClient(
        Action<IServiceCollection>? testServicesConfiguration = null
    )
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration(
                    (context, configBuilder) =>
                    {
                        configBuilder.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                { "Google:ClientId", "dummy-client-id" },
                                { "Google:ClientSecret", "dummy-client-secret" },
                            }
                        );
                    }
                );

                builder.ConfigureServices(services =>
                {
                    // silence logging during tests
                    services.RemoveAll<ILoggerFactory>();
                    services.AddSingleton<ILoggerFactory>(new NullLoggerFactory());

                    // mock specific services for the test as necessary
                    testServicesConfiguration?.Invoke(services);

                    // mock real DbContext using new in-memory database
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options
                            .UseInMemoryDatabase($"InMemoryTestDb_{Guid.NewGuid()}")
                            .ConfigureWarnings(warnings =>
                            {
                                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                            });
                    });
                });
            })
            .CreateClient();
    }

    /// <summary>
    /// Helper to create an HttpClient configured to use a specific IRoomSSEService instance.
    /// </summary>
    protected HttpClient CreateSSEClientWithMocks(
        IRoomSSEService sseService,
        Action<IServiceCollection>? testServicesConfiguration = null
    )
    {
        return CreateTestClient(services =>
        {
            services.RemoveAll<IRoomSSEService>();
            services.AddSingleton(sseService);

            services.AddScoped(_ => Mock.Of<IRoomService>());

            // mock specific services for the test as necessary
            testServicesConfiguration?.Invoke(services);
        });
    }
}
