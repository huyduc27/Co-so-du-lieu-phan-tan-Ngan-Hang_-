using Coordinator.Api.Services;
using Coordinator.Api.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class CoordinatorController : ControllerBase
{
    private readonly CoordinatorService _coordinatorService;

    public CoordinatorController(CoordinatorService coordinatorService)
    {
        _coordinatorService = coordinatorService;
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        var result = await _coordinatorService.ProcessTransferAsync(request);
        if (result.Contains("thành công"))
        {
            return Ok(new { success = true, message = result });
        }
        return BadRequest(new { success = false, message = result });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var trans = _coordinatorService.GetTransactions().Values;
        return Ok(trans);
    }
}
