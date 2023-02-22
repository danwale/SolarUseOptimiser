using SolarUseOptimiser.Models.ChargeHQ;

namespace SolarUseOptimiser
{
    public class GrowWattProducer : IDataSource
    {
        public string Name => "GrowWatt";

        public int PollRate => 5;

        public bool IsInitialised
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Task<IDataSource> InitialiseAsync(CancellationTokenSource cancellationTokenSource)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Authenticate(CancellationTokenSource cancellationTokenSource)
        {
            throw new NotImplementedException();
        }

        public SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource)
        {
            throw new NotImplementedException();
        }        
    }
}