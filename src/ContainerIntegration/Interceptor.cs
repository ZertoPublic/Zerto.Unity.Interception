﻿using System;
using Unity.Extension;
using Unity.Interception.Interceptors;
using Unity.Interception.Interceptors.InstanceInterceptors;
using Unity.Interception.Interceptors.TypeInterceptors.VirtualMethodInterception;
using Unity.Interception.Utilities;

namespace Unity.Interception.ContainerIntegration
{
    /// <summary>
    /// Stores information about the <see cref="IInterceptor"/> to be used to intercept an object and
    /// configures a container accordingly.
    /// </summary>
    /// <seealso cref="IInterceptionBehavior"/>
    public class Interceptor : InterceptionMember
    {
        private readonly IInterceptor _interceptor;
        private readonly Type _type;
        private readonly string _name;

        /// <summary>
        /// Initializes a new instance of the <see cref="Interceptor"/> class with an interceptor instance.
        /// </summary>
        /// <param name="interceptor">The <see cref="IInterceptor"/> to use.</param>
        /// <exception cref="ArgumentNullException">when <paramref name="interceptor"/> is
        /// <see langword="null"/>.</exception>
        public Interceptor(IInterceptor interceptor)
        {
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
            _type = interceptor.GetType();
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="Interceptor"/> class with a given
        /// name and type that will be resolved to provide interception.
        /// </summary>
        /// <param name="interceptorType">Type of the interceptor</param>
        /// <param name="name">name to use to resolve.</param>
        public Interceptor(Type interceptorType, string name)
        {
            Guard.TypeIsAssignable(typeof(IInterceptor), interceptorType ?? 
                  throw new ArgumentNullException(nameof(interceptorType)), 
                                                  nameof(interceptorType));

            _type = interceptorType;
            _name = name;
        }

        /// <summary>
        /// Initialize a new instance of the <see cref="Interceptor"/> class with
        /// a given type that will be resolved to provide interception.
        /// </summary>
        /// <param name="interceptorType">Type of the interceptor.</param>
        public Interceptor(Type interceptorType)
            : this(interceptorType, null)
        {
        }

        /// <summary>
        /// Interceptor to use.
        /// </summary>
        /// <param name="context">Context for current build operation.</param>
        public T GetInterceptor<TContext, T>(ref TContext context)
            where TContext : IBuilderContext
        {
            if (null != _interceptor) return (T)_interceptor;

            // TODO: Must be policy
            return (T)context.Resolve(_type, _name);
        }


        public override bool BuildRequired => _type == typeof(VirtualMethodInterceptor);

        private bool IsInstanceInterceptor
        {
            get
            {
                if (_interceptor != null)
                {
                    return _interceptor is IInstanceInterceptor;
                }
                return typeof(IInstanceInterceptor).IsAssignableFrom(_type);
            }
        }

        public override MatchRank Match(Type other)
        {
            if (_type.Equals(other)) return MatchRank.ExactMatch;

            if (other.IsAssignableFrom(_type)) return MatchRank.Compatible;

            return MatchRank.NoMatch;
        }
    }

    /// <summary>
    /// Generic version of <see cref="Interceptor"/> that lets you specify an interceptor
    /// type using generic syntax.
    /// </summary>
    /// <typeparam name="TInterceptor">Type of interceptor</typeparam>
    public class Interceptor<TInterceptor> : Interceptor
        where TInterceptor : IInterceptor
    {
        /// <summary>
        /// Initialize an instance of <see cref="Interceptor{TInterceptor}"/> that will
        /// resolve the given interceptor type.
        /// </summary>
        public Interceptor()
            : base(typeof(TInterceptor))
        {
        }

        /// <summary>
        /// Initialize an instance of <see cref="Interceptor{TInterceptor}"/> that will
        /// resolve the given interceptor type and name.
        /// </summary>
        /// <param name="name">Name that will be used to resolve the interceptor.</param>
        public Interceptor(string name)
            : base(typeof(TInterceptor), name)
        {
        }
    }
}