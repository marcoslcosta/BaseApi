﻿using AutoMapper;
using ImprovedApi.Api.Midlewares;
using ImprovedApi.Api.Security.Token;
using ImprovedApi.Infra.Loggers;
using ImprovedApi.Infra.Transactions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ImprovedApi.Api
{
    public abstract class ImprovedStartup
    {

        protected bool SwaggerEnabled { get; set; } = true;
        protected bool AuthenticationEnabled { get; set; } = false;

        protected readonly IConfiguration Configuration;
        protected List<string> AssembliesMidiatR { get; private set; } = new List<string>();
        protected List<Profile> AutoMapperProfiles { get; private set; } = new List<Profile>();
        protected readonly IHostingEnvironment HostingEnvironment;
        protected readonly ILogger<ImprovedStartup> Logger;
        protected IMvcBuilder _mvcBuilder;

        public ImprovedStartup(IConfiguration configuration, ILogger<ImprovedStartup> logger, ILoggerFactory logFactory, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out, "ImprovedApi Log"));
            Logger = logger;
            HostingEnvironment = hostingEnvironment;
            ApplicationLogging.LoggerFactory = logFactory;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {

            if (AuthenticationEnabled && Configuration.GetSection("TokenConfiguration") != null)
            {
                services.AddOptions();
                services.Configure<TokenConfiguration>(options => Configuration.GetSection("TokenConfiguration").Bind(options));
                services.AddSingleton<SigningConfigurations, SigningConfigurations>();

                AddAuthentication(services);
                AddAuthorization(services);
            }
            else
            {
                ImprovedLogger.Write(@"if you wish use token/authentication, please add 'TokenConfiguration' Section in your appsettings.json with properties as follows:
                                      'SecretKey', 'Audience', 'Issuer', 'Seconds'");
            }

            services.AddSingleton<IImprovedUnitOfWork, ImprovedUnitOfWork>()
                .AddSingleton(Configuration);


            AddMediatR(services);
            AddAutoMapper(services);

            if (SwaggerEnabled)
            {
                AddSwagger(services);
            }

            if (AuthenticationEnabled && Configuration.GetSection("TokenConfiguration") != null)
            {
                _mvcBuilder = services.AddMvc(config =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme‌​)
                        .RequireAuthenticatedUser().Build();

                    config.Filters.Add(new AuthorizeFilter(policy));
                });
            }
            else
            {
                _mvcBuilder = services.AddMvc();
            }

            MvcJsonOptions(_mvcBuilder);
        }

        public virtual void MvcJsonOptions(IMvcBuilder mvcBuilder)
        {
            _mvcBuilder
            .AddJsonOptions(options =>
            {
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            });
        }


        public virtual void AddAuthentication(IServiceCollection services)
        {

            var sp = services.BuildServiceProvider();
            var signingConfigurations = sp.GetService<SigningConfigurations>();
            var tokenConfigurations = sp.GetService<IOptions<TokenConfiguration>>();

            services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(bearerOptions =>
            {
                var paramsValidation = bearerOptions.TokenValidationParameters;
                paramsValidation.IssuerSigningKey = signingConfigurations.SigningCredentials.Key;
                paramsValidation.ValidAudience = tokenConfigurations.Value.Audience;
                paramsValidation.ValidIssuer = tokenConfigurations.Value.Issuer;
                paramsValidation.ValidateIssuerSigningKey = true;
                paramsValidation.ValidateLifetime = true;
                paramsValidation.ClockSkew = TimeSpan.Zero;
            });
        }

        public virtual void AddAuthorization(IServiceCollection services)
        {
            services.AddAuthorization(auth =>
            {
                auth.AddPolicy("Bearer", new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme‌​)
                    .RequireAuthenticatedUser().Build());
            });
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public virtual void Configure(IApplicationBuilder app)
        {
            if (SwaggerEnabled)
            {
                UseSwagger(app);
            }

            app.UseMiddleware(typeof(ErrorHandlingMiddleware));

            app.UseMvc();
        }

        #region MediatR
        protected virtual void AddMediatR(IServiceCollection services)
        {
            if (AssembliesMidiatR.Any())
            {
                services.AddMediatR(AssembliesMidiatR.Select(p => AppDomain.CurrentDomain.Load(p)).ToArray());
            }
            else
            {
                ImprovedLogger.Write("Please, inform the 'AssembliesMidiatR' inside contructor Startup class!");
            }


        }
        #endregion

        #region AutoMapper
        protected virtual void AddAutoMapper(IServiceCollection services)
        {
            if (AutoMapperProfiles.Any())
            {
                Mapper.Initialize(cfg =>
                {
                    AutoMapperProfiles.ForEach(p => cfg.AddProfile(p));
                });
            }

        }
        #endregion

        #region Swagger
        protected virtual void AddSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                try
                {
                    c.SwaggerDoc("v1", new Info
                    {
                        Version = "v1",
                        Title = "Improved Api",
                        Description = "An Example how to improve your api",
                        TermsOfService = "None",
                        Contact = new Contact() { Name = "Improved API", Url = "https://github.com/marcoslcosta/ImprovedApi" }
                    });
                    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                    c.IncludeXmlComments(xmlPath);
                    c.CustomSchemaIds(x => x.FullName);
                    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

                    if (AuthenticationEnabled)
                    {
                        c.AddSecurityDefinition("Bearer", new ApiKeyScheme { In = "header", Description = "Please enter JWT with Bearer into field", Name = "Authorization", Type = "apiKey" });
                        c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
                { "Bearer", Enumerable.Empty<string>() },
            });
                    }
                }
                catch (Exception ex)
                {
                    ImprovedLogger.Write($@"Please add {Path.Combine(AppContext.BaseDirectory, Assembly.GetExecutingAssembly().GetName().Name)}.xml in your project. \n
                        Go to properties -> build -> select checkbox 'XML documentation file' ");

                }


            });
        }

        public virtual void UseSwagger(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Improved Api");
            });
        }

        #endregion

        #region Cookies
        protected virtual void AddCookiePolicy(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
        }
        #endregion
    }
}
