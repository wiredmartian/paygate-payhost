#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using payhost.Models;
using RestSharp;

namespace payhost.Services
{
    public interface IPayment
    {
        /// <summary>
        /// make a payment and tokenize card for future use
        /// </summary>
        /// <param name="card">bank card information</param>
        /// <returns>request response from Paygate</returns>
        Task<CardPaymentResponse> AddNewCard(NewCard card);
        /// <summary>
        /// get the vaulted/tokenized card information
        /// </summary>
        /// <param name="vaultId">acquired when a card is vaulted</param>
        /// <returns>vaulted card information</returns>
        Task<JToken?> GetVaultedCard(string vaultId);
        /// <summary>
        /// query the status of a transaction using its id
        /// </summary>
        /// <param name="payRequestId">pay request id issued by Paygate when a payment requested is initialed</param>
        /// <returns>payment status information</returns>
        Task<JToken?> QueryTransaction(string payRequestId);
    }
    public class PaymentService : IPayment
    {
        private readonly RestClient _client;
        public PaymentService()
        {
            _client = new RestClient("https://secure.paygate.co.za/payhost/process.trans");
        }
        public async Task<CardPaymentResponse> AddNewCard(NewCard card)
        {
            CardPaymentResponse payResponse = new CardPaymentResponse
            {
                Completed = false,
            };
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
                payResponse.Response = JsonConvert.SerializeObject(result);
                JToken? paymentStatus = result["Status"];
                switch (paymentStatus?["StatusName"]?.ToString())
                {
                    case "Error":
                        throw new ApplicationException();
                    
                    case "Completed" when paymentStatus?["ResultCode"] != null:
                        payResponse.Completed = true;
                        payResponse.PayRequestId = paymentStatus?["PayRequestId"]?.ToString();
                        payResponse.Secure3DHtml = null;
                        if (paymentStatus?["ResultCode"]?.ToString() == "990017")
                        {
                            return payResponse;
                        }
                        throw new ApplicationException($"{paymentStatus?["ResultCode"]}: Payment declined");
                    
                    case "ThreeDSecureRedirectRequired":
                        // payment requires 3D verification
                        JToken? redirectXml = result["Redirect"];
                        if (redirectXml?["UrlParams"] != null)
                        {
                            HttpClient httpClient = new HttpClient();
                            string? redirectUrl = redirectXml["RedirectUrl"]?.ToString();
                            JArray urlParams = JArray.Parse(redirectXml["UrlParams"]?.ToString()!);
                            Dictionary<string, string> urlParamsDictionary = urlParams.Cast<JObject>()
                                .ToDictionary(item => item.GetValue("key")?.ToString(),
                                    item => item.GetValue("value")?.ToString())!;
                            string httpRequest = ToUrlEncodedString(urlParamsDictionary!);
                            StringContent content =
                                new StringContent(httpRequest, Encoding.UTF8, "application/x-www-form-urlencoded");
                            HttpResponseMessage res = await httpClient.PostAsync(redirectUrl, content);
                            res.EnsureSuccessStatusCode();
                            string responseContent = await res.Content.ReadAsStringAsync();
                            payResponse.Completed = false;
                            payResponse.Secure3DHtml = responseContent;
                            payResponse.PayRequestId = urlParamsDictionary["PAY_REQUEST_ID"];
                            return payResponse;
                        }
                        break;
                }
            }
            throw new ApplicationException("Payment request returned no results");
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
            }*/
            string[] map = {"SingleVaultResponse", "LookUpVaultResponse"};
            JToken? result = MapXmlResponseToObject(response.Content, map);
            return result;
        }

        public async Task<JToken?> QueryTransaction(string payRequestId)
        {
            RestRequest request = new RestRequest(Method.POST);
            request.AddHeader("Content-Type", "text/xml");
            request.AddHeader("SOAPAction", "SingleFollowUpRequest");
            
            // request body
            string body;
            using (StreamReader reader = new StreamReader(Directory.GetCurrentDirectory() + "/Templates/SingleFollowUpRequest.xml"))
            {
                body = await reader.ReadToEndAsync();
            }

            body = body.Replace("{PayGateId}", "");
            body = body.Replace("{Password}", "");
            body = body.Replace("{PayRequestId}", payRequestId);
            request.AddParameter("text/xml", body, ParameterType.RequestBody);
            IRestResponse response = await _client.ExecuteAsync(request);
            
            /*
             {
                "SingleFollowUpResponse": {
                "@xmlns:ns2": "http://www.paygate.co.za/PayHOST",
                "QueryResponse": {
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
                    },
                    "DateTime": "2021-06-14T15:03:25+02:00",
                    "TransactionType": "Authorisation"
                  }
                }
                }
                }
             */
            string[] map = {"SingleFollowUpResponse", "QueryResponse"};
            return MapXmlResponseToObject(response.Content, map);
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
        
        private static string ToUrlEncodedString(Dictionary<string, string?> request)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string key in request.Keys)
            {
                builder.Append("&");
                builder.Append(key);
                builder.Append("=");
                builder.Append(HttpUtility.UrlEncode(request[key]));
            }
            string result = builder.ToString().TrimStart('&');
            return result;
        }
    }
}