using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(Transform))]
public class TransformHUDEditor : Editor
{
	private bool fieldHUDShow;
	private bool fieldOptionsShow;
	private Color fieldBoxColor;
	private Vector2 fieldBoxOffset;
	private bool locToWcTransShow;
	private bool locTransShow;

	void OnEnable()
	{
		fieldHUDShow = EditorPrefs.GetBool("Pref.TransformHUDEditor.HUDShow", false);
		fieldOptionsShow = EditorPrefs.GetBool("Pref.TransformHUDEditor.OptionsShow", false);
		fieldBoxColor = new Color(EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorR", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorG", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorB", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorA", 0.25f));
		fieldBoxOffset = new Vector2(EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxOffsetX", 0.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxOffsetY", 0.0f));
		locToWcTransShow = EditorPrefs.GetBool("Pref.TransformHUDEditor.locToWcTransShow", false);
		locTransShow = EditorPrefs.GetBool("Pref.TransformHUDEditor.locTransShow", false);
	}
	void OnDisable()
	{
		EditorPrefs.SetBool("Pref.TransformHUDEditor.HUDShow", fieldHUDShow);
		EditorPrefs.SetBool("Pref.TransformHUDEditor.OptionsShow", fieldOptionsShow);
		EditorPrefs.SetBool("Pref.TransformHUDEditor.locToWcTransShow", locToWcTransShow);
		EditorPrefs.SetBool("Pref.TransformHUDEditor.locTransShow", locTransShow);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorR", fieldBoxColor.r);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorG", fieldBoxColor.g);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorB", fieldBoxColor.b);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorA", fieldBoxColor.a);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxOffsetX", fieldBoxOffset.x);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxOffsetY", fieldBoxOffset.y);
	}

	public override void OnInspectorGUI()
	{
		TransformInspector(inSceneView: true);

		GUILayout.BeginHorizontal();

		if (GUILayout.Button((fieldHUDShow) ? "Hide HUD" : "Show HUD"))
		{
			fieldHUDShow = !fieldHUDShow;
		}

		if (GUILayout.Button((fieldOptionsShow) ? "Hide Options" : "Show Options"))
		{
			fieldOptionsShow = !fieldOptionsShow;
		}

		GUILayout.EndHorizontal();

		if (fieldOptionsShow)
		{
			bool originalWideMode = EditorGUIUtility.wideMode;
			EditorGUIUtility.wideMode = true;
			fieldBoxOffset = EditorGUILayout.Vector2Field(new GUIContent("Box Offset", "The offset of the HUD box in pixels from the origin of the selected object in the Scene View."), fieldBoxOffset);
			EditorGUIUtility.wideMode = originalWideMode;

			fieldBoxColor = EditorGUILayout.ColorField(new GUIContent("Box Color", "The color and transparency of the HUD box in the Scene View."), fieldBoxColor);
		}

		if (GUI.changed)
		{
			EditorUtility.SetDirty(target);
		}
	}

	void OnSceneGUI()
	{
		if (fieldHUDShow)
		{
			var t = (Transform)target;
			var objpos = HandleUtility.WorldToGUIPoint(t.transform.position);

			Handles.BeginGUI();

			var boxWidth = 340.0f;
			var boxHeight = 120.0f;
			var bgBoxRect = new Rect(objpos.x + fieldBoxOffset.x, objpos.y - fieldBoxOffset.y, boxWidth, boxHeight);
			Color originalBackgroundColor = GUI.backgroundColor;
			GUI.backgroundColor = fieldBoxColor;
			GUI.Box(bgBoxRect, "");
			GUI.color = originalBackgroundColor;

			var pad = 10f;
			var areaRect = new Rect(bgBoxRect.x + pad, bgBoxRect.y + pad, bgBoxRect.width - 2 * pad, bgBoxRect.height - 2 * pad);
			GUILayout.BeginArea(areaRect);

			TransformInspector(inSceneView: true);

			if (GUILayout.Button((fieldHUDShow) ? "Hide HUD" : "Show HUD"))
			{
				fieldHUDShow = !fieldHUDShow;
				EditorUtility.SetDirty(target);
			}

			GUILayout.EndArea();

			Handles.EndGUI();
		}
	}

	(Vector4 v1, Vector4 v2, Vector4 v3, Vector4 v4) UnpackTransform(Matrix4x4 m)
	{
		//var v1 = new Vector4(m.m00, m.m01, m.m02, m.m03);
		//var v2 = new Vector4(m.m10, m.m11, m.m12, m.m13);
		//var v3 = new Vector4(m.m20, m.m21, m.m22, m.m23);
		//var v4 = new Vector4(m.m30, m.m31, m.m32, m.m33);
		var v1 = new Vector4(m.m00, m.m10, m.m20, m.m30);
		var v2 = new Vector4(m.m01, m.m11, m.m21, m.m31);
		var v3 = new Vector4(m.m02, m.m12, m.m22, m.m32);
		var v4 = new Vector4(m.m03, m.m13, m.m23, m.m33);

		return (v1, v2, v3, v4);
	}

	Matrix4x4 localMatrix(Transform xf)
	{
		var rv = xf.localToWorldMatrix;
		if (xf.parent != null)
		{
			// Use Unity's inverse function which might not be the fastest
			rv = xf.parent.localToWorldMatrix.inverse * xf.localToWorldMatrix;
		}
		return rv;
	}
	void TransformInspector(bool inSceneView)
	{
		var t = (Transform)target;

		var originalWideMode = EditorGUIUtility.wideMode;
		EditorGUIUtility.wideMode = true;
		EditorGUIUtility.labelWidth = 65;
		EditorGUIUtility.fieldWidth = 0;

		var wcoflocalorg = t.TransformPoint(Vector3.zero);

		var position = EditorGUILayout.Vector3Field("Position", t.localPosition);
		var eulerAngles = EditorGUILayout.Vector3Field("Rotation", t.localEulerAngles);
		var scale = EditorGUILayout.Vector3Field("Scale", t.localScale);
		var glbpos = EditorGUILayout.Vector3Field("GlbPos", t.position);
		var wcpos = EditorGUILayout.Vector3Field("WcPos", wcoflocalorg);
		var glbRot = EditorGUILayout.Vector3Field("GlbRot", t.eulerAngles);

		if (inSceneView)
		{
			GUILayout.BeginHorizontal();

			if (GUILayout.Button((locToWcTransShow) ? "Hide LocToWc" : "Show LocToWc"))
			{
				locToWcTransShow = !locToWcTransShow;
			}

			if (GUILayout.Button((locTransShow) ? "Hide Local" : "Show Local"))
			{
				locTransShow = !locTransShow;
			}
			GUILayout.EndHorizontal();
		}

		if (locToWcTransShow)
		{
			var (lwc1, lwc2, lwc3, lwc4) = UnpackTransform(t.localToWorldMatrix);
			EditorGUILayout.Vector4Field("locWC.r1", lwc1);
			EditorGUILayout.Vector4Field("locWC.r2", lwc2);
			EditorGUILayout.Vector4Field("locWC.r3", lwc3);
			EditorGUILayout.Vector4Field("locWC.r4", lwc4);
		}
		if (locTransShow)
		{
			var (l1, l2, l3, l4) = UnpackTransform(localMatrix(t));
			EditorGUILayout.Vector4Field("loc.r1", l1);
			EditorGUILayout.Vector4Field("loc.r2", l2);
			EditorGUILayout.Vector4Field("loc.r3", l3);
			EditorGUILayout.Vector4Field("loc.r4", l4);
		}


		EditorGUIUtility.labelWidth = 0;
		EditorGUIUtility.fieldWidth = 0;
		EditorGUIUtility.wideMode = originalWideMode;

		if (GUI.changed)
		{
			// For Undo
			Undo.RecordObject(t, "Transform Change");

			t.localPosition = FixIfNaN(position);
			t.localEulerAngles = FixIfNaN(eulerAngles);
			t.localScale = FixIfNaN(scale);
		}
	}

	private Vector3 FixIfNaN(Vector3 v)
	{
		if (float.IsNaN(v.x))
		{
			v.x = 0;
		}
		if (float.IsNaN(v.y))
		{
			v.y = 0;
		}
		if (float.IsNaN(v.z))
		{
			v.z = 0;
		}
		return v;
	}
}