using CalendarManager.Application.Common.Interfaces;
using CalendarManager.Infrastructure.BackgroundJobs;
using CalendarManager.Infrastructure.CalendarProviders;
using CalendarManager.Infrastructure.Data;
using CalendarManager.Infrastructure.Data.Interceptors;
using CalendarManager.Infrastructure.Identity;
using CalendarManager.Infrastructure.Push;
using CalendarManager.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.EnrichSqlServerDbContext<ApplicationDbContext>();

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.Services.AddAuthorizationBuilder();

        builder.Services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddApiEndpoints();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();

        var dataProtectionBuilder = builder.Services.AddDataProtection();

        // Encrypts the Data Protection keyring itself with an Azure Key Vault key, so the OAuth
        // refresh tokens it protects (see DataProtectionRefreshTokenProtector) are encrypted at
        // rest via Key Vault, per the PRD's security requirement. No-ops when unconfigured
        // (local dev), falling back to the default on-disk keyring.
        var dataProtectionKeyUri = builder.Configuration["AZURE_DATA_PROTECTION_KEY_URI"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeyUri))
        {
            dataProtectionBuilder.ProtectKeysWithAzureKeyVault(new Uri(dataProtectionKeyUri), new global::Azure.Identity.DefaultAzureCredential());
        }

        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<IRefreshTokenProtector, DataProtectionRefreshTokenProtector>();
        builder.Services.AddSingleton<IOAuthStateStore, MemoryCacheOAuthStateStore>();
        builder.Services.AddScoped<ICalendarProviderClientFactory, CalendarProviderClientFactory>();

        builder.Services.Configure<GoogleCalendarOptions>(builder.Configuration.GetSection(GoogleCalendarOptions.SectionName));
        builder.Services.Configure<OutlookCalendarOptions>(builder.Configuration.GetSection(OutlookCalendarOptions.SectionName));

        builder.Services.AddHttpClient<GoogleCalendarClient>();
        builder.Services.AddScoped<ICalendarProviderClient>(sp => sp.GetRequiredService<GoogleCalendarClient>());

        builder.Services.AddHttpClient<OutlookCalendarClient>();
        builder.Services.AddScoped<ICalendarProviderClient>(sp => sp.GetRequiredService<OutlookCalendarClient>());

        builder.Services.Configure<WebPushOptions>(builder.Configuration.GetSection(WebPushOptions.SectionName));
        builder.Services.AddSingleton<IPushNotificationService, WebPushNotificationService>();

        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, EmailSender>();

        builder.Services.AddQuartz(q =>
        {
            var syncJobKey = new JobKey(nameof(CalendarSyncJob));
            q.AddJob<CalendarSyncJob>(opts => opts.WithIdentity(syncJobKey));
            q.AddTrigger(opts => opts
                .ForJob(syncJobKey)
                .WithIdentity($"{nameof(CalendarSyncJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(10).RepeatForever()));

            var reminderJobKey = new JobKey(nameof(ReminderDispatchJob));
            q.AddJob<ReminderDispatchJob>(opts => opts.WithIdentity(reminderJobKey));
            q.AddTrigger(opts => opts
                .ForJob(reminderJobKey)
                .WithIdentity($"{nameof(ReminderDispatchJob)}-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever()));
        });
        builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);
    }
}
