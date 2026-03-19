using Conspectare.Workers;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class DistributedBackgroundServiceTests
{
    private readonly Mock<IDistributedLock> _lockMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

    public DistributedBackgroundServiceTests()
    {
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
    }

    [Fact]
    public async Task LockAcquired_JobRunsAndCompletes()
    {
        var disposed = false;
        var handle = new MockLockHandle(() => disposed = true);
        _lockMock.Setup(l => l.TryAcquireAsync("test-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle);

        var sut = new TestBackgroundService(_lockMock.Object, _scopeFactoryMock.Object,
            isOneShot: true, result: 5, startupDelay: TimeSpan.Zero);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await sut.StopAsync(CancellationToken.None);

        Assert.True(sut.JobRan);
        Assert.True(disposed);
    }

    [Fact]
    public async Task LockNotAcquired_JobSkips()
    {
        _lockMock.Setup(l => l.TryAcquireAsync("test-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable)null);

        var sut = new TestBackgroundService(_lockMock.Object, _scopeFactoryMock.Object,
            isOneShot: true, result: 0, startupDelay: TimeSpan.Zero);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await sut.StopAsync(CancellationToken.None);

        Assert.False(sut.JobRan);
    }

    [Fact]
    public async Task JobThrowsException_ExceptionHandled()
    {
        var handle = new MockLockHandle(() => { });
        _lockMock.Setup(l => l.TryAcquireAsync("test-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle);

        var sut = new TestBackgroundService(_lockMock.Object, _scopeFactoryMock.Object,
            isOneShot: true, throwException: true, startupDelay: TimeSpan.Zero);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await sut.StopAsync(CancellationToken.None);

        Assert.True(sut.JobRan);
    }

    [Fact]
    public async Task OneShotMode_ExitsAfterSingleRun()
    {
        var handle = new MockLockHandle(() => { });
        _lockMock.Setup(l => l.TryAcquireAsync("test-job", It.IsAny<CancellationToken>()))
            .ReturnsAsync(handle);

        var sut = new TestBackgroundService(_lockMock.Object, _scopeFactoryMock.Object,
            isOneShot: true, result: 1, startupDelay: TimeSpan.Zero);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(1, sut.RunCount);
    }

    private sealed class TestBackgroundService : DistributedBackgroundService
    {
        private readonly int _result;
        private readonly bool _throwException;
        private readonly TimeSpan _startupDelay;

        public bool JobRan { get; private set; }
        public int RunCount { get; private set; }

        protected override string JobName => "test-job";
        protected override TimeSpan Interval => TimeSpan.FromHours(1);
        protected override bool IsOneShot { get; }

        public TestBackgroundService(
            IDistributedLock distributedLock,
            IServiceScopeFactory scopeFactory,
            bool isOneShot,
            int result = 0,
            bool throwException = false,
            TimeSpan? startupDelay = null)
            : base(distributedLock, scopeFactory, Mock.Of<ILogger>())
        {
            IsOneShot = isOneShot;
            _result = result;
            _throwException = throwException;
            _startupDelay = startupDelay ?? TimeSpan.Zero;
        }

        protected override TimeSpan StartupDelay => _startupDelay;

        protected override TimeSpan ComputeDelay() => _startupDelay;

        protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
        {
            JobRan = true;
            RunCount++;

            if (_throwException)
                throw new InvalidOperationException("Test failure");

            return Task.FromResult(_result);
        }
    }

    private sealed class MockLockHandle : IAsyncDisposable
    {
        private readonly Action _onDispose;
        public MockLockHandle(Action onDispose) => _onDispose = onDispose;

        public ValueTask DisposeAsync()
        {
            _onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
