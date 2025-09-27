using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using ParadeGuard.Api.Config;
using ParadeGuard.Api.Middleware;
using ParadeGuard.Api.Models;
using ParadeGuard.Api.Services;
using ParadeGuard.Api.Services.Interfaces;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Serilog;
using System.IO.Compression;

namespace ParadeGuard.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Enhanced Serilog configuration with correlation IDs
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", "ParadeGuard.Api")
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .CreateBootstrapLogger();

            builder.Host.UseSerilog((ctx, lc) => lc
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", "ParadeGuard.Api")
            );

            // Configuration with validation
            builder.Services.Configure<ApiKeysConfig>(builder.Configuration.GetSection("ApiKeys"));
            builder.Services.Configure<CacheConfig>(builder.Configuration.GetSection("Cache"));
            builder.Services.Configure<ApiConfig>(builder.Configuration.GetSection("ApiSettings"));
            builder.Services.Configure<MonitoringConfig>(builder.Configuration.GetSection("Monitoring"));

            // Validate critical configuration and read from environment variables
            var apiKeysSection = builder.Configuration.GetSection("ApiKeys");
            var geocodingKey = apiKeysSection["GeocodingApiKey"] ??
                              Environment.GetEnvironmentVariable("GeocodingApiKey");

            var nasaKey = apiKeysSection["NasaApiKey"] ??
                         Environment.GetEnvironmentVariable("NasaApiKey");

            // Validate both required API keys
            var missingKeys = new List<string>();

            if (string.IsNullOrWhiteSpace(geocodingKey))
                missingKeys.Add("GeocodingApiKey");

            if (string.IsNullOrWhiteSpace(nasaKey))
                missingKeys.Add("NasaApiKey");

            if (missingKeys.Any())
            {
                var errorMessage = $"Missing required API keys: {string.Join(", ", missingKeys)}";
                Log.Fatal(errorMessage + ". Please ensure both API keys are configured.");

                Console.WriteLine("\n=== MISSING REQUIRED API KEYS ===");
                foreach (var key in missingKeys)
                {
                    Console.WriteLine($"Missing: {key}");
                }
                Console.WriteLine("Both API keys are required for this application to function properly.");
                Console.WriteLine("Please set them using environment variables or configuration files.");
                Console.WriteLine("================================\n");

                throw new InvalidOperationException($"Missing required API keys: {string.Join(", ", missingKeys)}");
            }

            // Override configuration with environment variables if they exist
            builder.Services.Configure<ApiKeysConfig>(options =>
            {
                options.GeocodingApiKey = geocodingKey;
                options.NasaApiKey = nasaKey;
            });

            Log.Information("Both required API keys configured successfully - Geocoding: ?, NASA: ?");

            // Response compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/json" });
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            // Enhanced memory cache with size limits
            builder.Services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1000;
                options.CompactionPercentage = 0.25;
            });

            // Simplified Polly policies - Fixed the circuit breaker issue
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        Log.Warning("Retry {RetryCount} for {PolicyKey} after {Timespan}ms. Reason: {Reason}",
                            retryCount, context.OperationKey, timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase ?? "Unknown");
                    });

            // HTTP Clients with policies
            builder.Services.AddHttpClient<IGeocodingService, GeocodingService>()
                .AddPolicyHandler(retryPolicy)
                .SetHandlerLifetime(TimeSpan.FromMinutes(10))
                .ConfigureHttpClient(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ParadeGuard-API/1.0");
                    client.Timeout = TimeSpan.FromSeconds(30);
                });

            builder.Services.AddHttpClient<INasaWeatherService, NasaWeatherService>()
                .AddPolicyHandler(retryPolicy)
                .SetHandlerLifetime(TimeSpan.FromMinutes(10))
                .ConfigureHttpClient(client =>
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "ParadeGuard-API/1.0");
                    client.Timeout = TimeSpan.FromSeconds(45);
                });

            builder.Services.AddHttpClient<ExternalServiceHealthCheck>()
                .AddPolicyHandler(retryPolicy)
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            // Register Services
            builder.Services.AddScoped<IGeocodingService, GeocodingService>();
            builder.Services.AddScoped<INasaWeatherService, NasaWeatherService>();
            builder.Services.AddSingleton<IProbabilityCalculator, ProbabilityCalculator>();

            // Enhanced Controllers with model validation
            builder.Services.AddControllers(options =>
            {
                options.ModelValidatorProviders.Clear();
                options.MaxModelValidationErrors = 10;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage))
                        .ToList();

                    return new BadRequestObjectResult(new ApiError
                    {
                        Message = "Validation failed",
                        Details = string.Join("; ", errors),
                        RequestId = context.HttpContext.TraceIdentifier
                    });
                };
            });

            // Enhanced API Documentation - Fixed Swagger configuration
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ParadeGuard Weather API",
                    Version = "v1.0",
                    Description = "Advanced weather prediction API for outdoor events using historical NASA POWER data",
                    Contact = new OpenApiContact
                    {
                        Name = "ParadeGuard Support",
                        Email = "support@paradeguard.com"
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // Add security definition for future API key authentication
                c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "API Key needed to access the endpoints",
                    In = ParameterLocation.Header,
                    Name = "X-API-Key",
                    Type = SecuritySchemeType.ApiKey
                });

                // Include XML comments
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

            });

            // Comprehensive Health Checks
            builder.Services.AddHealthChecks()
                .AddCheck<ExternalServiceHealthCheck>("external-services")
                .AddCheck("memory-cache", () =>
                {
                    try
                    {
                        var cache = builder.Services.BuildServiceProvider().GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                        return cache != null
                            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
                            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Memory cache not available");
                    }
                    catch (Exception ex)
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Memory cache error", ex);
                    }
                })
                .AddCheck("configuration", () =>
                {
                    var config = builder.Configuration;
                    var required = new[] { "ApiKeys:GeocodingApiKey", "ApiSettings:NasaBaseUrl", "ApiSettings:OpenCageBaseUrl" };

                    foreach (var key in required)
                    {
                        if (string.IsNullOrWhiteSpace(config[key]))
                        {
                            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"Missing configuration: {key}");
                        }
                    }

                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
                });

            // Enhanced Rate Limiting
            builder.Services.AddRateLimiter(options =>
            {
                // General API rate limiting
                options.AddFixedWindowLimiter("ApiPolicy", limiterOptions =>
                {
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.PermitLimit = 60;
                    limiterOptions.QueueLimit = 20;
                    limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                });

                // Stricter rate limiting for expensive operations
                options.AddFixedWindowLimiter("WeatherCheckPolicy", limiterOptions =>
                {
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.PermitLimit = 30;
                    limiterOptions.QueueLimit = 10;
                    limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                });

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, token) =>
                {
                    Log.Warning("Rate limit exceeded for {IP} on {Path}",
                        context.HttpContext.Connection.RemoteIpAddress,
                        context.HttpContext.Request.Path);

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(new ApiError
                        {
                            Message = "Rate limit exceeded. Please try again later.",
                            RequestId = context.HttpContext.TraceIdentifier
                        }), token);
                };
            });

            // Enhanced CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("DefaultPolicy", policy =>
                {
                    var allowedOrigins = builder.Configuration.GetSection("ApiSettings:AllowedOrigins").Get<string[]>()
                        ?? new[] { "https://localhost:3000", "https://paradeguard.com" };

                    policy.WithOrigins(allowedOrigins)
                          .WithMethods("GET", "POST", "OPTIONS")
                          .WithHeaders("Content-Type", "Authorization", "X-API-Key")
                          .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
                          .AllowCredentials();
                });

                // Development policy for local testing
                if (builder.Environment.IsDevelopment())
                {
                    options.AddPolicy("DevelopmentPolicy", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                }
            });

            var app = builder.Build();

            // Enhanced Middleware Pipeline
            app.UseResponseCompression();
            app.UseMiddleware<ErrorHandlingMiddleware>();

            // Security headers
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
                app.Use(async (context, next) =>
                {
                    context.Response.Headers.Add("X-Frame-Options", "DENY");
                    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                    await next();
                });
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ParadeGuard API v1");
                    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
                });
                app.UseCors("DevelopmentPolicy");
            }
            else
            {
                app.UseCors("DefaultPolicy");
            }

            app.UseHttpsRedirection();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();

            // Enhanced health checks endpoint
            app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    var response = new
                    {
                        status = report.Status.ToString(),
                        totalDuration = report.TotalDuration.TotalMilliseconds,
                        entries = report.Entries.Select(e => new
                        {
                            name = e.Key,
                            status = e.Value.Status.ToString(),
                            duration = e.Value.Duration.TotalMilliseconds,
                            description = e.Value.Description,
                            exception = e.Value.Exception?.Message
                        })
                    };
                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                }
            });

            try
            {
                Log.Information("Starting ParadeGuard API on {Environment}", app.Environment.EnvironmentName);
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}