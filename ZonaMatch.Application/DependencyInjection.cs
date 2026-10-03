using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.Analysis;
using ZonaMatch.Application.Ingestion;
using ZonaMatch.Application.PointsOfInterest;
using ZonaMatch.Application.Territory;

namespace ZonaMatch.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddSingleton(TimeProvider.System);

            services.AddScoped<TerritorialImportService>();
            services.AddScoped<ZoneBuildService>();
            services.AddScoped<PoiImportService>();
            services.AddScoped<PoiDeduplicationService>();

            services.AddScoped<IndicatorService>();
            services.AddScoped<ZoneQueryService>();
            services.AddScoped<PoiQueryService>();
            services.AddScoped<AnalysisService>();

            return services;
        }
    }
}
