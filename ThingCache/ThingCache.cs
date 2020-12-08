﻿using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();

        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
            {
                return thing;
            }

            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }

            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private const string thingId1 = "TheDress";

        private const string thingId2 = "CoolBoots";
        private readonly Thing thing1 = new Thing(thingId1);
        private readonly Thing thing2 = new Thing(thingId2);
        private ThingCache thingCache;
        private IThingService thingService;

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            thingCache = new ThingCache(thingService);
            Thing _;
            A.CallTo(() => thingService.TryRead(thingId1, out _)).Returns(true)
                .AssignsOutAndRefParameters(thing1);
            A.CallTo(() => thingService.TryRead(thingId2, out _)).Returns(true)
                .AssignsOutAndRefParameters(thing2);
        }

        [Test]
        public void Get_CallsService_WhenCalledOneTime()
        {
            var th1 = thingCache.Get(thingId1);

            A.CallTo(() => thingService.TryRead(thingId1, out th1)).MustHaveHappened(1, Times.Exactly);
            th1.Should().Be(thing1);
        }

        [Test]
        public void Get_CallsServiceOnce_WhenCacheCalledTwice()
        {
            var th1 = thingCache.Get(thingId1);
            th1 = thingCache.Get(thingId1);

            A.CallTo(() => thingService.TryRead(thingId1, out th1)).MustHaveHappenedOnceExactly();
            th1.Should().Be(thing1);
        }

        /** Проверки в тестах
         * Assert.AreEqual(expectedValue, actualValue);
         * actualValue.Should().Be(expectedValue);
         */

        /** Синтаксис AAA
         * Arrange:
         * var fake = A.Fake<ISomeService>();
         * A.CallTo(() => fake.SomeMethod(...)).Returns(true);
         * Assert:
         * var value = "42";
         * A.CallTo(() => fake.TryRead(id, out value)).MustHaveHappened();
         */

        /** Синтаксис out
         * var value = "42";
         * string _;
         * A.CallTo(() => fake.TryRead(id, out _)).Returns(true)
         *     .AssignsOutAndRefParameters(value);
         * A.CallTo(() => fake.TryRead(id, out value)).Returns(true);
         */

        /** Синтаксис Repeat
         * var value = "42";
         * A.CallTo(() => fake.TryRead(id, out value))
         *     .MustHaveHappened(Repeated.Exactly.Twice)
         */
    }
}