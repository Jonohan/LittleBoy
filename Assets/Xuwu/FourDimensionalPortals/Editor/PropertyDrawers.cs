using UnityEditor;
using UnityEngine;
#if USING_UNIVERSAL_RENDER_PIPELINE
using System.Reflection;
using UnityEngine.Rendering.Universal;
#endif

namespace Xuwu.FourDimensionalPortals.Editor
{
    [CustomPropertyDrawer(typeof(LayerFieldAttribute))]
    public class LayerFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUI.BeginChangeCheck();
                var intValue = EditorGUI.LayerField(position, label, property.intValue);
                if (EditorGUI.EndChangeCheck())
                    property.intValue = intValue;
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use LayerField with int.");
            }

            EditorGUI.EndProperty();
        }
    }

#if USING_UNIVERSAL_RENDER_PIPELINE
    [CustomPropertyDrawer(typeof(UniversalRendererFieldAttribute))]
    public class UniversalRendererFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                var rpAsset = UniversalRenderPipeline.asset;

                if (rpAsset)
                {
                    var rpAssetType = rpAsset.GetType();
                    var rendererDisplayListPropertyInfo = rpAssetType.GetProperty("rendererDisplayList", BindingFlags.Instance | BindingFlags.NonPublic);
                    var rendererIndexListPropertyInfo = rpAssetType.GetProperty("rendererIndexList", BindingFlags.Instance | BindingFlags.NonPublic);

                    var rendererDisplayList = rendererDisplayListPropertyInfo.GetValue(rpAsset) as GUIContent[];
                    var rendererIndexList = rendererIndexListPropertyInfo.GetValue(rpAsset) as int[];

                    EditorGUI.BeginChangeCheck();
                    var intValue = EditorGUI.IntPopup(position, label, property.intValue, rendererDisplayList, rendererIndexList);
                    if (EditorGUI.EndChangeCheck())
                        property.intValue = intValue;
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use UniversalRendererField with int.");
            }

            EditorGUI.EndProperty();
        }
    }
#endif
}
