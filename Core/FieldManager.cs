﻿using System.Collections.Generic;
using System.Linq;
using SS.Form.Core.Model;
using SS.Form.Core.Provider;
using SS.Form.Core.Utils;

namespace SS.Form.Core
{
    public static class FieldManager
    {
        private static class FieldManagerCache
        {
            private static readonly object LockObject = new object();
            private const string CacheKey = "SS.Form.Core.FieldManager";

            public static List<KeyValuePair<string, FieldInfo>> GetAllTableStyles()
            {
                var retval = CacheUtils.Get<List<KeyValuePair<string, FieldInfo>>>(CacheKey);
                if (retval != null) return retval;

                lock (LockObject)
                {
                    retval = CacheUtils.Get<List<KeyValuePair<string, FieldInfo>>>(CacheKey);
                    if (retval == null)
                    {
                        retval = FieldDao.GetAllFieldInfoList();

                        CacheUtils.InsertHours(CacheKey, retval, 12);
                    }
                }

                return retval;
            }

            public static void Clear()
            {
                CacheUtils.Remove(CacheKey);
            }
        }

        public static List<FieldInfo> GetFieldInfoList(int formId)
        {
            var fieldInfoList = new List<FieldInfo>();

            var entries = FieldManagerCache.GetAllTableStyles();
            var startKey = GetKeyPrefix(formId);
            var list = entries.Where(tuple => tuple.Key.StartsWith(startKey)).ToList();
            foreach (var pair in list)
            {
                if (pair.IsDefault()) continue;

                fieldInfoList.Add(pair.Value);
            }

            return fieldInfoList.OrderBy(fieldInfo => fieldInfo.Taxis == 0 ? int.MaxValue : fieldInfo.Taxis).ToList();
        }

        public static FieldInfo GetFieldInfo(int id)
        {
            var entries = FieldManagerCache.GetAllTableStyles();

            var entry = entries.FirstOrDefault(x => x.Value != null && x.Value.Id == id);
            return entry.IsDefault() ? null : entry.Value;
        }

        public static void ClearCache()
        {
            FieldManagerCache.Clear();
        }

        public static string GetKeyPrefix(int formId)
        {
            return $"{formId}$";
        }

        public static string GetKey(int formId, string title)
        {
            return $"{GetKeyPrefix(formId)}{title}";
        }

        public static string GetAttributeId(int fieldId)
        {
            return $"attr_{fieldId}";
        }

        public static string GetExtrasId(int fieldId, int itemId)
        {
            return $"attr_{fieldId}_{itemId}";
        }

        public static bool IsExtra(FieldInfo fieldInfo)
        {
            if (!FormUtils.IsSelectFieldType(fieldInfo.FieldType) || fieldInfo.Items == null || fieldInfo.Items.Count == 0) return false;
            foreach (var item in fieldInfo.Items)
            {
                if (item.IsExtras)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
