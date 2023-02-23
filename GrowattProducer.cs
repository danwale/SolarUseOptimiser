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
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else
                {
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

        private async Task<InvStatusResponse> GetInvStatus(CancellationToken cancellationToken)
        {
            try
            {
                _ = await GetDevices(cancellationToken);
                string url = Utility.GetUrl(GrowattSettings.BaseURI, Constants.Growatt.GET_INV_STATUS);
                url += "?plantId=" + PlantId; // TODO: Verify the URL has the plantId query string param
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("invSn", SerialNumber)); // TODO: Check what this param is called
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

                    var statusResponse = JsonConvert.DeserializeObject<InvStatusResponse>(json);
                    if (statusResponse != null && statusResponse.obj != null)
                    {
                        return statusResponse;
                    }
                }
                return null;

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred getting the Inverter device status data.");
                return null;
            }
        }

        private async Task<MixStatusResponse> GetMixStatus(CancellationToken cancellationToken)
        {
            try
            {
                _ = await GetDevices(cancellationToken);
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
            SiteMeterPush pushData = new SiteMeterPush
            {
                apiKey = userId,
                siteMeters = new SiteMeter()
            };
            if (DeviceType != null && DeviceType.Equals(Constants.Growatt.DEV_TYPE_MIX, StringComparison.CurrentCultureIgnoreCase))
            {
                var statusResponse = GetMixStatus(cancellationTokenSource.Token).GetAwaiter().GetResult();
                if (statusResponse != null && statusResponse.obj != null)
                {
                    pushData.siteMeters.production_kw = statusResponse.obj.ppv;
                    pushData.siteMeters.consumption_kw = statusResponse.obj.pLocalLoad;
                    pushData.siteMeters.exported_kwh = TotalPVPower;
                    if (statusResponse.obj.pactouser > 0)
                    {
                        pushData.siteMeters.net_import_kw = statusResponse.obj.pactouser;
                    }
                    else
                    {
                        pushData.siteMeters.net_import_kw = -1 * statusResponse.obj.pactogrid;
                    }
                    
                    if (statusResponse.obj.wBatteryType == "1" && GrowattSettings.UseBatteryData)
                    {
                        pushData.siteMeters.battery_soc = Double.Parse(statusResponse.obj.SOC) / 100;
                        pushData.siteMeters.battery_discharge_kw = Double.Parse(statusResponse.obj.pdisCharge1);
                        //pushData.siteMeters.battery_energy_kwh = //TODO: Find if we have the total battery current capacity
                    }
                }
                else
                {
                    pushData.error = "Failed to get the Growatt status data from the inverter/battery mix API.";
                }
            }
            else if (DeviceType != null && DeviceType.Equals(Constants.Growatt.DEV_TYPE_INV, StringComparison.CurrentCultureIgnoreCase))
            {
                var statusResponse = GetInvStatus(cancellationTokenSource.Token).GetAwaiter().GetResult();
                if (statusResponse != null && statusResponse.obj != null)
                {
                    // pushData.siteMeters.production_kw = statusResponse.obj.ppv;
                    // pushData.siteMeters.consumption_kw = statusResponse.obj.pLocalLoad;
                    // pushData.siteMeters.exported_kwh = TotalPVPower;
                    // if (statusResponse.obj.pactouser > 0)
                    // {
                    //     pushData.siteMeters.net_import_kw = statusResponse.obj.pactouser;
                    // }
                    // else
                    // {
                    //     pushData.siteMeters.net_import_kw = -1 * statusResponse.obj.pactogrid;
                    // }
                }
                else
                {
                    pushData.error = "Failed to get the Growatt status data from the inverter API.";
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