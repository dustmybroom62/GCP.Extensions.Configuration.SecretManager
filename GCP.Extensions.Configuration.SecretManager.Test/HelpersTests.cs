using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pose;

namespace GCP.Extensions.Configuration.SecretManager.Test
{
    [TestClass]
    public class HelpersTests
    {
        public static readonly string GOOGLE_CLOUD_PROJECT = "GOOGLE_CLOUD_PROJECT";
        public static readonly string GCLOUD_PROJECT = "GCLOUD_PROJECT";
        public static readonly string ProjectId = "Project123";

        [TestMethod]
        public void GetProjectId_Returns_Env_GOOGLE_CLOUD_PROJECT()
        {
            System.Environment.SetEnvironmentVariable(GCLOUD_PROJECT, null);
            System.Environment.SetEnvironmentVariable(GOOGLE_CLOUD_PROJECT, ProjectId);
            string result = SecretManager.Helpers.GetProjectId();
            Assert.AreEqual(ProjectId, result);
        }

        [TestMethod]
        public void GetProjectId_Returns_Env_GCLOUD_PROJECT()
        {
            System.Environment.SetEnvironmentVariable(GCLOUD_PROJECT, ProjectId);
            System.Environment.SetEnvironmentVariable(GOOGLE_CLOUD_PROJECT, null);
            string result = SecretManager.Helpers.GetProjectId();
            Assert.AreEqual(ProjectId, result);
        }

        [TestMethod]
        public void GetProjectId_Returns_Platform()
        {
            var details = new Google.Api.Gax.CloudRunPlatformDetails("{}", ProjectId, "zone", "service", "revision", "config");
            Google.Api.Gax.Platform platform = new Google.Api.Gax.Platform(details);
            Shim platformShim = Shim.Replace(() => Google.Api.Gax.Platform.Instance()).With( () => platform);

            System.Environment.SetEnvironmentVariable(GCLOUD_PROJECT, null);
            System.Environment.SetEnvironmentVariable(GOOGLE_CLOUD_PROJECT, null);

            PoseContext.Isolate( () => {
                string result = SecretManager.Helpers.GetProjectId();
                Assert.AreEqual(ProjectId, result);
            }, platformShim);
        }

        [TestMethod]
        public void GetProjectId_Returns_Null()
        {
            System.Environment.SetEnvironmentVariable(GCLOUD_PROJECT, null);
            System.Environment.SetEnvironmentVariable(GOOGLE_CLOUD_PROJECT, null);
            string result = SecretManager.Helpers.GetProjectId();
            Assert.IsNull(result);
        }
    }
}
