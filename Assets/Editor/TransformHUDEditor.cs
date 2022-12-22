using UnityEngine;
using UnityEditor;
using System.Collections;

//
// Copyright © 2015. Out of Web Site. All rights reserved.
// Unauthorized use or redistribution is prohibited.
//

[CustomEditor(typeof(Transform))]
public class TransformHUDEditor : Editor {

	// Fields
	private bool	fieldHUDShow;
	private bool 	fieldOptionsShow;
	private Color	fieldBoxColor;
	private Vector2	fieldBoxOffset;




	// OnEnable
	void OnEnable () {

		// Set intial field values to saved preferences (or defaults)
		fieldHUDShow     	= EditorPrefs.GetBool("Pref.TransformHUDEditor.HUDShow", false);
		fieldOptionsShow    = EditorPrefs.GetBool("Pref.TransformHUDEditor.OptionsShow", false);
		fieldBoxColor		= new Color(EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorR", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorG", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorB", 1.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxColorA", 0.25f));
		fieldBoxOffset     	= new Vector2(EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxOffsetX", 0.0f), EditorPrefs.GetFloat("Pref.TransformHUDEditor.BoxOffsetY", 0.0f));

	}



	// OnDisable
	void OnDisable() {
		
		// Save field values to preferences
		EditorPrefs.SetBool ("Pref.TransformHUDEditor.HUDShow"		, fieldHUDShow);
		EditorPrefs.SetBool ("Pref.TransformHUDEditor.OptionsShow"	, fieldOptionsShow);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorR"	, fieldBoxColor.r);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorG"	, fieldBoxColor.g);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorB"	, fieldBoxColor.b);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxColorA"	, fieldBoxColor.a);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxOffsetX"	, fieldBoxOffset.x);
		EditorPrefs.SetFloat("Pref.TransformHUDEditor.BoxOffsetY"	, fieldBoxOffset.y);

	}



	// Draw transform inspector in editor
	public override void OnInspectorGUI() {

		// Transform inspector
		MyDrawTransformInspector();



		GUILayout.BeginHorizontal();

		// Field: Show HUD
		if (GUILayout.Button ((fieldHUDShow) ? "Hide HUD" : "Show HUD")) {
			fieldHUDShow = !fieldHUDShow;
		}

		// Field: Show Options
		if (GUILayout.Button ((fieldOptionsShow) ? "Hide Options" : "Show Options")) {
			fieldOptionsShow = !fieldOptionsShow;
		}

		GUILayout.EndHorizontal();


		// Show options
		if (fieldOptionsShow) {

			// Field: Box Offset 
			bool originalWideMode = EditorGUIUtility.wideMode;
			EditorGUIUtility.wideMode = true;
			fieldBoxOffset = EditorGUILayout.Vector2Field (new GUIContent ("Box Offset", "The offset of the HUD box in pixels from the origin of the selected object in the Scene View."), fieldBoxOffset);
			EditorGUIUtility.wideMode = originalWideMode;

			// Field: Box Color
			fieldBoxColor = EditorGUILayout.ColorField (new GUIContent ("Box Color", "The color and transparency of the HUD box in the Scene View."), fieldBoxColor);
		}


		// Update UI
		if (GUI.changed) {
			EditorUtility.SetDirty(target);
		}
	}



	// Draw transform inspector in scene view
	void OnSceneGUI() {

		// Draw HUD
		if (fieldHUDShow) {

			// Selected object's transform
			Transform t = (Transform)target;
			Vector2 objectPosition = HandleUtility.WorldToGUIPoint (t.transform.position);

			// Begin GUI
			Handles.BeginGUI ();



			// Background box
			float boxWidth = 340.0f;
			float boxHeight = 120.0f;
			Rect backgroundBoxRect = new Rect (objectPosition.x + fieldBoxOffset.x, objectPosition.y - fieldBoxOffset.y, boxWidth, boxHeight);
			Color originalBackgroundColor = GUI.backgroundColor;
			GUI.backgroundColor = fieldBoxColor;
			GUI.Box (backgroundBoxRect, "");
			GUI.color = originalBackgroundColor;


			// Begin Area
			float padding = 10.0f;
			Rect areaRect = new Rect (backgroundBoxRect.x + padding, backgroundBoxRect.y + padding, backgroundBoxRect.width - 2.0f * padding, backgroundBoxRect.height - 2.0f * padding); 
			GUILayout.BeginArea (areaRect);


			// Draw the transform inspector
			MyDrawTransformInspector ();

			// Field: Show HUD
			if (GUILayout.Button ((fieldHUDShow) ? "Hide HUD" : "Show HUD")) {
				fieldHUDShow = !fieldHUDShow;
				EditorUtility.SetDirty(target);
			}


			// End Area
			GUILayout.EndArea ();



			// End GUI
			Handles.EndGUI ();
		}
	}



	// Transform inspector
	void MyDrawTransformInspector() {

		// Selected object's transform
		Transform t = (Transform)target;
		
		
		// Replicate the standard transform inspector
		bool originalWideMode = EditorGUIUtility.wideMode;
		EditorGUIUtility.wideMode = true;
		EditorGUIUtility.labelWidth = 65;
		EditorGUIUtility.fieldWidth = 0;

		var wcoflocalorg = t.TransformPoint(Vector3.zero);


		Vector3 position = EditorGUILayout.Vector3Field("Position", t.localPosition);
		Vector3 eulerAngles = EditorGUILayout.Vector3Field("Rotation", t.localEulerAngles);
		Vector3 scale 		= EditorGUILayout.Vector3Field("Scale"	 , t.localScale);
		Vector3 glbpos = EditorGUILayout.Vector3Field("GlbPos", t.position);
		Vector3 wcpos = EditorGUILayout.Vector3Field("WcPos", wcoflocalorg);
		Vector3 glbRot = EditorGUILayout.Vector3Field("GlbRot", t.eulerAngles);

		EditorGUIUtility.labelWidth = 0;
		EditorGUIUtility.fieldWidth = 0;
		EditorGUIUtility.wideMode = originalWideMode;
		
		
		// If changes made
		if (GUI.changed) {
			
			// Record Undo
			Undo.RecordObject(t, "Transform Change");


			// Fix invalid numbers
			t.localPosition = FixIfNaN(position);
			t.localEulerAngles = FixIfNaN(eulerAngles);
			t.localScale = FixIfNaN(scale);
		}
	}



	// Fix not a number (NAN) errors
	private Vector3 FixIfNaN(Vector3 v) {
		if (float.IsNaN(v.x)) {
			v.x = 0;
		}
		if (float.IsNaN(v.y)) {
			v.y = 0;
		}
		if (float.IsNaN(v.z)) {
			v.z = 0;
		}
		return v;
	}
	
}
