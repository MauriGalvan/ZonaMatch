# Matriz preliminar de pruebas de casos de uso

Esta matriz cruza los casos de uso del back con los archivos de pruebas existentes para identificar dónde revisar cobertura y dónde buscar pruebas faltantes.

## Resumen

- **Casos de uso identificados:** 75
- **Con referencia directa en archivos que contienen `[Fact]` o `[Theory]`:** 75
- **Sin referencia directa localizada:** 0

## Cómo leerla

- “Referencia directa” indica que hay un archivo de pruebas del caso de uso. La presencia del archivo no garantiza por sí sola cobertura completa.
- “Escenarios revisados” indica una revisión estática de los caminos principales del caso de uso frente a sus tests; no significa cobertura exhaustiva de todas las combinaciones posibles.
- La matriz indica qué escenarios tienen ejecución confirmada por el usuario; los escenarios nuevos agregados después de esa confirmación quedan pendientes hasta la siguiente ejecución.
- Esta matriz no mide cobertura de líneas ni reemplaza la revisión de escenarios.

## Avance de revisión de escenarios

- `GetLocationSummary`: además del distrito sin comuna y sin escuelas, se agregaron pruebas para comuna con escuela mapeada y coordenadas inválidas sin consultas a repositorios.
- `GetNearbyPointsOfInterest`: se agregaron pruebas para coordenadas y radios inválidos, categorías desconocidas y límite de resultados con marca de truncamiento.
- `GetZonePointsOfInterest`: se agregó la validación de slug inválido sin consultar el repositorio.
- `CalculateScores` y `CalculateGroupScore`: se agregaron escenarios para datos de zonas vacíos (individual), restringir el ranking a las zonas candidatas del repositorio y rechazar cuando no hay candidatas para los puntos de interés.
- `CalculateGroupScore`: también se agregaron escenarios para pesos fuera de rango, participantes ajenos, pesos duplicados o todos en cero, zona agregada inexistente, ausencia de perfiles y cambio del ranking al modificar ponderaciones.
- Se revisaron los casos de uso de listado y delegación simples: se añadieron respuestas vacías para grupos, notificaciones y zonas guardadas; los tests de criterios, aportes, creación de grupos y notificaciones masivas cubren sus transformaciones o delegación principal.
- `SaveGroupAnalysis`: el usuario confirmó que el escenario válido y la suite completa pasaron después de ajustar la expectativa al ranking automático.
- En la revisión prioritaria se añadieron escenarios para límites y snapshots inválidos de `CompareAnalyses`; zonas vacías o múltiples zonas iniciales y calculadores sin resultado en análisis guardados; rechazo de recálculo grupal, recálculo desde una versión y creación de versiones con zonas explícitas válidas o inválidas; creación de reseña con notificaciones sin duplicarlas al editar; y creación/aprobación de correcciones de categoría y comentarios informativos.
- En la revisión de prioridad media se añadieron escenarios para invitaciones al propio organizador, reportes PDF que superan el tamaño máximo, coordenadas de origen fuera de rango, búsquedas sin resultados y lectura de perfiles existentes.
- También se cubrieron respuestas vacías en aportes pendientes, preguntas de zona y análisis guardados, además de la propagación de cancelaciones durante envíos de correo de contacto, informes, recuperación de contraseña y respuestas a preguntas.
- En la tanda anterior se ampliaron `SaveMyProfile` con validaciones de nombres y rangos etarios, `LoginUser` con la comprobación de que email inexistente y contraseña incorrecta devuelven el mismo mensaje, y `DeleteAnalysis`/`RenameAnalysis` con análisis inexistentes. El usuario confirmó que la suite pasó. En esta tanda se revisó el resto de los escenarios pendientes y se agregaron casos para cambios de voto en aportes, preguntas sin usuarios suscritos y falta de datos de referencia de CABA.
- El usuario confirmó que la suite completa pasó, incluidos los escenarios de esta tanda. No se declara cobertura exhaustiva ni un porcentaje de cobertura.

## Medición de cobertura

El proyecto de tests incluye `coverlet.collector`. Para ejecutar la suite y generar el reporte Cobertura:

```powershell
dotnet test tests\ZonaMatch.Tests\ZonaMatch.Tests.csproj --collect:"XPlat Code Coverage"
```

El archivo `coverage.cobertura.xml` se genera dentro de `tests\ZonaMatch.Tests\TestResults`. El porcentaje se debe registrar cuando la suite pueda ejecutarse completa; todavía no hay una medición válida para informar.

## Detalle

| Área | Caso de uso | Archivo fuente | Archivo(s) de prueba localizado(s) | Estado preliminar |
|---|---|---|---|---|
| Auth | `LoginUser` | `ZonaMatch.Application/UseCases/Auth/LoginUser.cs` | tests/ZonaMatch.Tests/UseCases/Auth/AuthUseCasesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Auth | `RegisterUser` | `ZonaMatch.Application/UseCases/Auth/RegisterUser.cs` | tests/ZonaMatch.Tests/UseCases/Auth/AuthUseCasesTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Contact | `SendContactMessage` | `ZonaMatch.Application/UseCases/Contact/SendContactMessage.cs` | tests/ZonaMatch.Tests/UseCases/Contact/SendContactMessageTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Contribution | `CreateContributionCorrection` | `ZonaMatch.Application/UseCases/Contribution/CreateContributionCorrection.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/CreateContributionCorrectionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Contribution | `CreatePointContribution` | `ZonaMatch.Application/UseCases/Contribution/CreatePointContribution.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/CreatePointContributionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Contribution | `GetContributionSummary` | `ZonaMatch.Application/UseCases/Contribution/GetContributionSummary.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/GetContributionSummaryTests.cs | Escenario de conteos y estados revisado; ejecución confirmada por el usuario |
| Contribution | `ListPendingContributions` | `ZonaMatch.Application/UseCases/Contribution/ListPendingContributions.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/ListPendingContributionsTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Contribution | `ResolveContribution` | `ZonaMatch.Application/UseCases/Contribution/ResolveContribution.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/ResolveContributionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Contribution | `ValidateContribution` | `ZonaMatch.Application/UseCases/Contribution/ValidateContribution.cs` | tests/ZonaMatch.Tests/UseCases/Contribution/ValidateContributionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Criterion | `ListCriteria` | `ZonaMatch.Application/UseCases/Criterion/ListCriteria.cs` | tests/ZonaMatch.Tests/UseCases/Criterion/ListCriteriaTests.cs | Escenarios activo/inactivo y orden revisados; ejecución confirmada por el usuario |
| Geolocation | `GetCommuneByLocation` | `ZonaMatch.Application/UseCases/Geolocation/GetCommuneByLocation.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetCommuneByLocationTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `GetDistrictByLocation` | `ZonaMatch.Application/UseCases/Geolocation/GetDistrictByLocation.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetDistrictByLocationTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `GetLocationSummary` | `ZonaMatch.Application/UseCases/Geolocation/GetLocationSummary.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetLocationSummaryTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Geolocation | `GetNearbyPointsOfInterest` | `ZonaMatch.Application/UseCases/Geolocation/GetNearbyPointsOfInterest.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetNearbyPointsOfInterestTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Geolocation | `GetNearbySchools` | `ZonaMatch.Application/UseCases/Geolocation/GetNearbySchools.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetNearbySchoolsTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `GetNearestSchools` | `ZonaMatch.Application/UseCases/Geolocation/GetNearestSchools.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetNearestSchoolsTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `GetPointsOfInterestSummary` | `ZonaMatch.Application/UseCases/Geolocation/GetPointsOfInterestSummary.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/GetPointsOfInterestSummaryTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `ReverseGeocode` | `ZonaMatch.Application/UseCases/Geolocation/ReverseGeocode.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/ReverseGeocodeTests.cs | Ejecución confirmada por el usuario |
| Geolocation | `SearchPlaces` | `ZonaMatch.Application/UseCases/Geolocation/SearchPlaces.cs` | tests/ZonaMatch.Tests/UseCases/Geolocation/SearchPlacesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| GroupInvitation | `AcceptInvitation` | `ZonaMatch.Application/UseCases/GroupInvitation/AcceptInvitation.cs` | tests/ZonaMatch.Tests/UseCases/GroupInvitation/AcceptInvitationTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| GroupInvitation | `CreateInvitation` | `ZonaMatch.Application/UseCases/GroupInvitation/CreateInvitation.cs` | tests/ZonaMatch.Tests/UseCases/GroupInvitation/CreateInvitationTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| GroupInvitation | `GetInvitationByToken` | `ZonaMatch.Application/UseCases/GroupInvitation/GetInvitationByToken.cs` | tests/ZonaMatch.Tests/UseCases/GroupInvitation/GetInvitationByTokenTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| GroupLink | `GetGroupByLink` | `ZonaMatch.Application/UseCases/GroupLink/GetGroupByLink.cs` | tests/ZonaMatch.Tests/UseCases/GroupLink/GetGroupByLinkTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| GroupLink | `GetGroupInviteLink` | `ZonaMatch.Application/UseCases/GroupLink/GetGroupInviteLink.cs` | tests/ZonaMatch.Tests/UseCases/GroupLink/GetGroupInviteLinkTests.cs | Ejecución confirmada por el usuario |
| GroupLink | `JoinGroupByLink` | `ZonaMatch.Application/UseCases/GroupLink/JoinGroupByLink.cs` | tests/ZonaMatch.Tests/UseCases/GroupLink/JoinGroupByLinkTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| GroupLink | `RevokeInviteLink` | `ZonaMatch.Application/UseCases/GroupLink/RevokeInviteLink.cs` | tests/ZonaMatch.Tests/UseCases/GroupLink/RevokeInviteLinkTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Group | `CalculateGroupScore` | `ZonaMatch.Application/UseCases/Group/CalculateGroupScore.cs` | tests/ZonaMatch.Tests/UseCases/Group/CalculateGroupScoreTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Group | `CreateGroup` | `ZonaMatch.Application/UseCases/Group/CreateGroup.cs` | tests/ZonaMatch.Tests/UseCases/Group/CreateGroupTests.cs | Persistencia y rol de propietario revisados; ejecución confirmada por el usuario |
| Group | `DeleteGroup` | `ZonaMatch.Application/UseCases/Group/DeleteGroup.cs` | tests/ZonaMatch.Tests/UseCases/Group/DeleteGroupTests.cs | Ejecución confirmada por el usuario |
| Group | `GetGroupDetails` | `ZonaMatch.Application/UseCases/Group/GetGroupDetails.cs` | tests/ZonaMatch.Tests/UseCases/Group/GetGroupDetailsTests.cs | Ejecución confirmada por el usuario |
| Group | `GetGroups` | `ZonaMatch.Application/UseCases/Group/GetGroups.cs` | tests/ZonaMatch.Tests/UseCases/Group/GetGroupsTests.cs | Propietario/invitado y lista vacía; ejecución confirmada por el usuario |
| Group | `RemoveParticipant` | `ZonaMatch.Application/UseCases/Group/RemoveParticipant.cs` | tests/ZonaMatch.Tests/UseCases/Group/RemoveParticipantTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Group | `SaveGroupAnalysis` | `ZonaMatch.Application/UseCases/Group/SaveGroupAnalysis.cs` | tests/ZonaMatch.Tests/UseCases/Group/SaveGroupAnalysisTests.cs | Escenario válido y suite confirmados por el usuario |
| Group | `UpdateGroup` | `ZonaMatch.Application/UseCases/Group/UpdateGroup.cs` | tests/ZonaMatch.Tests/UseCases/Group/UpdateGroupTests.cs | Ejecución confirmada por el usuario |
| GuestReport | `SendGuestReport` | `ZonaMatch.Application/UseCases/GuestReport/SendGuestReport.cs` | tests/ZonaMatch.Tests/UseCases/GuestReport/SendGuestReportTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Map | `GetMapCensusTracts` | `ZonaMatch.Application/UseCases/Map/GetMapCensusTracts.cs` | tests/ZonaMatch.Tests/UseCases/Map/GetMapCensusTractsTests.cs | Ejecución confirmada por el usuario |
| Map | `GetMapCommunes` | `ZonaMatch.Application/UseCases/Map/GetMapCommunes.cs` | tests/ZonaMatch.Tests/UseCases/Map/GetMapCommunesTests.cs | Normalización de tolerancia revisada; ejecución confirmada por el usuario |
| Map | `GetMapDistricts` | `ZonaMatch.Application/UseCases/Map/GetMapDistricts.cs` | tests/ZonaMatch.Tests/UseCases/Map/GetMapDistrictsTests.cs | Tolerancia por defecto revisada; ejecución confirmada por el usuario |
| Map | `GetMapSchools` | `ZonaMatch.Application/UseCases/Map/GetMapSchools.cs` | tests/ZonaMatch.Tests/UseCases/Map/GetMapSchoolsTests.cs | Ejecución confirmada por el usuario |
| Notification | `GetNotifications` | `ZonaMatch.Application/UseCases/Notification/GetNotifications.cs` | tests/ZonaMatch.Tests/UseCases/Notification/GetNotificationsTests.cs | Mapeo, contador y lista vacía; ejecución confirmada por el usuario |
| Notification | `MarkAllNotificationsAsRead` | `ZonaMatch.Application/UseCases/Notification/MarkAllNotificationsAsRead.cs` | tests/ZonaMatch.Tests/UseCases/Notification/MarkAllNotificationsAsReadTests.cs | Delegación de usuario y hora actual revisada; ejecución confirmada por el usuario |
| Notification | `MarkNotificationAsRead` | `ZonaMatch.Application/UseCases/Notification/MarkNotificationAsRead.cs` | tests/ZonaMatch.Tests/UseCases/Notification/MarkNotificationAsReadTests.cs | Ejecución confirmada por el usuario |
| PasswordReset | `CompletePasswordReset` | `ZonaMatch.Application/UseCases/PasswordReset/CompletePasswordReset.cs` | tests/ZonaMatch.Tests/UseCases/PasswordReset/PasswordResetTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| PasswordReset | `RequestPasswordReset` | `ZonaMatch.Application/UseCases/PasswordReset/RequestPasswordReset.cs` | tests/ZonaMatch.Tests/UseCases/PasswordReset/PasswordResetTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Profile | `GetMyProfile` | `ZonaMatch.Application/UseCases/Profile/GetMyProfile.cs` | tests/ZonaMatch.Tests/UseCases/Profile/SaveMyProfileTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Profile | `SaveMyProfile` | `ZonaMatch.Application/UseCases/Profile/SaveMyProfile.cs` | tests/ZonaMatch.Tests/UseCases/Profile/SaveMyProfileTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Question | `AnswerQuestion` | `ZonaMatch.Application/UseCases/Question/AnswerQuestion.cs` | tests/ZonaMatch.Tests/UseCases/Question/AnswerQuestionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Question | `CreateQuestion` | `ZonaMatch.Application/UseCases/Question/CreateQuestion.cs` | tests/ZonaMatch.Tests/UseCases/Question/CreateQuestionTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Question | `ListZoneQuestions` | `ZonaMatch.Application/UseCases/Question/ListZoneQuestions.cs` | tests/ZonaMatch.Tests/UseCases/Question/ListZoneQuestionsTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Review | `DeleteMyReview` | `ZonaMatch.Application/UseCases/Review/DeleteMyReview.cs` | tests/ZonaMatch.Tests/UseCases/Review/DeleteMyReviewTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Review | `ListZoneReviews` | `ZonaMatch.Application/UseCases/Review/ListZoneReviews.cs` | tests/ZonaMatch.Tests/UseCases/Review/ListZoneReviewsTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Review | `SaveMyReview` | `ZonaMatch.Application/UseCases/Review/SaveMyReview.cs` | tests/ZonaMatch.Tests/UseCases/Review/SaveMyReviewTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Review | `SetReviewHelpful` | `ZonaMatch.Application/UseCases/Review/SetReviewHelpful.cs` | tests/ZonaMatch.Tests/UseCases/Review/SetReviewHelpfulTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| Route | `GetRoute` | `ZonaMatch.Application/UseCases/Route/GetRoute.cs` | tests/ZonaMatch.Tests/UseCases/Route/GetRouteTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `CompareAnalyses` | `ZonaMatch.Application/UseCases/SavedAnalysis/CompareAnalyses.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/CompareAnalysisDistanceTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `CreateAnalysis` | `ZonaMatch.Application/UseCases/SavedAnalysis/CreateAnalysis.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `CreateAnalysisVersion` | `ZonaMatch.Application/UseCases/SavedAnalysis/CreateAnalysisVersion.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `DeleteAnalysis` | `ZonaMatch.Application/UseCases/SavedAnalysis/DeleteAnalysis.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `GetAnalysisById` | `ZonaMatch.Application/UseCases/SavedAnalysis/GetAnalysisById.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/GetAnalysisByIdTests.cs | Ejecución confirmada por el usuario |
| SavedAnalysis | `ListMyAnalyses` | `ZonaMatch.Application/UseCases/SavedAnalysis/ListMyAnalyses.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `RecalculateAnalysis` | `ZonaMatch.Application/UseCases/SavedAnalysis/RecalculateAnalysis.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `RenameAnalysis` | `ZonaMatch.Application/UseCases/SavedAnalysis/RenameAnalysis.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| SavedAnalysis | `SetAnalysisFavorite` | `ZonaMatch.Application/UseCases/SavedAnalysis/SetAnalysisFavorite.cs` | tests/ZonaMatch.Tests/UseCases/SavedAnalysis/SavedAnalysesTests.cs | Escenarios revisados; ejecución confirmada por el usuario |
| SavedZone | `ListSavedZones` | `ZonaMatch.Application/UseCases/SavedZone/ListSavedZones.cs` | tests/ZonaMatch.Tests/UseCases/SavedZone/ListSavedZonesTests.cs | Mapeo y lista vacía; ejecución confirmada por el usuario |
| SavedZone | `RemoveSavedZone` | `ZonaMatch.Application/UseCases/SavedZone/RemoveSavedZone.cs` | tests/ZonaMatch.Tests/UseCases/SavedZone/RemoveSavedZoneTests.cs | Ejecución confirmada por el usuario |
| SavedZone | `SaveZone` | `ZonaMatch.Application/UseCases/SavedZone/SaveZone.cs` | tests/ZonaMatch.Tests/UseCases/SavedZone/SaveZoneTests.cs | Ejecución confirmada por el usuario |
| Scoring | `CalculateScores` | `ZonaMatch.Application/UseCases/Scoring/CalculateScores.cs` | tests/ZonaMatch.Tests/UseCases/Scoring/CalculateScoresTests.cs<br>tests/ZonaMatch.Tests/UseCases/Scoring/CalculateScoresCandidateZoneTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| ZoneRecommendation | `GetZoneProfileRecommendations` | `ZonaMatch.Application/UseCases/ZoneRecommendation/GetZoneProfileRecommendations.cs` | tests/ZonaMatch.Tests/UseCases/ZoneRecommendation/GetZoneProfileRecommendationsTests.cs | Ejecución confirmada por el usuario |
| Zone | `GetZone` | `ZonaMatch.Application/UseCases/Zone/GetZone.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZoneTests.cs | Ejecución confirmada por el usuario |
| Zone | `GetZoneIndicators` | `ZonaMatch.Application/UseCases/Zone/GetZoneIndicators.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZoneIndicatorsTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Zone | `GetZoneMarket` | `ZonaMatch.Application/UseCases/Zone/GetZoneMarket.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZoneMarketTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Zone | `GetZonePointsOfInterest` | `ZonaMatch.Application/UseCases/Zone/GetZonePointsOfInterest.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZonePointsOfInterestTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Zone | `GetZonePointsOfInterestSummary` | `ZonaMatch.Application/UseCases/Zone/GetZonePointsOfInterestSummary.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZonePointsOfInterestSummaryTests.cs | Ejecución confirmada por el usuario |
| Zone | `GetZoneTransit` | `ZonaMatch.Application/UseCases/Zone/GetZoneTransit.cs` | tests/ZonaMatch.Tests/UseCases/Zone/GetZoneTransitTests.cs | Escenarios ampliados; ejecución confirmada por el usuario |
| Zone | `SearchZones` | `ZonaMatch.Application/UseCases/Zone/SearchZones.cs` | tests/ZonaMatch.Tests/UseCases/Zone/SearchZonesTests.cs | Ejecución confirmada por el usuario |
