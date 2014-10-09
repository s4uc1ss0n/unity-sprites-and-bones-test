﻿/*
The MIT License (MIT)

Copyright (c) 2014 Banbury & Play-Em

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

[ExecuteInEditMode]
[SelectionBase]
public class Skeleton : MonoBehaviour {
    public bool editMode = true;
    public bool showBoneInfluence = true;

    //[HideInInspector]
    public Pose basePose;

    private Pose tempPose;

	[SerializeField] 
	[HideInInspector]
	private bool _flipY = false;

	public bool flipY
	{
		get { return _flipY; }
		set
		{
			_flipY = value;
			FlipY();
		}
	}

	[SerializeField] 
	[HideInInspector]
	private bool _flipX = false;
	public bool flipX
	{
		get { return _flipX; }
		set
		{
			_flipX = value;
			FlipX();
		}
	}

	[SerializeField] 
	[HideInInspector]
	private bool _useShadows = false;

	public bool useShadows
	{
		get { return _useShadows; }
		set
		{
			_useShadows = value;
			UseShadows();
		}
	}

	[SerializeField] 
	[HideInInspector]
	private bool _useZSorting = false;

	public bool useZSorting
	{
		get { return _useZSorting; }
		set
		{
			_useZSorting = value;
			UseZSorting();
		}
	}

	public Shader spriteShader;
	public Shader spriteShadowsShader;
	public Color colorRight = new Color(255.0f/255.0f, 128.0f/255.0f, 0f, 255.0f/255.0f);
	public Color colorLeft = Color.magenta;

	[HideInInspector]
	public bool hasChildPositionsSaved = false;

	private Bone[] bones;
	private Dictionary<Transform, float> renderers = new Dictionary<Transform, float>();

	private SkinnedMeshRenderer[] skins;
	private SpriteRenderer[] spriteRenderers;

#if UNITY_EDITOR
		[MenuItem("Sprites And Bones/Skeleton")]
		public static void Create ()
		{
			Undo.IncrementCurrentGroup ();

			GameObject o = new GameObject ("Skeleton");
			Undo.RegisterCreatedObjectUndo (o, "Create skeleton");
			o.AddComponent<Skeleton> ();

			GameObject b = new GameObject ("Bone");
			Undo.RegisterCreatedObjectUndo (b, "Create Skeleton");
			b.AddComponent<Bone> ();

			b.transform.parent = o.transform;

			Undo.CollapseUndoOperations (Undo.GetCurrentGroup ());
		}
#endif

    // Use this for initialization
	void Start () {
		spriteShader = Shader.Find("Sprites/Default");
		spriteShadowsShader = Shader.Find("Sprites/Skeleton-CutOut");
		if (Application.isPlaying) {
            SetEditMode(false);
        }
	}

#if UNITY_EDITOR
    void OnEnable() {
		EditorApplication.update += EditorUpdate;
    }

    void OnDisable() {
		EditorApplication.update -= EditorUpdate;
    }
#endif

    private void EditorUpdate() {
		if (bones != null)
		{
			for (int i=0; i<bones.Length; i++) {
                if (bones[i] != null)
				{
					InverseKinematics ik = bones[i].GetComponent<InverseKinematics>();

					if (ik != null && !editMode && ik.enabled && ik.influence > 0) {
						ik.resolveSK2D();
					}
				}
			}
		}
    }
	
	// Update is called once per frame
	void Update () {
		// Get Shaders if they are null
		if (spriteShader == null)
		{
			spriteShader = Shader.Find("Sprites/Default");
		}
		if (spriteShadowsShader == null)
		{
			spriteShadowsShader = Shader.Find("Sprites/Skeleton-CutOut");
		}

		if (bones == null || bones != null && bones.Length <= 0 || Application.isEditor){
			bones = gameObject.GetComponentsInChildren<Bone>();
		}

#if !UNITY_EDITOR
		EditorUpdate();
#else
        if (Application.isEditor && bones != null) {
            for (int i=0; i<bones.Length; i++) {
                if (bones[i] != null)
				{
					bones[i].editMode = editMode;
					bones[i].showInfluence = showBoneInfluence;
				}
            }
        }
#endif
    }

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.DrawIcon(transform.position, "man_icon.png", true);
    }

    public Pose CreatePose() {
        Pose pose = ScriptableObject.CreateInstance<Pose>();

        var bones = GetComponentsInChildren<Bone>();
        var cps = GetComponentsInChildren<ControlPoint>();

        List<RotationValue> rotations = new List<RotationValue>();
        List<PositionValue> positions = new List<PositionValue>();
        List<PositionValue> targets = new List<PositionValue>();
        List<PositionValue> controlPoints = new List<PositionValue>();

        foreach (Bone b in bones) {
            rotations.Add(new RotationValue(b.name, b.transform.localRotation));
            positions.Add(new PositionValue(b.name, b.transform.localPosition));

            if (b.GetComponent<InverseKinematics>() != null) {
                targets.Add(new PositionValue(b.name, b.GetComponent<InverseKinematics>().target.localPosition));
            }
        }

        // Use bone parent name + control point name for the search
        foreach (ControlPoint cp in cps) {
            controlPoints.Add(new PositionValue(cp.transform.parent.name + cp.name, cp.transform.localPosition));
        }

        pose.rotations = rotations.ToArray();
        pose.positions = positions.ToArray();
        pose.targets = targets.ToArray();
        pose.controlPoints = controlPoints.ToArray();

        return pose;
    }

    public void SavePose(string poseFileName) {
        if (poseFileName != null && poseFileName.Trim() != "") {
            ScriptableObjectUtility.CreateAsset(CreatePose(), poseFileName);
        } else {
            ScriptableObjectUtility.CreateAsset(CreatePose());
        }
    }

    public void RestorePose(Pose pose) {
        var bones = GetComponentsInChildren<Bone>();
        var cps = GetComponentsInChildren<ControlPoint>();
        Undo.RegisterCompleteObjectUndo(bones, "Assign Pose");
        Undo.RegisterCompleteObjectUndo(cps, "Assign Pose");

        if (bones.Length > 0)
		{
			foreach (RotationValue rv in pose.rotations) {
				Bone bone = bones.First(b => b.name == rv.name);
				if (bone != null) {
					Undo.RecordObject(bone.transform, "Assign Pose");
					bone.transform.localRotation = rv.rotation;
					EditorUtility.SetDirty (bone.transform);
				} else {
					Debug.Log("This skeleton has no bone '" + bone.name + "'");
				}
			}

			foreach (PositionValue pv in pose.positions) {
				Bone bone = bones.First(b => b.name == pv.name);
				if (bone != null) {
					Undo.RecordObject(bone.transform, "Assign Pose");
					bone.transform.localPosition = pv.position;
					EditorUtility.SetDirty (bone.transform);
				} else {
					Debug.Log("This skeleton has no bone '" + bone.name + "'");
				}
			}

			foreach (PositionValue tv in pose.targets) {
				Bone bone = bones.First(b => b.name == tv.name);

				if (bone != null) {
					InverseKinematics ik = bone.GetComponent<InverseKinematics>();

					if (ik != null) {
						Undo.RecordObject(ik.target, "Assign Pose");
						ik.target.transform.localPosition = tv.position;
						EditorUtility.SetDirty (ik.target.transform);
					}
				} else {
					Debug.Log("This skeleton has no bone '" + bone.name + "'");
				}
			}
		}

        if (cps.Length > 0)
		{
			foreach (PositionValue cpv in pose.controlPoints) {
				ControlPoint cp = cps.First(c => (c.transform.parent.name + c.name) == cpv.name);

				if (cp != null) {
					Undo.RecordObject(cp.transform, "Assign Pose");
					cp.transform.localPosition = cpv.position;
					EditorUtility.SetDirty (cp.transform);
				}
				else {
					Debug.Log("There is no control point '" + cpv.name + "'");
				}
			}
		}
    }

    public void SetBasePose(Pose pose) {
        basePose = pose;
    }
#endif

    public void SetEditMode(bool edit) {
#if UNITY_EDITOR
        if (!editMode && edit) {
            AnimationMode.StopAnimationMode();

            tempPose = CreatePose();
            tempPose.hideFlags = HideFlags.HideAndDontSave;

            if (basePose != null) {
                RestorePose(basePose);
            }
        }
        else if (editMode && !edit) {
            if (tempPose != null) {
                RestorePose(tempPose);
                Object.DestroyImmediate(tempPose);
            }
        }
#endif

        editMode = edit;
    }

#if UNITY_EDITOR
	public void CalculateWeights ()
	{
		CalculateWeights(false);
	}

	public void CalculateWeights (bool weightToParent)
	{
		//find all Skin2D elements
		Skin2D[] skins = transform.GetComponentsInChildren<Skin2D>();
		if(bones == null || bones.Length != null && bones.Length == 0) {
			Debug.Log("No bones in skeleton");
			return;
		}
		foreach(Skin2D skin in skins) {
			skin.CalculateBoneWeights(bones, weightToParent);
		}
	}
#endif

	private void MoveRenderersPositions(){
		foreach (Transform renderer in renderers.Keys){
			renderer.position = new Vector3(renderer.position.x, renderer.position.y, (float)renderers[renderer]);
		}
	}

	public void FlipY ()
	{
		int normal = -1;
		// Rotate the skeleton's local transform
		if (!flipY)
		{
			renderers = new Dictionary<Transform, float>();
			// Get the new positions for the renderers from the rotation of this transform
			if (useZSorting)
			{
				renderers = GetRenderersZ();
			}
			transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, 0.0f, transform.localEulerAngles.z);
			if (useZSorting)
			{
				MoveRenderersPositions();
			}
		}
		else
		{
			renderers = new Dictionary<Transform, float>();
			// Get the new positions for the renderers from the rotation of this transform
			if (useZSorting)
			{
				renderers = GetRenderersZ();
			}
			// Get the new positions for the renderers from the rotation of this transform
			transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, 180.0f, transform.localEulerAngles.z);
			if (useZSorting)
			{
				MoveRenderersPositions();
			}
		}

		if (transform.localEulerAngles.x == 0.0f && transform.localEulerAngles.y == 180.0f || transform.localEulerAngles.x == 180.0f && transform.localEulerAngles.y == 0.0f)
		{
			normal = 1;
		}

		ChangeRendererNormals(normal);
	}

	public void FlipX ()
	{
		int normal = -1;

		// Rotate the skeleton's local transform
		if (!flipX)
		{
			renderers = new Dictionary<Transform, float>();
			// Get the new positions for the renderers from the rotation of this transform
			if (useZSorting)
			{
				renderers = GetRenderersZ();
			}
			transform.localEulerAngles = new Vector3(0.0f, transform.localEulerAngles.y, transform.localEulerAngles.z);
			if (useZSorting)
			{
				MoveRenderersPositions();
			}
		}
		else
		{
			renderers = new Dictionary<Transform, float>();
			// Get the new positions for the renderers from the rotation of this transform
			if (useZSorting)
			{
				renderers = GetRenderersZ();
			}
			transform.localEulerAngles = new Vector3(180.0f, transform.localEulerAngles.y, transform.localEulerAngles.z);
			if (useZSorting)
			{
				MoveRenderersPositions();
			}
		}

		if (transform.localEulerAngles.x == 0.0f && transform.localEulerAngles.y == 180.0f || transform.localEulerAngles.x == 180.0f && transform.localEulerAngles.y == 0.0f)
		{
			normal = 1;
		}
		
		ChangeRendererNormals(normal);

	}

	public Dictionary<Transform, float> GetRenderersZ()
	{
		renderers = new Dictionary<Transform, float>();
		if (useZSorting)
		{
			//find all SkinnedMeshRenderer elements
			skins = transform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach(SkinnedMeshRenderer skin in skins) {
				if (skin.sharedMaterial != null)
				{
					renderers[skin.transform] = skin.transform.position.z;
				}
			}

			//find all SpriteRenderer elements
			SpriteRenderer[] spriteRenderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
			foreach(SpriteRenderer spriteRenderer in spriteRenderers) {
				if (spriteRenderer.sharedMaterial != null)
				{
					renderers[spriteRenderer.transform] = spriteRenderer.transform.position.z;
				}
			}
		}
		return renderers;
	}

	public void ChangeRendererNormals(int normal)
	{
		if (useShadows)
		{
			//find all SkinnedMeshRenderer elements
			skins = transform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach(SkinnedMeshRenderer skin in skins) {
				if (skin.sharedMaterial != null)
				{
					if (spriteShadowsShader != null && skin.sharedMaterial.shader == spriteShadowsShader)
					{
						skin.sharedMaterial.SetVector("_Normal", new Vector3(0, 0, normal));
					}
				}
			}

			//find all SpriteRenderer elements
			spriteRenderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
			foreach(SpriteRenderer spriteRenderer in spriteRenderers) {
				if (spriteRenderer.sharedMaterial != null)
				{
					if (spriteShadowsShader != null && spriteRenderer.sharedMaterial.shader == spriteShadowsShader)
					{
						spriteRenderer.sharedMaterial.SetVector("_Normal", new Vector3(0, 0, normal));
					}
				}
			}
		}
	}

	public void UseShadows ()
	{
		//find all SpriteRenderer elements
		skins = transform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
		
		foreach(SkinnedMeshRenderer skin in skins) {
			if (skin.sharedMaterial != null)
			{
				if (useShadows && spriteShadowsShader != null)
				{
					skin.sharedMaterial.shader = spriteShadowsShader;
				}
				else
				{
					skin.sharedMaterial.shader = spriteShader;
				}

				skin.castShadows = useShadows;
				skin.receiveShadows = useShadows;
			}
		}

		//find all SpriteRenderer elements
		spriteRenderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
		
		foreach(SpriteRenderer spriteRenderer in spriteRenderers) {
			if (spriteRenderer.sharedMaterial != null)
			{
				if (useShadows && spriteShadowsShader != null)
				{
					spriteRenderer.sharedMaterial.shader = spriteShadowsShader;
				}
				else
				{
					spriteRenderer.sharedMaterial.shader = spriteShader;
				}

				spriteRenderer.castShadows = useShadows;
				spriteRenderer.receiveShadows = useShadows;
			}
		}
	}

	public void UseZSorting ()
	{
		//find all SpriteRenderer elements
		skins = transform.GetComponentsInChildren<SkinnedMeshRenderer>(true);
		
		foreach(SkinnedMeshRenderer skin in skins) {
			if (skin.sharedMaterial != null)
			{
				if (useZSorting)
				{
					float z = skin.sortingOrder / -10000f;
					skin.transform.localPosition = new Vector3(skin.transform.localPosition.x, skin.transform.localPosition.y, z);
					skin.sortingLayerName = "Default";
					skin.sortingOrder = 0;
				}
				else
				{
					int sortLayer = Mathf.RoundToInt(skin.transform.localPosition.z * -10000);
					skin.transform.localPosition = new Vector3(skin.transform.localPosition.x, skin.transform.localPosition.y, 0);
					skin.sortingLayerName = "Default";
					skin.sortingOrder = sortLayer;
				}
			}
		}

		//find all SpriteRenderer elements
		spriteRenderers = transform.GetComponentsInChildren<SpriteRenderer>(true);
		
		foreach(SpriteRenderer spriteRenderer in spriteRenderers) {
			if (spriteRenderer.sharedMaterial != null)
			{
				if (useZSorting)
				{
					float z = spriteRenderer.sortingOrder / -10000f;
					spriteRenderer.transform.localPosition = new Vector3(spriteRenderer.transform.localPosition.x, spriteRenderer.transform.localPosition.y, z);
					spriteRenderer.sortingLayerName = "Default";
					spriteRenderer.sortingOrder = 0;
				}
				else
				{
					int sortLayer = Mathf.RoundToInt(spriteRenderer.transform.localPosition.z * -10000);
					spriteRenderer.transform.localPosition = new Vector3(spriteRenderer.transform.localPosition.x, spriteRenderer.transform.localPosition.y, 0);
					spriteRenderer.sortingLayerName = "Default";
					spriteRenderer.sortingOrder = sortLayer;
				}
			}
		}
	}
}
