﻿using Microsoft.Practices.Unity.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Unity;
using Unity.Injection;
using Unity.Interception;
using Unity.Interception.Interceptors.TypeInterceptors.VirtualMethodInterception;
using Unity.Interception.PolicyInjection.Pipeline;
using Unity.Lifetime;

namespace Configuration
{
    [TestClass]
    public class ConvenienceConfigurationFixture
    {
        #region Fields

        private const string Name       = "name";
        private const string PolicyName = "Policy1";
        private IUnityContainer Container;

        #endregion


        #region Scaffolding

        [TestInitialize]
        public void TestInitialize() => Container = new UnityContainer()
            .AddNewExtension<Interception>();

        #endregion


        [TestMethod("Empty Rule")]
        public void CanSetUpAnEmptyRule()
        {
            // empty
            Container.Configure<Interception>()
                     .AddPolicy(PolicyName);

            var policies = Container.Resolve<InjectionPolicy[]>();

            Assert.AreEqual(2, policies.Length);
            Assert.IsInstanceOfType(policies[0], typeof(AttributeDrivenPolicy));
            Assert.IsInstanceOfType(policies[1], typeof(RuleDrivenPolicy));
            Assert.AreEqual(PolicyName, policies[1].Name);
        }


        [TestMethod("Multiple Empty Rules")]
        public void CanSetUpSeveralEmptyRules()
        {
            // empty
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName).Interception
                    .AddPolicy("policy2");

            List<InjectionPolicy> policies
                = new List<InjectionPolicy>(Container.ResolveAll<InjectionPolicy>());

            Assert.AreEqual(3, policies.Count);
            Assert.IsInstanceOfType(policies[0], typeof(AttributeDrivenPolicy));
            Assert.IsInstanceOfType(policies[1], typeof(RuleDrivenPolicy));
            Assert.AreEqual(PolicyName, policies[1].Name);
            Assert.IsInstanceOfType(policies[2], typeof(RuleDrivenPolicy));
            Assert.AreEqual("policy2", policies[2].Name);
        }


        [TestMethod("Policy With Rules And Handlers")]
        public void CanSetUpAPolicyWithGivenRulesAndHandlers()
        {
            IMatchingRule rule1 = new AlwaysMatchingRule();
            ICallHandler handler1 = new InvokeCountHandler();

            Container.Configure<Interception>()
                     .AddPolicy(PolicyName)
                        .AddMatchingRule(rule1)
                        .AddCallHandler(handler1);

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, ((InvokeCountHandler)handler1).Count);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithGivenRulesAndHandlersTypes()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule(typeof(AlwaysMatchingRule))
                        .AddCallHandler(typeof(GlobalCountCallHandler));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["default"]);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithGivenRulesAndHandlersTypesWithGenerics()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule<AlwaysMatchingRule>()
                        .AddCallHandler<GlobalCountCallHandler>();

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["default"]);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithInjectedRulesAndHandlers()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule<AlwaysMatchingRule>()
                        .AddCallHandler<GlobalCountCallHandler>(
                            new InjectionConstructor("handler1"))
                        .AddCallHandler<GlobalCountCallHandler>(
                            new InjectionConstructor("handler2"),
                            new InjectionProperty("Order", 10));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler1"]);
            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler2"]);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithNonGenericInjectedRulesAndHandlers()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule(typeof(AlwaysMatchingRule))
                        .AddCallHandler(
                            typeof(GlobalCountCallHandler),
                            new InjectionConstructor("handler1"))
                        .AddCallHandler(
                            typeof(GlobalCountCallHandler),
                            new InjectionConstructor("handler2"),
                            new InjectionProperty("Order", 10));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler1"]);
            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler2"]);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithExternallyConfiguredRulesAndHandlers()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule("rule1")
                        .AddCallHandler("handler1")
                        .AddCallHandler("handler2").Interception.Container
                .RegisterType<IMatchingRule, AlwaysMatchingRule>("rule1")
                .RegisterType<ICallHandler, GlobalCountCallHandler>(
                    "handler1",
                    new InjectionConstructor("handler1"))
                .RegisterType<ICallHandler, GlobalCountCallHandler>(
                    "handler2",
                    new InjectionConstructor("handler2"),
                    new InjectionProperty("Order", 10));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler1"]);
            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler2"]);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithNamedInjectedRulesAndHandlers()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule<AlwaysMatchingRule>("rule1")
                        .AddCallHandler<GlobalCountCallHandler>(
                            "handler1",
                            new InjectionConstructor("handler1"))
                        .AddCallHandler<GlobalCountCallHandler>(
                            "handler2",
                            new InjectionConstructor("handler2"),
                            new InjectionProperty("Order", 10));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            GlobalCountCallHandler handler1 = (GlobalCountCallHandler)Container.Resolve<ICallHandler>("handler1");
            GlobalCountCallHandler handler2 = (GlobalCountCallHandler)Container.Resolve<ICallHandler>("handler2");

            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler1"]);
            Assert.AreEqual(1, GlobalCountCallHandler.Calls["handler2"]);
            Assert.AreEqual(0, handler1.Order);
            Assert.AreEqual(10, handler2.Order);
        }

        [TestMethod]
        public void CanSetUpAPolicyWithLifetimeManagedNamedInjectedRulesAndHandlers()
        {
            Container
                .Configure<Interception>()
                    .AddPolicy(PolicyName)
                        .AddMatchingRule<AlwaysMatchingRule>(
                            "rule1",
                            new ContainerControlledLifetimeManager())
                        .AddCallHandler<InvokeCountHandler>(
                            "handler1",
                            (LifetimeManager)null)
                        .AddCallHandler<InvokeCountHandler>(
                            "handler2",
                            new ContainerControlledLifetimeManager(),
                            new InjectionProperty("Order", 10));

            GlobalCountCallHandler.Calls.Clear();

            Container
                .Configure<Interception>()
                    .SetInterceptorFor<Wrappable>(Name, new VirtualMethodInterceptor());

            Wrappable wrappable1 = Container.Resolve<Wrappable>(Name);
            wrappable1.Method2();

            InvokeCountHandler handler1 = (InvokeCountHandler)Container.Resolve<ICallHandler>("handler1");
            InvokeCountHandler handler2 = (InvokeCountHandler)Container.Resolve<ICallHandler>("handler2");

            Assert.AreEqual(0, handler1.Count);     // not lifetime maanaged
            Assert.AreEqual(1, handler2.Count);     // lifetime managed
        }

        [TestMethod]
        public void SettingUpAPolicyWithANullRuleElementThrows()
        {
            try
            {
                Container
                    .Configure<Interception>()
                        .AddPolicy(PolicyName)
                            .AddMatchingRule(typeof(AlwaysMatchingRule))
                            .AddMatchingRule((string)null)
                            .AddCallHandler(new InvokeCountHandler());
                Assert.Fail("Should have thrown");
            }
            catch (ArgumentException)
            {
            }
        }
    }
}
