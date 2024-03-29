using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SolarUseOptimiser.Models;
using SolarUseOptimiser.Models.ChargeHQ;
using SolarUseOptimiser.Models.Configuration;
using SolarUseOptimiser.Models.Huawei;

namespace SolarUseOptimiser
{
    public class HuaweiProducer : IDataSource
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<HuaweiProducer> logger;

        public string Name => "Huawei FusionSolar API";

        private bool initialised = false;

        public bool IsInitialised
        {
            get; set;
        } = false;

        private HttpClient _client;
        private HttpClientHandler _handler;

        private HuaweiSettings HuaweiSettings
        {
            get; set;
        }
        
        private string StationCode
        {
            get; set;
        }

        private DeviceInfo Inverter
        {
            get; set;
        }

        private DeviceInfo PowerSensor
        {
            get; set;
        }

        private DeviceInfo GridMeter
        {
            get; set;
        }

        private DeviceInfo Battery
        {
            get; set;
        }

        public int DeviceCount
        {
            get; set;
        } = 0;

        private static int RetryCount
        {
            get; set;
        } = 0;

        public double PollRate
        {
            get; set;
        }

        private static CancellationTokenSource CancellationTokenSource;

        public HuaweiProducer(IConfiguration configuration, ILogger<HuaweiProducer> logger)
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
                HuaweiSettings = configuration.GetSection(Constants.ConfigSections.HUAWEI_CONFIG_SECTION).Get<HuaweiSettings>();
                PollRate = HuaweiSettings.PollRate;

                var authResposne = await Authenticate(cancellationTokenSource);
                IsInitialised = authResposne.Success;
            }
            return this;
        }

        private void CreateHttpClient()
        {
            var cookies = new CookieContainer();
            _handler = new HttpClientHandler();
            _handler.CookieContainer = cookies;
            _handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            _handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) =>
            {
                return true;
            };
            _client = new HttpClient(_handler);
        }

        public async Task<CommandResponse> Authenticate(CancellationTokenSource cancellationTokenSource)
        {
            var success = await GetXsrfToken(CancellationTokenSource.Token);
            if (success)
            {
                logger.LogInformation("Successfully authenticated the user during intialisation.");

                var stationListResponse = await PostDataRequestAsync<GetStationListResponse>(GetUri(Constants.Huawei.STATION_LIST_URI), null, CancellationTokenSource.Token);
                if (stationListResponse.success)
                {
                    if (stationListResponse.data != null && stationListResponse.data.Count > 0)
                    {
                        var selectedPlant = stationListResponse.data.Where(plant => plant.stationName.Equals(HuaweiSettings.StationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                        if (selectedPlant != null)
                        {
                            // In case someone has more than one plant/station in their account select the plant/station they are after
                            logger.LogInformation("Station name was found and will be used for all requests.");
                            StationCode = stationListResponse.data[0].stationCode;

                            var gdlr = new GetDeviceListRequest
                            {
                                stationCodes = StationCode
                            };
                            var deviceInfoResponse = await PostDataRequestAsync<DeviceInfoResponse>(GetUri(Constants.Huawei.DEV_LIST_URI),
                                            Utility.GetStringContent(gdlr), CancellationTokenSource.Token);
                            if (deviceInfoResponse.success)
                            {
                                if (deviceInfoResponse.data != null && deviceInfoResponse.data.Count > 0)
                                {
                                    bool foundInverter = SetDeviceInfos(deviceInfoResponse.data);
                                    string msg = foundInverter ? "inverter found" : "inverter not found";
                                    return new CommandResponse
                                    {
                                        Success = foundInverter,
                                        Message = msg
                                    };
                                }
                                else 
                                {
                                    return new CommandResponse
                                    {
                                        Success = false,
                                        Message = "inverter not found"
                                    };
                                }
                            }
                            else
                            {
                                return new CommandResponse
                                {
                                    Success = false,
                                    Message = "inverter not found"
                                };
                            }
                        }
                        else
                        {
                            string message = string.Format("The station with the name '{0}' could not be found when listing the stations.", HuaweiSettings.StationName);
                            logger.LogError(message);
                            return new CommandResponse
                            {
                                Success = false,
                                Message = message
                            };
                        }
                    }
                    else
                    {
                        string message = "There were no plants/stations associated with this login.";
                        logger.LogError(message);
                        return new CommandResponse
                        {
                            Success = false,
                            Message = message
                        };
                    }
                }
                else
                {
                    if (stationListResponse.failCode == 401)
                    {
                        logger.LogInformation("Using new station interface as the old one was not available.");
                        var selectedPlant = await GetPlantInformation(1);
                        if (selectedPlant != null)
                        {
                            // In case someone has more than one plant/station in their account select the plant/station they are after
                            logger.LogInformation("Station name was found and will be used for all requests. (new interface)");
                            StationCode = selectedPlant.plantCode;

                            var gdlr = new GetDeviceListRequest
                            {
                                stationCodes = StationCode
                            };
                            var deviceInfoResponse = await PostDataRequestAsync<DeviceInfoResponse>(GetUri(Constants.Huawei.DEV_LIST_URI),
                                            Utility.GetStringContent(gdlr), CancellationTokenSource.Token);
                            if (deviceInfoResponse.success)
                            {
                                if (deviceInfoResponse.data != null && deviceInfoResponse.data.Count > 0)
                                {
                                    bool foundInverter = SetDeviceInfos(deviceInfoResponse.data);
                                    string msg = foundInverter ? "inverter found" : "inverter not found";
                                    return new CommandResponse
                                    {
                                        Success = foundInverter,
                                        Message = msg
                                    };
                                }
                                else
                                {
                                    return new CommandResponse
                                    {
                                        Success = false,
                                        Message = "inverter not found"
                                    };
                                }
                            }
                            else
                            {
                                return new CommandResponse
                                {
                                    Success = false,
                                    Message = "inverter not found"
                                };
                            }
                        }
                        else
                        {
                            string message = string.Format("The station with the name '{0}' could not be found when listing the stations.", HuaweiSettings.StationName);
                            logger.LogError(message);
                            return new CommandResponse
                            {
                                Success = false,
                                Message = message
                            };
                        }
                    }
                    else
                    {
                        string message = string.Format("The reason for the failure of the original list stations interface was not 401, it was {0}", stationListResponse.failCode);
                        logger.LogError(message);
                        return new CommandResponse
                        {
                            Success = false,
                            Message = message
                        };
                    }
                }
            }
            else
            {
                string message = "Failed to authenticate the user during initialisation.";
                logger.LogError(message);
                return new CommandResponse
                {
                    Success = false,
                    Message = message
                };
            }
        }

        /// <summary>
        /// <c>GetPlantInformation</c> - Selects the Plant/Station from the list of returned plants/stations associated with the login. 
        /// If there is a list of stations it will request each page of plants/stations until it has been through them all looking for a match.
        /// </summary>
        /// <returns>The matching plant/station or null if it couldn't be found</returns>
        private async Task<PlantInfo> GetPlantInformation(int pageNumber)
        {
            GetStationsNewRequestParams requestParam = new GetStationsNewRequestParams
            {
                pageNo = pageNumber
            };
            var requestParamContent = Utility.GetStringContent(requestParam);
            var newStationListResponse = await PostDataRequestAsync<GetStationListNewResponse>(GetUri(Constants.Huawei.STATION_LIST_NEW_URI), requestParamContent, CancellationTokenSource.Token);
            if (newStationListResponse.success)
            {
                if (newStationListResponse.data != null && newStationListResponse.data.list != null && newStationListResponse.data.list.Count > 0)
                {
                    var selectedPlant = newStationListResponse.data.list.Where(plant => plant.plantName.Equals(HuaweiSettings.StationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                    if (selectedPlant != null)
                    {
                        return selectedPlant;
                    }
                    else if (selectedPlant == null && newStationListResponse.data.pageCount > pageNumber)
                    {
                        return await GetPlantInformation(pageNumber++); // see if it's on the next page of information
                    }
                    else
                    {
                        return null; // reached the last page and it's not found
                    }
                }
                else
                {
                    return null; // this is an unexpected result, the station wasn't going to be found
                }
            }
            else
            {
                return null; // the interface didn't return a station/plant list, it wasn't found
            }
        }

        /// <summary>
        /// <c>SetDeviceInfo</c> - Gets the Residential Inverter and the Power Sensor devices out of the returned
        /// plant devices. It will also find any residential batteries to monitor.
        /// </summary>
        /// <returns>true if the inverter was found</returns>
        private bool SetDeviceInfos(IList<DeviceInfo> devices)
        {
            logger.LogInformation("Detected Devices: " + JsonConvert.SerializeObject(devices));
            var powerSensor = devices.Where(device => device.devTypeId == 47).FirstOrDefault();
            if (powerSensor != null)
            {
                // If the setup has a power sensor use that for the accurate excess solar information
                PowerSensor = powerSensor;
                logger.LogDebug("Found a power sensor with ID: {0}", powerSensor.id);
                if (HuaweiSettings.UsePowerSensorData)
                {
                    DeviceCount++;
                }
            }
            else
            {
                logger.LogDebug("There was no power sensor found, this is optional for richer solar monitoring data.");
            }

            var gridMeter = devices.Where(device => device.devTypeId == 17).FirstOrDefault();
            if (gridMeter != null)
            {
                GridMeter = gridMeter;
                logger.LogDebug("Found a grid meter with ID: {0}", gridMeter.id);
                if (HuaweiSettings.UseGridMeterData)
                {
                    DeviceCount++;
                }
            }
            else
            {
                logger.LogDebug("There was no grid meter found, this is optional for richer solar monitoring data.");
            }

            var battery = devices.Where(device => device.devTypeId == 39).FirstOrDefault();
            if (battery != null)
            {
                Battery = battery;
                logger.LogDebug("Found a residential battery with ID: {0}", battery.id);
                if (HuaweiSettings.UseBatteryData)
                {
                    DeviceCount++;
                }
            }
            else
            {
                logger.LogDebug("There was no residential battery detected in the system, this is optionally supported.");
            }

            var residentialInverter = devices.Where(device => device.devTypeId == 38).FirstOrDefault();
            if (residentialInverter != null)
            {
                Inverter = residentialInverter;
                logger.LogDebug("Found a residential inverter with ID: {0}", residentialInverter.id);
                DeviceCount++;
            }
            else
            {
                var stringInverter = devices.Where(device => device.devTypeId == 1).FirstOrDefault();
                if (stringInverter != null)
                {
                    Inverter = stringInverter;
                    logger.LogDebug("Found a string inverter with ID: {0}", stringInverter.id);
                    DeviceCount++;
                }
                else
                {
                    logger.LogError("No string or residential inverter was found, this is a mandatory requirement to use this system.");
                }
            }
            if (Inverter != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// <c>GetXsrfToken</c> - Sends a Login Request to Huawei's Fusion Solar API, extracts the cookie and puts it in the default headers
        //  for all future requests to the API.
        /// </summary>
        /// <returns>True if it successfully logged in, otherwise False</returns>
        private async Task<bool> GetXsrfToken(CancellationToken cancellationToken)
        {
            if (initialised)
            {
                CreateHttpClient();
                LoginCredentialRequest lcr = new LoginCredentialRequest
                {
                    userName = HuaweiSettings.Username,
                    systemCode = HuaweiSettings.Password
                };
                var content = Utility.GetStringContent(lcr);
                var response = await PostDataRequestAsync<BaseResponse>(GetUri(Constants.Huawei.LOGIN_URI), content, cancellationToken);
                if (response.success)
                {
                    logger.LogInformation("Successfully did login and got back cookie");
                    bool cookieFound = false;
                    foreach (Cookie cookie in _handler.CookieContainer.GetCookies(new Uri(HuaweiSettings.BaseURI)).Cast<Cookie>())
                    {
                        if (cookie.Name == "XSRF-TOKEN")
                        {
                            _client.DefaultRequestHeaders.Add("XSRF-TOKEN", cookie.Value);
                            logger.LogDebug("XSRF-TOKEN found in coolies and added to default request headers");
                            cookieFound = true;
                            break;
                        }
                    }
                    return cookieFound;
                }
                else
                {
                    logger.LogError("The login request failed, it returned a fail code of {0} and message {1}", response.failCode, response.message);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// <c>PostDataRequestAsync</c> - Does a HTTP POST to the Huawei Fusion Solar API with automatic re-authentication if required.
        /// If it gets an error response from the API call it will retry every 5 seconds until it gets a valid response 
        /// (such as Fusion Solar being offline for maintenance).
        /// </summary>
        /// <returns>The HTTP response message or null if there was an error with the HTTP POST</returns>
        private async Task<T> PostDataRequestAsync<T>(string uri, StringContent content, CancellationToken cancellationToken) where T : BaseResponse
        {
            try
            {
                var response = await _client.PostAsync(uri, content, cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    bool success = Utility.WasSuccessMessage<T>(response, out string json, out T responseObj, cancellationToken);
                    if (!success && responseObj.failCode == 305)
                    {
                        success = await GetXsrfToken(cancellationToken);
                        if (success)
                        {
                            logger.LogInformation("Successfully re-authenticated with the API. Resending original API request.");
                            responseObj = await PostDataRequestAsync<T>(uri, content, cancellationToken);
                        }
                        else
                        {
                            logger.LogError("Failed to re-authenticate with the API.");
                        }
                    }
                    return responseObj;
                }
                else
                {
                    RetryCount++;
                    if (RetryCount < 5)
                    {
                        logger.LogWarning("There was a failed request, it is retring after 5 seconds...Retry Count: {0}", RetryCount);
                        Thread.Sleep(5000);
                        return await PostDataRequestAsync<T>(uri, content, cancellationToken);
                    }
                    else
                    {
                        RetryCount = 0;
                        return (T)new BaseResponse
                        {
                            success = false,
                            failCode = 1001,
                            message = string.Format("Failed after the maximum retry count for URI: '{0}'", uri)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("The exception that occurred while sending the POST was: {0}", ex);
                RetryCount++;
                if (RetryCount < 5)
                {
                    logger.LogWarning("There was a failed request, it is retring after 5 seconds...Retry Count: {0}", RetryCount);
                    Thread.Sleep(5000);
                    return await PostDataRequestAsync<T>(uri, content, cancellationToken);
                }
                else
                {
                    RetryCount = 0; //reset the retry count
                    return (T)new BaseResponse
                    {
                        success = false,
                        failCode = 1001,
                        message = string.Format("Failed after the maximum retry count for URI: '{0}'", uri)
                    };
                }
            }
        }


        public SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource)
        {
            SiteMeterPush pushData = new SiteMeterPush
            {
                apiKey = userId,
                siteMeters = new SiteMeter()
            };
            try
            {
                if (!CancellationTokenSource.IsCancellationRequested)
                {
                    // Request the Residential Inverters power data to get how much production is happening
                    var requestParam = new GetDevRealKpiRequest
                    {
                        devIds = Inverter.id.ToString(),
                        devTypeId = Inverter.devTypeId
                    };
                    DevRealKpiResponse<DevRealKpiResInvDataItemMap> residentialPowerData = null;
                    DevRealKpiResponse<DevRealKpiStrInvDataItemMap> stringPowerData = null;

                    long timestamp = -1;
                    double? active_power = null;
                    double? total_cap = null;
                    double inverter_state = -1;
                    bool inverterDataFound = false;
                    if (Inverter.devTypeId == 1)
                    {
                        logger.LogDebug("Getting String Inverter Power Data.");
                        stringPowerData = PostDataRequestAsync<DevRealKpiResponse<DevRealKpiStrInvDataItemMap>>(GetUri(Constants.Huawei.DEV_REAL_KPI_URI),
                                                                            Utility.GetStringContent(requestParam),
                                                                            CancellationTokenSource.Token).GetAwaiter().GetResult();
                        if (stringPowerData != null && stringPowerData.success && stringPowerData.data != null &&
                            stringPowerData.data.Count > 0 && stringPowerData.data[0].dataItemMap.inverter_state != -1)
                        {
                            var stringInverterPowerData = stringPowerData.data[0].dataItemMap as DevRealKpiStrInvDataItemMap;
                            timestamp = stringPowerData.parameters.currentTime;
                            active_power = stringInverterPowerData.active_power;
                            total_cap = stringInverterPowerData.total_cap;
                            inverter_state = stringInverterPowerData.inverter_state;
                            inverterDataFound = true;
                        }
                    }
                    else if (Inverter.devTypeId == 38)
                    {
                        logger.LogDebug("Getting Residential Inverter Power Data.");
                        residentialPowerData = PostDataRequestAsync<DevRealKpiResponse<DevRealKpiResInvDataItemMap>>(GetUri(Constants.Huawei.DEV_REAL_KPI_URI),
                                                                            Utility.GetStringContent(requestParam),
                                                                            CancellationTokenSource.Token).GetAwaiter().GetResult();
                        if (residentialPowerData != null && residentialPowerData.success && residentialPowerData.data != null &&
                            residentialPowerData.data.Count > 0 && residentialPowerData.data[0].dataItemMap.inverter_state != -1)
                        {
                            var residentialInverterPowerData = residentialPowerData.data[0].dataItemMap as DevRealKpiResInvDataItemMap;
                            timestamp = residentialPowerData.parameters.currentTime;
                            active_power = residentialInverterPowerData.active_power;
                            total_cap = residentialInverterPowerData.total_cap;
                            inverter_state = residentialInverterPowerData.inverter_state;
                            inverterDataFound = true;
                        }
                    }

                    if (inverterDataFound)
                    {
                        if (active_power.HasValue)
                        {
                            pushData.tsms = timestamp;
                            pushData.siteMeters.production_kw = active_power;
                            pushData.siteMeters.exported_kwh = total_cap; // this is the total lifetime energy produced by the inverter

                            bool isShutdown = inverter_state != 512;
                            if (isShutdown)
                            {
                                logger.LogDebug("The inverter is currently shutdown due to no sunlight");
                            }

                            // If there is a Power Sensor device enrich the ChargeHQ Push Data with the consumption meter fields
                            if (PowerSensor != null && HuaweiSettings.UsePowerSensorData)
                            {
                                var powerSensorRequestParam = new GetDevRealKpiRequest
                                {
                                    devIds = PowerSensor.id.ToString(),
                                    devTypeId = PowerSensor.devTypeId
                                };
                                var powerSensorData = PostDataRequestAsync<DevRealKpiResponse<DevRealKpiPowerSensorDataItemMap>>(GetUri(Constants.Huawei.DEV_REAL_KPI_URI),
                                                                                        Utility.GetStringContent(powerSensorRequestParam),
                                                                                        CancellationTokenSource.Token).GetAwaiter().GetResult();
                                if (powerSensorData != null && powerSensorData.success &&
                                    powerSensorData.data != null && powerSensorData.data.Count > 0 && powerSensorData.data[0].dataItemMap.meter_status != -1)
                                {
                                    var powerSensorPowerData = powerSensorData.data[0].dataItemMap as DevRealKpiPowerSensorDataItemMap;

                                    bool isOffline = powerSensorPowerData.meter_status == 0;
                                    if (isOffline)
                                    {
                                        logger.LogDebug("The power sensor is currently reporting as offline.");
                                    }
                                    else
                                    {
                                        if (powerSensorPowerData.active_power.HasValue)
                                        {
                                            pushData.siteMeters.net_import_kw = -1 * (powerSensorPowerData.active_power.Value / 1000);
                                            pushData.siteMeters.consumption_kw = pushData.siteMeters.production_kw - (powerSensorPowerData.active_power.Value / 1000);

                                            // With the Power Sensor it measures the amount of power exported/imported a positive active_cap will be exported
                                            if (powerSensorPowerData.active_cap.HasValue && powerSensorPowerData.active_cap > 0)
                                            {
                                                pushData.siteMeters.exported_kwh = powerSensorPowerData.active_cap; // this is the lifetime amount of energy exported
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("The power sensor power data returned from Huawei's Fusion Solar was not valid.");
                                    pushData.error = "Huawei's FusionSolar power sensor power data was not in an expected format.";
                                    return pushData;
                                }
                            }
                            else
                            {
                                // If there isn't a Power Sensor try use a Grid Meter if it exists to get this data
                                if (GridMeter != null && HuaweiSettings.UseGridMeterData)
                                {
                                    var gridMeterRequestParams = new GetDevRealKpiRequest
                                    {
                                        devIds = GridMeter.id.ToString(),
                                        devTypeId = GridMeter.devTypeId
                                    };
                                    var gridMeterData = PostDataRequestAsync<DevRealKpiResponse<DevRealKpiGridMeterDataItemMap>>(GetUri(Constants.Huawei.DEV_REAL_KPI_URI),
                                                                                        Utility.GetStringContent(gridMeterRequestParams),
                                                                                        CancellationTokenSource.Token).GetAwaiter().GetResult();
                                    if (gridMeterData != null && gridMeterData.success &&
                                        gridMeterData.data != null && gridMeterData.data.Count > 0)
                                    {
                                        var gridMeterPowerData = gridMeterData.data[0].dataItemMap as DevRealKpiGridMeterDataItemMap;

                                        if (gridMeterPowerData.active_power.HasValue)
                                        {
                                            pushData.siteMeters.net_import_kw = -1 * gridMeterPowerData.active_power.Value;
                                            pushData.siteMeters.consumption_kw = pushData.siteMeters.production_kw - gridMeterPowerData.active_power.Value;

                                            // With the Power Sensor it measures the amount of power exported/imported a positive active_cap will be exported
                                            if (gridMeterPowerData.active_cap.HasValue && gridMeterPowerData.active_cap > 0)
                                            {
                                                pushData.siteMeters.exported_kwh = gridMeterPowerData.active_cap; // this is the lifetime amount of energy exported
                                            }
                                        }
                                    }
                                    else
                                    {
                                        logger.LogWarning("The grid meter power data returned from Huawei's Fusion Solar was not valid.");
                                        pushData.error = "Huawei's FusionSolar grid meter power data was not in an expected format.";
                                        return pushData;
                                    }
                                }
                            }

                            if (Battery != null && HuaweiSettings.UseBatteryData)
                            {
                                var batteryReqestParams = new GetDevRealKpiRequest
                                {
                                    devIds = Battery.id.ToString(),
                                    devTypeId = Battery.devTypeId
                                };
                                var batteryData = PostDataRequestAsync<DevRealKpiResponse<DevRealKpiBattDataItemMap>>(GetUri(Constants.Huawei.DEV_REAL_KPI_URI),
                                                                                        Utility.GetStringContent(batteryReqestParams),
                                                                                        CancellationTokenSource.Token).GetAwaiter().GetResult();
                                if (batteryData != null && batteryData.success &&
                                    batteryData.data != null && batteryData.data.Count > 0 && batteryData.data[0].dataItemMap.battery_status != -1)
                                {
                                    var batteryPowerData = batteryData.data[0].dataItemMap as DevRealKpiBattDataItemMap;

                                    bool isOffline = batteryPowerData.battery_status == 0;
                                    if (isOffline)
                                    {
                                        logger.LogDebug("The residential battery is currently reporting as offline.");
                                    }
                                    else
                                    {
                                        double? battery_soc = null;
                                        if (batteryPowerData.battery_soc > 1)
                                        {
                                            // TODO: Remove this logic after capturing the debug output and settling on what Huawei send
                                            logger.LogInformation("The battery SOC was being reported as a double between 0 and 100");
                                            battery_soc = batteryPowerData.battery_soc / 100;
                                        }
                                        else
                                        {
                                            logger.LogInformation("The battery SOC was being reported as a double between 0 and 1");
                                            battery_soc = batteryPowerData.battery_soc;
                                        }
                                        pushData.siteMeters.battery_soc = battery_soc;
                                        pushData.siteMeters.battery_energy_kwh = batteryPowerData.charge_cap;
                                        pushData.siteMeters.battery_discharge_kw = batteryPowerData.ch_discharge_power;
                                    }
                                }
                            }

                            // Send active power to ChargeHQ
                            logger.LogDebug("Successfully got the Huawei power data and sending it to the consumer.");
                            return pushData;
                        }
                        else
                        {
                            logger.LogInformation("There was no active_power value from the inverter so no data was sent to ChargeHQ.");
                            pushData.error = "Huawei's FusionSolar did not return any production output from the inverter.";
                            return pushData;
                        }
                    }
                    else
                    {
                        logger.LogWarning("The residential inverter power data returned from Huawei's Fusion Solar was not valid.");
                        pushData.error = "Huawei's FusionSolar residential inverter power data was not in an expected format.";
                        return pushData;
                    }
                }
                else
                {
                    pushData.error = "The poller was being shutdown";
                    return pushData;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception was caught while polling for power data to send to ChargeHQ.");
                pushData.error = "An error occurred while polling the Huawei FusionSolar API";
                return pushData;
            }
        }

        /// <summary>
        /// <c>GetUri</c> - Combines the method URI and base URI for the Huawei services ensuring it handles configuration errors.
        /// </summary>
        /// <param name="methodUri">The relative URI path to the relevant service</param>
        /// <returns>The full URI for the Huawei Fusion Solar API target</returns>
        private string GetUri(string methodUri)
        {
            return Utility.GetUrl(HuaweiSettings.BaseURI, methodUri);
        }
    }
}