using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Events;
using Slamby.API.Filters;
using Slamby.API.Helpers;
using Slamby.API.Helpers.Swashbuckle;
using Slamby.API.Middlewares;
using Slamby.Common.Config;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories;
using StackExchange.Redis;
using Swashbuckle.Swagger.Model;

namespace Slamby.API
{
    public class Startup
    {
        #region Local variables 

        public IConfigurationRoot Configuration { get; set; }


        public ILoggerFactory LoggerFactory { get; private set; }

        public SiteConfig SiteConfig { get; set; } = new SiteConfig();

        public string ApiVersion { get; set; } = VersionHelper.GetProductVersion(typeof(Startup));

        #endregion

        private IHostingEnvironment CurrentEnvironment { get; set; }

        public Startup(IHostingEnvironment env)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Configuration.GetSection("SlambyApi").Bind(SiteConfig);

            StartupLogger(env);

            CurrentEnvironment = env;
        }

        #region Startup

        private void StartupLogger(IHostingEnvironment env)
        {
            var minimumLevel = Configuration.GetEnumValue("SlambyApi:Serilog:MinimumLevel", LogEventLevel.Debug);
            var retainedFileCountLimit = Configuration.GetValue("SlambyApi:Serilog:RetainedFileCountLimit", 7);
            var output = Configuration["SlambyApi:Serilog:Output"];

            var loggerConfiguration = new LoggerConfiguration()
               .MinimumLevel.Is(minimumLevel)
               .Enrich.FromLogContext();
            if (env.IsDevelopment()) loggerConfiguration.WriteTo.LiterateConsole(minimumLevel);

            if (!string.IsNullOrWhiteSpace(output))
            {
                if (!Path.IsPathRooted(output))
                {
                    output = Path.GetFullPath(Path.Combine(env.ContentRootPath, output));
                }

                loggerConfiguration.WriteTo.RollingFile(
                        Path.Combine(output, "log-{Date}.txt"),
                        restrictedToMinimumLevel: minimumLevel,
                        retainedFileCountLimit: retainedFileCountLimit,
                        fileSizeLimitBytes: null);
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }

        #endregion

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                if (!string.IsNullOrEmpty(Configuration.GetValue("SlambyApi:Elm:Key", string.Empty)))
                {
                    services.AddElm(options =>
                    {
                        options.Path = new PathString("/elm");
                        options.Filter = (name, level) => level >= LogLevel.Information;
                    });
                }

                ConfigureDataProtection(services);
                ConfigureMvc(services);
                if (CurrentEnvironment.IsDevelopment()) ConfigureSwagger(services);
                ConfigureOptions(services);
                ConfigureDependencies(services);
                ConfigureResourceDependentVars(services);
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "Fatal error occured at ConfigureServices");
                throw;
            }
        }

        #region ConfigureServices

        private void ConfigureDataProtection(IServiceCollection services)
        {
            var keysPath = Path.Combine(Configuration.GetValue("SlambyApi:Directory:Sys", "/Slamby/Sys"), "Keys");
            services.AddDataProtection()
                .SetApplicationName("Slamby.API")
                .SetDefaultKeyLifetime(TimeSpan.FromDays(180))
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .UseCryptographicAlgorithms(new AuthenticatedEncryptionSettings()
                {
                    EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
                    ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
                });
        }

        private void ConfigureOptions(IServiceCollection services)
        {
            // Required to use the Options<T> pattern
            services.AddOptions();

            // Add settings from configuration
            services.Configure<SiteConfig>(Configuration.GetSection("SlambyApi"));
            services.Configure<SiteConfig>(sc => sc.Version = this.ApiVersion);
        }
        
        private void ConfigureDependencies(IServiceCollection services)
        {
            //see: https://github.com/aspnet/Hosting/issues/793
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.AddSingleton(sp => sp.GetService<IOptions<SiteConfig>>().Value);
            services.AddTransient(sp => ElasticClientFactory.GetClient(sp));

            // Add DependencyAttribute (ScopedDependency, SingletonDependency, TransientDependency) to the class in order to be scanned
            services.ConfigureAttributedDependencies();

            {
                var options = ConfigurationOptions.Parse(Configuration["SlambyApi:Redis:Configuration"]);
                //HACK: https://github.com/dotnet/corefx/issues/8768
                //this should be removed when https://github.com/dotnet/corefx/issues/11564 is closed
                options = RedisDnsHelper.CorrectOption(options);
                if (options == null)
                {
                    Log.Logger.Error("Can't resolve the name of the Redis server!");
                    return;
                }
                services.AddSingleton(RedisDnsHelper.CorrectOption(options));
                services.AddSingleton<ConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(sp.GetService<ConfigurationOptions>()));
            }
        }

        private void ConfigureSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen();
            services.ConfigureSwaggerGen(options =>
            {
                options.SingleApiVersion(new Info
                {
                    Version = ApiVersion,
                    Title = "Slamby API",
                    Description = "Slamby API",
                    TermsOfService = "None"
                });
                //options.OrderActionGroupsBy(new SwaggerGroupNameComparer());

                options.GroupActionsBy(SwaggerHelper.GetActionGroup);

                //options.SecurityDefinitions.Add(new KeyValuePair<string, SecurityScheme>("Slamby", new ApiKeyScheme() { Type = "apiKey", In = "header", Name = "api_secret", Description = "Http authentication. Ex: Authorization: Slamby <apisecret>" }));
                //options.DocumentFilter(new SwaggerSchemaDocumentFilter(new List<string> { "http" }, "localhost:29689", "/"));
                options.DescribeAllEnumsAsStrings();

                //TODO: check this
                options.SchemaFilter<RegexSchemaFilter>();
                options.OperationFilter<ApplySwaggerResponseFilterAttributesOperationFilter>();

                var sdkXmlDocumentation = typeof(SDK.Net.ApiClient)
                    .GetTypeInfo()
                    .Assembly
                    .Location
                    .Replace(".dll", ".xml");
                options.IncludeXmlComments(sdkXmlDocumentation);
            });
        }

        private void ConfigureMvc(IServiceCollection services)
        {
            var mvcBuilder = services.AddMvc();

            //if (!HostingEnvironment.IsDevelopment())
            //{
            //    mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(SdkVersionFilter)));
            //}
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(ModelValidationFilter)));
            mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(GlobalExceptionFilter)));
            services.AddScoped<DiskSpaceLimitFilter>();

            if (SiteConfig.Stats.Enabled)
            {
                mvcBuilder.AddMvcOptions(o => o.Filters.Add(typeof(ThrottleActionFilter)));
            }

            mvcBuilder.AddJsonOptions(o =>
            {
                o.SerializerSettings.Converters.Add(new StringEnumConverter());
                //HACK: see https://github.com/aspnet/Mvc/issues/4842
                o.SerializerSettings.ContractResolver = new DefaultContractResolver();
            });

            services.AddRouting(routeOptions =>
            {
                routeOptions.AppendTrailingSlash = true;
                routeOptions.LowercaseUrls = true;
            });

            services.AddMemoryCache();

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.CookieName = ".SlambyAPI";
            });

            services.AddCors();
        }

        private void ConfigureResourceDependentVars(IServiceCollection services)
        {
            var maxIndexBulkSize = Configuration.GetValue("SlambyApi:Resources:MaxIndexBulkSize", 0);
            var maxIndexBulkCount = Configuration.GetValue("SlambyApi:Resources:MaxIndexBulkCount", 0);
            var maxSearchBulkCount = Configuration.GetValue("SlambyApi:Resources:MaxSearchBulkCount", 0);

            if (maxIndexBulkSize == 0 || maxIndexBulkCount == 0 || maxSearchBulkCount == 0)
            {
                var machineResourceService = services.BuildServiceProvider().GetService<Common.Services.MachineResourceService>();
                machineResourceService.UpdateResourcesManually();

                if (machineResourceService.Status.TotalMemory > 0)
                {
                    maxIndexBulkSize = Convert.ToInt32(machineResourceService.Status.TotalMemory * 1024 * 1024 / 2 / 500);
                    maxIndexBulkCount = Convert.ToInt32(machineResourceService.Status.TotalMemory / 12);
                }
                else
                {
                    maxIndexBulkSize = 100000;
                    maxIndexBulkCount = 50;
                }

                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkSize = maxIndexBulkSize);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxIndexBulkCount = maxIndexBulkCount);
                services.Configure<SiteConfig>(sc => sc.Resources.MaxSearchBulkCount = maxIndexBulkCount);
            }
        }

        #endregion

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, StartupService startupService)
        {
            loggerFactory.AddSerilog();

            if (env.IsDevelopment())
            {
                loggerFactory.AddConsole(Configuration.GetSection("Logging"));
                loggerFactory.AddDebug();
                app.UseDeveloperExceptionPage();
            }

            if (!string.IsNullOrEmpty(SiteConfig.BaseUrlPrefix))
            {
                app.UsePathBase();
            }

            app.UseSession();

            app.UseSecretValidator();
            app.UseRequestSizeLimit();

            if (!string.IsNullOrEmpty(Configuration.GetValue("SlambyApi:Elm:Key", string.Empty)))
            {
                app.UseElmSecurity();
                app.UseElmStyleUrlFix();
                app.UseElmPage(); // Shows the logs at the specified path
                app.UseElmCapture(); // Adds the ElmLoggerProvider
            }

            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());

            var logger = loggerFactory.CreateLogger<Startup>();

            try
            {
                startupService.Startup();

                if (env.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUi("swagger/ui", $"/swagger/{ApiVersion}/swagger.json");
                }

                app.UseGzip();

                app.UseRequestLogger();

                // Set up custom content types - associating file extension to MIME type
                var provider = new FileExtensionContentTypeProvider();
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(SiteConfig.Directory.User),
                    RequestPath = new PathString(Common.Constants.FilesPath),
                    ContentTypeProvider = provider
                });
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(Path.Combine(env.WebRootPath, "assets")),
                    RequestPath = new PathString("/assets"),
                    ContentTypeProvider = provider
                });

                app.UseApiHeaderVersion();
                app.UseApiHeaderAuthentication();
                app.UseElapsedTime();
                app.UseMvc();

                app.UseNotFound();
                app.UseTerminal();
            }
            catch (Exception ex)
            {
                app.WriteExceptionResponse(logger, ex, "Startup Runtime Error");
            }
        }
    }
}