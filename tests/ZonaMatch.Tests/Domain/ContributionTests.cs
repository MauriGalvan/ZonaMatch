using FluentAssertions;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Tests.Domain
{
    public class ContributionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private static void Vote(Contribution contribution, int count, bool confirms)
        {
            for (var i = 0; i < count; i++)
                contribution.RecordVote(Guid.NewGuid(), confirms, Now);
        }

        [Fact]
        public void RecordVote_WhenThreeConfirmationsAreReached_ApprovesContribution()
        {
            var contribution = new Contribution();

            Vote(contribution, 2, confirms: true);
            contribution.Status.Should().Be(ContributionStatus.Pending);

            Vote(contribution, 1, confirms: true);
            contribution.Status.Should().Be(ContributionStatus.Approved);
            contribution.ResolvedAt.Should().Be(Now);
        }

        [Fact]
        public void RecordVote_WhenConfirmationsExceedRejectionsByThree_ApprovesContribution()
        {
            var contribution = new Contribution();

            Vote(contribution, 1, confirms: false);
            Vote(contribution, 3, confirms: true);
            contribution.Status.Should().Be(ContributionStatus.Pending);

            Vote(contribution, 1, confirms: true);
            contribution.Status.Should().Be(ContributionStatus.Approved);
        }

        [Fact]
        public void RecordVote_WhenRejectionsExceedConfirmationsByThree_RejectsContribution()
        {
            var contribution = new Contribution();

            Vote(contribution, 3, confirms: false);

            contribution.Status.Should().Be(ContributionStatus.Rejected);
        }

        [Fact]
        public void RecordVote_WhenSameUserVotesAgain_UpdatesExistingVote()
        {
            var contribution = new Contribution();
            var user = Guid.NewGuid();

            contribution.RecordVote(user, confirms: true, Now);
            contribution.RecordVote(user, confirms: false, Now);

            var vote = contribution.Votes.Should().ContainSingle().Which;
            vote.Confirms.Should().BeFalse();
            contribution.Confirmations.Should().Be(0);
            contribution.Rejections.Should().Be(1);
        }
    }
}
