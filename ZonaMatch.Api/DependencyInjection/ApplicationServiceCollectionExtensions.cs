using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Aportes;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Application.UseCases.Contact;
using ZonaMatch.Application.UseCases.Criterios;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Application.UseCases.GuestReports;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Application.UseCases.GroupLinks;
using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Application.UseCases.Notifications;
using ZonaMatch.Application.UseCases.PasswordResets;
using ZonaMatch.Application.UseCases.Perfil;
using ZonaMatch.Application.UseCases.Preguntas;
using ZonaMatch.Application.UseCases.Puntajes;
using ZonaMatch.Application.UseCases.Resenas;
using ZonaMatch.Application.UseCases.Routes;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Application.UseCases.SavedZones;
using ZonaMatch.Application.UseCases.ZoneRecommendations;
using ZonaMatch.Application.UseCases.Zonas;

namespace ZonaMatch.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<GetZoneProfileRecommendations>();
        services.AddScoped<GetZoneMarket>();
        services.AddScoped<SearchPlaces>();
        services.AddScoped<IGeolocationService, GeolocationService>();
        services.AddScoped<IMapaService, MapaService>();
        services.AddScoped<IZonaService, ZonaService>();

        services.AddScoped<CreateGroup>();
        services.AddScoped<GetGroupDetails>();
        services.AddScoped<CalcularPuntajeEnGrupo>();
        services.AddScoped<GuardarAnalisisEnGrupo>();
        services.AddScoped<GetGroups>();
        services.AddScoped<DeleteGroup>();
        services.AddScoped<UpdateGroup>();
        services.AddScoped<RemoveParticipant>();

        services.AddScoped<GetNotifications>();
        services.AddScoped<MarkNotificationAsRead>();
        services.AddScoped<MarkAllNotificationsAsRead>();

        services.AddScoped<CreateInvitation>();
        services.AddScoped<GetInvitationByToken>();
        services.AddScoped<AcceptInvitation>();
        services.AddScoped<GetGroupByLink>();
        services.AddScoped<GetGroupInviteLink>();
        services.AddScoped<JoinGroupByLink>();
        services.AddScoped<RevokeInviteLink>();

        services.AddScoped<RequestPasswordReset>();
        services.AddScoped<CompletePasswordReset>();
        services.AddScoped<LoginUser>();
        services.AddScoped<RegisterUser>();

        services.AddScoped<ListarResenas>();
        services.AddScoped<GuardarMiResena>();
        services.AddScoped<EliminarMiResena>();
        services.AddScoped<MarcarResenaUtil>();
        services.AddScoped<ListarPreguntas>();
        services.AddScoped<CrearPregunta>();
        services.AddScoped<ResponderPregunta>();
        services.AddScoped<ResumirAportes>();
        services.AddScoped<ListarAportesPendientes>();
        services.AddScoped<CrearPuntoNuevo>();
        services.AddScoped<CrearCorreccion>();
        services.AddScoped<ValidarAporte>();
        services.AddScoped<CommunityPoiUpdater>();
        services.AddScoped<ResolveContribution>();

        services.AddScoped<ListarCriterios>();
        services.AddScoped<ObtenerMiPerfil>();
        services.AddScoped<GuardarMiPerfil>();

        services.AddScoped<ListMyAnalyses>();
        services.AddScoped<GetAnalysisById>();
        services.AddScoped<CompareAnalyses>();
        services.AddScoped<CreateAnalysis>();
        services.AddScoped<CreateAnalysisVersion>();
        services.AddScoped<IAnalysisResultCalculator, AnalysisResultCalculator>();
        services.AddScoped<RecalculateAnalysis>();
        services.AddScoped<SetAnalysisFavorite>();
        services.AddScoped<RenameAnalysis>();
        services.AddScoped<DeleteAnalysis>();
        services.AddScoped<ListSavedZones>();
        services.AddScoped<SaveZone>();
        services.AddScoped<RemoveSavedZone>();

        services.AddScoped<CalcularPuntajes>();
        services.AddScoped<GetRoute>();
        services.AddScoped<ObtenerIndicadoresZona>();
        services.AddScoped<ObtenerTransporteZona>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new FrontendLinks(configuration["Frontend:BaseUrl"] ?? "http://localhost:5173"));
        services.AddSingleton(new ContactOptions(
            configuration["Contact:RecipientEmail"]
                ?? configuration["Contacto:Destinatario"]
                ?? "zonamatchtpi@gmail.com"));
        services.AddScoped<SendContactMessage>();
        services.AddSingleton<GuestReportQuota>();
        services.AddScoped<SendGuestReport>();

        return services;
    }
}
