using System;
using SolarUseOptimiser.Models.ChargeHQ;

namespace SolarUseOptimiser
{
    public interface IDataTarget
    {
        string UserId
        {
            get;
        }

        string Name
        {
            get;
        }

        Task<bool> SendErrorData(SiteMeterPush errorMessage);

        Task<bool> SendData(SiteMeterPush data);
    }
}