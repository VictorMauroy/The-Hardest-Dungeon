using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CollisionExtensions {

	public static bool OnGround(this CollisionFlags cf){
		return (cf & CollisionFlags.Below)!=0;
	}
	public static bool TouchingSides(this CollisionFlags cf){
		return (cf & CollisionFlags.Sides)!=0;
	}
	public static bool TouchingHead(this CollisionFlags cf){
		return (cf & CollisionFlags.Above)!=0;
	}

}
