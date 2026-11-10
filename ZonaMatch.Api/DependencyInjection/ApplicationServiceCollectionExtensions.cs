using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Emails;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Services;
using ZonaMatch.Application.UseCases.Admin;
using ZonaMatch.Application.UseCases.AnalysisFollowUp;
using ZonaMatch.Application.UseCases.Contribution;
using ZonaMatch.Application.UseCases.Auth;
using ZonaMatch.Application.UseCases.Contact;
using ZonaMatch.Application.UseCases.Criterion;
using ZonaMatch.Application.UseCases.Geolocation;
using ZonaMatch.Application.UseCases.GuestReport;
using ZonaMatch.Application.UseCases.GroupInvitation;
using ZonaMatch.Application.UseCases.GroupLink;
using ZonaMatch.Application.UseCases.Image;
using ZonaMatch.Application.UseCases.Group;
using ZonaMatch.Application.UseCases.Map;
using ZonaMatch.Application.UseCases.Notification;
using ZonaMatch.Application.UseCases.PasswordReset;
using ZonaMatch.Application.UseCases.Profile;
using ZonaMatch.Application.UseCases.Question;
using ZonaMatch.Application.UseCases.Scoring;
using ZonaMatch.Application.UseCases.Review;
using ZonaMatch.Application.UseCases.Route;
using ZonaMatch.Application.UseCases.SavedAnalysis;
using ZonaMatch.Application.UseCases.SavedZone;
using ZonaMatch.Application.UseCases.ZoneRecommendation;
using ZonaMatch.Application.UseCases.Zone;

namespace ZonaMatch.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<GetZoneProfileRecommendations>();
        services.AddScoped<GetZoneMarket>();
        services.AddScoped<RefreshZoneProfiles>();
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
        services.AddScoped<SaveGroupAnalysis>();
        services.AddScoped<GetGroups>();
        services.AddScoped<DeleteGroup>();
        services.AddScoped<UpdateGroup>();
        services.AddScoped<RemoveParticipant>();

        services.AddScoped<GetNotifications>();
        services.AddScoped<InviteAnalysisFollowUp>();
        services.AddScoped<SubmitAnalysisFollowUp>();
        services.AddScoped<PostponeAnalysisFollowUp>();
        services.AddScoped<GetFeedbackPreferences>();
        services.AddScoped<UpdateFeedbackPreferences>();
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

        services.AddScoped<ListUsers>();
        services.AddScoped<UpdateUserRole>();
        services.AddScoped<GetAdminStats>();

        services.AddScoped<ListZoneReviews>();
        services.AddScoped<SaveMyReview>();
        services.AddScoped<DeleteMyReview>();
        services.AddScoped<SetReviewHelpful>();
        services.AddScoped<ReviewModerationEmails>();
        services.AddScoped<ListPendingReviews>();
        services.AddScoped<ResolveReview>();
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
        services.AddScoped<GetZoneTransitRoutes>();

        services.AddSingleton(TimeProvider.System);
        services.AddOptions<FrontendOptions>()
            .BindConfiguration(FrontendOptions.SectionName)
            .PostConfigure<IHostEnvironment>((options, environment) =>
            {
                if (string.IsNullOrWhiteSpace(options.BaseUrl) && environment.IsDevelopment())
                    options.BaseUrl = FrontendOptions.DevelopmentBaseUrl;
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(provider => new FrontendLinks(provider.GetRequiredService<IOptions<FrontendOptions>>().Value.BaseUrl));
        services.AddSingleton(new ContactOptions(
            configuration["Contact:RecipientEmail"]
                ?? configuration["Contacto:Destinatario"]
                ?? "zonamatchtpi@gmail.com"));
        services.AddScoped<SendContactMessage>();
        services.AddScoped<GuestReportQuota>();
        services.AddScoped<SendGuestReport>();
        services.AddScoped<UploadImage>();

        return services;
    }
}
