using System.Threading;
using System.Threading.Tasks;
using Stl.Fusion.Interception;

namespace Stl.Fusion.Swapping
{
    public interface ISwapService
    {
        ValueTask<IResult?> LoadAsync((ComputeMethodInput Input, LTag Version) key, CancellationToken cancellationToken = default);
        ValueTask StoreAsync((ComputeMethodInput Input, LTag Version) key, IResult value, CancellationToken cancellationToken = default);
    }
}
