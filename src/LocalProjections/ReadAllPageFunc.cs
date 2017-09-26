namespace LocalProjections
{
    using System.Threading;
    using System.Threading.Tasks;

    public delegate Task<ReadAllPage> ReadAllPageFunc(
        AllStreamPosition fromPosition,
        CancellationToken cancellationToken);
}