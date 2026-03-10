using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace LenovoController.Features
{
    public class AbstractWmiFeature<T> : IFeature<T> where T : struct, IComparable
    {
        private readonly string _methodNameSuffix;
        private readonly int _offset;

        protected AbstractWmiFeature(string methodNameSuffix, int offset)
        {
            _methodNameSuffix = methodNameSuffix;
            _offset = offset;
        }

        public T GetState()
        {
            return FromInternal(ExecuteGamezone("Get" + _methodNameSuffix, "Data"));
        }

        public void SetState(T state)
        {
            ExecuteGamezone("Set" + _methodNameSuffix, "Data",
                new Dictionary<string, string>
                {
                    { "Data", ToInternal(state).ToString() }
                });
        }

        private int ToInternal(T state) => (int)(object)state + _offset;
        private T FromInternal(int state) => (T)(object)(state - _offset);

        private static int ExecuteGamezone(string methodName, string resultProperty,
            Dictionary<string, string> methodParams = null)
        {
            return Execute("SELECT * FROM LENOVO_GAMEZONE_DATA", methodName, resultProperty, methodParams);
        }

        private static int Execute(string query, string methodName, string resultProperty,
            Dictionary<string, string> methodParams = null)
        {
            try
            {
                var scope = new ManagementScope("ROOT\\WMI");
                scope.Connect();

                using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                using (var results  = searcher.Get())
                {
                    var enumerator = results.GetEnumerator();
                    if (!enumerator.MoveNext())
                        throw new InvalidOperationException(
                            $"WMI query returned no results: {query}");

                    using (var mo = (ManagementObject)enumerator.Current)
                    {
                        var inParams = mo.GetMethodParameters(methodName);

                        if (methodParams != null)
                            foreach (var pair in methodParams)
                                inParams[pair.Key] = pair.Value;

                        using (var outParams = mo.InvokeMethod(methodName, inParams, null))
                        {
                            if (outParams == null)
                                throw new InvalidOperationException(
                                    $"WMI method '{methodName}' returned null");

                            return Convert.ToInt32(outParams.Properties[resultProperty].Value);
                        }
                    }
                }
            }
            catch (ManagementException ex)
            {
                Trace.TraceError($"WMI error calling '{methodName}': {ex.Message} (ErrorCode={ex.ErrorCode})");
                throw;
            }
        }
    }
}
