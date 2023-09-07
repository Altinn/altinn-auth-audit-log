using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Altinn.Auth.AuditLog.Functions.Models;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using Action = Altinn.Auth.AuditLog.Functions.Models.Action;
using Resources = Altinn.Auth.AuditLog.Functions.Models.Resource;
using Attribute = Altinn.Auth.AuditLog.Functions.Models.Attribute;

namespace Altinn.Auth.AuditLog.Functions.Tests.Utils
{
    /// <summary>
    /// Class with methods that can help with assertions of larger objects.
    /// </summary>
    public static class AssertionUtil
    {
        /// <summary>
        /// Asserts that two collections of objects have the same property values in the same positions.
        /// </summary>
        /// <typeparam name="T">The Type</typeparam>
        /// <param name="expected">A collection of expected instances</param>
        /// <param name="actual">The collection of actual instances</param>
        /// <param name="assertMethod">The assertion method to be used</param>
        public static void AssertCollections<T>(ICollection<T> expected, ICollection<T> actual, Action<T, T> assertMethod)
        {
            if (expected == null)
            {
                Assert.Null(actual);
                return;
            }

            Assert.Equal(expected.Count, actual.Count);

            Dictionary<int, T> expectedDict = new Dictionary<int, T>();
            Dictionary<int, T> actualDict = new Dictionary<int, T>();

            int i = 1;
            foreach (T ex in expected)
            {
                expectedDict.Add(i, ex);
                i++;
            }

            i = 1;
            foreach (T ac in actual)
            {
                actualDict.Add(i, ac);
                i++;
            }

            foreach (int key in expectedDict.Keys)
            {
                assertMethod(expectedDict[key], actualDict[key]);
            }
        }

        /// <summary>
        /// Assert that two <see cref="Action"/> have the same property in the same positions.
        /// </summary>
        /// <param name="expected">An instance with the expected values.</param>
        /// <param name="actual">The instance to verify.</param>
        public static void AssertRuleEqual(Action expected, Action actual)
        {
            Assert.NotNull(actual);
            Assert.NotNull(expected);

            AssertionUtil.AssertCollections(expected.Attribute, actual.Attribute, AssertRuleEqual);
        }

        /// <summary>
        /// Assert that two <see cref="AccessSubject"/> have the same property in the same positions.
        /// </summary>
        /// <param name="expected">An instance with the expected values.</param>
        /// <param name="actual">The instance to verify.</param>
        public static void AssertRuleEqual(AccessSubject expected, AccessSubject actual)
        {
            Assert.NotNull(actual);
            Assert.NotNull(expected);

            AssertionUtil.AssertCollections(expected.Attribute, actual.Attribute, AssertRuleEqual);
        }

        /// <summary>
        /// Assert that two <see cref="Resources"/> have the same property in the same positions.
        /// </summary>
        /// <param name="expected">An instance with the expected values.</param>
        /// <param name="actual">The instance to verify.</param>
        public static void AssertRuleEqual(Resources expected, Resources actual)
        {
            Assert.NotNull(actual);
            Assert.NotNull(expected);

            AssertionUtil.AssertCollections(expected.Attribute, actual.Attribute, AssertRuleEqual);
        }

        /// <summary>
        /// Assert that two <see cref="Attribute"/> have the same property in the same positions.
        /// </summary>
        /// <param name="expected">An instance with the expected values.</param>
        /// <param name="actual">The instance to verify.</param>
        public static void AssertRuleEqual(Attribute expected, Attribute actual)
        {
            Assert.NotNull(actual);
            Assert.NotNull(expected);

            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.IncludeInResult, actual.IncludeInResult);
            Assert.Equal(expected.DataType, actual.DataType);
        }
    }
}
