﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ServiceStack.Common;
using ServiceStack.Common.Utils;
using ServiceStack.Common.Web;
using ServiceStack.ServiceHost;
using ServiceStack.Text;

namespace ServiceStack.ServiceInterface
{
	public static class ServiceRoutesExtensions
	{
		/// <summary>
		///     Scans the supplied Assemblies to infer REST paths and HTTP verbs.
		/// </summary>
		///<param name="routes">The <see cref="IServiceRoutes"/> instance.</param>
		///<param name="assembliesWithServices">
		///     The assemblies with REST services.
		/// </param>
		/// <returns>The same <see cref="IServiceRoutes"/> instance;
		///		never <see langword="null"/>.</returns>
		public static IServiceRoutes AddFromAssembly(this IServiceRoutes routes,
													 params Assembly[] assembliesWithServices)
		{
			foreach (Assembly assembly in assembliesWithServices)
			{
				IEnumerable<Type> restServices = 
                    from t in assembly.GetExportedTypes()
					where
						!t.IsAbstract &&
						t.IsSubclassOfRawGeneric(typeof(RestServiceBase<>))
					select t;

				foreach (Type restService in restServices)
				{
					Type baseType = restService.BaseType;

					//go up the hierarchy to the first generic base type
					while (!baseType.IsGenericType)
					{
						baseType = baseType.BaseType;
					}

					Type requestType = baseType.GetGenericArguments()[0];

					//find overriden REST methods
					var allowedMethods = new List<string>();
					if (restService.GetMethod("OnGet").DeclaringType == restService)
					{
						allowedMethods.Add(HttpMethods.Get);
					}

					if (restService.GetMethod("OnPost").DeclaringType == restService)
					{
						allowedMethods.Add(HttpMethods.Post);
					}

					if (restService.GetMethod("OnPut").DeclaringType == restService)
					{
						allowedMethods.Add(HttpMethods.Put);
					}

					if (restService.GetMethod("OnDelete").DeclaringType == restService)
					{
						allowedMethods.Add(HttpMethods.Delete);
					}

					if (restService.GetMethod("OnPatch").DeclaringType == restService)
					{
						allowedMethods.Add(HttpMethods.Patch);
					}

					if (allowedMethods.Count == 0) continue;
					var allowedVerbs = string.Join(" ", allowedMethods.ToArray());

					routes.Add(requestType, restService.Name, allowedVerbs, null);

					var hasIdField = requestType.GetProperty(IdUtils.IdField) != null;
					if (hasIdField)
					{
						var routePath = restService.Name + "/{" + IdUtils.IdField + "}";
						routes.Add(requestType, routePath, allowedVerbs, null);
					}
				}
			}

			return routes;
		}

		public static IServiceRoutes Add<TRequest>(this IServiceRoutes routes, string restPath, ApplyTo verbs)
		{
			return routes.Add<TRequest>(restPath, verbs.ToVerbsString());
		}

		public static IServiceRoutes Add<TRequest>(this IServiceRoutes routes, string restPath, ApplyTo verbs, string defaultContentType)
		{
			return routes.Add<TRequest>(restPath, verbs.ToVerbsString(), defaultContentType);
		}

		public static IServiceRoutes Add(this IServiceRoutes routes, Type requestType, string restPath, ApplyTo verbs, string defaultContentType)
		{
			return routes.Add(requestType, restPath, verbs.ToVerbsString(), defaultContentType);
		}

		private static string ToVerbsString(this ApplyTo verbs)
		{
			var allowedMethods = new List<string>();
			if (verbs.Has(ApplyTo.Get))
				allowedMethods.Add(HttpMethods.Get);
			if (verbs.Has(ApplyTo.Post))
				allowedMethods.Add(HttpMethods.Post);
			if (verbs.Has(ApplyTo.Put))
				allowedMethods.Add(HttpMethods.Put);
			if (verbs.Has(ApplyTo.Delete))
				allowedMethods.Add(HttpMethods.Delete);
			if (verbs.Has(ApplyTo.Patch))
				allowedMethods.Add(HttpMethods.Patch);
			
			return string.Join(" ", allowedMethods.ToArray());
		}

		public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
		{
			while (toCheck != typeof(object))
			{
				Type cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == cur)
				{
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}

        private static string FormatRoute<T>(string path, params Expression<Func<T, object>>[] propertyExpressions)
        {
            var properties = propertyExpressions.Select(x => string.Format("{{{0}}}", PropertyName(x))).ToArray();
            return string.Format(path, properties);
        }

        private static string PropertyName(LambdaExpression ex)
        {
            return (ex.Body is UnaryExpression ? (MemberExpression)((UnaryExpression)ex.Body).Operand : (MemberExpression)ex.Body).Member.Name;
        }

        public static void Add<T>(this IServiceRoutes serviceRoutes, string httpMethod, string url, params Expression<Func<T, object>>[] propertyExpressions)
        {
            serviceRoutes.Add<T>(FormatRoute(url, propertyExpressions), httpMethod);
        }
	}
}