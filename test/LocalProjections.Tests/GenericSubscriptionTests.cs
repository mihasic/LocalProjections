namespace LocalProjections.Tests
{
    using System.Threading.Tasks;
    using Shouldly;
    using Xunit;
    using Xunit.Abstractions;

    public class GenericSubscriptionTests
    {
        private readonly ITestOutputHelper _output;

        public GenericSubscriptionTests(ITestOutputHelper output) =>
            _output = output;

        [Fact]
        public async Task Normal_subscription_with_appended_events()
        {
            using (var fixture = new SubscriptionFixture(_output.WriteLine))
            {
                await fixture.Subscription.Started.ConfigureAwait(false);
                await fixture.WaitForCaughtUp().ConfigureAwait(false);
                fixture.Subscription.LastPosition.ShouldBe(fixture.LastProcessed);
                fixture.LastProcessed.ToInt64().ShouldBe(fixture.MaxPosition);

                fixture.AppendEvents(35);

                await fixture.WaitForCaughtUp().ConfigureAwait(false);
                fixture.Subscription.LastPosition.ShouldBe(fixture.LastProcessed);
                fixture.LastProcessed.ToInt64().ShouldBe(fixture.MaxPosition);
            }
        }

        [Fact]
        public async Task Gets_disposed_on_error()
        {
            using (var fixture = new SubscriptionFixture(_output.WriteLine,
                maxPosition: 10,
                handlerExceptionPosition: 11))
            {
                await fixture.Subscription.Started.ConfigureAwait(false);
                await fixture.WaitForCaughtUp().ConfigureAwait(false);
                fixture.Subscription.LastPosition.ShouldBe(fixture.LastProcessed);
                fixture.LastProcessed.ToInt64().ShouldBe(fixture.MaxPosition);

                fixture.AppendEvents(5);

                var error = await fixture.WaitForException.ConfigureAwait(false);
                error.ShouldNotBeNull();
                fixture.AppendEvents(5);

                fixture.Subscription.LastPosition.ToInt64().ShouldBe(11);
            }
        }
    }
}