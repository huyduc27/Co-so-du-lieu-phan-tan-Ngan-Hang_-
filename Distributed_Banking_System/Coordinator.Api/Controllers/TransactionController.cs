using Coordinator.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TransactionController : ControllerBase
{
    private readonly CoordinatorService _coordinatorService;

    public TransactionController(CoordinatorService coordinatorService)
    {
        _coordinatorService = coordinatorService;
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        var result = await _coordinatorService.ProcessTransferAsync(request);
        return Ok(new { message = result });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_coordinatorService.GetTransactions().Values);
    }
}
