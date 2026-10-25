using Microsoft.AspNetCore.Mvc;

namespace BackChess.Controllers;

/// <summary>
/// Родительский контроллер
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RootController : ControllerBase
{
    /// <summary>
    /// Логгирование
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// .ctor
    /// </summary>
    /// <param name="logger"></param>
    public RootController(ILogger logger)
    {
        _logger = logger;
    }

}