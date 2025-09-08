using System;
using UnityEngine;

namespace Xuwu.FourDimensionalPortals
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class LayerFieldAttribute : PropertyAttribute { }

#if USING_UNIVERSAL_RENDER_PIPELINE
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class UniversalRendererFieldAttribute : PropertyAttribute { }
#endif
}
