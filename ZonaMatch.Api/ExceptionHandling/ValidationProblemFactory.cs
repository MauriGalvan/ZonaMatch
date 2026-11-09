using Microsoft.AspNetCore.Mvc;

namespace ZonaMatch.Api.ExceptionHandling
{
    // Response of the automatic request validation ([ApiController]): the same ProblemDetails shape as the global
    // exception handler, with the title and the framework's own messages in Spanish. Our messages are kept as written.
    public static class ValidationProblemFactory
    {
        public const string Title = "Revisá los datos enviados.";

        public static IActionResult Create(ActionContext context)
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = Title,
            };

            foreach (var field in problem.Errors.Keys.ToList())
                problem.Errors[field] = problem.Errors[field].Select(Translate).Distinct().ToArray();

            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        }

        // ASP.NET writes its own messages in English: missing fields, unreadable JSON, values of the wrong type.
        public static string Translate(string message)
        {
            if (message.EndsWith("field is required.", StringComparison.Ordinal))
                return "Este dato es obligatorio.";

            if (message.StartsWith("A non-empty request body is required", StringComparison.Ordinal))
                return "Falta el contenido del pedido.";

            if (message.Contains("JSON", StringComparison.Ordinal)
                || message.StartsWith("The value '", StringComparison.Ordinal)
                || message.EndsWith("is invalid.", StringComparison.Ordinal)
                || message.EndsWith("must be a number.", StringComparison.Ordinal))
                return "El valor no tiene el formato esperado.";

            return message;
        }
    }
}
