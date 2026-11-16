using Grpc.Core;
using Grpc.Core.Interceptors;
using Notifications.Server.Application.Exceptions;

namespace Notifications.Server.Api;

/// <summary>
/// Translates exceptions into gRPC status codes, in one place for every RPC.
/// The client always gets a meaningful status code, never a raw exception.
/// <list type="bullet">
///   <item><see cref="ValidationException"/> -> INVALID_ARGUMENT</item>
///   <item><see cref="NotFoundException"/> -> NOT_FOUND</item>
///   <item><see cref="SubscriberOverflowException"/> -> RESOURCE_EXHAUSTED</item>
///   <item>Cancelled by the client -> CANCELLED, deadline reached -> DEADLINE_EXCEEDED</item>
///   <item>Anything else -> INTERNAL, logged on the server, details are not sent to the client</item>
/// </list>
/// </summary>
public sealed class ExceptionMappingInterceptor : Interceptor
{
    private readonly ILogger<ExceptionMappingInterceptor> _logger;

    public ExceptionMappingInterceptor(ILogger<ExceptionMappingInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw ToRpcException(exception, context);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw ToRpcException(exception, context);
        }
    }

    private RpcException ToRpcException(Exception exception, ServerCallContext context)
    {
        var status = exception switch
        {
            ValidationException validation => new Status(StatusCode.InvalidArgument, validation.Message),
            NotFoundException notFound => new Status(StatusCode.NotFound, notFound.Message),
            SubscriberOverflowException overflow => new Status(StatusCode.ResourceExhausted, overflow.Message),
            OperationCanceledException when context.CancellationToken.IsCancellationRequested => CancellationStatus(context),
            _ => InternalErrorStatus(exception, context),
        };

        return new RpcException(status);
    }

    private static Status CancellationStatus(ServerCallContext context) =>
        context.Deadline <= DateTime.UtcNow
            ? new Status(StatusCode.DeadlineExceeded, "The deadline was exceeded.")
            : new Status(StatusCode.Cancelled, "The call was cancelled by the client.");

    private Status InternalErrorStatus(Exception exception, ServerCallContext context)
    {
        _logger.LogError(exception, "Unhandled error in {Method}", context.Method);

        return new Status(StatusCode.Internal, "An internal error occurred.");
    }
}
