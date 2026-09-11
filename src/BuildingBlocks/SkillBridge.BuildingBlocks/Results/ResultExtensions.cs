using Microsoft.AspNetCore.Http;

namespace SkillBridge.BuildingBlocks.Results;

public static class ResultExtensions
{
    public static IResult ToProblemDetails(this Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: statusCode,
            title: error.Code,
            detail: error.Description);
    }

    public static IResult ToHttpResult(this Result result)
    {
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.Ok()
            : result.Error.ToProblemDetails();
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        return result.IsSuccess
            ? Microsoft.AspNetCore.Http.Results.Ok(result.Value)
            : result.Error.ToProblemDetails();
    }
}
