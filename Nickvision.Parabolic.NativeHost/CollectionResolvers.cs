using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal sealed record ResolvedCollectionItem(
    string Id,
    string Title,
    string Url,
    string? PublishedAt);

internal sealed record ResolvedCollection(
    string Resolver,
    string SourceUrl,
    IReadOnlyList<ResolvedCollectionItem> Items);

internal interface ICollectionResolver
{
    string Name { get; }
    bool CanResolve(Uri source);
    Task<ResolvedCollection> ResolveAsync(Uri source, int limit, CancellationToken cancellationToken);
}

internal sealed class CollectionResolverRegistry
{
    private readonly IReadOnlyList<ICollectionResolver> _resolvers;

    public CollectionResolverRegistry(IEnumerable<ICollectionResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    public async Task<ResolvedCollection> ResolveAsync(Uri source, int limit, CancellationToken cancellationToken)
    {
        foreach (var resolver in _resolvers.Where(candidate => candidate.CanResolve(source)))
        {
            try
            {
                return await resolver.ResolveAsync(source, Math.Clamp(limit, 1, 100), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new NativeRequestException(
                    "COLLECTION_RESOLUTION_FAILED",
                    $"The {resolver.Name} collection resolver could not read this source.",
                    exception);
            }
        }
        throw new NativeRequestException("COLLECTION_NOT_SUPPORTED", "No registered collection resolver accepts this URL.");
    }
}

internal sealed class RssCollectionResolver : ICollectionResolver
{
    private readonly RssSubscriptionService _subscriptions;

    public string Name => "rss";

    public RssCollectionResolver(RssSubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public bool CanResolve(Uri source) =>
        source.Scheme is "http" or "https";

    public async Task<ResolvedCollection> ResolveAsync(Uri source, int limit, CancellationToken cancellationToken)
    {
        var items = await _subscriptions.PreviewAsync(source, cancellationToken);
        return new ResolvedCollection(
            Name,
            source.AbsoluteUri,
            items
                .OrderByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(limit)
                .Select(item => new ResolvedCollectionItem(
                    item.Id,
                    item.Title,
                    item.Url.AbsoluteUri,
                    item.PublishedAt?.ToString("O")))
                .ToList());
    }
}
