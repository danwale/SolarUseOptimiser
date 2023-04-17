using System.Threading;


using SolarUseOptimiser.Models.ChargeHQ;

namespace SolarUseOptimiser
{
    public interface IDataSource
    {
        string Name
        {
            get;
        }

        double PollRate
        {
            get;
        }

        bool IsInitialised
        {
            get;
        }

        Task<IDataSource> InitialiseAsync(CancellationTokenSource cancellationTokenSource);

        Task<bool> Authenticate(CancellationTokenSource cancellationTokenSource);

        SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource);

        Task Restart(CancellationTokenSource cancellationTokenSource);
    }
}