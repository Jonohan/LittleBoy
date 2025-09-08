using UnityEditor;

namespace Xuwu.FourDimensionalPortals.Editor
{
    [CustomEditor(typeof(Portal))]
    public class PortalEditor : UnityEditor.Editor
    {
        private string _undoHistoryName;
        private Portal _portal;

        private SerializedProperty _config;
        private SerializedProperty _linkedPortal;
        private SerializedProperty _overrideEndMaterial;
        private SerializedProperty _customViewMaterial;
        private SerializedProperty _detectionZoneScale;
        private SerializedProperty _useObliqueProjectionMatrix;
        private SerializedProperty _onTravellerTransferFromLinkedPortal;
        private SerializedProperty _onTravellerTransferToLinkedPortal;

        private void OnEnable()
        {
            _undoHistoryName = $"Modified Property in {target.name}";
            _portal = target as Portal;

            _config = serializedObject.FindProperty(nameof(_config));
            _linkedPortal = serializedObject.FindProperty(nameof(_linkedPortal));
            _overrideEndMaterial = serializedObject.FindProperty(nameof(_overrideEndMaterial));
            _customViewMaterial = serializedObject.FindProperty(nameof(_customViewMaterial));
            _detectionZoneScale = serializedObject.FindProperty(nameof(_detectionZoneScale));
            _useObliqueProjectionMatrix = serializedObject.FindProperty(nameof(_useObliqueProjectionMatrix));
            _onTravellerTransferFromLinkedPortal = serializedObject.FindProperty(nameof(_onTravellerTransferFromLinkedPortal));
            _onTravellerTransferToLinkedPortal = serializedObject.FindProperty(nameof(_onTravellerTransferToLinkedPortal));
        }

        public override void OnInspectorGUI()
        {
            Undo.RecordObject(_portal, _undoHistoryName);
            if (_portal.LinkedPortal)
                Undo.RecordObject(_portal.LinkedPortal, _undoHistoryName);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_config);
            if (EditorGUI.EndChangeCheck())
                _portal.Config = _config.objectReferenceValue as PortalConfig;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_linkedPortal);
            if (EditorGUI.EndChangeCheck())
            {
                var targetLinkedPortal = _linkedPortal.objectReferenceValue as Portal;
                if (targetLinkedPortal)
                    Undo.RecordObject(targetLinkedPortal, _undoHistoryName);

                _portal.LinkPortal(targetLinkedPortal);
                SceneView.RepaintAll();
            }

            serializedObject.Update();

            EditorGUILayout.PropertyField(_overrideEndMaterial);
            EditorGUILayout.PropertyField(_customViewMaterial);
            EditorGUILayout.PropertyField(_detectionZoneScale);
            EditorGUILayout.PropertyField(_useObliqueProjectionMatrix);
            EditorGUILayout.PropertyField(_onTravellerTransferFromLinkedPortal);
            EditorGUILayout.PropertyField(_onTravellerTransferToLinkedPortal);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
