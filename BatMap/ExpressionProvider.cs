﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace BatMap {

    public class ExpressionProvider: IExpressionProvider {
        private static readonly Lazy<ExpressionProvider> _lazyInstance = new Lazy<ExpressionProvider>();
        protected static readonly MethodInfo MapMethod;
        protected static readonly MethodInfo MapToListMethod;
        protected static readonly MethodInfo MapToCollectionMethod;
        protected static readonly MethodInfo MapToArrayMethod;
        protected static readonly MethodInfo MapToDictionaryMethod;

        static ExpressionProvider() {
            var type = typeof(MapContext);
            MapMethod = type.GetMethod("Map");
            MapToListMethod = type.GetMethod("MapToList");
            MapToCollectionMethod = type.GetMethod("MapToCollection");
            MapToArrayMethod = type.GetMethod("MapToArray");
            MapToDictionaryMethod = type.GetMethod("MapToDictionary");
        }

        internal static ExpressionProvider Instance => _lazyInstance.Value;

        public virtual MemberBinding CreateMemberBinding(MapMember outMember, MapMember inMember, ParameterExpression inObjPrm, ParameterExpression mapContextPrm) {
            if (inMember.IsPrimitive) {
                Expression member = Expression.PropertyOrField(inObjPrm, inMember.Name);
                if (inMember.Type != outMember.Type)
                    member = Expression.MakeUnary(ExpressionType.Convert, member, outMember.Type);

                return Expression.Bind(outMember.MemberInfo, member);
            }

            var inEnumerableType = inMember.Type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            var outEnumerableType = outMember.Type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (inEnumerableType != null) {
                if (outEnumerableType == null)
                    throw new ArrayTypeMismatchException($"Navigation type mismatch for property {outMember.Name}");
            }
            else {
                if (outEnumerableType != null)
                    throw new ArrayTypeMismatchException($"Navigation type mismatch for property {outMember.Name}");

                return Expression.Bind(
                    outMember.MemberInfo, 
                    Expression.Call(
                        mapContextPrm, 
                        MapMethod.MakeGenericMethod(inMember.Type, outMember.Type), 
                        Expression.PropertyOrField(inObjPrm, inMember.Name)
                    )
                );
            }

            return CreateEnumerableBinding(inMember, outMember, inEnumerableType, outEnumerableType, inObjPrm, mapContextPrm);
        }

        protected virtual MemberBinding CreateEnumerableBinding(MapMember inMember, MapMember outMember, Type inEnumerableType, Type outEnumerableType,
                                                                ParameterExpression inObjPrm, ParameterExpression mapContextPrm) {
            var outEnumType = outEnumerableType.GetGenericArguments()[0];
            MethodInfo mapMethod;
            if (outMember.Type.IsGenericType && outMember.Type.GetGenericTypeDefinition() == typeof(Collection<>)) {
                mapMethod = MapToCollectionMethod.MakeGenericMethod(inEnumerableType.GetGenericArguments()[0], outEnumType);
            }
            else if (outMember.Type.IsArray) {
                mapMethod = MapToArrayMethod.MakeGenericMethod(inEnumerableType.GetGenericArguments()[0], outEnumType);
            }
            else if (outEnumType.IsGenericType && outEnumType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) {
                var inGens = inMember.Type.GetGenericArguments();
                var outGens = outMember.Type.GetGenericArguments();
                mapMethod = MapToDictionaryMethod.MakeGenericMethod(inGens[0], inGens[1], outGens[0], outGens[1]);
            }
            else {
                mapMethod = MapToListMethod.MakeGenericMethod(inEnumerableType.GetGenericArguments()[0], outEnumType);
            }

            return Expression.Bind(
                outMember.MemberInfo,
                Expression.Call(
                    mapContextPrm,
                    mapMethod,
                    Expression.PropertyOrField(inObjPrm, inMember.Name)
                )
            );
        }
    }

    public interface IExpressionProvider {
        MemberBinding CreateMemberBinding(MapMember outMember, MapMember inMember, ParameterExpression inObjPrm, ParameterExpression mapContextPrm);
    }
}
