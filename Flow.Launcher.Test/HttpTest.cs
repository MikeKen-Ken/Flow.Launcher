using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Net.Http;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Infrastructure.Http;

namespace Flow.Launcher.Test
{
    [TestFixture]
    class HttpTest
    {
        [Test]
        public void GivenHttpProxy_WhenUpdated_ThenWebProxyShouldAlsoBeUpdatedToTheSame()
        {
            HttpProxy proxy = new HttpProxy();
            Http.Proxy = proxy;

            proxy.Enabled = true;
            proxy.Server = "127.0.0.1";
            ClassicAssert.AreEqual(Http.WebProxy.Address, new Uri($"http://{proxy.Server}:{proxy.Port}"));
            ClassicAssert.IsNull(Http.WebProxy.Credentials);
            Assert.That(HttpClient.DefaultProxy, Is.SameAs(Http.WebProxy));

            proxy.UserName = "test";
            ClassicAssert.NotNull(Http.WebProxy.Credentials);
            ClassicAssert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").UserName, proxy.UserName);
            ClassicAssert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").Password, "");

            proxy.Password = "test password";
            ClassicAssert.AreEqual(Http.WebProxy.Credentials.GetCredential(Http.WebProxy.Address, "Basic").Password, proxy.Password);

            proxy.Enabled = false;
            Assert.That(HttpClient.DefaultProxy, Is.Not.SameAs(Http.WebProxy));
            Assert.That(Http.WebProxy.Address, Is.Null);
        }
    }
}
