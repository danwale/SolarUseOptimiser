namespace SolarUseOptimiser.Models.AlphaESS
{
    public class LoginResponse
    {
        public int code
        {
            get; set;
        }

        public string info
        {
            get; set;
        }

        public LoginResponseData data
        {
            get; set;
        }
    }

    public class LoginResponseData 
    {
        public string AccessToken
        {
            get; set;
        }

        public double ExpiresIn
        {
            get; set;
        }

        public string TokenCreateTime
        {
            get; set;
        }

        public string RefreshTokenKey
        {
            get; set;
        }
    }
}