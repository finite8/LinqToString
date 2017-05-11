using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace System.Linq.Dynamic
{
    public static class DynamicLinqParser
    {
        public static string ParseExpression<T>(Expression<Func<T, bool>> expr)
        {
            LinqParser<T> parser = new LinqParser<T>()
            {
                origExpr = expr
            };
            return parser.ResolveExpression();
        }

        public static string ParseExpression(Type type, Expression expr)
        {
            var consType = typeof(LinqParser<>).MakeGenericType(new Type[] { type });

            object parser = Activator.CreateInstance(consType);
            consType.GetRuntimeProperty("origExpr").SetValue(parser, expr, null);
            var method = consType.GetRuntimeMethod("ResolveExpression", new Type[0]);
            return (string)method.Invoke(parser, null);
        }

        public static string GetOperatorForNodeType(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.And:
                    return "AND";
                case ExpressionType.AndAlso:
                    return "AND";
                case ExpressionType.Or:
                    return "OR";
                case ExpressionType.OrElse:
                    return "OR";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.NotEqual:
                    return "!=";
                case ExpressionType.Equal:
                    return "=";
                default:
                    return null;
            }
        }




    }

    
}
