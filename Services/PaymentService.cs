#nullable enable
using System;
using System.IO;
using System.Linq;
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
        Task<JToken?> GetVaultedCard(string vaultId);
    }
    public class PaymentService : IPayment
    {
        private readonly RestClient _client;
        public PaymentService()
        {
            _client = new RestClient("https://secure.paygate.co.za/payhost/process.trans");
        }
        public async Task<JToken> AddNewCard(NewCard card)
        {
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
            IRestResponse response = await _client.ExecuteAsync(request);
            
            // example response
            /*{
                  "SinglePaymentResponse": {
                    "@xmlns:ns2": "http://www.paygate.co.za/PayHOST",
                    "CardPaymentResponse": {
                      "Status": {
                        "TransactionId": "292777334",
                        "Reference": "55813452-ddb6-4cfd-bdd5-bdef07fdd2ea",
                        "AcquirerCode": "00",
                        "StatusName": "Completed",
                        "AuthCode": "JIUW72",
                        "PayRequestId": "4EE45210-F7E4-494C-82CD-6C2FB97F2102",
                        "VaultId": "8b1f081f-9bf0-4351-8403-0ff28e8fab36",
                        "PayVaultData": [
                          {
                            "name": "cardNumber",
                            "value": "xxxxxxxxxxxx0015"
                          },
                          {
                            "name": "expDate",
                            "value": "102023"
                          }
                        ],
                        "TransactionStatusCode": "1",
                        "TransactionStatusDescription": "Approved",
                        "ResultCode": "990017",
                        "ResultDescription": "Auth Done",
                        "Currency": "ZAR",
                        "Amount": "1530",
                        "RiskIndicator": "AP",
                        "PaymentType": {
                          "Method": "CC",
                          "Detail": "MasterCard"
                        }
                      }
                    }
                  }
                }
             */
            string[] map = {"SinglePaymentResponse", "CardPaymentResponse"};
            JToken? result = MapXmlResponseToObject(response.Content, map);
            // check payment response
            if (result?["Status"] != null)
            {
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
                        throw new ApplicationException($"{paymentStatus["ResultCode"]}: Payment declined");
                    
                    case "ThreeDSecureRedirectRequired":
                        return result;
                }
            }
            return result ?? throw new ApplicationException("Payment request returned no results");
        }

        public async Task<JToken?> GetVaultedCard(string vaultId)
        {
            RestRequest request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/xml");
            request.AddHeader("SOAPAction", "SingleVaultRequest");
            
            // request body
            string body;
            using (StreamReader reader = new StreamReader(Directory.GetCurrentDirectory() + "/Templates/SingleVaultRequest.xml"))
            {
                body = await reader.ReadToEndAsync();
            }

            body = body.Replace("{PayGateId}", "");
            body = body.Replace("{Password}", "");
            body = body.Replace("{VaultId}", vaultId);
            request.AddParameter("text/xml", body, ParameterType.RequestBody);
            IRestResponse response = await _client.ExecuteAsync(request);
            
            // example positive response
            /*{
              "SingleVaultResponse": {
                "@xmlns:ns2": "http://www.paygate.co.za/PayHOST",
                "LookUpVaultResponse": {
                  "Status": {
                    "StatusName": "Completed",
                    "PayVaultData": [
                      {
                        "name": "cardNumber",
                        "value": "520000xxxxxx0015"
                      },
                      {
                        "name": "expDate",
                        "value": "102023"
                      }
                    ],
                    "PaymentType": {
                      "Method": "CC",
                      "Detail": "MasterCard"
                    }
                  }
                }
              }
            }
             */
            string[] map = {"SingleVaultResponse", "LookUpVaultResponse"};
            JToken? result = MapXmlResponseToObject(response.Content, null);
            return result;
        }

        private static JToken? MapXmlResponseToObject(string xmlContent, string[]? responseKeys)
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
            JToken? response = paymentResponse["SOAP-ENV:Envelope"]?["SOAP-ENV:Body"];
            if (responseKeys != null)
            {
                response = responseKeys.Aggregate(response, (current, t) => current?[t]);
            }
            return response;
        }
    }
}