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
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "{Method} - invalid argument ({Request})", context.Method, request);
                throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Method} - unhandled exception ({Request})", context.Method, request);
                throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
            }
        }
    }
}
