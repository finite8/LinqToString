using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Linq.Dynamic.Core;

namespace System.Linq.Dynamic.Test
{
    [TestClass]
    public class LinqToStringTests
    {
        public class TestClass
        {
            public TestClass()
            {
                inn = new Inner()
                {
                    val = string.Empty
                };
            }
            public string test { get; set; }
            public int another { get; set; }

            public Inner inn { get; set; }
            public List<Inner> innerList { get; set; }
            public List<DifferentInner> innerListTwo { get; set; }
        }

        public class DifferentInner
        {
            public string val { get; set; }
        }

        public class Inner
        {
            public string val { get; set; }
            public List<AnotherInner> AnInner { get; set; }
        }

        public class AnotherInner
        {
            public string val2 { get; set; }
        }
        [TestMethod]
        public void Contains()
        {
            int[] myArray = new int[] { 1, 2, 3, 4, 5 };
            Expression<Func<TestClass, bool>> expr = (c) => myArray.Contains(c.another);
            var result = Transform(expr);
            TestClass t = new TestClass();
            t.another = 0;
            Assert.IsFalse(result.Compile()(t));
            t.another = 1;
            Assert.IsTrue(result.Compile()(t));
        }

        [TestMethod]
        public void Parse_LinqToString()
        {
            //int[] testArray = new int[] { 1, 2, 3, 4, 5 };
            string testString = "blah";
            Expression<Func<TestClass, bool>> expr = (c) => (c.test == "test" && c.another == 6) || (c.inn.val == testString) || (c.inn.val.Contains("blergh"));
            //|| testArray.Contains(c.another);
            var resultExpr = Transform(expr);
            var testObj = new TestClass();
            Assert.IsFalse(resultExpr.Compile().Invoke(testObj));
            testObj.inn.val = "jkljdlkasjklblerghjkljl";
            Assert.IsTrue(resultExpr.Compile().Invoke(testObj));
            testObj = new TestClass();
            testObj.test = "test";
            Assert.IsFalse(resultExpr.Compile().Invoke(testObj));
            testObj.another = 6;
            Assert.IsTrue(resultExpr.Compile().Invoke(testObj));
            testObj = new TestClass();
            testObj.inn.val = testString;
            Assert.IsTrue(resultExpr.Compile().Invoke(testObj));

            Expression<Func<TestClass, bool>> expr2 = (c) => c.innerList.Any(i => i.AnInner.Any(iTwo => iTwo.val2 == "test")) && c.innerListTwo.Any(ithree => ithree.val == "test2");
            //Expression<Func<TestClass, bool>> expr2 = (c) => c.innerList.Where(i => i.AnInner.Where(iTwo => iTwo.val2 == "test").Any()).Any() && c.innerListTwo.Where(ithree => ithree.val == "test2").Any();

            var res = Transform(expr2);
        }

        Expression<Func<T, bool>> Transform<T>(Expression<Func<T, bool>> expr)
        {
            string exprString = LinqToString.ToDynamicLinqString(expr);
            return (Expression < Func<T, bool> >) DynamicExpressionParser.ParseLambda(false, typeof(T), typeof(bool), exprString, null);


            
        }
    }
}
