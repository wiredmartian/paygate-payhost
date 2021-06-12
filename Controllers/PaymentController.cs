using Microsoft.AspNetCore.Mvc;

namespace payhost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        // GET
        public IActionResult Index()
        {
            return Ok(new { message = "Hello world"});
        }
    }
}