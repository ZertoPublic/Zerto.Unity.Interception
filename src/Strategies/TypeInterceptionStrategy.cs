﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Builder;
using Unity.Injection;
using Unity.Interception.ContainerIntegration.Selection;
using Unity.Interception.Extensions;
using Unity.Interception.InterceptionBehaviors;
using Unity.Interception.Interceptors;
using Unity.Interception.Interceptors.TypeInterceptors;
using Unity.Policy;
using Unity.Strategies;

namespace Unity.Interception.ContainerIntegration.ObjectBuilder
{
    /// <summary>
    /// A <see cref="BuilderStrategy"/> that hooks up type interception. It looks for
    /// a <see cref="ITypeInterceptionPolicy"/> for the current build key, or the current
    /// build type. If present, it substitutes types so that that proxy class gets
    /// built up instead. On the way back, it hooks up the appropriate handlers.
    /// </summary>
    public class TypeInterceptionStrategy : BuilderStrategy
    {
        #region BuilderStrategy

        /// <summary>
        /// Called during the chain of responsibility for a build operation. The
        /// PreBuildUp method is called when the chain is being executed in the
        /// forward direction.
        /// </summary>
        /// <remarks>In this class, PreBuildUp is responsible for figuring out if the
        /// class is proxyable, and if so, replacing it with a proxy class.</remarks>
        /// <param name="context">Context of the build operation.</param>
        public override void PreBuildUp(ref BuilderContext context)
        {
            if (context.Existing != null)
            {
                return;
            }

            Type typeToBuild = context.Type;


            ITypeInterceptor interceptor =
                typeof(ITypeInterceptor).FindInjectedMember<Interceptor>(ref context)?
                                            .GetInterceptor<ITypeInterceptor>(ref context);
            if (null == interceptor)
            { 
                var interceptionPolicy = GetPolicyOrDefault<ITypeInterceptionPolicy>(ref context);
                if (interceptionPolicy == null)
                {
                    return;
                }

                interceptor = interceptionPolicy.GetInterceptor(ref context);
            }

            if (!interceptor.CanIntercept(typeToBuild))
            {
                return;
            }

            var behavior = typeof(IInterceptionBehavior).FindInjectedMember<InterceptionBehavior>(ref context);
                                                        

            var interceptionBehaviorsPolicy = GetPolicyOrDefault<IInterceptionBehaviorsPolicy>(ref context);

            IEnumerable<IInterceptionBehavior> interceptionBehaviors =
                interceptionBehaviorsPolicy == null
                    ?
                        Enumerable.Empty<IInterceptionBehavior>()
                    :
                        interceptionBehaviorsPolicy.GetEffectiveBehaviors(
                            ref context, interceptor, typeToBuild, typeToBuild)
                        .Where(ib => ib.WillExecute);

            IAdditionalInterfacesPolicy additionalInterfacesPolicy =
                GetPolicyOrDefault<IAdditionalInterfacesPolicy>(ref context);

            IEnumerable<Type> additionalInterfaces =
                additionalInterfacesPolicy != null ? additionalInterfacesPolicy.AdditionalInterfaces : Type.EmptyTypes;

            var enumerable = interceptionBehaviors as IInterceptionBehavior[] ?? interceptionBehaviors.ToArray();
            context.Registration.Set(typeof(EffectiveInterceptionBehaviorsPolicy), 
                new EffectiveInterceptionBehaviorsPolicy { Behaviors = enumerable });

            Type[] allAdditionalInterfaces =
                Intercept.GetAllAdditionalInterfaces(enumerable, additionalInterfaces);

            Type interceptingType =
                interceptor.CreateProxyType(typeToBuild, allAdditionalInterfaces);

            DerivedTypeConstructorSelectorPolicy.SetPolicyForInterceptingType(ref context, interceptingType);
        }

        /// <summary>
        /// Called during the chain of responsibility for a build operation. The
        /// PostBuildUp method is called when the chain has finished the PreBuildUp
        /// phase and executes in reverse order from the PreBuildUp calls.
        /// </summary>
        /// <remarks>In this class, PostBuildUp checks to see if the object was proxyable,
        /// and if it was, wires up the handlers.</remarks>
        /// <param name="context">Context of the build operation.</param>
        public override void PostBuildUp(ref BuilderContext context)
        {
            IInterceptingProxy proxy = context.Existing as IInterceptingProxy;

            if (proxy == null)
            {
                return;
            }

            var effectiveInterceptionBehaviorsPolicy = (EffectiveInterceptionBehaviorsPolicy)context.Registration.Get(
                typeof(EffectiveInterceptionBehaviorsPolicy));

            if (effectiveInterceptionBehaviorsPolicy == null)
            {
                return;
            }

            foreach (var interceptionBehavior in effectiveInterceptionBehaviorsPolicy.Behaviors)
            {
                proxy.AddInterceptionBehavior(interceptionBehavior);
            }
        }

        public static TPolicyInterface GetPolicyOrDefault<TPolicyInterface>(ref BuilderContext context)
        {
            return (TPolicyInterface)(GetNamedPolicy(ref context, context.RegistrationType, context.Name) ?? 
                                      GetNamedPolicy(ref context, context.RegistrationType, UnityContainer.All));

            object GetNamedPolicy(ref BuilderContext c, Type t, string n)
            {
                return (c.Get(t, n, typeof(TPolicyInterface)) ?? (
#if NETCOREAPP1_0 || NETSTANDARD1_0
                    t.GetTypeInfo().IsGenericType
#else
                            t.IsGenericType
#endif
                                ? c.Get(t.GetGenericTypeDefinition(), n, typeof(TPolicyInterface)) ?? c.Get(null, null, typeof(TPolicyInterface))
                                : c.Get(null, null, typeof(TPolicyInterface))));
            }
        }

        #endregion


        #region Nested Types


        private class EffectiveInterceptionBehaviorsPolicy 
        {
            public EffectiveInterceptionBehaviorsPolicy()
            {
                Behaviors = new List<IInterceptionBehavior>();
            }

            public IEnumerable<IInterceptionBehavior> Behaviors { get; set; }
        }

        private class DerivedTypeConstructorSelectorPolicy : ISelect<ConstructorInfo>
        {
            internal readonly Type InterceptingType;
            internal readonly ISelect<ConstructorInfo> OriginalConstructorSelectorPolicy;

            internal DerivedTypeConstructorSelectorPolicy(
                Type interceptingType,
                ISelect<ConstructorInfo> originalConstructorSelectorPolicy)
            {
                InterceptingType = interceptingType;
                OriginalConstructorSelectorPolicy = originalConstructorSelectorPolicy;
            }


            public IEnumerable<object> Select(Type type, IPolicySet registration)
            {
                object originalConstructor =
                    OriginalConstructorSelectorPolicy.Select(type, registration)
                                                     .First();
                switch (originalConstructor)
                {
                    case ConstructorInfo info:
                        return new []{ FromConstructorInfo(info, InterceptingType) };

                    case SelectedConstructor selectedConstructor:
                        return new []{ FromSelectedConstructor(selectedConstructor, InterceptingType) };

                    case InjectionMethodBase<ConstructorInfo> methodBaseMember:
                        return new []{ FromMethodBaseMember(methodBaseMember, InterceptingType) };
                }

                throw new InvalidOperationException("Unknown type");
            }


            private static SelectedConstructor FromMethodBaseMember(InjectionMethodBase<ConstructorInfo> methodBaseMember, Type interceptingType)
            {
                return new SelectedConstructor(methodBaseMember.MemberInfo(interceptingType), methodBaseMember.Data);
            }

            private static SelectedConstructor FromSelectedConstructor(SelectedConstructor selectedConstructor, Type interceptingType)
            {
                var newConstructorInfo = interceptingType.GetConstructor(selectedConstructor.Info.GetParameters().Select(pi => pi.ParameterType).ToArray()); 
                var newConstructor = new SelectedConstructor(newConstructorInfo, selectedConstructor.Data);

                return newConstructor;
            }

            private static SelectedConstructor FromConstructorInfo(ConstructorInfo info, Type interceptingType)
            {
                var newConstructorInfo = interceptingType.GetConstructor(info.GetParameters().Select(pi => pi.ParameterType).ToArray());
                return new SelectedConstructor(newConstructorInfo);
            }

            public static void SetPolicyForInterceptingType(ref BuilderContext context, Type interceptingType)
            {
                var currentSelectorPolicy =
                    GetPolicy<ISelect<ConstructorInfo>>(ref context);

                if (!(currentSelectorPolicy is DerivedTypeConstructorSelectorPolicy currentDerivedTypeSelectorPolicy))
                {
                    context.Registration.Set(typeof(ISelect<ConstructorInfo>),
                        new DerivedTypeConstructorSelectorPolicy(interceptingType, currentSelectorPolicy));
                }
                else if (currentDerivedTypeSelectorPolicy.InterceptingType != interceptingType)
                {
                    context.Registration.Set(typeof(ISelect<ConstructorInfo>),
                        new DerivedTypeConstructorSelectorPolicy(interceptingType, 
                            currentDerivedTypeSelectorPolicy.OriginalConstructorSelectorPolicy));
                }
            }
        }

        #endregion
    }
}
