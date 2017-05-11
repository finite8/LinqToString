using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace System.Linq.Dynamic
{
    internal class LinqParser<T>
    {

        public class ParseResult
        {
            public ParseResult(Type t, string text)
            {
                this.ReturnType = t;
                this.TextualResult = text;
            }
            public string TextualResult { get; set; }
            public Type ReturnType { get; set; }
            public object ConstantValue { get; set; }
        }
        public Expression<Func<T, bool>> origExpr { get; set; }
        public string ResolveExpression()
        {
            Expression bodyExpr = (Expression)origExpr.Body;
            return ParsePart(bodyExpr, true).TextualResult;
        }


        private ParseResult ParsePart(Expression expr, bool isRoot)
        {
            if (expr is BinaryExpression)
            {
                expr = ConvertVBStringCompare((BinaryExpression)expr);
            }
            if (expr.NodeType == ExpressionType.MemberAccess || expr.NodeType == ExpressionType.Constant)
            {
                return ParseValueExpression(expr);
            }
            else if (expr.NodeType == ExpressionType.Call)
            {
                return ParseMethodCall((MethodCallExpression)expr);
            }
            else if (expr is BinaryExpression)
            {
                BinaryExpression bExpr = (BinaryExpression)expr;
                //if (bExpr.Method == null)
                //{
                string op = DynamicLinqParser.GetOperatorForNodeType(bExpr.NodeType);
                if (op != null)
                {
                    string formatStr;
                    if (isRoot)
                    {
                        formatStr = "{0} {1} {2}";
                    }
                    else
                    {
                        formatStr = "({0} {1} {2})";
                    }
                    // this is not an evaluateable method. We now need to explore the "left" and "right" parts.
                    var leftPart = ParsePart(bExpr.Left, false);
                    var rightPart = ParsePart(bExpr.Right, false);
                    var leftBase = (Nullable.GetUnderlyingType(leftPart.ReturnType) ?? leftPart.ReturnType);
                    var rightBase = (Nullable.GetUnderlyingType(rightPart.ReturnType) ?? rightPart.ReturnType);
                    if (leftBase != rightBase)
                    {
                        // our types don't match. We may have an enum here. lets check to see if one side is an enum.

                        if (leftBase.GetTypeInfo().IsEnum && !rightBase.GetTypeInfo().IsEnum)
                        {
                            // we now need to try and see if we can get a string named version for our enum.
                            rightPart = new ParseResult(leftBase, string.Format("\"{0}\"", Enum.GetName(leftBase, rightPart.ConstantValue)));
                        }
                        else
                        {

                        }

                    }
                    return new ParseResult(typeof(bool), string.Format(formatStr, leftPart.TextualResult, op, rightPart.TextualResult));
                }
                else
                {
                    return new ParseResult(typeof(bool), expr.ToString());
                }
                //}
            }
            else if (expr is UnaryExpression)
            {

                var result = ParsePart(((UnaryExpression)expr).Operand, false);
                if (expr.NodeType == ExpressionType.Not)
                {
                    return new ParseResult(result.ReturnType, " not " + result.TextualResult);
                }
                else
                {
                    return result;
                }
            }

            throw new NotImplementedException();

        }

        private static ParseResult GetValueExpression(Type leafType, object value)
        {
            if (value is string || (Nullable.GetUnderlyingType(leafType) ?? leafType).GetTypeInfo().IsEnum)
            {
                return new ParseResult(leafType, string.Format("\"{0}\"", value));
            }
            else if (value is DateTime)
            {
                DateTime dt = (DateTime)value;
                return new ParseResult(leafType, $"DateTime({dt.Year},{dt.Month},{dt.Day})");
            }
            else if (value is IEnumerable)
            {
                IEnumerable arr = (IEnumerable)value;
                List<ParseResult> arrayContents = new List<ParseResult>();
                foreach (var i in arr)
                {
                    Type t = i.GetType();
                    arrayContents.Add(GetValueExpression(t, i));
                }
                string arrayExpr = string.Join(",", arrayContents.Select(r => r.TextualResult).ToArray());
                return new ParseResult(leafType, $"({arrayExpr})");
            }
            else if (value is null)
            {
                return new ParseResult(leafType, "NULL");
            }
            else
            {
                return new ParseResult(leafType, string.Format("{0}", value));
            }
        }

        private ParseResult ParseValueExpression(Expression expr)
        {
            if (expr == null)
            {
                return null;
            }
            if (expr.NodeType == ExpressionType.MemberAccess)
            {

                MemberExpression mExpr = (MemberExpression)expr;

                List<string> parts = new List<string>();
                Type leafType = null;
                // we will explore through this untill we hit our originating property item
                while (mExpr != null)
                {
                    leafType = mExpr.Type;
                    parts.Insert(0, mExpr.Member.Name);
                    if (mExpr.Expression.NodeType == ExpressionType.Parameter)
                    {
                        // we are at the end. Break.
                        break;
                    }
                    else
                    {
                        if (mExpr.Expression.NodeType == ExpressionType.Constant)
                        {
                            Type delegateType = typeof(Func<>).MakeGenericType(expr.Type);
                            object value = Expression.Lambda(delegateType, expr).Compile().DynamicInvoke();
                            return GetValueExpression(leafType, value);
                            //if (value is string || (Nullable.GetUnderlyingType(leafType) ?? leafType).IsEnum)
                            //{
                            //    return new ParseResult(expr.Type, string.Format("\"{0}\"", value));
                            //}
                            //else if (value is DateTime)
                            //{
                            //    DateTime dt = (DateTime)value;
                            //    return new ParseResult(expr.Type, $"DateTime({dt.Year},{dt.Month},{dt.Day})");
                            //}
                            //else if (value is null)
                            //{
                            //    return new ParseResult(expr.Type, "NULL");
                            //}
                            //else
                            //{
                            //    return new ParseResult(expr.Type, string.Format("{0}", value));
                            //}
                        }
                        else
                        {
                            mExpr = (MemberExpression)mExpr.Expression;
                        }
                    }
                }
                return new ParseResult(leafType, string.Join(".", parts));
            }
            else if (expr.NodeType == ExpressionType.Constant)
            {
                ConstantExpression cExpr = (ConstantExpression)expr;

                if (cExpr.Type == typeof(string))
                {
                    return new ParseResult(typeof(string), string.Format("\"{0}\"", cExpr.Value));
                }
                else
                {
                    Type baseType = Nullable.GetUnderlyingType(cExpr.Type) ?? cExpr.Type;
                    object valueToUse = cExpr.Value;
                    if (valueToUse != null)
                    {
                        if (baseType.GetTypeInfo().IsEnum)
                        {
                            // we are dealing with an enum here. Lets make sure we get the string representation of the enum
                            valueToUse = Enum.GetName(baseType, valueToUse);
                        }

                    }

                    return new ParseResult(cExpr.Type, string.Format("{0}", valueToUse ?? "NULL"))
                    {
                        ConstantValue = valueToUse
                    };
                }
            }
            else if (expr.NodeType == ExpressionType.Convert)
            {
                UnaryExpression uExpr = (UnaryExpression)expr;
                if (uExpr.Operand is MemberExpression)
                {
                    return ParseValueExpression(uExpr.Operand);
                }
            }
            else if (expr.NodeType == ExpressionType.Lambda)
            {
                LambdaExpression lExpr = (LambdaExpression)expr;
                return new ParseResult(lExpr.ReturnType, DynamicLinqParser.ParseExpression(lExpr.Parameters[0].Type, lExpr));
            }
            throw new InvalidOperationException();

        }

        private ParseResult ParseMethodCall(System.Linq.Expressions.MethodCallExpression expr)
        {
            ParseResult objPart = ParseValueExpression(expr.Object);
            string textualPart = objPart == null ? null : objPart.TextualResult;
            switch (expr.Method.Name)
            {
                case "Any":
                    return new ParseResult(expr.Type, string.Format("{0}{1}.{2}({3})"
                        , string.IsNullOrWhiteSpace(textualPart) ? string.Empty : textualPart + "."
                        , ParseValueExpression(expr.Arguments[0]).TextualResult
                        , expr.Method.Name
                        , ParseValueExpression(expr.Arguments[1]).TextualResult
                        ));
                case "Contains":
                    // we have to express this as [Constant] in [Array].
                    // our constant is our first element. our array is the second.
                    if (expr.Method.GetParameters().Count() == 2)
                    {
                        var ArrayResult = ParseValueExpression(expr.Arguments[0]);
                        var ConstantResult = ParseValueExpression(expr.Arguments[1]);
                        return new ParseResult(expr.Type, $"{ConstantResult.TextualResult} IN {ArrayResult.TextualResult}");
                    }
                    break;

            }
            return new ParseResult(expr.Type, string.Format("{0}{1}({2})"
                , string.IsNullOrWhiteSpace(textualPart) ? string.Empty : textualPart + "."
                , expr.Method.Name
                , string.Join(",", expr.Arguments.Select(a => ParseValueExpression(a).TextualResult))));

            //switch (expr.Method.Name)
            //{
            //    case "Contains":
            //    case "StartsWith":
            //    case "EndsWith":
            //        return string.Format("{0}.{2}({1})"
            //            , ParseValueExpression(expr.Object)
            //            , ParseValueExpression(expr.Arguments[0])
            //            , expr.Method.Name);


            //    default:
            //        throw new NotImplementedException();
            //}
        }

        static internal Expression ConvertVBStringCompare(BinaryExpression exp)
        {
            if (exp.Left.NodeType == ExpressionType.Call)
            {
                dynamic compareStringCall = (MethodCallExpression)exp.Left;
                if (compareStringCall.Method.DeclaringType.FullName == "Microsoft.VisualBasic.CompilerServices.Operators" && compareStringCall.Method.Name == "CompareString")
                {
                    dynamic arg1 = compareStringCall.Arguments[0];
                    dynamic arg2 = compareStringCall.Arguments[1];

                    switch (exp.NodeType)
                    {
                        case ExpressionType.LessThan:
                            return Expression.LessThan(arg1, arg2);
                        case ExpressionType.LessThanOrEqual:
                            return Expression.GreaterThan(arg1, arg2);
                        case ExpressionType.GreaterThan:
                            return Expression.GreaterThan(arg1, arg2);
                        case ExpressionType.GreaterThanOrEqual:
                            return Expression.GreaterThanOrEqual(arg1, arg2);
                        default:
                            return Expression.Equal(arg1, arg2);
                    }
                }
            }
            return exp;
        }
    }
}
