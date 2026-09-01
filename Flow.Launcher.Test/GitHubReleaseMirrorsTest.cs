using System.Linq;
using Flow.Launcher.Core;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    class GitHubReleaseMirrorsTest
    {
        [Test]
        public void GivenGitHubReleaseAsset_WhenCandidatesRequested_ThenIncludesDirectUrlAndMirrors()
        {
            var url = "https://github.com/MikeKen-Ken/Flow.Launcher/releases/download/v2.1.17/RELEASES";
            var candidates = GitHubReleaseMirrors.Candidates(url).ToList();

            Assert.That(candidates[0], Is.EqualTo("https://ghfast.top/" + url));
            Assert.That(candidates[^1], Is.EqualTo(url));
            Assert.That(candidates, Has.Count.EqualTo(1 + GitHubReleaseMirrors.Prefixes.Length));
        }

        [Test]
        public void GivenNonGitHubUrl_WhenCandidatesRequested_ThenReturnsOriginalOnly()
        {
            var url = "https://example.com/file.bin";
            var candidates = GitHubReleaseMirrors.Candidates(url).ToArray();

            Assert.That(candidates, Is.EqualTo(new[] { url }));
        }

        [Test]
        public void GivenGitHubTagPage_WhenCandidatesRequested_ThenDoesNotMirror()
        {
            var url = "https://github.com/MikeKen-Ken/Flow.Launcher/releases/tag/v2.1.17";
            var candidates = GitHubReleaseMirrors.Candidates(url).ToArray();

            Assert.That(candidates, Is.EqualTo(new[] { url }));
        }

        [Test]
        public void GivenGitHubLatestDownloadUrl_WhenCandidatesRequested_ThenIncludesMirrors()
        {
            var url = "https://github.com/MikeKen-Ken/Flow.Launcher/releases/latest/download/RELEASES";
            var candidates = GitHubReleaseMirrors.Candidates(url).ToList();

            Assert.That(candidates[0], Is.EqualTo("https://ghfast.top/" + url));
            Assert.That(candidates[^1], Is.EqualTo(url));
        }

        [Test]
        public void GivenSignedS3Url_WhenCandidatesRequested_ThenDoesNotMirror()
        {
            var url = "https://github-cloud.s3.amazonaws.com/releases/123/asset?X-Amz-Signature=abc";
            var candidates = GitHubReleaseMirrors.Candidates(url).ToArray();

            Assert.That(candidates, Is.EqualTo(new[] { url }));
        }
    }
}
