using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SolarUseOptimiser.Models.ChargeHQ;
using SolarUseOptimiser.Models.Configuration;
using SolarUseOptimiser.Models.IoTaWatt;

namespace SolarUseOptimiser
{
    public class IoTaWattProducer : IDataSource
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<IoTaWattProducer> logger;

        public string Name => "IoTaWatt Power Sensor";

        public double PollRate {get; set;}

        public bool IsInitialised {get;set;} = false;

        private HttpClient _client;
        private HttpClientHandler _handler;

        private IoTaWattSettings IoTaWattSettings
        {
            get; set;
        }

        private string QueryUrl { get; set; }

        private static CancellationTokenSource CancellationTokenSource;

        public IoTaWattProducer(IConfiguration configuration, ILogger<IoTaWattProducer> logger)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public Task<IDataSource> InitialiseAsync(CancellationTokenSource cancellationTokenSource)
        {
            if (!IsInitialised)
            {
                IsInitialised = true;
                CancellationTokenSource = cancellationTokenSource;
                IoTaWattSettings = configuration.GetSection(Constants.ConfigSections.IOTAWATT_CONFIG_SECTION).Get<IoTaWattSettings>();
                PollRate = IoTaWattSettings.PollRate;
                QueryUrl = Constants.IoTaWatt.URL_TEMPLATE.Replace("{IOTAWATT_IP_ADDRESS}", IoTaWattSettings.IPAddress);

                _handler = new HttpClientHandler();
                _handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                _handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) =>
                {
                    return true;
                };
                _client = new HttpClient(_handler);
            }
            return Task.FromResult<IDataSource>(this);
        }

        public Task<bool> Authenticate(CancellationTokenSource cancellationTokenSource)
        {
            return Task.FromResult<bool>(true);
        }

        public SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource)
        {
            SiteMeterPush pushData = new SiteMeterPush
            {
                apiKey = userId,
                siteMeters = new SiteMeter()
            };
            var response = _client.GetAsync(QueryUrl, cancellationTokenSource.Token).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.OK)
            {
                 var jsonResponse = Utility.GetJsonResponse(response, cancellationTokenSource.Token);
                 if (!string.IsNullOrWhiteSpace(jsonResponse))
                 {
                    logger.LogDebug("Response: {0}", jsonResponse);
                    var respObj = JsonConvert.DeserializeObject<QueryResponse>(jsonResponse);
                    if (respObj != null && respObj.Data != null &&  respObj.Data.Length > 0)
                    {
                        pushData.siteMeters.net_import_kw = respObj.Data[0][0] / 1000;
                        pushData.siteMeters.production_kw = respObj.Data[0][1] / 1000;
                        pushData.siteMeters.consumption_kw = respObj.Data[0][2] / 1000;
                    }
                    else 
                    {
                        logger.LogError("The response string did not deserialise as expected.");
                        pushData.error = "Failed to get a valid response from the IoTaWatt Power Sensor device.";
                    }
                 }
                 else
                 {
                    logger.LogError("The response string from the service was null or whitespace.");
                    pushData.error = "Failed to get a valid response from the IoTaWatt Power Sensor device.";
                 }
            }
            else
            {
                logger.LogError("The HTTP status code of the response was not OK.");
                pushData.error = "Failed to get a valid response from the IoTaWatt Power Sensor device.";
            }
            return pushData;
        }
    }
}