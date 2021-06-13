#nullable enable
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using payhost.Models;
using RestSharp;

namespace payhost.Services
{
    public interface IPayment
    {
        Task<JToken> AddNewCard(NewCard card);
    }
    public class PaymentService : IPayment
    {
        public async Task<JToken> AddNewCard(NewCard card)
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
            JToken result = MapXmlResponseToObject(response.Content);
            // check payment response
            if (result?["Status"] == null) return result;
            
            JToken? paymentStatus = result["Status"];
            switch (paymentStatus?["StatusName"]?.ToString())
            {
                case "Error":
                    throw new ApplicationException();
                    
                case "Completed" when paymentStatus?["ResultCode"] != null:
                    if (paymentStatus["ResultCode"]?.ToString() == "990017")
                    {
                        return result;
                    }
                    else
                    {
                        throw new ApplicationException($"{paymentStatus["ResultCode"]}: Payment declined");
                    }
                case "ThreeDSecureRedirectRequired":
                    return result;
            }
            return result;
        }

        private static JToken MapXmlResponseToObject(string xmlContent)
        {
            XmlDocument xmlResult = new XmlDocument();
            // throws exception if it fails to parse xml
            xmlResult.LoadXml(xmlContent);
            // convert to json
            string result = JsonConvert.SerializeXmlNode(xmlResult);
            // remove prefix tags
            result = Regex.Replace(result, @"\bns2:\b", "");
            // parse as json object
            JObject paymentResponse = JObject.Parse(result);
            // return response
            return paymentResponse["SOAP-ENV:Envelope"]?["SOAP-ENV:Body"]?["SinglePaymentResponse"]?["CardPaymentResponse"];
        }
    }
}