using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SPL_API.Zoho;

namespace SPL_API.CartonCloud
{
    internal class APIController
    {
        private string ClientID;
        private string ClientSecret;
        private string CartonCloudAPIURL;
        private string TenantUUID;
        public string WarehouseUUID { get; private set; }
        public string CustomerUUID { get; private set; }
        private string AccessToken;
        private DateTime AccessTokenExpiresAt;
        private DateTime CreatedTime;
        private HttpClient Client;

        public APIController(ZohoCartonCloudConfiguration config)
        {
            ClientID = config.client_id;
            ClientSecret = config.client_secret;
            CartonCloudAPIURL = config.url;
            TenantUUID = config.tenant_uuid;
            CustomerUUID = config.customer_uuid;
            WarehouseUUID = config.warehouse_uuid;
            CreatedTime = DateTime.Now;

            ClientInit();
            if (Client == null)
            {
                throw new Exception("Unable to initialise Client");
            }
        }

        ~APIController()
        {
            ClientDestroy();
        }

        public void ClientInit()
        {
            if (Client == null)
            {
                Client = new HttpClient();
                Client.DefaultRequestHeaders.Add("Accept-Version", "1");

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{CartonCloudAPIURL}/uaa/oauth/token?grant_type=client_credentials");
                request.Headers.Add("Authorization", $"Basic {AppUtilities.Base64Encode($"{ClientID}:{ClientSecret}")}");

                string postData = HttpUtility.UrlEncode("grant_type=client_credentials", Encoding.UTF8);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(postData);
                ByteArrayContent content = new ByteArrayContent(data);
                request.Content = content;
                request.Content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                var response = Client.SendAsync(request).Result;

                string responseText = response.Content.ReadAsStringAsync().Result;
                string responseStatusCode = response.StatusCode.ToString();
                if (responseStatusCode != "OK")
                {
                    ClientDestroy();
                    return;
                }
                var result = JsonConvert.DeserializeObject<AuthResponse>(responseText);

                if (result == null)
                {
                    ClientDestroy();
                    return;
                }

                AccessToken = result.access_token;
                AccessTokenExpiresAt = CreatedTime.AddSeconds(result.expires_in);

                Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AccessToken}");
            }
        }

        public void ClientDestroy()
        {
            if (Client != null)
            {
                Client.Dispose();
                Client = null!;
            }
        }

        public async Task<GETProductResponse> GETProduct(string ProductUUID)
        {
            Console.WriteLine("Carton Cloud GETProduct: ", ProductUUID);
            if (ProductUUID == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/warehouse-products/{ProductUUID}");
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud GETProduct: Invalid ProductUUID");
                return null;
            }

            GETProductResponse data = JsonConvert.DeserializeObject<GETProductResponse>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;
        }

        public async Task<POSTProductResponse> POSTProduct(POSTProductRequest postBody)
        {
            Console.WriteLine("Carton Cloud POSTProduct");
            if (postBody == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/warehouse-products")
            {
                Content = new StringContent(JsonConvert.SerializeObject(postBody, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }), Encoding.UTF8, "application/json"),
            };
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud POSTProduct: Invalid RequestBody");
                return null;
            }

            POSTProductResponse data = JsonConvert.DeserializeObject<POSTProductResponse>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;

        }

        public async Task<string> POSTSOHReport()
        {
            Console.WriteLine("Carton Cloud POSTRequestSOHReport");

            const int PAGE_SIZE = 100;
            List<string> AGGREGATE_BY = new() { "productType", "expiryDate", "unitOfMeasure", "productStatus" };

            POSTSOHReportRequest requestBody = new()
            {
                parameters =
                {
                    aggregateBy = AGGREGATE_BY,
                    pageSize = PAGE_SIZE,
                    customer =
                    {
                        id = CustomerUUID
                    },
                    warehouse =
                    {
                        id = WarehouseUUID
                    }
                }
            };

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/report-runs")
            {
                Content = new StringContent(JsonConvert.SerializeObject(requestBody, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }), Encoding.UTF8, "application/json"),
            };
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.Created)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud POSTSOHReport: Invalid RequestBody");
                return null;
            }

            POSTSOHReportResponse data = JsonConvert.DeserializeObject<POSTSOHReportResponse>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data?.id;
        }

        public async Task<GETSOHReportResponse> GETSOHReport(string reportUUID)
        {
            Console.WriteLine("Carton Cloud GETSOHReport: ", reportUUID);
            if (reportUUID == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/report-runs/{reportUUID}");
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud GETSOHReport: Invalid ReportUUID");
                return null;
            }

            GETSOHReportResponse data = JsonConvert.DeserializeObject<GETSOHReportResponse>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;
        }

        public async Task<GETOutboundOrder> GETSalesOrder(string SalesOrderUUID)
        {
            Console.WriteLine("Carton Cloud GETSalesOrder: ", SalesOrderUUID);
            if (SalesOrderUUID == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/outbound-orders/{SalesOrderUUID}");
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud GETSalesOrder: Invalid SalesOrderUUID");
                return null;
            }

            GETOutboundOrder data = JsonConvert.DeserializeObject<GETOutboundOrder>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;
        }

        public async Task<GETOutboundOrder> POSTSalesOrder(POSTOutboundOrder postBody)
        {
            Console.WriteLine("Carton Cloud POSTSalesOrder");
            if (postBody == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/outbound-orders")
            {
                Content = new StringContent(JsonConvert.SerializeObject(postBody, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }), Encoding.UTF8, "application/json"),
            };
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud POSTSalesOrder: Invalid RequestBody");
                return null;
            }

            GETOutboundOrder data = JsonConvert.DeserializeObject<GETOutboundOrder>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;
        }

        public async Task<GETInboundOrder> POSTPurchaseOrder(POSTInboundOrder postBody)
        {
            Console.WriteLine("Carton Cloud POSTPurchaseOrder");
            if (postBody == null)
            {
                return null;
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{CartonCloudAPIURL}/tenants/{TenantUUID}/inbound-orders")
            {
                Content = new StringContent(JsonConvert.SerializeObject(postBody, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }), Encoding.UTF8, "application/json"),
            };
            HttpResponseMessage response = await Client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);

                request.Dispose();
                response.Dispose();
                Console.WriteLine("Carton Cloud POSTPurchaseOrder: Invalid RequestBody");
                return null;
            }

            GETInboundOrder data = JsonConvert.DeserializeObject<GETInboundOrder>(response.Content.ReadAsStringAsync().Result);

            request.Dispose();
            response.Dispose();

            return data;
        }
    }

    #region Base Objects

    public class AuthResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string scope { get; set; }
        public string iss { get; set; }
        public string jti { get; set; }

        // Also { "user" : { "id" : string } } but doesn't seem necessary
    }

    public class References
    {
        public string customer { get; set; }
        public string tracking { get; set; }
        public string alternateReference { get; set; }
    }

    public class Customer
    {
        public string id { get; set; }
        public string name { get; set; }
        public CustomerReferences references { get; set; }

        public Customer()
        {
            references = new CustomerReferences();
        }
    }

    public class CustomerReferences
    {
        public string code { get; set; }
    }

    public class Warehouse
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class Address
    {
        public string companyName { get; set; }
        public string contactName { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string suburb { get; set; }
        public string city { get; set; }
        public State state { get; set; }
        public string postcode { get; set; }
        public Country country { get; set; }
        public string phone { get; set; }
        public string email { get; set; }

        public Address()
        {
            state = new State();
            country = new Country();
        }
    }

    public class State
    {
        public string code { get; set; }
        public string name { get; set; }
    }

    public class Country
    {
        public string name { get; set; }
        public string iso2Code { get; set; }
        public string iso3Code { get; set; }
    }

    public class DeliveryMethod
    {
        public string type { get; set; } // SHIPPING  | PICKUP
        public string requestedService { get; set; } // Standard | Express | Overnight etc??
    }


    public class UnitOfMeasure
    {
        public string type { get; set; }
    }

    public class Money
    {
        public int amount { get; set; }
        public string currency { get; set; }
    }


    #endregion

    #region GET Product Objects
    public class GETProductResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public string scope { get; set; }
        public string defaultUnitOfMeasure { get; set; }
        public GETProductResponseCustomer customer { get; set; }
        public GETProductResponseReferences references { get; set; }
        public GETProductResponseDetails details { get; set; }
        public GETProductResponseUnitOfMeasures unitOfMeasures { get; set; }
    }

    public class GETProductResponseCustomer
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool enabled { get; set; }
    }

    public class GETProductResponseReferences
    {
        public string code { get; set; }
    }

    public class GETProductResponseDetails
    {
        public bool variableWeight { get; set; }
        public GETProductResponseStorage storage { get; set; }
        public GETProductResponseInbound inbound { get; set; }
        public GETProductResponseStockSelection stockSelection { get; set; }
    }

    public class GETProductResponseInbound
    {
        public string initialStatus { get; set; }
    }

    public class GETProductResponseStockSelection
    {
        public string method { get; set; }
        public string secondaryMethod { get; set; }
        public bool strict { get; set; }
        public int expiryThresholdDays { get; set; }
    }

    public class GETProductResponseStorage
    {
        public string chargeMethod { get; set; }
    }

    public class GETProductResponseUnitOfMeasures
    {
        public CartonCloudCTN CTN { get; set; }
    }

    public class CartonCloudCTN
    {
        public string name { get; set; }
        public int baseQty { get; set; }
        public int weight { get; set; }
        public int volume { get; set; }
        public string barcode { get; set; }
        public bool isDecimal { get; set; }
    }
    #endregion

    #region POST Product Objects
    public class POSTProductRequest
    {
        public string type { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string scope { get; set; }
        public string defaultUnitOfMeasure { get; set; }
        public ProductReferences references { get; set; }
        public CartonCloudIDSelector customer { get; set; }
        public ProductDetails details { get; set; }
        public ItemPropertyRequirements itemPropertyRequirements { get; set; }
        public ProductUnitOfMeasures unitOfMeasures { get; set; }
        public List<Notification> notifications { get; set; }
    }

    public class POSTProductResponse
    {
        public string id { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string scope { get; set; }
        public string defaultUnitOfMeasure { get; set; }
        public References references { get; set; }
        public CartonCloudIDSelector customer { get; set; }
        public ProductDetails details { get; set; }
        public ItemPropertyRequirements itemPropertyRequirements { get; set; }
        public ProductUnitOfMeasures unitOfMeasures { get; set; }
        public List<Notification> notifications { get; set; }
    }

    public class ProductDetails
    {
        public bool variableWeight { get; set; }
        public Storage storage { get; set; }
        public Inbound inbound { get; set; }
        public StockSelection stockSelection { get; set; }
    }

    public class StockSelection
    {
        public string method { get; set; }
        public string secondaryMethod { get; set; }
        public bool strict { get; set; }
        public int expiryThresholdDays { get; set; }
    }

    public class Storage
    {
        public string chargeMethod { get; set; }
    }

    public class Inbound
    {
        public string initialStatus { get; set; }
    }

    public class ItemPropertyRequirements
    {
        public string expiry { get; set; }
        public string batch { get; set; }
    }

    public class ProductUnitOfMeasures
    {
        public Units units { get; set; }
        public Cartons cartons { get; set; }
        public Pallets pallets { get; set; }
    }

    public class Units
    {
        public int baseQty { get; set; }
        public double weight { get; set; }
        public double volume { get; set; }
    }

    public class Cartons
    {
        public int baseQty { get; set; }
    }

    public class Pallets
    {
        public int baseQty { get; set; }
    }

    public class Notification
    {
        public string type { get; set; }
        public int thresholdDays { get; set; }
        public int? thresholdCount { get; set; }
    }
    #endregion

    #region POST SOH Report Objects

    public class POSTSOHReportRequest
    {
        public string type { get; set; } = "STOCK_ON_HAND";
        public POSTSOHReportParameters parameters { get; set; }

        public POSTSOHReportRequest()
        {
            parameters = new POSTSOHReportParameters();
        }

    }

    public class POSTSOHReportParameters
    {
        public int pageSize { get; set; }
        public CartonCloudIDSelector warehouse { get; set; }
        public CartonCloudIDSelector customer { get; set; }
        public List<string> aggregateBy { get; set; }
        // AggregateBy can only be a fixed set of values as found here https://api-docs.cartoncloud.com/#create-report-run

        public POSTSOHReportParameters()
        {
            warehouse = new CartonCloudIDSelector();
            customer = new CartonCloudIDSelector();
        }

    }

    public class CartonCloudIDSelector
    {
        public string id { get; set; }
    }


    public class POSTSOHReportResponse
    {
        public string id { get; set; }
        public string type { get; set; }
        public string status { get; set; } // "In Progress"
        public POSTSOHReportParameters parameters { get; set; }
    }

    #endregion

    #region GET SOH Report Objects

    public class GETSOHReportResponse
    {
        public string id { get; set; }
        public string type { get; set; }
        public string status { get; set; }
        public string reportTime { get; set; }
        public POSTSOHReportParameters parameters { get; set; }
        public List<SOHWarehouseItem> items { get; set; }

        public GETSOHReportResponse()
        {
            parameters = new POSTSOHReportParameters();
            items = new List<SOHWarehouseItem>();
        }
    }

    public class SOHWarehouseItem
    {
        public string type { get; set; }
        public SOHWarehouseItemMeasures measures { get; set; }
        public SOHWarehouseItemProperties properties { get; set; }
        public SOHWarehouseItemDetails details { get; set; }

        public SOHWarehouseItem()
        {
            measures = new SOHWarehouseItemMeasures();
            properties = new SOHWarehouseItemProperties();
            details = new SOHWarehouseItemDetails();
        }
    }


    public class SOHWarehouseItemMeasures
    {
        public int quantity { get; set; }
        public int quantityFree { get; set; }
        public int quantityIncoming { get; set; }
        public int quantityAllocated { get; set; }
    }

    public class SOHWarehouseItemProperties
    {
        public string productType { get; set; }
        public string productStatus { get; set; }
        public string expiryDate {get; set;}
        public InboundOrder inboundOrder { get; set; }
        public UnitOfMeasure unitOfMeasure { get; set; }

        public SOHWarehouseItemProperties()
        {
            inboundOrder = new InboundOrder();
            unitOfMeasure = new UnitOfMeasure();
        }
    }

    public class InboundOrder
    {
        public string id { get; set; }
    }


    public class SOHWarehouseItemDetails
    {
        public Product product { get; set; }
        public UnitOfMeasure unitOfMeasure { get; set; }

        public SOHWarehouseItemDetails()
        {
            product = new Product();
            unitOfMeasure = new UnitOfMeasure();
        }
    }

    public class Product
    {
        public string id { get; set; }
        public string name { get; set; }
        public ProductReferences references { get; set; }
        public ProductCustomer customer { get; set; }

        public Product()
        {
            references = new ProductReferences();
            customer = new ProductCustomer();
        }
    }

    public class ProductReferences
    {
        public string code { get; set; } // Not sure what fills this up. Use product code eg 72361 (copy of product name)
    }

    public class ProductCustomer
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool enabled { get; set; }
    }
    #endregion

    #region GET Outbound Order Objects


    public class GETOutboundOrder
    {
        public string id { get; set; }
        public string type { get; set; } // OUTBOUND
        public References references { get; set; }
        public Customer customer { get; set; }
        public Warehouse warehouse { get; set; }
        public OutboundOrderDetailsWithError details { get; set; }
        public string status { get; set; } // DRAFT | AWAITING_PICK_AND_PACK | PACKING_IN_PROGRESS | PACKED | DISPATCHED | REJECTED
        public OutboundOrderCustomProperties properties { get; set; }
        public List<OutboundOrderItem> items { get; set; }
        public double version { get; set; }
    }

    public class OutboundOrderDetailsWithError
    {
        public bool urgent { get; set; }
        public string instructions { get; set; }
        public Collect collect { get; set; }
        public Deliver deliver { get; set; }
        public Money invoiceValue { get; set; }
        public List<OutboundOrderDetailsError> errors { get; set; }
    }

    #endregion

    #region POST Outbound Order Objects


    public class POSTOutboundOrder
    {
        public string type { get; set; } // OUTBOUND
        public References references { get; set; }
        public Customer customer { get; set; } // Hardcode in Azure
        public Warehouse warehouse { get; set; } // Hardcode in Azure
        public OutboundOrderDetails details { get; set; }
        public OutboundOrderCustomProperties properties { get; set; } // NULL
        public List<OutboundOrderItem> items { get; set; }

        public POSTOutboundOrder()
        {
            references = new References();
            customer = new Customer();
            warehouse = new Warehouse();
            details = new OutboundOrderDetails();
            properties = new OutboundOrderCustomProperties();
            items = new List<OutboundOrderItem>();
        }
    }



    public class OutboundOrderDetails
    {
        public bool urgent { get; set; } = false;
        public string instructions { get; set; } = string.Empty;
        public Collect collect { get; set; }
        public Deliver deliver { get; set; }
        public Money invoiceValue { get; set; } // NULL

        public OutboundOrderDetails()
        {
            collect = new Collect();
            deliver = new Deliver();
            invoiceValue = new Money();
        }
    }



    public class OutboundOrderDetailsError
    {
        public string message { get; set; }
        public bool isResolved { get; set; }
    }

    public class Collect
    {
        public string requiredDate { get; set; }
    }

    public class Deliver
    {
        public Address address { get; set; }
        public DeliveryMethod method { get; set; } 
        public string instructions { get; set; }
        public string requiredDate { get; set; }
        public Money cashPaymentAmount { get; set; } // I think is null??

        public Deliver()
        {
            address = new Address();
            method = new DeliveryMethod();
            cashPaymentAmount = new Money();
        }
    }

    public class OutboundOrderCustomProperties
    {
        public string transportCompany { get; set; } = "IJ KM Knighton";
    }

    public class OutboundOrderItem
    {
        public OutboundOrderItemCustomProperties properties { get; set; }
        public OutboundOrderItemDetails details { get; set; }
        public OutboundOrderItemMeasures measures { get; set; }

        public OutboundOrderItem()
        {
            measures = new OutboundOrderItemMeasures();
            properties = new OutboundOrderItemCustomProperties();
            details = new OutboundOrderItemDetails();
        }
    }

    public class OutboundOrderItemCustomProperties
    {
        public string myCustomField { get; set; }
    }

    public class OutboundOrderItemDetails
    {
        public Product product { get; set; }
        public UnitOfMeasure unitOfMeasure { get; set; }

        public OutboundOrderItemDetails()
        {
            product = new Product();
            unitOfMeasure = new UnitOfMeasure();
        }
    }

    public class OutboundOrderItemMeasures
    {
        public double quantity { get; set; }

    }
    #endregion

    #region POST Inbound Order Objects

    public class GETInboundOrder
    {
        public string id { get; set; }
        public string type { get; set; } // INBOUND
        public string status { get; set; }
        public References references { get; set; }
        public Customer customer { get; set; }
        public Warehouse warehouse { get; set; }
        public InboundOrderDetails details { get; set; }
        public InboundOrderCustomProperties properties { get; set; }
        public List<InboundOrderItem> items { get; set; }
    }

    public class POSTInboundOrder
    {
        public string type { get; set; } = "INBOUND";
        public string status { get; set; } // = "DRAFT"; // Don't need to include this once tested
        public References references { get; set; }
        public Customer customer { get; set; }
        public Warehouse warehouse { get; set; }
        public InboundOrderDetails details { get; set; }
        public InboundOrderCustomProperties properties { get; set; }
        public List<InboundOrderItem> items { get; set; }
        
        public POSTInboundOrder()
        {
            references = new References();
            customer = new Customer();
            warehouse = new Warehouse();
            details = new InboundOrderDetails();
            properties = new InboundOrderCustomProperties();
            items = new List<InboundOrderItem>();
        }
    }

    public class InboundOrderDetails
    {
        public bool urgent { get; set; } = false;
        public string instructions { get; set; } = string.Empty;
        public string arrivalDate { get; set; } = string.Empty;
    }

    public class InboundOrderCustomProperties
    {
        public string container { get; set; }
        public string containerSize { get; set; }
        public string carrier { get; set; }
    }

    public class InboundOrderItem
    {
        public InboundOrderItemCustomProperties properties { get; set; }
        public InboundOrderItemDetails details { get; set; }
        public InboundOrderItemMeasures measures { get; set; }
        public InboundOrderItem()
        {
            measures = new InboundOrderItemMeasures();
            properties = new InboundOrderItemCustomProperties();
            details = new InboundOrderItemDetails();
        }
    }

    public class InboundOrderItemCustomProperties
    {
        public string expiryDate { get; set; }
        public string batch { get; set; }
    }

    public class InboundOrderItemDetails
    {
        public Product product { get; set; }
        public UnitOfMeasure unitOfMeasure { get; set; }
        public InboundOrderItemDetails()
        {
            product = new Product();
            unitOfMeasure = new UnitOfMeasure();
        }
    }

    public class InboundOrderItemMeasures
    {
        public double quantity { get; set; }
        public double cubic { get; set; }
    }
    #endregion
}
