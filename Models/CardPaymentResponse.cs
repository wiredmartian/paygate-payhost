using Newtonsoft.Json.Linq;

namespace payhost.Models
{
    public class CardPaymentResponse
    {
        public bool Completed { get; set; }
        public string Secure3DHtml { get; set; }
        public string PayRequestId { get; set; }
        public string Response { get; set; }
    }
}