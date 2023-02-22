using System.Net;
using System.Timers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using SolarUseOptimiser.Models.ChargeHQ;
using SolarUseOptimiser.Models.Configuration;
using SolarUseOptimiser.Models.Huawei;

using Timer = System.Timers.Timer;

namespace SolarUseOptimiser
{
    public class PeriodicPoller
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<PeriodicPoller> logger;

        private bool isStarted = false;

        private bool initialised = false;

        private Timer Timer
        {
            get; set;
        }

        private IDataTarget DataTarget
        {
            get; set;
        }

        private IDataSource DataSource
        {
            get; set;
        }

        private static CancellationTokenSource CancellationTokenSource;

        public PeriodicPoller(IConfiguration configuration, IDataSource dataProducer, IDataTarget dataConsumer, ILogger<PeriodicPoller> logger)
        {
            this.configuration = configuration;
            this.DataSource = dataProducer;
            this.DataTarget = dataConsumer;

            this.logger = logger;
        }

         /// <summary>
        /// <c>InitialiseAsync</c> - Sets up the component for polling the Huawei Fusion Solar API.
        /// It will login to the API and read some data objects out that will be used for polling solar production data
        /// </summary>
        /// <returns>The HuaweiSolarPoller object that was initialised</summary>
        public async Task<PeriodicPoller> InitialiseAsync(CancellationTokenSource cancellationTokenSource)
        {
            if (!initialised)
            {
                initialised = true;
                CancellationTokenSource = cancellationTokenSource;

                await DataSource.InitialiseAsync(cancellationTokenSource);

                Timer = new Timer(DataSource.PollRate * 60000);
                Timer.Enabled = false;
                Timer.AutoReset = true;
                Timer.Elapsed += PollGenerationStatistics_Elapsed;
            }
            return this;
        }

        /// <summary>
        /// <c>Start</c> - Starts the poller and does an initial poll immediately.
        /// </summary>
        internal void Start()
        {
            if (!isStarted)
            {
                isStarted = true;
                logger.LogInformation("Starting Polling. The interval is every {0}ms.", Timer.Interval);
                Timer.Start();

                //Do first poll manually
                PollGenerationStatistics_Elapsed(this, null);
            }
        }

        /// <summary>
        /// <c>Stop</c> - Stops the poller
        /// </summary>
        internal void Stop()
        {
            if (isStarted)
            {
                Timer.Stop();
                CancellationTokenSource.Cancel();
                isStarted = false;
            }
        }

        private void PollGenerationStatistics_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!CancellationTokenSource.IsCancellationRequested)
                {
                    while (!DataSource.IsInitialised)
                    {
                        DataSource.Authenticate(CancellationTokenSource).GetAwaiter().GetResult();
                    }

                    SiteMeterPush pushData = DataSource.GetSiteMeterData(DataTarget.UserId, CancellationTokenSource);

                    if (string.IsNullOrWhiteSpace(pushData.error))
                    {
                        bool successfullySent = DataTarget.SendData(pushData).GetAwaiter().GetResult();
                        if (successfullySent)
                        {
                            logger.LogDebug("Sent the power data successfully to {0}.", DataTarget.Name);
                        }
                        else
                        {
                            logger.LogError("Failed to send the power data to {0}.", DataTarget.Name);
                        }
                    }
                    else
                    {
                        bool sentErrorDataSuccess = DataTarget.SendErrorData(pushData).GetAwaiter().GetResult();
                        if (sentErrorDataSuccess)
                        {
                            logger.LogDebug("Sent the error data successfully to {0}.", DataTarget.Name);
                        }
                        else
                        {
                            logger.LogError("Failed to send the error data to {0}.", DataTarget.Name);
                        }
                    }                    
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception was caught while polling for power data to send to {0}.", DataTarget.Name);
                SiteMeterPush pushData = new SiteMeterPush
                {
                    apiKey = DataTarget.UserId,
                    error = string.Format("An error occurred while polling the {0}", DataSource.Name)
                };
                bool successfullySent = this.DataTarget.SendErrorData(pushData).GetAwaiter().GetResult();
                if (successfullySent)
                {
                    logger.LogDebug("Sent the error data successfully to {0}.", DataTarget.Name);
                }
                else
                {
                    logger.LogError("Failed to send the error data to {0}.", DataTarget.Name);
                }
            }
        }
    }
}