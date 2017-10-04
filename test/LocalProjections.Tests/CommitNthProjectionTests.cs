namespace LocalProjections.Tests
{
    using System.Threading.Tasks;
    using Shouldly;
    using Xunit;

    public class CommitNthProjectionTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        public async Task Commit_happens_only_after_nth_attempt(int n)
        {
            AllStreamPosition projected = AllStreamPosition.None;
            bool committed = false;

            using (var sut = new CommitNthProjection(
                Util.Projection(m => projected = m.Checkpoint, () => committed = true),
                n))
            {
                for (int i = 0; i < n; i++)
                {
                    var expected = new AllStreamPosition(i);
                    await sut.Project(new Envelope(expected, null));
                    projected.ShouldBe(expected);
                    if (i < n-1)
                        committed.ShouldBeFalse();
                }
            }
            committed.ShouldBeTrue();
        }
    }
}
