using UnityEditor;

namespace Xuwu.FourDimensionalPortals.Editor
{
    [CustomEditor(typeof(PortalConfig))]
    public class PortalConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _planeMesh;
        private SerializedProperty _detectionMesh;
        private SerializedProperty _frameMesh;
        private SerializedProperty _endMaterial;
        private SerializedProperty _penetratingViewMesh;

        private void OnEnable()
        {
            _planeMesh = serializedObject.FindProperty(nameof(_planeMesh));
            _detectionMesh = serializedObject.FindProperty(nameof(_detectionMesh));
            _frameMesh = serializedObject.FindProperty(nameof(_frameMesh));
            _endMaterial = serializedObject.FindProperty(nameof(_endMaterial));
            _penetratingViewMesh = serializedObject.FindProperty(nameof(_penetratingViewMesh));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_planeMesh);
            if (!_planeMesh.objectReferenceValue)
                EditorGUILayout.HelpBox("PlaneMesh is missing, portals that reference this config will not be workable.", MessageType.Error);

            EditorGUILayout.PropertyField(_detectionMesh);
            EditorGUILayout.PropertyField(_frameMesh);
            EditorGUILayout.PropertyField(_endMaterial);
            EditorGUILayout.PropertyField(_penetratingViewMesh);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
