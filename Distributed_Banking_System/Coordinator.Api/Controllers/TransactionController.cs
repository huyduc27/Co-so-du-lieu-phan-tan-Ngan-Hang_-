using Coordinator.Api.Services;
using Coordinator.Api.Requests;
using Coordinator.Api.Responses;
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
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var trans = _coordinatorService.GetTransactions().Values;
        return Ok(new CoordinatorResponse
        {
            Success = true,
            Message = "Retrieved transactions successfully",
            Data = trans
        });
    }
}
