using System;

namespace SolarUseOptimiser
{
    public class Constants
    {
        public class ConfigSections
        {
            public const string HUAWEI_CONFIG_SECTION = "Huawei";
            public const string GROWATT_CONFIG_SECTION = "Growatt";
            public const string IOTAWATT_CONFIG_SECTION = "IoTaWatt";
            public const string CHARGE_HQ_CONFIG_SECION = "ChargeHQ";
            public const string ROUTING_SECTION = "Routing";
        }

        public class Routes
        {
            public class DataSources
            {
                public const string ROUTE_DATASOURCE_HUAWEI = "Huawei";
                public const string ROUTE_DATASOURCE_GROWATT = "Growatt"; 
                public const string ROUTE_DATASOURCE_IOTAWATT = "IoTaWatt";
                public const string ROUTE_DATASOURCE_ALPHAESS = "AlphaESS";
            }

            public class Targets
            {
                public const string ROUTE_TARGET_CHARGEHQ = "ChargeHQ";
            }
        }

        public class Huawei 
        {
            // need to login every 30 minutes
            public const string LOGIN_URI = "thirdData/login";

            public const string STATION_LIST_URI = "thirdData/getStationList";

            public const string STATION_LIST_NEW_URI = "thirdData/stations";

            // can't call this more than 10 times a minute
            public const string DEV_LIST_URI = "thirdData/getDevList";

            // can't call this more than once every 5 minutes
            public const string DEV_REAL_KPI_URI = "thirdData/getDevRealKpi";
        }

        public class Growatt
        {
            public const string LOGIN_URI = "login";

            public const string PLANT_LIST = "index/getPlantListTitle";

            public const string DEVICE_LIST = "panel/getDevicesByPlantList";

            public const string GET_MIX_STATUS = "panel/mix/getMIXStatusData"; 

            public const string DEV_TYPE_MIX = "mix";
            public const string DEV_TYPE_INV = "inv";
        }

        public class IoTaWatt
        {
            public const string URL_TEMPLATE = "http://{IOTAWATT_IP_ADDRESS}/query?select=[Mains.watts,Solar.watts,Consumption.watts]&begin=s-1m&end=s&group=all&header=no";
        }

        public class AlphaESS
        {
            public const string URL_REAL_TIME_DATA = "https://cloud.alphaess.com/api/ESS/GetLastPowerDataBySN?sys_sn={SERIAL_NUMBER}&noLoading=true"; //POST

            public const string GET_CUSTOMER_DATA = "https://cloud.alphaess.com/api/Account/GetCustomMenuESSlist"; //GET
        }
    }
}