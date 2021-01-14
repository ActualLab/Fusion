using System.Threading;
using System.Threading.Tasks;
using RestEase;
using Stl.Fusion.Client;

namespace Stl.Fusion.Tests.Services
{
    [RestEaseReplicaService(Scope = ServiceScope.ClientServices)]
    [BasePath("EdgeCase")]
    public interface IEdgeCaseClient
    {
        [Get("GetSuffix")]
        Task<string> GetSuffixAsync(CancellationToken cancellationToken = default);
        [Post("SetSuffix")]
        Task SetSuffixAsync(string suffix, CancellationToken cancellationToken = default);

        [Get("ThrowIfContainsError"), ComputeMethod(KeepAliveTime = 10)]
        Task<string> ThrowIfContainsErrorAsync(string source, CancellationToken cancellationToken = default);
        [Get("ThrowIfContainsErrorRewriteErrors"), ComputeMethod(KeepAliveTime = 10)]
        Task<string> ThrowIfContainsErrorRewriteErrorsAsync(string source, CancellationToken cancellationToken = default);
        [Get("ThrowIfContainsErrorNonCompute")]
        Task<string> ThrowIfContainsErrorNonComputeAsync(string source, CancellationToken cancellationToken = default);
    }

    [RestEaseReplicaService]
    [BasePath("EdgeCaseRewrite")]
    public interface IEdgeCaseRewriteClient : IEdgeCaseClient { }
}
