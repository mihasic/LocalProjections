namespace LocalProjections.Tests
{
    using System;
    using System.Threading.Tasks;
    using Shouldly;
    using Xunit;

    public class SuspendableProjectionTests
    {
        [Fact]
        public async Task Suspend_happens_on_error()
        {
            Exception suspended = null;

            using (var sut = new SuspendableProjection(
                Util.Projection(m => throw new Exception("test")),
                ex => suspended = ex))
            {
                for (int i = 1; i < 5; i++)
                    await sut.Project(new Envelope(new AllStreamPosition(i), null));
            }
            suspended.ShouldNotBeNull();
            suspended.Message.ShouldBe("test");
        }

        [Fact]
        public async Task On_suspend_commit_does_not_happen()
        {
            bool committed = false;

            using (var sut = new SuspendableProjection(
                Util.Projection(m => throw new Exception("test"), () => committed = true)))
            {
                for (int i = 1; i < 5; i++)
                    await sut.Project(new Envelope(new AllStreamPosition(i), null));

                await sut.Commit();
            }
            committed.ShouldBeFalse();
        }

        [Fact]
        public async Task Without_suspend_commit_happens()
        {
            bool committed = false;

            using (var sut = new SuspendableProjection(
                Util.Projection(commit: () => committed = true)))
            {
                for (int i = 1; i < 5; i++)
                    await sut.Project(new Envelope(new AllStreamPosition(i), null));

                await sut.Commit();
            }
            committed.ShouldBeTrue();
        }

        [Fact]
        public async Task Only_first_messages_before_error_are_processed()
        {
            var expected = new AllStreamPosition(3);
            AllStreamPosition lastCheckpoint = AllStreamPosition.None;

            using (var sut = new SuspendableProjection(
                Util.Projection(m =>
                {
                    lastCheckpoint = m.Checkpoint;
                    if (m.Checkpoint == expected) throw new Exception("test");
                })))
            {
                for (int i = 1; i < 5; i++)
                    await sut.Project(new Envelope(new AllStreamPosition(i), null));
            }
            lastCheckpoint.ShouldBe(expected);
        }
    }
}