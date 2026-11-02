using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Contributions;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Application.UseCases.Contact;
using ZonaMatch.Application.UseCases.Criteria;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Application.UseCases.GuestReports;
using ZonaMatch.Application.UseCases.GroupInvitations;
using ZonaMatch.Application.UseCases.GroupLinks;
using ZonaMatch.Application.UseCases.Groups;
using ZonaMatch.Application.UseCases.Map;
using ZonaMatch.Application.UseCases.Notifications;
using ZonaMatch.Application.UseCases.PasswordResets;
using ZonaMatch.Application.UseCases.Profile;
using ZonaMatch.Application.UseCases.Questions;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Application.UseCases.Reviews;
using ZonaMatch.Application.UseCases.Routes;
using ZonaMatch.Application.UseCases.SavedAnalyses;
using ZonaMatch.Application.UseCases.SavedZones;
using ZonaMatch.Application.UseCases.ZoneRecommendations;
using ZonaMatch.Application.UseCases.Zones;

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
        services.AddScoped<PointOfInterestResponseBuilder>();
        services.AddScoped<LocationBoundaryLookup>();
        services.AddScoped<GetLocationSummary>();
        services.AddScoped<GetNearbySchools>();
        services.AddScoped<GetNearestSchools>();
        services.AddScoped<GetNearbyPointsOfInterest>();
        services.AddScoped<GetPointsOfInterestSummary>();
        services.AddScoped<GetDistrictByLocation>();
        services.AddScoped<GetCommuneByLocation>();
        services.AddScoped<ReverseGeocode>();
        services.AddScoped<SearchZones>();
        services.AddScoped<GetZone>();
        services.AddScoped<GetZonePointsOfInterest>();
        services.AddScoped<GetZonePointsOfInterestSummary>();
        services.AddScoped<GetMapDistricts>();
        services.AddScoped<GetMapCommunes>();
        services.AddScoped<GetMapCensusTracts>();
        services.AddScoped<GetMapSchools>();

        services.AddScoped<CreateGroup>();
        services.AddScoped<GetGroupDetails>();
        services.AddScoped<CalculateGroupScore>();
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

        services.AddScoped<ListZoneReviews>();
        services.AddScoped<SaveMyReview>();
        services.AddScoped<DeleteMyReview>();
        services.AddScoped<SetReviewHelpful>();
        services.AddScoped<ListZoneQuestions>();
        services.AddScoped<CreateQuestion>();
        services.AddScoped<AnswerQuestion>();
        services.AddScoped<GetContributionSummary>();
        services.AddScoped<ListPendingContributions>();
        services.AddScoped<CreatePointContribution>();
        services.AddScoped<CreateContributionCorrection>();
        services.AddScoped<ValidateContribution>();
        services.AddScoped<CommunityPoiUpdater>();
        services.AddScoped<ResolveContribution>();

        services.AddScoped<ListCriteria>();
        services.AddScoped<GetMyProfile>();
        services.AddScoped<SaveMyProfile>();

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

        services.AddScoped<CalculateScores>();
        services.AddScoped<GetRoute>();
        services.AddScoped<GetZoneIndicators>();
        services.AddScoped<GetZoneTransit>();

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
