using System;
using System.IO;
using System.Threading.Tasks;
using payhost.Models;
using RestSharp;

namespace payhost.Services
{
    public interface IPayment
    {
        Task<string> AddNewCard(NewCard card);
    }
    public class PaymentService : IPayment
    {
        public async Task<string> AddNewCard(NewCard card)
        {
            RestClient client = new RestClient("https://secure.paygate.co.za/payhost/process.trans");
            RestRequest request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/xml");
            request.AddHeader("SOAPAction", "WebPaymentRequest");
            
            // request body
            string body;
            using (StreamReader reader = new StreamReader(Directory.GetCurrentDirectory() + "/Templates/SinglePaymentRequest.xml"))
            {
                body = await reader.ReadToEndAsync();
            }

            body = body.Replace("{PayGateId}", "");
            body = body.Replace("{Password}", "");
            body = body.Replace("{FirstName}", card.FirstName);
            body = body.Replace("{LastName}", card.LastName);
            body = body.Replace("{Mobile}", "");
            body = body.Replace("{Email}", card.Email);
            body = body.Replace("{CardNumber}", card.CardNumber.ToString());
            body = body.Replace("{CardExpiryDate}", card.CardExpiry.ToString());
            body = body.Replace("{CVV}", card.Cvv.ToString());
            // body = body.Replace("{Vault}", false.ToString());
            body = body.Replace("{MerchantOrderId}", Guid.NewGuid().ToString());
            // convert amount to cents (amount * 100)
            body = body.Replace("{Amount}", (card.Amount * 100).ToString("0000"));

            request.AddParameter("text/xml", body, ParameterType.RequestBody);
            IRestResponse response = await client.ExecuteAsync(request);
            return response.Content;
        }
    }
}