#region
using System;
using System.Linq;
#endregion

namespace Editor.Model.Interpolators
{
	public class DelegatedInterpolator<T> : IInterpolator
	{
		private readonly Func<float, T[], T> _arrayImplementation;
		private readonly Func<float, T, T, T> _pairImplementation;

		public DelegatedInterpolator(Func<float, T, T, T> impl, Func<float, T[], T> impl2)
		{
			_pairImplementation = impl;
			_arrayImplementation = impl2;
		}

		public T Interpolate(float gradient, T first, T second)
		{
			return _pairImplementation(gradient, first, second);
		}

		public T InterpolateCast(float gradient, params T[] values)
		{
			return _arrayImplementation(gradient, values);
		}

		public object Interpolate(float gradient, object first, object second)
		{
			return Interpolate(gradient, (T)first, (T)second);
		}

		public object Interpolate(float gradient, params object[] objects)
		{
			return InterpolateCast(gradient, objects.Cast<T>().ToArray());
		}
	}
}