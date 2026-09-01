using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.NativeHost;

internal sealed record PostDownloadResult(
    string? Sha256,
    IReadOnlyList<string> CompletedSteps);

internal interface IPostDownloadProcessor
{
    string Name { get; }
    Task<string?> ProcessAsync(string path, CancellationToken cancellationToken);
}

internal interface IPostDownloadPipeline
{
    Task<PostDownloadResult> ExecuteAsync(
        string path,
        IReadOnlyList<string> requestedSteps,
        Action<string>? stepStarted,
        CancellationToken cancellationToken);
}

internal sealed class PostDownloadPipeline : IPostDownloadPipeline
{
    private readonly IReadOnlyDictionary<string, IPostDownloadProcessor> _processors;

    public PostDownloadPipeline()
    {
        IPostDownloadProcessor[] processors =
        [
            new VerifyOutputProcessor(),
            new Sha256Processor()
        ];
        _processors = processors.ToDictionary(processor => processor.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<PostDownloadResult> ExecuteAsync(
        string path,
        IReadOnlyList<string> requestedSteps,
        Action<string>? stepStarted,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> steps = requestedSteps.Count == 0 ? ["verify-output"] : requestedSteps;
        var completed = new List<string>(steps.Count);
        string? sha256 = null;
        foreach (var step in steps.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_processors.TryGetValue(step, out var processor))
            {
                throw new InvalidOperationException($"Unsupported post-processing step: {step}.");
            }
            stepStarted?.Invoke(processor.Name);
            var result = await processor.ProcessAsync(path, cancellationToken);
            if (processor.Name.Equals("sha256", StringComparison.OrdinalIgnoreCase))
            {
                sha256 = result;
            }
            completed.Add(processor.Name);
        }
        return new PostDownloadResult(sha256, completed);
    }
}

internal sealed class VerifyOutputProcessor : IPostDownloadProcessor
{
    public string Name => "verify-output";

    public Task<string?> ProcessAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The downloader reported success but the output file does not exist.", path);
        }
        if (file.Length <= 0)
        {
            throw new InvalidDataException("The downloader produced an empty output file.");
        }
        return Task.FromResult<string?>(null);
    }
}

internal sealed class Sha256Processor : IPostDownloadProcessor
{
    public string Name => "sha256";

    public async Task<string?> ProcessAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
