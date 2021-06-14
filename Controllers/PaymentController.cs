using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using payhost.Models;
using payhost.Services;

namespace payhost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPayment _payment;

        public PaymentController(IPayment payment)
        {
            _payment = payment;
        }
        // GET
        public IActionResult Index()
        {
            return Ok(new { message = "Hello world"});
        }
        
        [HttpPost(Name = nameof(AddNewCard))]
        public async Task<IActionResult> AddNewCard([FromBody] NewCard model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState.Values.SelectMany(err => err.Errors[0].ErrorMessage));
                }

                JToken response = await _payment.AddNewCard(model);
                return Ok(response.ToString());
            }
            catch (ApplicationException e)
            {
                return BadRequest(new {error = e.Message});
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }

        [HttpGet("{vaultId}", Name = nameof(GetVaultedCard))]
        public async Task<IActionResult> GetVaultedCard([FromRoute] string vaultId)
        {
            try
            {
                JToken result = await _payment.GetVaultedCard(vaultId);
                return Ok(result?.ToString());
            }
            catch (ApplicationException e)
            {
                return BadRequest(new {error = e.Message});
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }
        
        [HttpGet("query/{payRequestId}", Name = nameof(QueryTransaction))]
        public async Task<IActionResult> QueryTransaction([FromRoute] string payRequestId)
        {
            try
            {
                JToken result = await _payment.QueryTransaction(payRequestId);
                return Ok(result?.ToString());
            }
            catch (ApplicationException e)
            {
                return BadRequest(new {error = e.Message});
            }
            catch (Exception e)
            {
                return StatusCode(500, new { error = e.Message });
            }
        }
    }
}