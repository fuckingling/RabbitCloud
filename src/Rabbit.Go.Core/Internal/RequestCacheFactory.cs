﻿using Microsoft.Extensions.Options;
using Rabbit.Go.Codec;
using Rabbit.Go.Core.Codec;
using Rabbit.Go.Formatters;
using Rabbit.Go.Interceptors;
using Rabbit.Go.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rabbit.Go
{
    public class RequestCacheFactory : IRequestCacheFactory
    {
        private readonly IGoClient _goClient;
        private readonly IKeyValueFormatterFactory _keyValueFormatterFactory;
        private readonly GoOptions _goOptions;
        private readonly EmptyRetryer _emptyRetryer = new EmptyRetryer();

        public RequestCacheFactory(IOptions<GoOptions> goOptions, IGoClient goClient, IKeyValueFormatterFactory keyValueFormatterFactory)
        {
            _goClient = goClient;
            _keyValueFormatterFactory = keyValueFormatterFactory;
            _goOptions = goOptions.Value;
        }

        private readonly ConcurrentDictionary<MethodDescriptor, RequestCache> _requestCaches = new ConcurrentDictionary<MethodDescriptor, RequestCache>();

        #region Implementation of IRequestCacheFactory

        public RequestCache GetRequestCache(MethodDescriptor descriptor)
        {
            if (_requestCaches.TryGetValue(descriptor, out var cache))
                return cache;

            var typeAndMethodAttributes = descriptor.ClienType.GetCustomAttributes().Concat(descriptor.MethodInfo.GetCustomAttributes()).ToArray();

            var interceptors = new List<IInterceptorMetadata>(_goOptions.GlobalInterceptors);
            interceptors.AddRange(typeAndMethodAttributes.OfType<IInterceptorMetadata>());

            IEncoder encoder = _goOptions.ForamtterEncoder;
            IDecoder decoder = _goOptions.ForamtterDecoder;

            var bodyParameterDescriptor = descriptor.Parameters.FirstOrDefault(i => i.Target == ParameterTarget.Body);
            if (bodyParameterDescriptor != null)
            {
                typeAndMethodAttributes = typeAndMethodAttributes
                    .Concat(bodyParameterDescriptor.ParameterType.GetCustomAttributes())
                    .ToArray();

                encoder = typeAndMethodAttributes.OfType<EncoderAttribute>().LastOrDefault() ?? encoder;
                decoder = typeAndMethodAttributes.OfType<DecoderAttribute>().LastOrDefault() ?? decoder;
            }

            cache = new RequestCache
            {
                Encoder = encoder,
                Decoder = decoder,
                Interceptors = interceptors.OrderBy(i => i is IOrderedInterceptor ordered ? ordered.Order : -10).ToArray(),
                RequestOptions = RequestOptions.Default,
                Client = _goClient,
                Descriptor = descriptor,
                KeyValueFormatterFactory = _keyValueFormatterFactory,
                RetryerFactory = () => _emptyRetryer
            };

            var type = descriptor.ClienType;
            var method = descriptor.MethodInfo;

            cache.DefaultQuery
                .Merge(type.GetTypeAttributes<DefaultQueryAttribute>().ToDictionary(i => i.Name, i => i.Value))
                .Merge(method.GetTypeAttributes<DefaultQueryAttribute>().ToDictionary(i => i.Name, i => i.Value));

            cache.DefaultHeaders
                .Merge(type.GetTypeAttributes<DefaultHeaderAttribute>().ToDictionary(i => i.Name, i => i.Value))
                .Merge(method.GetTypeAttributes<DefaultHeaderAttribute>().ToDictionary(i => i.Name, i => i.Value));

            _requestCaches[descriptor] = cache;

            return cache;
        }

        #endregion Implementation of IRequestCacheFactory
    }
}