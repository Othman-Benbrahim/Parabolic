using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Nickvision.Parabolic.NativeHost;

internal sealed class RssSubscriptionRecord
{
    public string Id { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool AutoDownload { get; set; } = true;
    public bool DownloadLatestOnly { get; set; } = true;
    public string KeywordFilter { get; set; } = string.Empty;
    public string Preset { get; set; } = "best";
    public string Priority { get; set; } = "normal";
    public int PollMinutes { get; set; } = 180;
    public string? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
    public List<string> SeenItemIds { get; set; } = [];
}

internal sealed record RssDiscoveredItem(
    string SubscriptionId,
    string SubscriptionTitle,
    string ItemId,
    string Title,
    Uri Url,
    DateTimeOffset? PublishedAt,
    string Preset,
    string Priority);

internal sealed record ParsedFeedItem(
    string Id,
    string Title,
    Uri Url,
    DateTimeOffset? PublishedAt);

internal sealed class RssSubscriptionService : IDisposable
{
    private const int MaxFeedCharacters = 2_000_000;
    private readonly HttpClient _httpClient;
    private readonly string _storePath;
    private readonly SemaphoreSlim _gate;
    private readonly SemaphoreSlim _checkGate;
    private readonly CancellationTokenSource _lifetime;
    private readonly List<RssSubscriptionRecord> _subscriptions;
    private Timer? _timer;
    private bool _loaded;

    public Func<RssDiscoveredItem, CancellationToken, Task<bool>>? ItemDiscovered { get; set; }

    public RssSubscriptionService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        var dataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _storePath = Path.Combine(dataRoot, "Parabolic", "rss-subscriptions.json");
        _gate = new SemaphoreSlim(1, 1);
        _checkGate = new SemaphoreSlim(1, 1);
        _lifetime = new CancellationTokenSource();
        _subscriptions = [];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        _timer ??= new Timer(
            _ => _ = CheckDueSubscriptionsSafeAsync(),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public async Task<IReadOnlyList<RssSubscriptionRecord>> ListAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _subscriptions
                .OrderBy(subscription => subscription.Title, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RssSubscriptionRecord> AddAsync(
        AddSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryCreateHttpUri(request.FeedUrl, out var feedUri))
        {
            throw new NativeRequestException("INVALID_FEED_URL", "An RSS subscription requires an HTTP or HTTPS feed URL.");
        }
        if (request.Preset is not ("best" or "1080" or "720" or "480" or "audio"))
        {
            throw new NativeRequestException("INVALID_PRESET", "The RSS download preset is not supported.");
        }
        if (request.Priority is not ("high" or "normal" or "low"))
        {
            throw new NativeRequestException("INVALID_PRIORITY", "RSS priority must be high, normal, or low.");
        }
        await FetchFeedAsync(feedUri, cancellationToken);
        var pollMinutes = Math.Clamp(request.PollMinutes <= 0 ? 180 : request.PollMinutes, 15, 1440);
        var requestedTitle = request.Title.Trim();
        await EnsureLoadedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = _subscriptions.FirstOrDefault(subscription =>
                string.Equals(subscription.FeedUrl, feedUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return Clone(existing);
            }
            var subscription = new RssSubscriptionRecord
            {
                Id = $"rss-{Guid.NewGuid():N}"[..20],
                FeedUrl = feedUri.AbsoluteUri,
                Title = string.IsNullOrWhiteSpace(requestedTitle) ? feedUri.Host : requestedTitle[..Math.Min(requestedTitle.Length, 200)],
                Enabled = true,
                AutoDownload = request.AutoDownload,
                DownloadLatestOnly = request.DownloadLatestOnly,
                KeywordFilter = request.KeywordFilter?.Trim() ?? string.Empty,
                Preset = request.Preset,
                Priority = request.Priority,
                PollMinutes = pollMinutes
            };
            _subscriptions.Add(subscription);
            await SaveLockedAsync(cancellationToken);
            return Clone(subscription);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var removed = _subscriptions.RemoveAll(subscription => subscription.Id == id) > 0;
            if (removed)
            {
                await SaveLockedAsync(cancellationToken);
            }
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CheckAllAsync(bool force, CancellationToken cancellationToken)
    {
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            List<string> ids;
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var now = DateTimeOffset.UtcNow;
                ids = _subscriptions
                    .Where(subscription => subscription.Enabled && (force || IsDue(subscription, now)))
                    .Select(subscription => subscription.Id)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
            var discovered = 0;
            foreach (var id in ids)
            {
                discovered += await CheckOneAsync(id, cancellationToken);
            }
            return discovered;
        }
        finally
        {
            _checkGate.Release();
        }
    }

    public Task<IReadOnlyList<ParsedFeedItem>> PreviewAsync(Uri feedUri, CancellationToken cancellationToken) =>
        FetchFeedAsync(feedUri, cancellationToken);

    public void Dispose()
    {
        _lifetime.Cancel();
        _timer?.Dispose();
        _checkGate.Dispose();
        _gate.Dispose();
        _lifetime.Dispose();
    }

    private async Task<int> CheckOneAsync(string id, CancellationToken cancellationToken)
    {
        RssSubscriptionRecord subscription;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            subscription = Clone(_subscriptions.First(item => item.Id == id));
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            var items = await FetchFeedAsync(new Uri(subscription.FeedUrl), cancellationToken);
            var candidates = items
                .Where(item => !subscription.SeenItemIds.Contains(item.Id, StringComparer.Ordinal))
                .Where(item => MatchesKeywords(item, subscription.KeywordFilter))
                .OrderByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(subscription.DownloadLatestOnly ? 1 : 25)
                .ToList();
            var accepted = 0;
            foreach (var item in candidates)
            {
                var dispatched = !subscription.AutoDownload || ItemDiscovered is null
                    || await ItemDiscovered(new RssDiscoveredItem(
                        subscription.Id,
                        subscription.Title,
                        item.Id,
                        item.Title,
                        item.Url,
                        item.PublishedAt,
                        subscription.Preset,
                        subscription.Priority), cancellationToken);
                if (dispatched)
                {
                    subscription.SeenItemIds.Add(item.Id);
                    accepted++;
                }
            }
            subscription.SeenItemIds = subscription.SeenItemIds.TakeLast(500).ToList();
            subscription.LastCheckedAt = DateTimeOffset.UtcNow.ToString("O");
            subscription.LastError = null;
            await ReplaceAndSaveAsync(subscription, cancellationToken);
            return accepted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            subscription.LastCheckedAt = DateTimeOffset.UtcNow.ToString("O");
            subscription.LastError = exception.Message.Length <= 300 ? exception.Message : exception.Message[..300];
            await ReplaceAndSaveAsync(subscription, cancellationToken);
            return 0;
        }
    }

    private async Task<IReadOnlyList<ParsedFeedItem>> FetchFeedAsync(Uri feedUri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(feedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxFeedCharacters)
        {
            throw new InvalidDataException("The RSS feed is larger than the supported 2 MB limit.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = MaxFeedCharacters,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(stream, settings, feedUri.AbsoluteUri);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
        return ParseFeed(document, feedUri);
    }

    internal static IReadOnlyList<ParsedFeedItem> ParseFeed(XDocument document, Uri feedUri)
    {
        if (document.Root is null || document.Root.Name.LocalName is not ("rss" or "feed" or "RDF"))
        {
            throw new InvalidDataException("The address did not return a supported RSS or Atom document.");
        }
        var nodes = document.Descendants().Where(element => element.Name.LocalName is "item" or "entry");
        var items = new List<ParsedFeedItem>();
        foreach (var node in nodes)
        {
            var title = ChildValue(node, "title").Trim();
            var link = node.Elements().FirstOrDefault(element => element.Name.LocalName == "link");
            var linkValue = link?.Attribute("href")?.Value ?? link?.Value ?? string.Empty;
            if (!Uri.TryCreate(feedUri, linkValue.Trim(), out var itemUri)
                || (itemUri.Scheme != Uri.UriSchemeHttp && itemUri.Scheme != Uri.UriSchemeHttps))
            {
                continue;
            }
            var id = ChildValue(node, "guid");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = ChildValue(node, "id");
            }
            if (string.IsNullOrWhiteSpace(id))
            {
                id = itemUri.AbsoluteUri;
            }
            var dateValue = ChildValue(node, "pubDate");
            if (string.IsNullOrWhiteSpace(dateValue))
            {
                dateValue = ChildValue(node, "published");
            }
            if (string.IsNullOrWhiteSpace(dateValue))
            {
                dateValue = ChildValue(node, "updated");
            }
            DateTimeOffset? publishedAt = DateTimeOffset.TryParse(dateValue, out var parsed) ? parsed : null;
            items.Add(new ParsedFeedItem(id.Trim(), string.IsNullOrWhiteSpace(title) ? itemUri.Host : title, itemUri, publishedAt));
        }
        return items;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }
            if (File.Exists(_storePath))
            {
                await using var stream = File.OpenRead(_storePath);
                var stored = await JsonSerializer.DeserializeAsync(
                    stream,
                    NativeJsonContext.Default.ListRssSubscriptionRecord,
                    cancellationToken);
                if (stored is not null)
                {
                    _subscriptions.AddRange(stored);
                }
            }
            _loaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ReplaceAndSaveAsync(RssSubscriptionRecord subscription, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = _subscriptions.FindIndex(item => item.Id == subscription.Id);
            if (index >= 0)
            {
                _subscriptions[index] = subscription;
                await SaveLockedAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveLockedAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(directory);
        var temporary = $"{_storePath}.tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                _subscriptions,
                NativeJsonContext.Default.ListRssSubscriptionRecord,
                cancellationToken);
        }
        File.Move(temporary, _storePath, true);
    }

    private async Task CheckDueSubscriptionsSafeAsync()
    {
        try
        {
            await CheckAllAsync(false, _lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unable to check RSS subscriptions: {exception.Message}");
        }
    }

    private static bool IsDue(RssSubscriptionRecord subscription, DateTimeOffset now) =>
        !DateTimeOffset.TryParse(subscription.LastCheckedAt, out var lastChecked)
        || lastChecked.AddMinutes(subscription.PollMinutes) <= now;

    private static bool MatchesKeywords(ParsedFeedItem item, string filter)
    {
        var keywords = filter.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return keywords.Length == 0 || keywords.Any(keyword => item.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value ?? string.Empty;

    private static bool TryCreateHttpUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }

    private static RssSubscriptionRecord Clone(RssSubscriptionRecord source) => new()
    {
        Id = source.Id,
        FeedUrl = source.FeedUrl,
        Title = source.Title,
        Enabled = source.Enabled,
        AutoDownload = source.AutoDownload,
        DownloadLatestOnly = source.DownloadLatestOnly,
        KeywordFilter = source.KeywordFilter,
        Preset = source.Preset,
        Priority = source.Priority,
        PollMinutes = source.PollMinutes,
        LastCheckedAt = source.LastCheckedAt,
        LastError = source.LastError,
        SeenItemIds = [.. source.SeenItemIds]
    };
}
