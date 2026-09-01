using System;
using System.Runtime.InteropServices;
using Flow.Launcher.Helper;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class ExceptionHelperTest
{
    private const int DwmCompositionDisabled = unchecked((int)0x80263001);
    private const int StatusMessageLost = unchecked((int)0xD0000701);

    [Test]
    public void DwmCompositionDisabled_IsRecoverable()
    {
        var exception = new COMException("desktop composition is disabled", DwmCompositionDisabled);

        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(exception), Is.True);
    }

    [Test]
    public void MessageLostFromPresentationFramework_IsRecoverable()
    {
        var exception = new COMException("message lost", StatusMessageLost)
        {
            Source = "PresentationFramework"
        };

        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(exception), Is.True);
    }

    [Test]
    public void MessageLostFromOtherSource_IsNotRecoverable()
    {
        var exception = new COMException("message lost", StatusMessageLost)
        {
            Source = "mscorlib"
        };

        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(exception), Is.False);
    }

    [Test]
    public void WrappedDwmException_IsRecoverable()
    {
        var inner = new COMException("desktop composition is disabled", DwmCompositionDisabled);
        var wrapped = new InvalidOperationException("window chrome failed", inner);

        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(wrapped), Is.True);
    }

    [Test]
    public void AggregateDwmException_IsRecoverable()
    {
        var inner = new COMException("desktop composition is disabled", DwmCompositionDisabled);
        var aggregate = new AggregateException(inner);

        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(aggregate), Is.True);
    }

    [Test]
    public void UnrelatedException_IsNotRecoverable()
    {
        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(new InvalidOperationException("nope")), Is.False);
        Assert.That(ExceptionHelper.IsRecoverableDwmCompositionException(new COMException("nope", 0)), Is.False);
    }
}
