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
using System;
using System.Collections.Generic;
using System.Threading;

[ExecuteInEditMode]
[RequireComponent(typeof(Bone))]
public class InverseKinematics : MonoBehaviour {
    [HideInInspector]
    public float influence = 1.0f;
    public int chainLength = 0;
    public Transform target;
	private Skeleton skeleton = null;

    public Transform RootBone {
        get {
            Transform root = null;

            if (chainLength == 0) {
                root = transform.root;
            }
            else {
                int n = chainLength;
                root = transform;
                while (n-- > 0) {
                    if (root.parent == null)
                        break;
                    else
                        root = root.parent;
                }
            }
            return root;
        }
    }

    private int ChainLength {
        get {
            if (chainLength > 0)
                return chainLength;
            else {
                int n = 0;
                var parent = transform.parent;
                while (parent != null && parent.gameObject.GetComponent<Bone>() != null) {
                    n++;
                    parent = parent.parent;
                }
                return n+1;
            }
        }
    }

	public int iterations = 20;

	[Range(0.01f, 1)]
	public float damping = 1;

	public Node[] angleLimits = new Node[0];

	Dictionary<Transform, Node> nodeCache; 
	[System.Serializable]
	public class Node
	{
		public Transform Transform;
		public float min;
		public float max;
	}

	void OnValidate()
	{
		// min & max has to be between 0 ... 360
		foreach (var node in angleLimits)
		{
			node.min = Mathf.Clamp (node.min, 0, 360);
			node.max = Mathf.Clamp (node.max, 0, 360);
		}
	}

	void Start()
	{
		// Cache optimization
		nodeCache = new Dictionary<Transform, Node>(angleLimits.Length);
		foreach (var node in angleLimits)
			if (!nodeCache.ContainsKey(node.Transform))
				nodeCache.Add(node.Transform, node);
		Skeleton[] skeletons = transform.root.gameObject.GetComponentsInChildren<Skeleton>(true);
		foreach (Skeleton s in skeletons)
		{
			if (transform.IsChildOf(s.transform))
			{
				skeleton = s;
			}
		}
	}

    void Update() {
        if (chainLength < 0)
            chainLength = 0;
		if (!Application.isPlaying)
			Start();
    }

	/**
     * Code ported from the Gamemaker tool SK2D by Drifter
     * http://gmc.yoyogames.com/index.php?showtopic=462301
     * Angle Limit code adapted from Veli-Pekka Kokkonen's SimpleCCD http://goo.gl/6oSzDx
     **/
    public void resolveSK2D() {
        Transform tip = transform;

        for (int it = 0; it < iterations; it++) {
            int i = ChainLength;
            Transform bone = transform;
            Bone b = bone.GetComponent<Bone>();

            while (--i >= 0 && bone != null) {
                Vector3 root = bone.position;

                Vector3 root2tip = ((Vector3)b.Head - root);
                Vector3 root2target = (((target != null) ? target.transform.position : (Vector3)b.Head) - root);

				// Calculate how much we should rotate to get to the target
				float angle = SignedAngle(root2tip, root2target, bone);

				// If you want to flip the bone on the y axis invert the angle
				float yAngle = Utils.ClampAngle(bone.rotation.eulerAngles.y);
				if (yAngle > 90 && yAngle < 270)
				angle *= -1;

				// "Slows" down the IK solving
				angle *= damping;

				// Wanted angle for rotation
				angle = -(angle - bone.localRotation.eulerAngles.z);

				// Take care of angle limits 
				if (nodeCache != null && nodeCache.ContainsKey(bone))
				{
					// Clamp angle in local space
					var node = nodeCache[bone];
					Bone pb = bone.parent.GetComponent<Bone>();
					float parentRotation = pb ? Vector2.Angle(pb.Head, b.Head) : 0;
					angle -= parentRotation;
					angle = ClampAngle(angle, node.min, node.max);
					angle += parentRotation;
				}

				bone.localRotation = Quaternion.Euler(bone.localRotation.eulerAngles.x, bone.localRotation.eulerAngles.y, angle);

                bone = bone.parent;
            }
        }
    }

	public float SignedAngle (Vector3 a, Vector3 b, Transform t)
	{
		float angle = Vector3.Angle (a, b);

		// Use skeleton as root, change dir if the rotation is flipped
		Vector3 dir = (skeleton && skeleton.transform.localRotation.eulerAngles.y == 180.0f && skeleton.transform.localRotation.eulerAngles.x == 0.0f) ? Vector3.forward : Vector3.back;
		float sign = Mathf.Sign (Vector3.Dot (dir, Vector3.Cross (a, b)));
		angle = angle * sign;
		// Flip sign if character is turned around
		angle *= Mathf.Sign(t.root.localScale.x);
		return angle;
	}

	float ClampAngle (float angle, float min, float max)
	{
		angle = Mathf.Abs((angle % 360) + 360) % 360;
		return Mathf.Clamp(angle, min, max);
	}
}
