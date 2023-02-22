using System.Net.Mime;
using System.Text;

using Newtonsoft.Json;

using Serilog;

using SolarUseOptimiser.Models.Huawei;

namespace SolarUseOptimiser
{
    public static class Utility
    {
        public static StringContent GetStringContent(string json)
        {
            return new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json);
        }

        public static StringContent GetStringContent(object obj, Formatting formatOption = Formatting.None)
        {
            string json = JsonConvert.SerializeObject(obj, formatOption);
            Log.Logger.Debug("JSON Request Content: '{0}'", json);
            return GetStringContent(json);
        }

        public static string GetJsonResponse(HttpResponseMessage message, CancellationToken cancellationToken)
        {
            return message.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        }

        public static bool WasSuccessMessage<T>(HttpResponseMessage message, out string json, out T response, CancellationToken cancellationToken) where T : BaseResponse
        {
            json = string.Empty;
            response = null;
            if (message != null)
            {
                try
                {
                    json = GetJsonResponse(message, cancellationToken);
                    Log.Logger.Debug("JSON Response Content: '{0}'", json);
                    response = JsonConvert.DeserializeObject<T>(json);
                    if (response != null) 
                    {
                        return response.success;
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "Failed to get a valid response from the service. JSON: '{0}'", json);
                    return false;
                }
            }
            return false;
        }

        public static string GetUrl(string baseUri, string methodUri)
        {
            if (methodUri.StartsWith("/"))
            {
                methodUri = methodUri.TrimStart('/');
            }
            if (baseUri.EndsWith("/"))
            {
                baseUri = baseUri.TrimEnd('/');
            }
            return string.Format("{0}/{1}", baseUri, methodUri);
        }
    }
}
