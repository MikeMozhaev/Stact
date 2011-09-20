// Copyright 2010 Chris Patterson
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Stact.Routing.Contexts
{
    using System;
    using Magnum.Caching;
    using Magnum.Reflection;


    public class RequestRoutingContextImpl<T> :
        AbstractRoutingContext,
        RoutingContext<Request<T>>,
        RoutingContext<T>

    {
        static readonly Cache<Type, RoutingContextProxyFactory<T>> _proxyFactoryCache =
            new ConcurrentCache<Type, RoutingContextProxyFactory<T>>(CreateMissingProxyFactory);

        readonly Request<T> _request;

        public RequestRoutingContextImpl(Request<T> request)
        {
            _request = request;
        }

        Request<T> RoutingContext<Request<T>>.Body
        {
            get { return _request; }
        }

        public void Match(Action<RoutingContext<Message<Request<T>>>> messageCallback,
                          Action<RoutingContext<Request<Request<T>>>> requestCallback,
                          Action<RoutingContext<Response<Request<T>>>> responseCallback)
        {
            throw new StactException("Nesting of header interfaces is not supported.");
        }

        void RoutingContext<Request<T>>.Convert<TResult>(Action<RoutingContext<TResult>> callback)
        {
            RoutingContext<TResult> proxy = _proxyFactoryCache[typeof(TResult)].CreateProxy<TResult>(this);
            callback(proxy);
        }

        public void Match(Action<RoutingContext<Message<T>>> messageCallback,
                          Action<RoutingContext<Request<T>>> requestCallback,
                          Action<RoutingContext<Response<T>>> responseCallback)
        {
            requestCallback(this);
        }

        void RoutingContext<T>.Convert<TResult>(Action<RoutingContext<TResult>> callback)
        {
            RoutingContext<TResult> proxy = _proxyFactoryCache[typeof(TResult)].CreateProxy<TResult>(this);
            callback(proxy);
        }


        T RoutingContext<T>.Body
        {
            get { return _request.Body; }
        }

        static RoutingContextProxyFactory<T> CreateMissingProxyFactory(Type key)
        {
            return
                (RoutingContextProxyFactory<T>)
                FastActivator.Create(typeof(RequestRoutingContextProxyFactory<,>), new[] {typeof(T), key});
        }
    }
}