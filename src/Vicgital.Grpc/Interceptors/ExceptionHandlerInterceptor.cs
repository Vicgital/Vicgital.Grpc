using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Vicgital.Grpc.Interceptors
{
    public class ExceptionHandlerInterceptor(ILogger<ExceptionHandlerInterceptor> logger) : Interceptor
    {
        private readonly ILogger<ExceptionHandlerInterceptor> _logger = logger;

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (RpcException)
            {
                // Already a deliberate gRPC status (e.g. from request validation) - pass it through untouched.
                throw;
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex, context, request);
            }
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            ServerCallContext context,
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(requestStream, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex, context);
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
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex, context, request);
            }
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                await continuation(requestStream, responseStream, context);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex, context);
            }
        }

        private RpcException ToRpcException(Exception ex, ServerCallContext context, object? request = null)
        {
            if (ex is ArgumentException)
            {
                _logger.LogWarning(ex, "{Method} - invalid argument ({Request})", context.Method, request);
                return new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }

            _logger.LogError(ex, "{Method} - unhandled exception ({Request})", context.Method, request);
            return new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }
}
