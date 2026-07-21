using System.Diagnostics.Metrics;
using ActualLab.Fusion;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Diagnostics;

public sealed class MetricInstrumentContractTest
{
    [Theory]
    [InlineData(nameof(ComputedRegistry.MeterSet.CapacityCounter))]
    [InlineData(nameof(ComputedRegistry.MeterSet.NodeCounter))]
    [InlineData(nameof(ComputedRegistry.MeterSet.EdgeCounter))]
    public void ComputedRegistryLiveCountMustBeGauge(string fieldName)
        => typeof(ComputedRegistry.MeterSet).GetField(fieldName)!.FieldType
            .Should().Be(typeof(ObservableGauge<long>));

    [Fact]
    public void RpcTransportLiveCountMustBeGauge()
        => typeof(RpcFrameBasedTransport.FrameMeterSet)
            .GetField(nameof(RpcFrameBasedTransport.FrameMeterSet.ChannelCounter))!.FieldType
            .Should().Be(typeof(ObservableGauge<long>));
}
