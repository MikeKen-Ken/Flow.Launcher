using System;
using Flow.Launcher.Core.WebDavSync;
using NUnit.Framework;

namespace Flow.Launcher.Test.WebDavSync
{
    [TestFixture]
    public class WebDavSyncPlannerTest
    {
        [Test]
        public void Decide_WhenRemoteIsMissing_Uploads()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                lastSuccessfulSyncUtc: null,
                remoteExportedAtUtc: null);

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.Uploaded));
        }

        [Test]
        public void Decide_WhenLocalIsNewer_Uploads()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                lastSuccessfulSyncUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                remoteExportedAtUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.Uploaded));
        }

        [Test]
        public void Decide_WhenRemoteIsNewer_Downloads()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                lastSuccessfulSyncUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                remoteExportedAtUtc: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.Downloaded));
        }

        [Test]
        public void Decide_WhenTimesAreWithinClockSkew_IsAlreadyInSync()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                lastSuccessfulSyncUtc: new DateTime(2026, 1, 2, 0, 0, 1, DateTimeKind.Utc),
                remoteExportedAtUtc: new DateTime(2026, 1, 2, 0, 0, 2, DateTimeKind.Utc));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.AlreadyInSync));
        }

        [Test]
        public void Decide_WhenOnlyRemoteExists_Downloads()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: null,
                lastSuccessfulSyncUtc: null,
                remoteExportedAtUtc: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.Downloaded));
        }

        [Test]
        public void Decide_BeforeFirstSyncWithExistingRemote_Downloads()
        {
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: DateTime.UtcNow,
                lastSuccessfulSyncUtc: null,
                remoteExportedAtUtc: DateTime.UtcNow.AddMinutes(-1));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.Downloaded));
        }

        [Test]
        public void Decide_AfterSuccessfulApply_DoesNotUploadBecauseOfExtractTimestamps()
        {
            var appliedAt = new DateTime(2026, 1, 4, 12, 0, 0, DateTimeKind.Utc);
            var action = WebDavSyncPlanner.Decide(
                localMaxWriteUtc: appliedAt,
                lastSuccessfulSyncUtc: appliedAt,
                remoteExportedAtUtc: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(action, Is.EqualTo(WebDavSyncActionTaken.AlreadyInSync));
        }
    }
}
