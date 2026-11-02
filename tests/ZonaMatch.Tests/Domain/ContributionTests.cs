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
        public void Three_confirmations_approve_the_contribution()
        {
            var contribution = new Contribution();

            Vote(contribution, 2, confirms: true);
            Assert.Equal(ContributionStatus.Pending, contribution.Status);

            Vote(contribution, 1, confirms: true);
            Assert.Equal(ContributionStatus.Approved, contribution.Status);
            Assert.Equal(Now, contribution.ResolvedAt);
        }

        [Fact]
        public void One_rejection_offsets_one_confirmation()
        {
            var contribution = new Contribution();

            Vote(contribution, 1, confirms: false);
            Vote(contribution, 3, confirms: true);
            Assert.Equal(ContributionStatus.Pending, contribution.Status);

            Vote(contribution, 1, confirms: true);
            Assert.Equal(ContributionStatus.Approved, contribution.Status);
        }

        [Fact]
        public void Three_vote_difference_rejects_the_contribution()
        {
            var contribution = new Contribution();

            Vote(contribution, 3, confirms: false);

            Assert.Equal(ContributionStatus.Rejected, contribution.Status);
        }

        [Fact]
        public void Voting_again_changes_the_vote_without_adding_another()
        {
            var contribution = new Contribution();
            var user = Guid.NewGuid();

            contribution.RecordVote(user, confirms: true, Now);
            contribution.RecordVote(user, confirms: false, Now);

            var vote = Assert.Single(contribution.Votes);
            Assert.False(vote.Confirms);
            Assert.Equal(0, contribution.Confirmations);
            Assert.Equal(1, contribution.Rejections);
        }
    }
}
