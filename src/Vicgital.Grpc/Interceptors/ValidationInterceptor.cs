using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Vicgital.Grpc.Interceptors
{
    /// <summary>
    /// Validates incoming request messages against a registered FluentValidation <see cref="IValidator{T}"/>
    /// for the request type, rejecting invalid requests with <see cref="StatusCode.InvalidArgument"/> before
    /// they reach the service implementation. Request types with no registered validator pass through unvalidated.
    /// </summary>
    public class ValidationInterceptor(IServiceProvider serviceProvider) : Interceptor
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            await ValidateAsync(_serviceProvider, request, context.CancellationToken);
            return await continuation(request, context);
        }

        public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            ServerCallContext context,
            ClientStreamingServerMethod<TRequest, TResponse> continuation)
        {
            return await continuation(new ValidatingStreamReader<TRequest>(requestStream, _serviceProvider), context);
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
            TRequest request,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            await ValidateAsync(_serviceProvider, request, context.CancellationToken);
            await continuation(request, responseStream, context);
        }

        public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
            IAsyncStreamReader<TRequest> requestStream,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context,
            DuplexStreamingServerMethod<TRequest, TResponse> continuation)
        {
            await continuation(new ValidatingStreamReader<TRequest>(requestStream, _serviceProvider), responseStream, context);
        }

        private static async Task ValidateAsync<TRequest>(
            IServiceProvider serviceProvider,
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : class
        {
            if (serviceProvider.GetService(typeof(IValidator<TRequest>)) is not IValidator validator)
                return;

            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            if (!result.IsValid)
                throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", result.Errors.Select(e => e.ErrorMessage))));
        }

        private sealed class ValidatingStreamReader<TRequest>(
            IAsyncStreamReader<TRequest> inner,
            IServiceProvider serviceProvider) : IAsyncStreamReader<TRequest>
            where TRequest : class
        {
            public TRequest Current => inner.Current;

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (!await inner.MoveNext(cancellationToken))
                    return false;

                await ValidateAsync(serviceProvider, inner.Current, cancellationToken);
                return true;
            }
        }
    }
}
