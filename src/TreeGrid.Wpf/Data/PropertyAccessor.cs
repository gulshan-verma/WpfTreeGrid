using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace TreeGrid.Wpf.Data
{
    /// <summary>
    /// Caches compiled delegates for property access. Reflection on every cell of a
    /// 100k-row grid is the single easiest way to make a tree grid feel slow, so all
    /// property reads in the data layer go through here.
    /// </summary>
    public static class PropertyAccessor
    {
        private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> Getters
            = new ConcurrentDictionary<(Type, string), Func<object, object>>();

        private static readonly ConcurrentDictionary<(Type, string), Action<object, object>> Setters
            = new ConcurrentDictionary<(Type, string), Action<object, object>>();

        private static readonly ConcurrentDictionary<(Type, string), PropertyInfo> Infos
            = new ConcurrentDictionary<(Type, string), PropertyInfo>();

        public static PropertyInfo GetPropertyInfo(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            return Infos.GetOrAdd((type, name), key => ResolveProperty(key.Item1, key.Item2));
        }

        public static object GetValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
                return null;

            var getter = Getters.GetOrAdd((instance.GetType(), propertyName), key => BuildGetter(key.Item1, key.Item2));
            return getter?.Invoke(instance);
        }

        public static void SetValue(object instance, string propertyName, object value)
        {
            if (instance == null || string.IsNullOrEmpty(propertyName))
                return;

            var setter = Setters.GetOrAdd((instance.GetType(), propertyName), key => BuildSetter(key.Item1, key.Item2));
            setter?.Invoke(instance, value);
        }

        /// <summary>Supports dotted paths such as "Manager.Name".</summary>
        private static PropertyInfo ResolveProperty(Type type, string name)
        {
            PropertyInfo info = null;
            var current = type;

            foreach (var part in name.Split('.'))
            {
                info = current.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (info == null)
                    return null;
                current = info.PropertyType;
            }

            return info;
        }

        private static Func<object, object> BuildGetter(Type type, string name)
        {
            var parameter = Expression.Parameter(typeof(object), "instance");
            Expression body = Expression.Convert(parameter, type);

            foreach (var part in name.Split('.'))
            {
                var info = body.Type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (info == null || !info.CanRead)
                    return null;
                body = Expression.Property(body, info);
            }

            var converted = Expression.Convert(body, typeof(object));
            return Expression.Lambda<Func<object, object>>(converted, parameter).Compile();
        }

        private static Action<object, object> BuildSetter(Type type, string name)
        {
            var info = ResolveProperty(type, name);
            if (info == null || !info.CanWrite)
                return null;

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            Expression target = Expression.Convert(instanceParam, type);
            var parts = name.Split('.');

            for (var i = 0; i < parts.Length - 1; i++)
                target = Expression.Property(target, parts[i]);

            var assign = Expression.Assign(
                Expression.Property(target, info),
                Expression.Convert(valueParam, info.PropertyType));

            return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
        }
    }
}
