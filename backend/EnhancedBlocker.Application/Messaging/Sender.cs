using System.Collections.Concurrent;
using System.Reflection;

namespace EnhancedBlocker.Application.Messaging;

/// <summary>
/// Reflection-based dispatcher: for a request of runtime type <c>TRequest</c> implementing
/// <c>IRequest&lt;TResponse&gt;</c>, resolves <c>IRequestHandler&lt;TRequest, TResponse&gt;</c> from the
/// service provider and invokes its <see cref="IRequestHandler{TRequest,TResponse}.Handle"/>.
/// Handler-type lookup and the cached <see cref="MethodInfo"/> make dispatch allocation-light.
/// </summary>
public sealed class Sender(IServiceProvider services) : ISender
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoker = Invokers.GetOrAdd(request.GetType(), static requestType =>
        {
            var responseType = typeof(TResponse);
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var method = handlerType.GetMethod(nameof(IRequestHandler<,>.Handle))
                ?? throw new InvalidOperationException($"Handler method not found for {handlerType}.");
            return new HandlerInvoker(handlerType, method);
        });

        var handler = services.GetService(invoker.HandlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for {request.GetType().Name}.");

        return (Task<TResponse>)invoker.Method.Invoke(handler, [request, ct])!;
    }

    private readonly record struct HandlerInvoker(Type HandlerType, MethodInfo Method);
}
