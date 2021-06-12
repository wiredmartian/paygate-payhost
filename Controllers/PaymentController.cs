using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.Values.SelectMany(err => err.Errors[0].ErrorMessage));
            }
            string response = await _payment.AddNewCard(model);
            return Ok(response);
        }
    }
}