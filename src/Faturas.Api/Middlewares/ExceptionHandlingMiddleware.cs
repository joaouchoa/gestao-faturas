using Faturas.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Faturas.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (DomainException ex)
        {
            await HandleDomainExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado: {Message}", ex.Message);
            await HandleUnexpectedExceptionAsync(context);
        }
    }

    private static Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title  = "Erro de validação",
            Type   = "https://tools.ietf.org/html/rfc7807"
        };

        return WriteProblemAsync(context, problem, StatusCodes.Status400BadRequest);
    }

    private static Task HandleDomainExceptionAsync(HttpContext context, DomainException ex)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title  = "Regra de negócio violada",
            Detail = ex.Message,
            Type   = "https://tools.ietf.org/html/rfc4918#section-11.2"
        };

        return WriteProblemAsync(context, problem, StatusCodes.Status422UnprocessableEntity);
    }

    private static Task HandleUnexpectedExceptionAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title  = "Erro interno do servidor",
            Detail = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            Type   = "https://tools.ietf.org/html/rfc7807"
        };

        return WriteProblemAsync(context, problem, StatusCodes.Status500InternalServerError);
    }

    private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem, int statusCode)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, problem.GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
