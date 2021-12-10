using System;
using System.Collections.Generic;
using System.Text;

namespace GCP.Extensions.Configuration.SecretManager
{
    public class Helpers
    {
        public const string FilterVersions_Enabled = "state:ENABLED";
        public const string DoubleUnderscore = "__";

        public static string GetProjectId()
        {
            string instance = Google.Api.Gax.Platform.Instance()?.ProjectId;
            string googleCloudProject = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
            string gCloudProject = Environment.GetEnvironmentVariable("GCLOUD_PROJECT");
            return instance ?? googleCloudProject ?? gCloudProject;
        }
    }
}
