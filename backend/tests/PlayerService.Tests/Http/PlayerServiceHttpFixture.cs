namespace PlayerService.Tests.Http
{
    public class PlayerServiceHttpFixture : IAsyncLifetime
    {
        public HttpClient Client { get; private set; } = null!;
        public PlayerServiceWebApplicationFactory Factory { get; private set; } = null!;

        public Task InitializeAsync()
        {
            Factory = new PlayerServiceWebApplicationFactory();
            Client = Factory.CreateClient();

            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
        }
    }
}
