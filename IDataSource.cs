using System.Threading;

using SolarUseOptimiser.Models;
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

        int DeviceCount
        {
            get;
        }

        bool IsInitialised
        {
            get;
        }

        Task<IDataSource> InitialiseAsync(CancellationTokenSource cancellationTokenSource);

        Task<CommandResponse> Authenticate(CancellationTokenSource cancellationTokenSource);

        SiteMeterPush GetSiteMeterData(string userId, CancellationTokenSource cancellationTokenSource);

        Task Restart(CancellationTokenSource cancellationTokenSource);
    }
}