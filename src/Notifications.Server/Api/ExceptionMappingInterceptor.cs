using Grpc.Core;
using Grpc.Core.Interceptors;
using Notifications.Server.Application.Exceptions;

namespace Notifications.Server.Api;

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
