using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SolarUseOptimiser.Models.ChargeHQ;
using SolarUseOptimiser.Models.Configuration;
using SolarUseOptimiser.Models.Growatt;

namespace SolarUseOptimiser
{
    public class GrowattProducer : IDataSource
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<GrowattProducer> logger;

        private bool initialised = false;

        public string Name => "Growatt API";

        private HttpClient _client;
        private HttpClientHandler _handler;

        public bool IsInitialised
        {
            get; set; 
        } = false;

        private GrowattSettings GrowattSettings
        {
            get; set;
        }

        private string PlantId
        {
            get; set;
        }

        private string SerialNumber
        {
            get; set;
        }

        private double TotalPVPower
        {
            get; set;
        }

        private double InverterCurrentProduction
        {
            get; set;
        }

        private long? EpocTimestamp
        {
            get; set;
        }

        private string DeviceType
        {
            get; set;
        }

        public double PollRate
        {
            get; set;
        }

        private static CancellationTokenSource CancellationTokenSource;

        public GrowattProducer(IConfiguration configuration, ILogger<GrowattProducer> logger)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public async Task Restart(CancellationTokenSource cancellationTokenSource)
        {
            initialised = false;
            await InitialiseAsync(cancellationTokenSource);
        }

        public async Task<IDataSource> InitialiseAsync(CancellationTokenSource cancellationTokenSource)
        {
            if (!initialised)
            {
                initialised = true;
                CancellationTokenSource = cancellationTokenSource;
                GrowattSettings = configuration.GetSection(Constants.ConfigSections.GROWATT_CONFIG_SECTION).Get<GrowattSettings>();
                PollRate = GrowattSettings.PollRate;

                var cookies = new CookieContainer();
                _handler = new HttpClientHandler();
                _handler.CookieContainer = cookies;
                _handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                _handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) =>
                {
                    return true;
                };
                _client = new HttpClient(_handler);
                IsInitialised = await Authenticate(cancellationTokenSource);
            }
            return this;
        }

        public async Task<bool> Authenticate(CancellationTokenSource cancellationTokenSource)
        {
            var success = await GetCookies(CancellationTokenSource.Token);
            if (success) 
            {
                logger.LogInformation("Successfully authenticated the user against the Growatt API");

                var plantListSuccess = await GetPlantList(cancellationTokenSource.Token);
                if (plantListSuccess)
                {
                    var deviceListSuccess = await GetDevices(cancellationTokenSource.Token);
                    if (deviceListSuccess)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private async Task<bool> GetCookies(CancellationToken cancellationToken)
        {
            if (initialised)
            {
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("account", GrowattSettings.Username));
                nvc.Add(new KeyValuePair<string, string>("password", GrowattSettings.Password));
                nvc.Add(new KeyValuePair<string, string>("validateCode", string.Empty));
                nvc.Add(new KeyValuePair<string, string>("IsReadPact", "0"));
                string url = Utility.GetUrl(GrowattSettings.BaseURI, Constants.Growatt.LOGIN_URI);
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(nvc) };
                var res = await _client.SendAsync(req);
                var jsonResponse = Utility.GetJsonResponse(res, cancellationToken);
                var loginResponse = JsonConvert.DeserializeObject<LogonResponse>(jsonResponse);
                if (res.StatusCode == HttpStatusCode.OK && loginResponse.result > 0)
                {
                    return true;
                }
                else
                {
                    logger.LogWarning($"Failed to authenticate with Growatt server returning the message: {loginResponse.msg}");
                    return false;
                }
            }
            return false;
        }

        private async Task<bool> GetPlantList( CancellationToken cancellationToken)
        {
            try
            {
                string url = Utility.GetUrl(GrowattSettings.BaseURI, Constants.Growatt.PLANT_LIST);
                var response = await _client.PostAsync(url, null, cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool wasCookieValid = WasCookieValid(response, out string json, cancellationToken);
                    if (!wasCookieValid)
                    {
                        // refresh the cookies
                        await GetCookies(cancellationToken);
                    }

                    var plantList = JsonConvert.DeserializeObject<IList<PlantListResponse>>(json);
                    if (plantList != null && plantList.Count > 0)
                    {
                        PlantId = plantList[0].id;
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred getting the plant list.");
                return false;
            }
        }

        private async Task<bool> GetDevices(CancellationToken cancellationToken)
        {
            try
            {
                string url = Utility.GetUrl(GrowattSettings.BaseURI, Constants.Growatt.DEVICE_LIST);
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("currPage", "1"));
                nvc.Add(new KeyValuePair<string, string>("plantId", PlantId));
                 var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(nvc) };
                var response = await _client.SendAsync(req);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool wasCookieValid = WasCookieValid(response, out string json, cancellationToken);
                    if (!wasCookieValid)
                    {
                        // refresh the cookies
                        await GetCookies(cancellationToken);
                    }

                    var deviceList = JsonConvert.DeserializeObject<DeviceListResponse>(json);
                    if (deviceList != null && deviceList.obj != null && deviceList.obj.datas.Count > 0)
                    {
                        SerialNumber = deviceList.obj.datas[0].sn;
                        TotalPVPower = Double.Parse(deviceList.obj.datas[0].eTotal);
                        DeviceType = deviceList.obj.datas[0].deviceTypeName;
                        InverterCurrentProduction = Double.Parse(deviceList.obj.datas[0].pac) / 1000; //convert to kW
                        EpocTimestamp = GetEpocTimestampInMs(deviceList.obj.datas[0].lastUpdateTime);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred getting the device serial number and total power output.");
                return false;
            }
        }

        private long? GetEpocTimestampInMs(string dateTimeString)
        {
            try
            {
                DateTime dt = DateTime.Parse(dateTimeString);
                TimeSpan ts = dt.ToUniversalTime() - new DateTime(1970, 1, 1);
                return (long)ts.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to parse the date time string for the last updated {0}", dateTimeString);
                return null;
            }
        }

        private async Task<MixStatusResponse> GetMixStatus(CancellationToken cancellationToken)
        {
            try
            {
                string url = Utility.GetUrl(GrowattSettings.BaseURI, Constants.Growatt.GET_MIX_STATUS);
                url += "?plantId=" + PlantId;
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("mixSn", SerialNumber));
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(nvc) };
                var response = await _client.SendAsync(req);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool wasCookieValid = WasCookieValid(response, out string json, cancellationToken);
                    if (!wasCookieValid)
                    {
                        // refresh the cookies
                        await GetCookies(cancellationToken);
                    }

                    var statusResponse = JsonConvert.DeserializeObject<MixStatusResponse>(json);
                    if (statusResponse != null && statusResponse.obj != null)
                    {
                        return statusResponse;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred getting the MIX device status data.");
                return null;
            }
        }

        private bool WasCookieValid(HttpResponseMessage message, out string json, CancellationToken cancellationToken)
        {
            json = string.Empty;
            if (message != null)
            {
                try
                {
                    json = Utility.GetJsonResponse(message, cancellationToken);
                    if (json.Contains("data-name=\"dumpLogin\">"))
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get a valid response");
                    return false;
                }
            }
            return false;
        }

        public SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource)
        {
            bool deviceDataSuccess= GetDevices(cancellationTokenSource.Token).GetAwaiter().GetResult();
            SiteMeterPush pushData = new SiteMeterPush
            {
                apiKey = userId,
                tsms = EpocTimestamp,
                siteMeters = new SiteMeter()
            };
            if (DeviceType != null && DeviceType.Equals(Constants.Growatt.DEV_TYPE_MIX, StringComparison.CurrentCultureIgnoreCase))
            {
                var statusResponse = GetMixStatus(cancellationTokenSource.Token).GetAwaiter().GetResult();
                if (statusResponse != null && statusResponse.obj != null)
                {
                    pushData.siteMeters.production_kw = InverterCurrentProduction;
                    pushData.siteMeters.consumption_kw = statusResponse.obj.pLocalLoad;
                    pushData.siteMeters.exported_kwh = TotalPVPower;
                    if (statusResponse.obj.pactouser > 0)
                    {
                        // Grid Import
                        pushData.siteMeters.net_import_kw = statusResponse.obj.pactouser;
                    }
                    else
                    {
                        // Grid Export
                        pushData.siteMeters.net_import_kw = -1 * statusResponse.obj.pactogrid;
                    }
                    
                    if (statusResponse.obj.wBatteryType == "1" && GrowattSettings.UseBatteryData)
                    {
                        pushData.siteMeters.battery_soc = Double.Parse(statusResponse.obj.SOC) / 100;
                        pushData.siteMeters.battery_discharge_kw = Double.Parse(statusResponse.obj.pdisCharge1);
                    }
                }
                else
                {
                    pushData.error = "Failed to get the Growatt status data from the inverter/battery mix API.";
                }
            }
            else if (DeviceType != null && DeviceType.Equals(Constants.Growatt.DEV_TYPE_INV, StringComparison.CurrentCultureIgnoreCase))
            {
                if (deviceDataSuccess)
                {
                    pushData.siteMeters.production_kw = InverterCurrentProduction;
                    pushData.siteMeters.exported_kwh = TotalPVPower;
                }
                else
                {
                    pushData.error = "Failed to get the Growatt status data from the inverter.";
                }
            }
            else
            {
                pushData.error = "An invalid device type was detected, it should be 'inv' or 'mix'.";
            }

            return pushData;
        }        
    }
}