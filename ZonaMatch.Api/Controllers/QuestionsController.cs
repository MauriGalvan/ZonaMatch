using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Question.Requests;
using ZonaMatch.Application.DTOs.Question.Responses;
using ZonaMatch.Application.UseCases.Questions;

namespace ZonaMatch.Api.Controllers;

// Questions for a zone's community and their answers. Reading is public; writing requires authentication.
[ApiController]
[Route("api/v1/zones/{slug:regex(" + ZoneRouteConstraints.Slug + ")}/questions")]
public sealed class QuestionsController : ControllerBase
{
    // GET /api/v1/zones/villa-luro/questions
    // Newest question first; answers within each question are oldest first.
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuestionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        string slug,
        [FromServices] ListZoneQuestions listZoneQuestions,
        CancellationToken cancellationToken) =>
        Ok(await listZoneQuestions.ExecuteAsync(slug, User.GetAuthenticatedUserId(), cancellationToken));

    // POST /api/v1/zones/villa-luro/questions    Body: { "text": "..." }
    [Authorize]
    [HttpPost]
    [ProducesResponseType<QuestionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync(
        string slug,
        [FromBody] CreateQuestionRequest request,
        [FromServices] CreateQuestion createQuestion,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserContext() is not { } currentUser)
            return Unauthorized();

        var question = await createQuestion.ExecuteAsync(slug, currentUser, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, question);
    }

    // POST /api/v1/zones/villa-luro/questions/{id}/answers    Body: { "text": "..." }
    [Authorize]
    [HttpPost("{id:guid}/answers")]
    [ProducesResponseType<AnswerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnswerAsync(
        string slug,
        Guid id,
        [FromBody] CreateAnswerRequest request,
        [FromServices] AnswerQuestion answerQuestion,
        CancellationToken cancellationToken)
    {
        if (User.GetAuthenticatedUserContext() is not { } currentUser)
            return Unauthorized();

        var answer = await answerQuestion.ExecuteAsync(slug, id, currentUser, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, answer);
    }
}
