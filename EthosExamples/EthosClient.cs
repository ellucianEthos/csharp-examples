
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace EthosExamples
{
    /// <summary>
    /// Class to interact with Ethos Hub RESTful services as a single tenant application
    /// </summary>
    public class EthosClient : HttpClient
    {

        #region constants
        private const int tokenExpiration = 5; //Number of minutes ethose token is valid
        private const string authUriPath = "auth";
        private const string publishUriPath = "publish";
        private const string consumeUriPath = "consume";
        private const string defaultJsonContentType = "application/json";
        private const string xRemainingHeader = "X-Remaining";
        public const string Offset_Param = "offset";
        public const string Limit_Param = "limit";
        protected const string DefaultJsonContentType = "application/json"; //defined as private in core base class :(
        public const string Accept_Header = "Accept";
        public const string Accept_Charset = "Accept-Charset";
        protected const string Media_Type_Header = "X-Media-Type";
        protected const string Total_Count_Header = "X-Total-Count";
        protected const string Content_Restricted_Header = "X-Content-Restricted";
        
        #endregion

        /// <summary>
        /// The URI of the ethos integration api endpoint
        /// </summary>
        public string EthosBaseURI { get; set; }

        /// <summary>
        /// The static API key of the application accessing the ethos integration api
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The number of messages to consume from the message queue at a time. Defaults to 10.
        /// </summary>
        public int MaxMessagesToConsume { get; set; }

        SemaphoreSlim authLock = new SemaphoreSlim(1, 1);

        private string authToken = null;
        private DateTime authTokenExpires = DateTime.MinValue;

        private string _ethosTenantId;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ethosURI"></param>
        /// <param name="apiKey"></param>
        /// <param name="source"></param>
        /// <param name="tenantId"></param>
        public EthosClient(string ethosUri, string apiKey, string tenantId = null) : base()
        {
            EthosBaseURI = ethosUri;
            ApiKey = apiKey;
            MaxMessagesToConsume = 10;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; 
            _ethosTenantId = tenantId;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public EthosClient() : base() { }

        /// <summary>
        /// Ensures a valid access token is set on the object
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Authenticate()
        {
            bool validToken = false;

            await authLock.WaitAsync();
            try
            {
                if (authToken == null || DateTime.UtcNow.CompareTo(authTokenExpires) >= 0)
                {
                    authToken = null;
                    authTokenExpires = DateTime.MinValue;
                    if (DefaultRequestHeaders.Contains(HttpRequestHeader.Authorization.ToString()))
                    {
                        DefaultRequestHeaders.Remove(HttpRequestHeader.Authorization.ToString());
                    }
                    try
                    {

                        DefaultRequestHeaders.Add(HttpRequestHeader.Authorization.ToString(), "Bearer " + ApiKey);
                        HttpResponseMessage authResponse = await PostAsync(EthosBaseURI + authUriPath, null);

                        if (authResponse.IsSuccessStatusCode)
                        {
                            authToken = authResponse.Content.ReadAsStringAsync().Result;
                            authTokenExpires = DateTime.UtcNow.AddMinutes(tokenExpiration);
                            DefaultRequestHeaders.Remove(HttpRequestHeader.Authorization.ToString());
                            DefaultRequestHeaders.Add(HttpRequestHeader.Authorization.ToString(), "Bearer " + authToken);
                            validToken = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        return false;

                    }
                }
                else
                {
                    //already have a valid token that has not expired
                    validToken = true;
                }
            }
            finally
            {
                authLock.Release();
            }
            return validToken;
        }

        /// <summary>
        /// Send a publish change notification
        /// </summary>
        /// <param name="publishRequest"></param>
        /// <returns></returns>
        public bool PublishChangeNotification(ChangeNotificationV2 publishRequest)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, EthosBaseURI + publishUriPath);
            httpRequest.Content = new StringContent(JsonConvert.SerializeObject(publishRequest));
            httpRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ChangeNotificationV2.HeaderContentType);
            var response = SendAsync(httpRequest).Result;
            return response.IsSuccessStatusCode;
        }


        /// <summary>
        /// Consume a single set of notifications. Expectation is that if there are remaining notifications, they will be picked up by next request from task service.
        /// </summary>
        /// <param name="lastMessageProcessedId">Include the ID of the last change notification processed to acknowledge previously retrieved notifications</param>
        /// <returns></returns>
        public IEnumerable<ChangeNotificationV2> ConsumeChangeNotifications(string lastMessageProcessedId = "-1")
        {
            string getURL = string.Format("{0}{1}?lastProcessedID={2}&max={3}", EthosBaseURI, consumeUriPath, lastMessageProcessedId, MaxMessagesToConsume);
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, getURL);
            //httpRequest.Headers.Accept = ChangeNotificationV2.HeaderContentType;
            var response = SendAsync(httpRequest).Result;
            if (response.IsSuccessStatusCode)
            {
                List<ChangeNotificationV2> notifications = JsonConvert.DeserializeObject<List<ChangeNotificationV2>>(response.Content.ReadAsStringAsync().Result);
                return notifications;
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }

        private void ConstructAcceptHeader(NameValueCollection headers, string versionHeaderType)
        {
            if (string.IsNullOrEmpty(versionHeaderType))
            {
                headers.Add(Accept_Header, DefaultJsonContentType);
            }
            else
            {
                headers.Add(Accept_Header, versionHeaderType);
            }

            headers.Add(Accept_Charset, "UTF-8");
        }

        protected virtual string ConstructResourceUri(string urlPrefix, string resourceName, string guid = null)
        {
            if (string.IsNullOrEmpty(urlPrefix) || string.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException("Invalid parameter values urlPrefix or resourceName cannot be null");
            }

            if (string.IsNullOrEmpty(guid))
            {
                return string.Format("{0}/{1}", urlPrefix, resourceName);
            }
            else
            {
                return string.Format("{0}/{1}/{2}", urlPrefix, resourceName, guid.ToString());
            }

        }


        private string EncodedUrlArguments(Dictionary<string, string> urlArgumentCollection)
        {
            if (urlArgumentCollection == null)
            {
                throw new ArgumentNullException("urlArgumentCollection");
            }

            var urlArgumentStrings = urlArgumentCollection.Select((keyValuePair) =>
            {
                var formattedString = string.Format("{0}={1}",
                                                    keyValuePair.Key,
                                                    HttpUtility.UrlEncode(keyValuePair.Value));
                return formattedString;
            });

            var formattedUrlArgumentString = string.Join("&", urlArgumentStrings);

            return formattedUrlArgumentString;
        }

        private string BuildFullUrl(string urlPath, Dictionary<string, string> urlArguments = null)
        {
            string combinedPath = EthosBaseURI + urlPath;
            if (urlArguments != null)
            {
                combinedPath += "?" + EncodedUrlArguments(urlArguments);
            }
            return combinedPath;
        }

        /// <summary>
        /// Returns the HttpResponseMessage, including error responses
        /// </summary>
        /// <param name="method"></param>
        /// <param name="jsonString"></param>
        /// <param name="relativePath"></param>
        /// <param name="urlArguments"></param>
        /// <param name="headers"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        protected HttpResponseMessage ExecuteRequest(HttpMethod method, string jsonString, string relativePath, Dictionary<string, string> urlArguments = null, NameValueCollection headers = null, string contentType = DefaultJsonContentType)
        {
            var fullUrl = BuildFullUrl(relativePath, urlArguments);

            var request = new HttpRequestMessage(method, fullUrl);
            if (headers != null)
            {
                foreach (var header in headers.AllKeys)
                {
                    request.Headers.Add(header, headers[header]);
                }
            }

            if (jsonString != null && string.IsNullOrEmpty(contentType))
            {
                contentType = DefaultJsonContentType;
            }


            if (jsonString != null && contentType != null)
            {
                request.Content = new StringContent(jsonString, Encoding.UTF8, contentType);
            }
            
            var responseMessage = SendAsync(request).Result;            
            return responseMessage;
        }
        
        /// <summary>
        /// Get a specific resource. 
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="resourceName"></param>
        /// <param name="versionContentType">Specify if a specific version of the model should be requested, if null, versionless will be used</param>
        /// <returns></returns>
        public virtual GetResponse Get(Guid guid, string resourceName, string versionContentType)
        {
            if (resourceName == null)
            {
                throw new Exception("ResourcePluralName missing. Unable to identify resource name : [" + resourceName + "]");
            }

            bool authenticated = Authenticate().Result;
            if (authenticated)
            {
                NameValueCollection headers = new NameValueCollection();
                ConstructAcceptHeader(headers, versionContentType);
                var resource = ConstructResourceUri("api", resourceName, guid.ToString());
                var response = ExecuteRequest(HttpMethod.Get, null, resource, null, headers);

                if (response.IsSuccessStatusCode)
                {
                    GetResponse getResponse = new GetResponse();
                    getResponse.Data = response.Content.ReadAsStringAsync().Result;

                    if (response.Headers.Contains(Media_Type_Header))
                        getResponse.Version = response.Headers.GetValues(Media_Type_Header).FirstOrDefault();

                    getResponse.TotalCount = 1;

                    return getResponse;
                }
                else
                {
                    //throw exception
                    throw new Exception();
                }
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }

        /// <summary>
        /// Get a page of resources from the authoritative source starting at offset
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="urlParameters"></param>
        /// <param name="offset"></param>
        /// <param name="limit"></param>
        /// <param name="versionContentType">Specify if a specific version of the model should be requested, if null, versionless will be used</param>
        /// <returns></returns>
        public virtual GetResponse GetAll(string resourceName, Dictionary<string, string> urlParameters = null, int offset = 0, int limit = -1, string versionContentType = null)
        {
            if (resourceName == null)
            {
                throw new Exception("ResourcePluralName missing. Unable to identify resource name : [" + resourceName + "]");
            }


            bool authenticated = Authenticate().Result;
            if (authenticated)
            {
                var resource = ConstructResourceUri("api", resourceName);
                NameValueCollection headers = new NameValueCollection();
                ConstructAcceptHeader(headers, versionContentType);

                if (urlParameters == null)
                    urlParameters = new Dictionary<string, string>();


                urlParameters[Offset_Param] = offset.ToString();

                if (limit > 0)
                {
                    urlParameters[Limit_Param] = limit.ToString();
                }

                var response = ExecuteRequest(HttpMethod.Get, null, resource, urlParameters, headers);

                if (response.IsSuccessStatusCode)
                {
                    GetResponse getAllResponse = new GetResponse();
                    getAllResponse.Data = response.Content.ReadAsStringAsync().Result;
                    if (response.Headers.Contains(Media_Type_Header))
                        getAllResponse.Version = response.Headers.GetValues(Media_Type_Header).FirstOrDefault();
                    if (response.Headers.Contains(Total_Count_Header))
                    {
                        var countHeader = response.Headers.GetValues(Total_Count_Header).FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(countHeader))
                            getAllResponse.TotalCount = Int32.Parse(countHeader);
                    }
                    else
                    {
                        // Todo: Temp workaround for the Mock API Server until it returns the header record count
                        //throw new HttpRequestFailedException("Missing X-Total-Count header", response.StatusCode);
                        getAllResponse.TotalCount = 0;
                    }

                    return getAllResponse;
                }
                else
                {
                    //throw exception
                    throw new Exception();
                }
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }
        public virtual T Create<T>(T model) where T : EthosEntityBase, new()
        {
            T typeMeta = new T();
            var resourceName = typeMeta.ResourcePluralName;
            var versioncontentType = typeMeta.HeaderContentType;

            if (resourceName == null)
            {
                throw new Exception("EEDM Attribute missing. Unable to identify resource name : [" + typeof(T) + "]");
            }


            bool authenticated = Authenticate().Result;
            if (authenticated)
            {

                var resource = ConstructResourceUri("api", resourceName);
                NameValueCollection headers = new NameValueCollection();
                ConstructAcceptHeader(headers, versioncontentType);

                var response = ExecuteRequest(HttpMethod.Post, JsonConvert.SerializeObject(model), resource, null, headers, versioncontentType);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);
                    }
                    catch (JsonSerializationException ex)
                    {
                        //log exception
                        throw ex;
                    }
                }
                else
                {
                    //log and throw exception
                    throw new Exception();
                }
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }

        public virtual T Delete<T>(T model, Guid Id, string resourceName, string versioncontentType)
        {
            
            if (resourceName == null)
            {
                throw new Exception("ResourcePluralName missing. Unable to identify resource name : [" + typeof(T) + "]");
            }


            bool authenticated = Authenticate().Result;
            if (authenticated)
            {

                var resource = ConstructResourceUri("api", resourceName, Id.ToString());
                NameValueCollection headers = new NameValueCollection();
                ConstructAcceptHeader(headers, versioncontentType);

                var response = ExecuteRequest(HttpMethod.Delete, JsonConvert.SerializeObject(model), resource, null, headers);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);
                    }
                    catch (JsonSerializationException ex)
                    {
                        //log exception
                        throw ex;
                    }
                }
                else
                {
                    //log and throw exception
                    throw new Exception();
                }
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }

        public virtual T Update<T>(T model, Guid Id) where T : EthosEntityBase, new()
        {
            T typeMeta = new T();
            var resourceName = typeMeta.ResourcePluralName;
            var versioncontentType = typeMeta.HeaderContentType;

            if (resourceName == null)
            {
                throw new Exception("ResourcePluralName missing. Unable to identify resource name : [" + typeof(T) + "]");
            }


            bool authenticated = Authenticate().Result;
            if (authenticated)
            {

                var resource = ConstructResourceUri("api", resourceName, Id.ToString());
                NameValueCollection headers = new NameValueCollection();
                ConstructAcceptHeader(headers, versioncontentType);

                var response = ExecuteRequest(HttpMethod.Put, JsonConvert.SerializeObject(model), resource, null, headers, versioncontentType);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result);
                    }
                    catch (JsonSerializationException ex)
                    {
                        //log
                        throw ex;
                    }
                }
                else
                {
                    //log and throw exception
                    throw new Exception();
                }
            }
            else
            {
                //throw exception
                throw new Exception();
            }
        }

        /// <summary>
        /// Test if the requested version is supported by the authoritative source. For now, this does a GetAll retrieving a single record, in the future
        /// this could use HEAD or the resources API.
        /// </summary>
        /// <param name="resourceName"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public virtual bool VersionSupported(string resourceName, string version)
        {
            bool supported = false;
            var response = GetAll(resourceName, offset: 0, limit: 1, versionContentType: version);
            if (response != null)
                supported = true;

            return supported;
        }}
}

