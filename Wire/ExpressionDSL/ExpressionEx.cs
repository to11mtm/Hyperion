﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Wire.ExpressionDSL
{
    public class Compiler<TDel>
    {
        private readonly List<Expression> _expressions = new List<Expression>();
        private readonly List<Expression> _content = new List<Expression>();
        private readonly List<ParameterExpression> _variables = new List<ParameterExpression>();
        private readonly List<ParameterExpression> _parameters = new List<ParameterExpression>();

        public int NewObject(Type type)
        {
            var exp = ExpressionEx.GetNewExpression(type);
            _expressions.Add(exp);
            return _expressions.Count - 1;
        }

        public int Parameter<T>(string name)
        {
            var exp = Expression.Parameter(typeof(T),name);
            _parameters.Add(exp);
            _expressions.Add(exp);
            return _expressions.Count - 1;
        }

        public int Variable<T>(string name)
        {
            var exp = Expression.Variable(typeof(T), name);
            _variables.Add(exp);
            _expressions.Add(exp);
            return _expressions.Count - 1;
        }

        public int Constant(object value)
        {
            var constant = value.ToConstant();
            _expressions.Add(constant);
            return _expressions.Count - 1;
        }

        public int CastOrUnbox(int value, Type type)
        {
            Expression tempQualifier = _expressions[value];
            var cast = type.GetTypeInfo().IsValueType
                // ReSharper disable once AssignNullToNotNullAttribute
                ? Expression.Unbox(tempQualifier, type)
                // ReSharper disable once AssignNullToNotNullAttribute
                : Expression.Convert(tempQualifier, type);
            var exp = (Expression) cast;
            _expressions.Add(exp);
            return _expressions.Count - 1;
        }
        
        public void EmitCall(MethodInfo method, int target, params int[] arguments)
        {
            var targetExp = _expressions[target];
            var argumentsExp = arguments.Select(n => _expressions[n]).ToArray();
            var call = Expression.Call(targetExp, method, argumentsExp);
            _content.Add(call);
        }

        public void EmitStaticCall(MethodInfo method, params int[] arguments)
        {
            var argumentsExp = arguments.Select(n => _expressions[n]).ToArray();
            var call = Expression.Call(null, method, argumentsExp);
            _content.Add(call);
        }

        public int Call(MethodInfo method, int target, params int[] arguments)
        {
            var targetExp = _expressions[target];
            var argumentsExp = arguments.Select(n => _expressions[n]).ToArray();
            var call = Expression.Call(targetExp, method, argumentsExp);
            _expressions.Add(call);
            return _expressions.Count - 1;
        }

        public int StaticCall(MethodInfo method, params int[] arguments)
        {
            var argumentsExp = arguments.Select(n => _expressions[n]).ToArray();
            var call = Expression.Call(null, method, argumentsExp);
            _expressions.Add(call);
            return _expressions.Count - 1;
        }

        public int ReadField(FieldInfo field, int target)
        {
            var targetExp = _expressions[target];
            var accessExp = Expression.Field(targetExp, field);
            _expressions.Add(accessExp);
            return _expressions.Count - 1;
        }

        public int WriteField(FieldInfo field, int target,int value)
        {
            if (field.IsInitOnly)
            {
                //TODO: field is readonly, can we set it via IL or only via reflection
                var method = typeof(FieldInfo).GetTypeInfo().GetMethod(nameof(FieldInfo.SetValue), new[] { typeof(object), typeof(object) });
                var fld = Constant(field);
                var valueToObject = ConvertTo<object>(value);
                return Call(method, fld, target, valueToObject);
            }
            var targetExp = _expressions[target];
            var valueExp = _expressions[value];
            var accessExp = Expression.Field(targetExp, field);
            var writeExp = Expression.Assign(accessExp, valueExp);
            _expressions.Add(writeExp);
            return _expressions.Count - 1;
        }

        public Expression ToBlock()
        {
            if (!_content.Any())
            {
                _content.Add(Expression.Empty());
            }

            return Expression.Block(_variables.ToArray(),_content);
        }

        public TDel Compile()
        {
            var body = ToBlock();
            var parameters = _parameters.ToArray();
            var res = Expression.Lambda<TDel>(body, parameters).Compile();
            return res;
        }
        public int ConvertTo<T>(int value)
        {
            var valueExp = _expressions[value];
            var con = (Expression) Expression.Convert(valueExp, typeof(T));
            _expressions.Add(con);
            return _expressions.Count - 1;
        }

        public int WriteVar(int variable, int value)
        {
            var varExp = _expressions[variable];
            var valueExp = _expressions[value];
            var assign = Expression.Assign(varExp, valueExp);
            _expressions.Add(assign);
            return _expressions.Count - 1;
        }

        public void Emit(int value)
        {
            var exp = _expressions[value];
            _content.Add(exp);
        }

        public int Convert(int value, Type type)
        {
            var valueExp = _expressions[value];
            var conv = (Expression) Expression.Convert(valueExp, type);
            _expressions.Add(conv);
            return _expressions.Count - 1;
        }
    }
    public static class ExpressionEx
    {
        public static ConstantExpression ToConstant(this object self)
        {
            return Expression.Constant(self);
        }


        public static Expression GetNewExpression(Type type)
        {
#if SERIALIZATION
            var defaultCtor = type.GetTypeInfo().GetConstructor(new Type[] {});
            var il = defaultCtor?.GetMethodBody()?.GetILAsByteArray();
            var sideEffectFreeCtor = il != null && il.Length <= 8; //this is the size of an empty ctor
            if (sideEffectFreeCtor)
            {
                //the ctor exists and the size is empty. lets use the New operator
                return Expression.New(defaultCtor);
            }
#endif
            var emptyObjectMethod = typeof(TypeEx).GetTypeInfo().GetMethod(nameof(TypeEx.GetEmptyObject));
            var emptyObject = Expression.Call(null, emptyObjectMethod, type.ToConstant());

            return emptyObject;
        }
    }
}
