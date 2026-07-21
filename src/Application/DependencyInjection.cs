using System.Reflection;
using CalendarManager.Application.Common.Behaviours;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddAutoMapper(cfg => 
            cfg.AddMaps(Assembly.GetExecutingAssembly()));

        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Services.AddMediatR(cfg => {
            cfg.LicenseKey = "eyJhbGciOiJSUzI1NiIsImtpZCI6Ikx1Y2t5UGVubnlTb2Z0d2FyZUxpY2Vuc2VLZXkvYmJiMTNhY2I1OTkwNGQ4OWI0Y2IxYzg1ZjA4OGNjZjkiLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJodHRwczovL2x1Y2t5cGVubnlzb2Z0d2FyZS5jb20iLCJhdWQiOiJMdWNreVBlbm55U29mdHdhcmUiLCJleHAiOiIxODE2MTI4MDAwIiwiaWF0IjoiMTc4NDY0ODg1MSIsImFjY291bnRfaWQiOiIwMTlmODU1YmM0YmE3NzEwYTZkZjdiYjA1MWMwZThjYyIsImN1c3RvbWVyX2lkIjoiMDE5Zjg1NWJjNGJhNzcxMGE2ZGY3YmIwNTFjMGU4Y2MiLCJzdWJfaWQiOiItIiwiZWRpdGlvbiI6IjAiLCJ0eXBlIjoiMiJ9.Qud78QB9Q9eXZtrJ6KgRZZj6zN_a5txeQWuxsJ71aC0g1wkCr_Z5heUUOnOeeRTB0gyuyun5xFQ-SfwDB-F4MlZ01psZBtwRGIn1ahyEvvVqfU971oeGw3zrfQmvOLrzWttS-C3p_sWjqLroT7q9XbnBJ_3jANKRem9kwlyXG8nRaGVGZAM2KEfTdcYUPP54ygcblu-lJbu_ZQHSuTmZ1574aEseOf6wOeorP7troBxhj1qE_jF6nC2FE3zpPGlK-8xnUKaUGVdID7IUNFRehW8JeOGSpBpW-Jseg757826ZXBsHn9p27EzC_SHUaGvika7d7vSnWWWdVHFTWmy3hg";
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
        });
    }
}
