using LexiVocab.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace LexiVocab.API.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true, data = result.Data });
        
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }

    protected IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, new { success = true });
        
        return StatusCode(result.StatusCode, new { success = false, error = result.Error });
    }
}
