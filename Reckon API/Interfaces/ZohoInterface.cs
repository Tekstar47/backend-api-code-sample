using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;


// Note Zoho Response Codes
// 3000 is success
// 3100 is no data found (also a success as far as we are concerned)
namespace SPL_API.Zoho
{
    public static class ZohoAPIController
    {
        private static string ZohoAccessToken = null;
        private static DateTime CreatedTime = DateTime.MinValue;
        private static HttpClient Client = new HttpClient();

        private static async Task RefreshAccessToken()
        {
            // Access token is valid for 30 minutes. So we will refresh it if it is older than 20 minutes or if it is null.
            if ((CreatedTime.AddMinutes(20) >= DateTime.Now && ZohoAccessToken != null))
            {
                return;
            }

            if (Client == null)
            {
                Client = new();
            }


            string ZOHO_API_CLIENT_ID = Environment.GetEnvironmentVariable("ZOHO_API_CLIENT_ID");
            string ZOHO_API_CLIENT_SECRET = Environment.GetEnvironmentVariable("ZOHO_API_CLIENT_SECRET");
            string ZOHO_API_REFRESH_TOKEN = Environment.GetEnvironmentVariable("ZOHO_API_REFRESH_TOKEN");
            string URL = $"https://accounts.zoho.com/oauth/v2/token?refresh_token={ZOHO_API_REFRESH_TOKEN}&client_id={ZOHO_API_CLIENT_ID}&client_secret={ZOHO_API_CLIENT_SECRET}&grant_type=refresh_token";

            Console.WriteLine("GET SPL Access Token");

            Dictionary<string, string> postBody = new();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, URL) { Content = new FormUrlEncodedContent(postBody) };
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Unable to generate access token");
            }

            string accessToken = JsonConvert.DeserializeObject<ZohoAuthResponse>(response.Content.ReadAsStringAsync().Result)?.access_token;

            if (accessToken == null)
            {
                throw new Exception("Unable to generate access token");
            }

            request.Dispose();
            response.Dispose();

            ZohoAccessToken = accessToken;
            CreatedTime = DateTime.Now;
            Client.DefaultRequestHeaders.Remove("Authorization");
            Client.DefaultRequestHeaders.Add("Authorization", $"Zoho-oauthtoken {ZohoAccessToken}");
            return;
        }

        // This provides generic methods to interact with the Zoho API to reduce code duplication.
        private static class ZohoAPI<T> where T : class, new()
        {
            public static async Task<List<T>> GETAll(string URL)
            {
                List<T> result = new();

                try
                {
                    await RefreshAccessToken();
                    const int MAX_LOOP_ITERATIONS = 200;
                    int loop_count = 0;

                    string record_cursor = null;

                    Console.WriteLine($"GET {URL}");
                    do
                    {
                        loop_count++;
                        Dictionary<string, string> postBody = new();
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, URL);
                        if (record_cursor != null)
                        {
                            request.Headers.Add("record_cursor", record_cursor);
                        }
                        HttpResponseMessage response = await Client.SendAsync(request);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            request.Dispose();
                            response.Dispose();
                            Console.WriteLine("Non-OK Response");
                            return result;
                        }

                        // If record cursor exists, there is more data to fetch.
                        if (response.Headers.Contains("record_cursor")) { record_cursor = response.Headers.GetValues("record_cursor").FirstOrDefault(); }
                        else { record_cursor = null; }

                        ZohoGETResponseHeader<T> res = JsonConvert.DeserializeObject<ZohoGETResponseHeader<T>>(response.Content.ReadAsStringAsync().Result);

                        request.Dispose();
                        response.Dispose();

                        if (res == null)
                        {
                            throw new Exception("No response data");

                        }

                        switch (res.code)
                        {
                            case 3000:
                                {
                                    result.AddRange(res.data);
                                    break;
                                }
                            // Do nothing as no data was returned
                            case 3100: { break; }
                            default: { throw new Exception("Bad response data"); }
                        }
                    } while (loop_count <= MAX_LOOP_ITERATIONS && record_cursor != null);
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unhandled exception while fetching data from Zoho API: " + ex.Message);
                    throw new Exception(ex.Message);
                    //return result;
                }
            }

            public static async Task<List<string>> POSTAll(string URL, List<T> items)
            {
                Console.WriteLine($"POST: {URL}");
                List<string> newZIDs = new();
                if (items == null || items.Count == 0)
                {
                    Console.WriteLine("No items");
                    return newZIDs;
                }
                try
                {
                    await RefreshAccessToken();

                    const int MAX_LOOP_ITERATIONS = 1000;
                    const int MAX_RECORDS_IN_SINGLE_POST = 200;
                    int loopCount = 0;
                    int recordCounter = 0;

                    do
                    {
                        // POST
                        var count = Math.Min(MAX_RECORDS_IN_SINGLE_POST, items.Count() - recordCounter);
                        ZohoPOSTRequest<T[]> requestBody = new() { data = items.GetRange(recordCounter, count).ToArray() };

                        var p = JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });

                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, URL)
                        {
                            Content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            }), Encoding.UTF8, "application/json"),
                        };
                        HttpResponseMessage response = await Client.SendAsync(request);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            request.Dispose();
                            response.Dispose();
                            throw new Exception("Non-Ok response");
                        }

                        ZohoPOSTResponseBody res = JsonConvert.DeserializeObject<ZohoPOSTResponseBody>(response.Content.ReadAsStringAsync().Result);

                        request.Dispose();
                        response.Dispose();

                        if (res == null)
                        {
                            throw new Exception("No response data");
                        }

                        res.result.ForEach(x => newZIDs.Add(x.data.ID));
                        recordCounter += count;
                        loopCount += 1;
                    }
                    while (loopCount <= MAX_LOOP_ITERATIONS && recordCounter < items.Count());

                    return newZIDs;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unhandled exception while posting data to Zoho API: " + ex.Message);
                    return newZIDs;
                }
            }
        }

        /// <summary>
        /// Fetches the Carton Cloud configuration from the zoho server by a given warehouse ZID
        /// </summary>
        /// <param name="warehouseZID">ZID for a warehouse</param>
        /// <returns>Object containing the configurations for the carton cloud warehouse to interact with the carton cloud API</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<ZohoCartonCloudConfiguration> GETCartonCloudConfiguration(string warehouseZID)
        {
            Console.WriteLine("GET SPL CartonCloud Configuration");
            if (string.IsNullOrEmpty(warehouseZID))
            {
                return null;
            }

            ZohoCriteriaString criteria = new();
            criteria.Add($"Warehouse={warehouseZID}");
            
            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/report/CartonCloudConfiguration_Report?max_records=200&{criteria.ToString()}";

            var result = await ZohoAPI<ZohoCartonCloudConfiguration>.GETAll(URL); 
            if (result.Count() == 0)
            {
                return null;
            }

            return result[0];
        }

        /// <summary>
        /// Fetches the Carton Cloud configuration from the zoho server by Client ID and Client Secret
        /// </summary>
        /// <param name="clientID">Carton Cloud Client ID</param>
        /// <param name="clientSecret">Carton Cloud Client Secret</param>
        /// <returns>Object containing the configurations for the carton cloud warehouse to interact with the carton cloud API</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<ZohoCartonCloudConfiguration> GETCartonCloudConfiguration(string clientID, string clientSecret)
        {
            Console.WriteLine("GET SPL CartonCloud Configuration");
            if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret))
            {
                return null;
            }

            ZohoCriteriaString criteria = new();
            criteria.Add($"client_id=\"{clientID}\"");
            criteria.Add($"client_secret=\"{clientSecret}\"");

            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/report/CartonCloudConfiguration_Report?max_records=200&{criteria.ToString()}";

            var result = await ZohoAPI<ZohoCartonCloudConfiguration>.GETAll(URL);
            if (result.Count() == 0)
            {
                return null;
            }

            return result[0];
        }


        /// <summary>
        /// Fetches the webhook record from the zoho server by receipt reference
        /// </summary>
        /// <returns>Webhook Record</returns>
        /// <exception cref="Exception">Some unhandled error case</exception>
        public static async Task<ZohoCartonCloudWebhookRecord> GETCartonCloudWebhookRecord(string numericId, string status)
        {
            await RefreshAccessToken();

            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/report/AZURE_CartonCloud_Webhook_Records_Report?max_records=200&criteria=NumericId=\"" + numericId + "\"%26%26STATUS=\"" + status + "\"";
            string record_cursor = null;

            Console.WriteLine("GETCartonCloudWebhookRecord");
            Dictionary<string, string> postBody = new();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, URL);
            if (record_cursor != null)
            {
                request.Headers.Add("record_cursor", record_cursor);
            }
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("No receipt found");
                return null;
            }

            // If record cursor exists, there is more data to fetch.
            if (response.Headers.Contains("record_cursor")) { record_cursor = response.Headers.GetValues("record_cursor").FirstOrDefault(); }
            else { record_cursor = null; }

            ZohoGETResponseHeader<ZohoCartonCloudWebhookRecord> res = JsonConvert.DeserializeObject<ZohoGETResponseHeader<ZohoCartonCloudWebhookRecord>>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            if (res == null)
            {
                throw new Exception("Bad response when fetching webhook records");
            }

            switch (res.code)
            {
                case 3000:
                    {
                        return res.data[0];
                    }
                // Do nothing as no data was returned
                case 3100: { break; }
                default: { throw new Exception("Bad response when fetching webhook records"); }
            }

            return null;
        }


        /// <summary>
        /// Posts a log to the zoho server containing logging/debug information for the webhook.
        /// </summary>
        /// <param name="type">See CartonCloudWebhookLogTypes</param>
        /// <returns></returns>
        public static async Task<bool> POSTWebhookLog(string type, string log)
        {
            await RefreshAccessToken();
            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/form/Carton_Cloud_Inbound_Outbound_Log";
            try
            {
                ZohoPOSTRequest<ZohoInboundOutboundLog> requestBody = new() { data = new() { LogType = type, Log = log } };

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, URL)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }), Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await Client.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    request.Dispose();
                    response.Dispose();
                    throw new Exception("Unable to POST POSTWebhookLog record");
                }

                ZohoPOSTResponseBody res = JsonConvert.DeserializeObject<ZohoPOSTResponseBody>(response.Content.ReadAsStringAsync().Result);

                request.Dispose();
                response.Dispose();

                if (res == null)
                {
                    throw new Exception("POSTWebhookLog: Bad response when adding log");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        /// <summary>
        /// Posts the webhook record to the zoho server. This will be converted into a receipt or a pick slip based on the content of the webhook record.
        /// </summary>
        /// <param name="webhookRecord">A newly created receipt/pickslip</param>
        /// <returns>recordZID</returns>
        public static async Task<string> POSTWebhook(ZohoCartonCloudWebhookRecord webhookRecord)
        {
            Console.WriteLine("POSTWebhook");
            if (webhookRecord == null)
            {
                return null;
            }

            await RefreshAccessToken();

            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/form/CartonCloudWebhookRecords";

            try
            {
                ZohoPOSTRequest<ZohoCartonCloudWebhookRecord> requestBody = new() { data = webhookRecord };

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, URL)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }), Encoding.UTF8, "application/json"),
                };

                HttpResponseMessage response = await Client.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    request.Dispose();
                    response.Dispose();
                    throw new Exception("Unable to POST receipt record");
                }

                ZohoPOSTResponseBodyResult res = JsonConvert.DeserializeObject<ZohoPOSTResponseBodyResult>(await response.Content.ReadAsStringAsync());

                request.Dispose();
                response.Dispose();

                if (res == null)
                {
                    throw new Exception("Bad response when posting webhook records");
                }

                switch (res.code)
                {
                    case 3000:
                        {
                            // Success
                            return res.data.ID;
                        }
                    // Do nothing as no data was returned
                    default:
                        {
                            Console.WriteLine("Unable to POST webhook record");
                            break;
                        }
                }

                return null;

            }
            catch (Exception)
            {
                return null;
            }
        }


        /// <summary>
        /// Posts the webhook lines to the zoho server.
        /// </summary>
        /// <param name="webhookLines">A newly created webhook lines</param>
        public static async Task<bool> POSTWebhookLines(List<ZohoCartonCloudWebhookRecordLine> webhookLines)
        {
            Console.WriteLine("POSTWebhookLines");
            if (webhookLines == null)
            {
                return false;
            }

            await RefreshAccessToken();

            string URL = $"https://creator.zoho.com/api/v2.1/redacted/redacted/form/CartonCloudWebhookRecordLines";

            try
            {
                ZohoPOSTRequest<ZohoCartonCloudWebhookRecordLine[]> requestBody = new() { data = webhookLines.ToArray() };

                var p = JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, URL)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    }), Encoding.UTF8, "application/json"),
                };
                HttpResponseMessage response = await Client.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    request.Dispose();
                    response.Dispose();
                    throw new Exception("Unable to POST receipt lines record");
                }

                ZohoPOSTResponseBody res = JsonConvert.DeserializeObject<ZohoPOSTResponseBody>(response.Content.ReadAsStringAsync().Result);

                request.Dispose();
                response.Dispose();

                if (res == null)
                {
                    throw new Exception("Bad response when posting webhook lines records");
                }

                if (res.code != 3000)
                {
                    return false;
                }

                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// This class is used to build a criteria string for Zoho API requests. 
    /// </summary>
    public class ZohoCriteriaString
    {
        private string Criteria { get; set; } = "";

        public override string ToString()
        {
            return Criteria;
        }

        public void Add(string newCriteria)
        {
            if (Criteria.Length == 0)
            {
                Criteria = "criteria=" + newCriteria;
            }
            else
            {
                Criteria = Criteria + "%26%26" + newCriteria;
            }
        }

        public void Add(ZohoCriteriaString newCriteria)
        {
            this.Add(newCriteria.ToString().Replace("criteria=", ""));
        }

        public bool IsNullOrEmpty()
        {
            return String.IsNullOrEmpty(Criteria);
        }


    }

    #region ZohoPureObjects
    public class ZohoAuthResponse
    {
        public string access_token { get; set; }
        public string scope { get; set; }
        public string api_domain { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
    }

    public class ZohoLookupField
    {
        public string ID { get; set; }
        public string zc_display_value { get; set; }
    }

    public class ZohoPOSTRequest<T>
    {
        public T data { get; set; }
        public ZohoPOSTRequestResult result { get; set; }
    }

    public class ZohoPOSTRequestResult
    {
        public List<string> fields { get; set; }
        public bool message { get; set; }
        public bool tasks { get; set; }
    }

    public class ZohoPOSTResponseBody
    {
        public int code { get; set; }
        public List<ZohoPOSTResponseBodyResult> result { get; set; }
    }

    public class ZohoPOSTResponseBodyResult
    {
        public int code { get; set; }
        public ZohoPOSTResponseBodyResultData data { get; set; }
        public string message { get; set; }
    }

    public class ZohoPOSTResponseBodyResultData
    {
        public string ID { get; set; }
    }

    public class ZohoGETResponseHeader<T>
    {
        public int code { get; set; }
        public List<T> data { get; set; }
    }

    public class ZohoPATCHRequestBody<T>
    {
        public T data { get; set; }
    }

    public class ZohoDELETERequestBody
    {
        public string criteria { get; set; }
        public List<string> skip_workflow { get; set; }
        public ZohoDELETERequestBodyResult result { get; set; }
    }

    public class ZohoDELETERequestBodyResult
    {
        public bool message { get; set; }
        public bool tasks { get; set; }
    }

    public class ZohoDELETEReponseBody
    {
        public int code { get; set; }
        public object result { get; set; }
        public bool more_records { get; set; }
    }

    public class ZohoAddress
    {
        public string address_line_1 { get; set; }
        public string address_line_2 { get; set; }
        public string district_city { get; set; }
        public string state_province { get; set; }
        public string country { get; set; }
        public string post_code { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
    }

    #endregion

    #region ZohoApplicationObjects

    public class ZohoInboundOutboundLog
    {
        public string LogType { get; set; }
        public string Log { get; set; }
    }

    public class ZohoCartonCloudWebhookRecord
    {
        public string ID { get; set; }
        public string Type_field { get; set; }
        public string Date_field { get; set; }
        public string STATUS { get; set; }
        public string DeliveryInstructions { get; set; }
        public string Warehouse { get; set; }
        public string NumericId { get; set; }
        public string Customer_Reference { get; set; }
        public List<ZohoCartonCloudWebhookRecordLine> Lines { get; set; }
    }

    public class ZohoCartonCloudWebhookRecordLine
    {
        public string ID { get; set; }
        public string Record { get; set; } // WebhookRecordZID
        public string ProductCode { get; set; }
        public int Quantity { get; set; }
        public string Expiry_Date_String { get; set; } // Optional
    }

    public class ZohoCustomerAddress
    {
        public string Email { get; set; }
        public ZohoAddress Address { get; set; }
        public string ID { get; set; }
        public string zc_display_value { get; set; }
    }
    #endregion

    #region CartonCloudObjects
    public class ZohoCartonCloudConfiguration
    {
        public string ID { get; set; }
        public ZohoCartonCloudConfigurationWarehouse Warehouse { get; set; }
        public string client_id { get; set; }
        public string client_secret { get; set; }
        public string url { get; set; }
        public string tenant_uuid { get; set; }
        public string customer_uuid { get; set; }
        public string warehouse_uuid { get; set; }
    }

    public class ZohoCartonCloudConfigurationWarehouse : ZohoLookupField
    {
        public string Warehouse_Name { get; set; }
    }

    #endregion


}



