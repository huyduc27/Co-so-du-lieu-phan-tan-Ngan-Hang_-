using BankB.Api.Services;
using Microsoft.AspNetCore.Mvc;
using static BankB.Api.Requests.BankRequest;

namespace BankB.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BankController : ControllerBase
    {
        private readonly BankService _bankService;

        public BankController(BankService bankService)
        {
            _bankService = bankService;
        }

        [HttpGet("Balance/{accountId}")]
        public IActionResult GetBalanceById([FromRoute] string accountId)
        {
            var response = _bankService.GetBalance(accountId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("prepare")]
        public IActionResult Prepare([FromBody] PrepareRequest req)
        {
            var response = _bankService.Prepare(req.AccountId, req.TransactionId, req.Amount);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("commit")]
        public IActionResult Commit([FromBody] TransactionRequest req)
        {
            var response = _bankService.Commit(req.TransactionId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("rollback")]
        public IActionResult Rollback([FromBody] TransactionRequest req)
        {
            var response = _bankService.Rollback(req.TransactionId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // ── GET /Bank/pending-transactions ─────────────
        [HttpGet("pending-transactions")]
        public IActionResult GetPendingTransactions()
        {
            var pending = _bankService.GetPendingTransactions();
            return Ok(pending);
        }
    }
}
