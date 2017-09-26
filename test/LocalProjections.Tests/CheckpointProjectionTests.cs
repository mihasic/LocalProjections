namespace LocalProjections.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using LightningStore;
    using Shouldly;
    using Xunit;

    public class CheckpointProjectionTests
    {
        [Fact]
        public async Task Checkpoint_is_stored_on_commit()
        {
            var expected = new AllStreamPosition(10);

            using (var cpStore = Util.BuildCheckpointStore())
            using (var sut = new CheckpointProjection(new DelegateProjection(), cpStore))
            {
                await sut.Project(new Envelope(expected, null));

                await sut.Commit();
                cpStore.Read().ShouldBe(expected.ToInt64());
            }
        }

        [Fact]
        public async Task Checkpoint_is_notified_but_not_stored_on_project()
        {
            var expected = new AllStreamPosition(10);

            AllStreamPosition projected = AllStreamPosition.None;
            AllStreamPosition notified = AllStreamPosition.None;

            using (var cpStore = Util.BuildCheckpointStore())
            using (var sut = new CheckpointProjection(
                new DelegateProjection(m => projected = m.Checkpoint),
                cpStore,
                p => notified = p))
            {
                await sut.Project(new Envelope(expected, null));
                projected.ShouldBe(expected);
                notified.ShouldBe(expected);
                cpStore.Read().ShouldBeNull();
            }
        }
    }
}
