using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Moq;
using GCP.Extensions.Configuration.SecretManager.Test.Helpers;

namespace GCP.Extensions.Configuration.SecretManager.Test
{
    [TestClass]
    public class GcpKeyValueSecretsConfigurationTests
    {
        public static readonly string GOOGLE_CLOUD_PROJECT = "GOOGLE_CLOUD_PROJECT";
        public static readonly string GCLOUD_PROJECT = "GCLOUD_PROJECT";
        public static readonly string ProjectId = "Project123";
        public static readonly string SecretId01 = "SecretId01";

        private List<Secret> _testSecrets;
        private List<SecretVersion> _testVersions;
        private Helpers.PagedEnumerableHelper<ListSecretsResponse, Secret> _pagedSecretResponse;
        private Helpers.PagedEnumerableHelper<ListSecretVersionsResponse, SecretVersion> _pagedVersionResponse;
        private Mock<SecretManagerServiceClient> _mockClient;

        [TestInitialize]
        public void Init()
        {
            _testSecrets = new List<Secret>
            {
                new() { SecretName = new SecretName(ProjectId, SecretId01) }
            };

            _pagedSecretResponse = new PagedEnumerableHelper<ListSecretsResponse, Secret>(_testSecrets);

            _testVersions = new List<SecretVersion>
            {
                new() { State = SecretVersion.Types.State.Enabled, Name = (new SecretVersionName(ProjectId, SecretId01, "V1")).ToString() }
            };

            _pagedVersionResponse = new PagedEnumerableHelper<ListSecretVersionsResponse, SecretVersion>(_testVersions);

            _mockClient = new Mock<SecretManagerServiceClient>();
        }

        [TestMethod]
        public void AddGcpKeyValueSecrets_NoArgs()
        {
            Mock<IConfigurationBuilder> builder = new();
            KeyValueConfigurationExtensions.AddGcpKeyValueSecrets(builder.Object);

            builder.Verify(x => x.Add(It.IsAny<GcpKeyValueSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpKeyValueSecrets_Arg_ListFilter()
        {
            Mock<IConfigurationBuilder> builder = new();
            KeyValueConfigurationExtensions.AddGcpKeyValueSecrets(builder.Object, "filter");

            builder.Verify(x => x.Add(It.IsAny<GcpKeyValueSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpKeyValueSecrets_Arg_GoogleCred()
        {
            Mock<IConfigurationBuilder> builder = new();
            ServiceAccountCredential.Initializer serviceAccountInit = new("id") {
                Key = System.Security.Cryptography.RSA.Create()
            };
            ServiceAccountCredential serviceAccountCredential = new(serviceAccountInit);
            GoogleCredential googleCred = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential);
            //Mock<GoogleCredential> googleCred = new Mock<GoogleCredential>();
            KeyValueConfigurationExtensions.AddGcpKeyValueSecrets(builder.Object, null, googleCred);

            builder.Verify(x => x.Add(It.IsAny<GcpKeyValueSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpKeyValueSecrets_Arg_ProjectId()
        {
            Mock<IConfigurationBuilder> builder = new();
            KeyValueConfigurationExtensions.AddGcpKeyValueSecrets(builder.Object, null, null, ProjectId);

            builder.Verify(x => x.Add(It.IsAny<GcpKeyValueSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpKeyValueSecrets_Arg_Action()
        {
            Mock<IConfigurationBuilder> builder = new();
            KeyValueConfigurationExtensions.AddGcpKeyValueSecrets(builder.Object, options => { });

            builder.Verify(x => x.Add(It.IsAny<GcpKeyValueSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void GcpKeyValueSecretsConfigurationSource_Build()
        {
            ServiceAccountCredential.Initializer serviceAccountInit = new("id")
            {
                Key = System.Security.Cryptography.RSA.Create()
            };
            ServiceAccountCredential serviceAccountCredential = new(serviceAccountInit);
            GoogleCredential googleCred = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential);
            GcpKeyValueSecretsConfigurationSource source = new(null, googleCred, null);
            Mock<IConfigurationBuilder> builder = new();
            IConfigurationProvider provider = source.Build(builder.Object);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOfType(provider, typeof(GcpKeyValueSecretsConfigurationProvider));
        }

        [TestMethod]
        public void GcpKeyValueSecretsConfigurationProvider_Load()
        {
            string key = SecretId01;
            string value = "value1";
            _mockClient.Setup(x => x.ListSecrets(It.IsAny<ListSecretsRequest>(), null)).Returns(_pagedSecretResponse);
            _mockClient.Setup(x => x.ListSecretVersions(It.IsAny<ListSecretVersionsRequest>(), null)).Returns(_pagedVersionResponse);
            AccessSecretVersionResponse response = new() {
                Payload = new SecretPayload() { Data = Google.Protobuf.ByteString.CopyFromUtf8(value) }
            };
            _mockClient.Setup(x => x.AccessSecretVersion(It.IsAny<SecretVersionName>(), null)).Returns(response);

            GcpKeyValueSecretOptions options = new()
						{
                ProjectId = ProjectId,
                SecretMangerClient = _mockClient.Object
            };
            GcpKeyValueSecretsConfigurationProvider provider = new(options);
            provider.Load();

            Assert.IsTrue(provider.TryGet(key, out string configValue));
            Assert.AreEqual(value, configValue);
        }
    }
}
