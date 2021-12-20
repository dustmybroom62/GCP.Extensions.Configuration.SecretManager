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
    public class JsonSecretsConfigurationTests
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
                new Secret { SecretName = new SecretName(ProjectId, SecretId01) }
            };

            _pagedSecretResponse = new PagedEnumerableHelper<ListSecretsResponse, Secret>(_testSecrets);

            _testVersions = new List<SecretVersion>
            {
                new SecretVersion { State = SecretVersion.Types.State.Enabled, Name = (new SecretVersionName(ProjectId, SecretId01, "V1")).ToString() }
            };

            _pagedVersionResponse = new PagedEnumerableHelper<ListSecretVersionsResponse, SecretVersion>(_testVersions);

            _mockClient = new Mock<SecretManagerServiceClient>();

        }

        [TestMethod]
        public void AddGcpJsonSecrets_NoArgs()
        {
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            JsonConfigurationExtensions.AddGcpJsonSecrets(builder.Object);

            builder.Verify(x => x.Add(It.IsAny<GcpJsonSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpJsonSecrets_Arg_ListFilter()
        {
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            JsonConfigurationExtensions.AddGcpJsonSecrets(builder.Object, "filter");

            builder.Verify(x => x.Add(It.IsAny<GcpJsonSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpJsonSecrets_Arg_GoogleCred()
        {
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            ServiceAccountCredential.Initializer serviceAccountInit = new ServiceAccountCredential.Initializer("id") {
                Key = System.Security.Cryptography.RSA.Create()
            };
            ServiceAccountCredential serviceAccountCredential = new ServiceAccountCredential(serviceAccountInit);
            GoogleCredential googleCred = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential);
            //Mock<GoogleCredential> googleCred = new Mock<GoogleCredential>();
            JsonConfigurationExtensions.AddGcpJsonSecrets(builder.Object, null, googleCred);

            builder.Verify(x => x.Add(It.IsAny<GcpJsonSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpJsonSecrets_Arg_ProjectId()
        {
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            JsonConfigurationExtensions.AddGcpJsonSecrets(builder.Object, null, null, ProjectId);

            builder.Verify(x => x.Add(It.IsAny<GcpJsonSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void AddGcpJsonSecrets_Arg_Action()
        {
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            JsonConfigurationExtensions.AddGcpJsonSecrets(builder.Object, options => { });

            builder.Verify(x => x.Add(It.IsAny<GcpJsonSecretsConfigurationSource>()), Times.Once());
        }

        [TestMethod]
        public void GcpJsonSecretsConfigurationSource_Build()
        {
            ServiceAccountCredential.Initializer serviceAccountInit = new ServiceAccountCredential.Initializer("id")
            {
                Key = System.Security.Cryptography.RSA.Create()
            };
            ServiceAccountCredential serviceAccountCredential = new ServiceAccountCredential(serviceAccountInit);
            GoogleCredential googleCred = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential);
            GcpJsonSecretsConfigurationSource source = new GcpJsonSecretsConfigurationSource(null, googleCred, null);
            Mock<IConfigurationBuilder> builder = new Mock<IConfigurationBuilder>();
            IConfigurationProvider provider = source.Build(builder.Object);
            Assert.IsNotNull(provider);
            Assert.IsInstanceOfType(provider, typeof(GcpJsonSecretsConfigurationProvider));
        }

        [TestMethod]
        public void GcpJsonSecretsConfigurationProvider_Load()
        {
            string key = "key1";
            string value = "value1";
            string json = $"{{ \"{key}\": \"{value}\" }}";
            _mockClient.Setup(x => x.ListSecrets(It.IsAny<ListSecretsRequest>(), null)).Returns(_pagedSecretResponse);
            _mockClient.Setup(x => x.ListSecretVersions(It.IsAny<ListSecretVersionsRequest>(), null)).Returns(_pagedVersionResponse);
            AccessSecretVersionResponse response = new AccessSecretVersionResponse() {
                Payload = new SecretPayload() { Data = Google.Protobuf.ByteString.CopyFromUtf8(json) }
            };
            _mockClient.Setup(x => x.AccessSecretVersion(It.IsAny<SecretVersionName>(), null)).Returns(response);
            GcpJsonSecretsConfigurationSource source = new GcpJsonSecretsConfigurationSource();
            GcpJsonSecretOptions options = new GcpJsonSecretOptions {
                ProjectId = ProjectId,
                SecretMangerClient = _mockClient.Object
            };
            GcpJsonSecretsConfigurationProvider provider = new GcpJsonSecretsConfigurationProvider(source, options);
            provider.Load();

            Assert.IsTrue(provider.TryGet(key, out string configValue));
            Assert.AreEqual(value, configValue);
        }
    }
}
