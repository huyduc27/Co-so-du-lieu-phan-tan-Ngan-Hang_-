using BankA.Api.Data;
using BankA.Api.Models;
using BankA.Api.Services;
using Microsoft.AspNetCore.Mvc;
using static BankA.Api.Request.BankRequest;

namespace BankA.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BankController : Controller
    {
        private readonly BankService _bankService;

        public BankController(BankService bankService)
        {
            _bankService = bankService;
        }


        // ── GET /api/Bank/Balance/?accountId=A01
        [HttpGet("Balance/{accountId}")]
        public IActionResult GetBalanceById([FromRoute] string accountId)
        {

            var response = _bankService.GetBalance(accountId);
            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // ── POST /api/Bank/prepare ─────────────────────
        [HttpPost("prepare")]
        public IActionResult Prepare([FromBody] PrepareRequest req)
        {
            var response = _bankService.Prepare(req.AccountId, req.TransactionId, req.Amount);
            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // ── POST /api/Bank/commit ──────────────────────
        [HttpPost("commit")]
        public IActionResult Commit([FromBody] TransactionRequest req)
        {
            var response = _bankService.Commit(req.TransactionId);
            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // ── POST /api/Bank/rollback ────────────────────
        [HttpPost("rollback")]
        public IActionResult Rollback([FromBody] TransactionRequest req)
        {
            var response = _bankService.Rollback(req.TransactionId);
            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }
    }
    
}
