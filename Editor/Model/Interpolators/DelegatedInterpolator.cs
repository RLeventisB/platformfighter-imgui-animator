using System;
using System.Linq;

namespace Editor.Model.Interpolators
{
    public class DelegatedInterpolator<T> : IInterpolator
    {
        private Func<float, T, T, T> _pairImplementation;
        private Func<float, T[], T> _arrayImplementation;
        public DelegatedInterpolator(Func<float, T, T, T> impl, Func<float, T[], T> impl2)
        {
            _pairImplementation = impl;
            _arrayImplementation = impl2;
        }

        public object Interpolate(float gradient, object first, object second)
        {
            return _pairImplementation(gradient, (T)first, (T)second);
        }
        public object Interpolate(float gradient, params object[] values)
        {
            return _arrayImplementation(gradient, values.Cast<T>().ToArray());
        }
    }
}