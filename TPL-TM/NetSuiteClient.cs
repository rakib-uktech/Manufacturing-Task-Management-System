using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace NetSuite
{
    public class NetSuiteClient
    {
        private readonly string _baseUrl;
        private readonly OAuth1HeaderGenerator _oauthHeaderGenerator;
        private readonly HttpClient _httpClient;

        public NetSuiteClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public NetSuiteClient(string consumerKey, string consumerSecret, string accessToken, string tokenSecret, string realm, string baseUrl)
        {
            if (string.IsNullOrEmpty(consumerKey) || string.IsNullOrEmpty(consumerSecret) || string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(tokenSecret))
            {
                throw new ArgumentException("OAuth credentials cannot be null or empty.");
            }

            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _oauthHeaderGenerator = new OAuth1HeaderGenerator(consumerKey, consumerSecret, accessToken, tokenSecret, realm);
        }

        public async Task<AssemblyItem> GetAssemblyItemAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                throw new ArgumentNullException(nameof(itemId), "Item ID cannot be null or empty.");
            }

            var url = $"{_baseUrl}/assemblyitem/{itemId}";
            using var client = new HttpClient();

            try
            {
                var authHeader = _oauthHeaderGenerator.Generate("GET", url);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = authHeader;
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error: {response.StatusCode}, Details: {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AssemblyItem>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve assembly item data: {ex.Message}", ex);
            }
        }

        public async Task<List<AssemblyItem>> GetAllAssemblyItemsAsync(int pageSize = 100)
        {
            var url = $"{_baseUrl}/assemblyitem?limit={pageSize}";
            using var client = new HttpClient();
            var allItems = new List<AssemblyItem>();
            int offset = 0;

            try
            {
                while (true)
                {
                    var paginatedUrl = $"{url}&offset={offset}";
                    var authHeader = _oauthHeaderGenerator.Generate("GET", paginatedUrl);

                    var request = new HttpRequestMessage(HttpMethod.Get, paginatedUrl);
                    request.Headers.Authorization = authHeader;
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Error: {response.StatusCode}, Details: {errorContent}");
                    }

                    var jsonResult = await response.Content.ReadAsStringAsync();
                    var assemblyItemResponse = JsonConvert.DeserializeObject<AssemblyItemResponse>(jsonResult);

                    if (assemblyItemResponse?.Data == null || assemblyItemResponse.Data.Count == 0)
                    {
                        break;
                    }

                    allItems.AddRange(assemblyItemResponse.Data);
                    offset += pageSize;
                }

                return allItems;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve assembly items: {ex.Message}", ex);
            }
        }

        public async Task<WorkOrder> GetWorkOrderAsync(string workOrderId)
        {
            if (string.IsNullOrEmpty(workOrderId))
            {
                throw new ArgumentNullException(nameof(workOrderId), "Work Order ID cannot be null or empty.");
            }

            var url = $"{_baseUrl}/workorder/{workOrderId}";
            using var client = new HttpClient();

            try
            {
                var authHeader = _oauthHeaderGenerator.Generate("GET", url);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = authHeader;
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error: {response.StatusCode}, Details: {errorContent}");
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WorkOrder>(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve work order data: {ex.Message}", ex);
            }
        }
       public async Task<object> UpdateWorkOrderScheduleAsync(
       string workOrderId,
       DateTime startDate,
       DateTime endDate,
       string customStartTime,
       string customEndTime)
        {
            // Build NetSuite payload
            var payload = new
            {
                id = workOrderId,
                tranDate = startDate.ToString("yyyy-MM-dd"), // main transaction date
                customStartTime = customStartTime, // custom field for production start
                customEndTime = customEndTime      // custom field for production end
            };

            Console.WriteLine($"Payload to NetSuite: {System.Text.Json.JsonSerializer.Serialize(payload)}");

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync("/rest/workorder/update", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<object>(json);
        }


    }

    public class AssemblyItem
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public string DisplayName { get; set; }
        public string Custitemproduct_Spec_Qtyperinnerlayer { get; set; }
        public string Custitemproduct_Spec_Casewtgrosskg { get; set; }
        public string Custitemproduct_Spec_Sku { get; set; }
        public string Custitemcustom_Product_Sepc_Case_Gtin { get; set; }
        public string Custitem13 { get; set; }
        public string Custitemproduct_Spec_Palletwtnetkg { get; set; }
        public string Custitemproduct_Spec_Palletwtgrosskg { get; set; }
        public string Custitemproduct_Spec_Caseperpallet { get; set; }
        public string Custitemproduct_Spec_Qtyperpallet { get; set; }
        public string Custitem_Approval_Status { get; set; }
        public string RefName { get; set; }
    }

    public class AssemblyItemResponse
    {
        [JsonProperty("data")]
        public List<AssemblyItem> Data { get; set; }
        [JsonProperty("links")]
        public object Links { get; set; }
    }

    public class WorkOrder
    {
        public string Id { get; set; }
        public string TranId { get; set; }
        public string TranDate { get; set; }
        public AssemblyItem AssemblyItem { get; set; }

    }


}
