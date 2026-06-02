namespace EnhancedBlocker.Application.Messaging;

/// <summary>Marker for a CQRS request (command or query) that yields a <typeparamref name="TResponse"/>.</summary>
public interface IRequest<TResponse>;

/// <summary>Handles a single <typeparamref name="TRequest"/> use case.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}

/// <summary>
/// Dispatches a request to its registered handler.
/// <para>
/// MediatR decision: as of v12+ MediatR ships under a commercial license that is ambiguous for
/// this kind of personal/redistributable use, so rather than take on that licensing risk we use
/// this ~30-line dispatcher. It covers everything M1 needs (one handler per request, no pipeline
/// behaviors). If behaviors/streaming are ever required, this is the single seam to revisit.
/// </para>
/// </summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}
