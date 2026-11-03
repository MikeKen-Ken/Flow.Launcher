using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Flow.Launcher.Helper;
using NUnit.Framework;

namespace Flow.Launcher.Test;

public class SingleInstanceIntegrityTest
{
    [Test]
    public async Task CreateInboundPipe_IsMediumIntegrityAndAcceptsAClient()
    {
        var name = "Flow.Launcher.Test." + Guid.NewGuid().ToString("N");
        await using var server = SingleInstanceNativeMethods.CreateInboundPipe(name);
        var label = SingleInstanceNativeMethods.GetIntegrityLabelSid(server.SafePipeHandle);

        using var client = new NamedPipeClientStream(".", name, PipeDirection.Out);
        var connect = Task.Run(() => client.Connect(2000));
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await connect.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Multiple(() =>
        {
            Assert.That(label, Is.EqualTo(SingleInstanceNativeMethods.MediumIntegritySid));
            Assert.That(client.IsConnected, Is.True);
        });
    }
}
